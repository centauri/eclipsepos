# Read-only inventory. Does not install software, connect to SQL, load vendor DLLs or claim devices.
[CmdletBinding()]
param(
    [string]$PosNetSdkDir,
    [string]$SqlServer,
    [switch]$AsJson
)
$ErrorActionPreference = 'Stop'
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [string]$State, [string]$Detail) {
    $checks.Add([pscustomobject]@{ Name = $Name; State = $State; Detail = $Detail })
}
if ($env:OS -ne 'Windows_NT') { throw 'Run this inventory on Windows.' }
$os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$build = [int]$os.CurrentBuildNumber
$osName = if ($build -ge 22000) { 'Windows 11' } else { 'Windows 10 or earlier' }
Add-Check 'Windows' $(if ($build -ge 19045) { 'PASS' } else { 'REVIEW' }) "$osName; build $build; edition $($os.EditionID). This is not an installation certification."
if ($build -lt 22000) { Add-Check 'Windows lifecycle' 'REVIEW' 'Check the exact Windows 10 edition and ESU/LTSC servicing entitlement; ordinary Windows 10 support ended in October 2025.' }
$framework = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
Add-Check '.NET Framework 4.8+' $(if ($framework.Release -ge 528040) { 'PASS' } else { 'MISSING' }) "Release key: $($framework.Release). Required for the modern demo and retargeted legacy projects."
if ([string]::IsNullOrWhiteSpace($PosNetSdkDir)) {
    $candidates = @()
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base) { $candidates += Join-Path $base 'Microsoft Point Of Service\SDK' }
    }
    $PosNetSdkDir = $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Microsoft.PointOfService.dll') } | Select-Object -First 1
}
$sdkPresent = $false
if ($PosNetSdkDir) {
    $sdkPresent = (Test-Path -LiteralPath (Join-Path $PosNetSdkDir 'Microsoft.PointOfService.dll')) -and (Test-Path -LiteralPath (Join-Path $PosNetSdkDir 'Microsoft.PointOfService.ControlBase.dll'))
}
Add-Check 'POS for .NET SDK' $(if ($sdkPresent) { 'FOUND' } else { 'MISSING' }) $(if ($sdkPresent) { "$PosNetSdkDir. Presence only; vendor compatibility is not tested." } else { 'Required to compile the original Workbench; not required for ModernPOS demo mode.' })
Add-Check 'Vendor service objects' 'MANUAL' 'Test exact Epson/scanner/drawer/scale models and x86/x64 drivers using vendor tools. DLL presence does not prove device operation.'
$services = @(Get-Service -Name 'MSSQLSERVER', 'MSSQL$*' -ErrorAction SilentlyContinue)
if ($SqlServer) {
    Add-Check 'SQL Server' 'MANUAL' "External/configured target: $SqlServer. No connection or schema validation performed."
} elseif ($services.Count -gt 0) {
    Add-Check 'SQL Server' 'FOUND' (($services | ForEach-Object { $_.Name + ': ' + $_.Status }) -join '; ')
} else {
    Add-Check 'SQL Server' 'MISSING' 'No local SQL Server engine service detected. The original app requires a configured local or remote database; demo mode does not.'
}
Add-Check 'Database/schema' 'MANUAL' 'Validate a new database and a backed-up upgrade separately. The old creation scripts contain DROP TABLE statements and are not an upgrade migration.'
$settings = Join-Path $env:ProgramData 'EclipseSoftware'
Add-Check 'Settings permissions' 'MANUAL' "Original shared settings path: $settings. Test read/write as the actual cashier account; this script makes no changes."
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
Add-Check '.NET SDK command' $(if ($dotnet) { 'FOUND' } else { 'MISSING' }) $(if ($dotnet) { 'dotnet found; SDK version/build compatibility must still be verified.' } else { 'Needed for source builds, not for running the packaged demo.' })
$report = [pscustomobject]@{
    CheckedAtUtc = [DateTime]::UtcNow.ToString('o')
    FullAppInstallVerified = $false
    Note = 'Presence/readiness inventory only. No application, database, device or installer acceptance test.'
    Checks = @($checks.ToArray())
}
if ($AsJson) { $report | ConvertTo-Json -Depth 5 }
else { $checks | Format-Table -Wrap -AutoSize; Write-Output $report.Note }
