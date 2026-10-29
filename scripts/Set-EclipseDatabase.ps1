#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('CreateDemo','Upgrade','Verify')][string]$Mode = 'Verify',
    [string]$Server = '.\SQLEXPRESS',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,63}$')][string]$Database = 'EclipsePOSDemo',
    [switch]$TrustServerCertificate,
    [string]$BackupPath,
    [switch]$ConfigureApps
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$root = Split-Path $PSScriptRoot -Parent
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder.DataSource = $Server
$builder.InitialCatalog = 'master'
$builder.IntegratedSecurity = $true
$builder.Encrypt = $true
$builder.TrustServerCertificate = $TrustServerCertificate.IsPresent
$builder.ConnectTimeout = 15
$connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
$transaction = $null
function Invoke-Sql([string]$Text, [hashtable]$Parameters = @{}, [switch]$Scalar) {
    $command = $connection.CreateCommand()
    try {
        $command.CommandText = $Text
        $command.CommandTimeout = 600
        if ($script:transaction) { $command.Transaction = $script:transaction }
        foreach ($key in $Parameters.Keys) { [void]$command.Parameters.AddWithValue($key, $Parameters[$key]) }
        if ($Scalar) { return $command.ExecuteScalar() }
        [void]$command.ExecuteNonQuery()
    } finally { $command.Dispose() }
}
try {
    $connection.Open()
    $version = Invoke-Sql "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'))" -Scalar
    if ($version -lt 17) { throw 'This installer targets SQL Server 2025 (17.x). Upgrade the SQL instance first.' }
    $exists = Invoke-Sql 'SELECT COUNT(*) FROM sys.databases WHERE name = @name' @{'@name'=$Database} -Scalar
    if ($Mode -eq 'CreateDemo') {
        if ($exists -ne 0) { throw 'CreateDemo refuses to overwrite an existing database. Choose a new name.' }
        Invoke-Sql "CREATE DATABASE [$Database]"
    } elseif ($exists -ne 1) { throw 'The database does not exist.' }
    $connection.ChangeDatabase($Database)
    if ($Mode -eq 'Upgrade') {
        if (!$BackupPath -or ![IO.Path]::IsPathRooted($BackupPath) -or ! $BackupPath.EndsWith('.bak')) {
            throw 'Upgrade requires -BackupPath: a .bak path writable by SQL Server on the server machine.'
        }
        $backupName = 'EclipsePOS-' + [guid]::NewGuid().ToString('N')
        Invoke-Sql "BACKUP DATABASE [$Database] TO DISK = @path WITH COPY_ONLY, CHECKSUM, NOINIT, NAME = @name; DECLARE @position int = (SELECT TOP (1) position FROM msdb.dbo.backupset WHERE name = @name AND database_name = DB_NAME() ORDER BY backup_finish_date DESC); IF @position IS NULL THROW 51001, 'Backup position unavailable.', 1; RESTORE VERIFYONLY FROM DISK = @path WITH FILE = @position, CHECKSUM;" @{'@path'=$BackupPath; '@name'=$backupName}
    }
    if ($Mode -ne 'Verify') {
        $transaction = $connection.BeginTransaction()
        Invoke-Sql "SET XACT_ABORT ON; DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'EclipsePOS.Schema', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51002, 'Another schema upgrade is running.', 1;"
        $baseline = [IO.File]::ReadAllText((Join-Path $root 'database/001-legacy-demo.sql'), [Text.Encoding]::GetEncoding(1252))
        $batches = [regex]::Split($baseline, '(?im)^\s*GO\s*(?:--[^\r\n]*)?$')
        if ($Mode -eq 'CreateDemo') {
            foreach ($batch in $batches) {
                # Context and creation are controlled here, never by the legacy script.
                $batch = [regex]::Replace($batch, '(?im)^\s*(use\s+\[(master|possite)\]|create\s+database\s+\[possite\])\s*$', '')
                if ($batch.Trim()) { Invoke-Sql $batch }
            }
        }
        Invoke-Sql ([IO.File]::ReadAllText((Join-Path $root 'database/002-modern-security.sql')))
        # Widen signatures from the installed definitions, preserving customized bodies.
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandText = "SELECT m.definition FROM sys.sql_modules m WHERE EXISTS (SELECT 1 FROM sys.parameters p WHERE p.object_id=m.object_id AND p.name IN ('@logon_pass','@subscriber_pass') AND p.max_length=32)"
        $definitions = New-Object System.Data.DataTable
        try { $reader = $command.ExecuteReader(); try { $definitions.Load($reader) } finally { $reader.Dispose() } } finally { $command.Dispose() }
        foreach ($definition in $definitions.Rows) {
            if ($definition.definition -is [DBNull]) { throw 'An encrypted password procedure cannot be migrated automatically.' }
            $updated = [regex]::Replace($definition.definition, '(?i)\b(?:create\s+or\s+alter|create|alter)\s+proc(?:edure)?\b', 'CREATE OR ALTER PROCEDURE')
            $updated = [regex]::Replace($updated, '(?i)(@(?:logon_pass|subscriber_pass)\s+(?:\[varchar\]|varchar)\s*\(\s*)32(\s*\))', '${1}256$2')
            Invoke-Sql $updated
        }
        $narrow = Invoke-Sql "SELECT COUNT(*) FROM sys.parameters WHERE name IN ('@logon_pass','@subscriber_pass') AND max_length < 256" -Scalar
        if ($narrow -gt 0) { throw 'One or more password procedures still have narrow parameters. Migration rolled back.' }
        if ($Mode -eq 'CreateDemo') {
            Add-Type -Path (Join-Path $root 'Shared/PasswordHash.cs')
            $adminPassword = Read-Host 'Choose a management password for this demo (minimum 12 characters)' -AsSecureString
            $pin = Read-Host 'Choose a cashier PIN (6-9 digits, first digit nonzero)' -AsSecureString
            $adminText = [System.Net.NetworkCredential]::new('', $adminPassword).Password
            $pinText = [System.Net.NetworkCredential]::new('', $pin).Password
            if ($adminText.Length -lt 12 -or $pinText -notmatch '^[1-9][0-9]{5,8}$') { throw 'Password or PIN does not meet the requirement.' }
            Invoke-Sql 'UPDATE dbo.sub SET subscriber_pass = @hash' @{'@hash'=[EclipsePOS.Platform.PasswordHash]::Create($adminText)}
            Invoke-Sql 'UPDATE dbo.employee SET logon_pass = @hash' @{'@hash'=[EclipsePOS.Platform.PasswordHash]::Create($pinText)}
            $adminText = $null; $pinText = $null
        }
        $transaction.Commit(); $transaction.Dispose(); $transaction = $null
    }
    $schema = Invoke-Sql 'SELECT MAX(version) FROM dbo.EclipseSchemaVersion' -Scalar
    if ($schema -ne 2) { throw 'Unsupported database schema version.' }
    $width = Invoke-Sql "SELECT COL_LENGTH('dbo.employee','logon_pass')" -Scalar
    if ($width -lt 256) { throw 'Password schema migration is incomplete.' }
    if ($ConfigureApps) {
        $builder.InitialCatalog = $Database
        $directory = $env:ECLIPSEPOS_DATA_DIR
        if (!$directory) { $directory = Join-Path $env:LOCALAPPDATA 'EclipsePOS/Data' }
        New-Item -ItemType Directory $directory -Force | Out-Null
        $file = Join-Path $directory 'EclipsePOS.settings'
        if (Test-Path $file) { [xml]$settings = Get-Content $file -Raw; Copy-Item $file ($file + '.before-setup.bak') }
        else { [xml]$settings = '<Settings />' }
        $machine = $settings.DocumentElement.SelectSingleNode([Xml.XmlConvert]::EncodeLocalName($env:COMPUTERNAME))
        if (!$machine) { $machine = $settings.CreateElement([Xml.XmlConvert]::EncodeLocalName($env:COMPUTERNAME)); [void]$settings.DocumentElement.AppendChild($machine) }
        $values = @{DataSource=$builder.ConnectionString; possiteConnectionString=$builder.ConnectionString}
        if ($Mode -eq 'CreateDemo') { $values.Organization='1'; $values.Store='2'; $values.Station='1'; $values.Configuration='1' }
        foreach ($key in $values.Keys) {
            $node = $machine.SelectSingleNode($key)
            if (!$node) { $node=$settings.CreateElement($key); [void]$machine.AppendChild($node) }
            $node.InnerText=$values[$key]
        }
        $temporary = $file + '.tmp'
        $settings.Save($temporary)
        if (Test-Path $file) { [IO.File]::Replace($temporary,$file,$file+'.bak') } else { [IO.File]::Move($temporary,$file) }
    }
    Write-Host "PASS: $Server / $Database is at EclipsePOS schema 2."
    if ($Mode -eq 'CreateDemo') { Write-Host 'Sample retail data: management user admin; cashier 100; use the passwords you just chose. All seeded users share these demo passwords. Change them before real use.' }
} catch {
    if ($transaction) { try { $transaction.Rollback() } catch {} }
    throw
} finally { if ($transaction) { $transaction.Dispose() }; $connection.Dispose() }

