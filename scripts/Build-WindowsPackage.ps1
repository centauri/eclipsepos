#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PosNetSdkDir,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$PosNetSdkDir = [IO.Path]::GetFullPath($PosNetSdkDir)
if (!(Test-Path (Join-Path $PosNetSdkDir 'Microsoft.PointOfService.dll'))) { throw 'Supply the official POS for .NET 1.14.1 SDK directory.' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$package = Join-Path $OutputDirectory 'EclipsePOS-Windows-0.2-preview'
if (Test-Path $package) { throw 'Package directory already exists. Use a fresh output directory.' }
$builds = @{
    'EclipsePosWorkbench'='V.1.0.8/EclipsePOS.WPF.PosWorkBench/EclipsePOS.WPF.PosWorkBench/EclipsePOS.WPF.PosWorkBench.csproj';
    'EclipsePosSystemManager'='V.1.0.8/EclipsePOS.WPF.SystemManager/EclipsePOS.WPF.SystemManager/EclipsePOS.WPF.SystemManager.csproj';
    'Demo'='ModernPOS/ModernPOS.csproj'
}
foreach ($name in $builds.Keys) {
    $project = Join-Path $root $builds[$name]
    & dotnet build $project -c Release "-p:PosNetSdkDir=$PosNetSdkDir" -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $name" }
    $destination = Join-Path $package "payload/$name"
    New-Item -ItemType Directory $destination -Force | Out-Null
    $bin = Join-Path (Split-Path $project -Parent) 'bin/Release/net48'
    Get-ChildItem $bin -File | Where-Object { $_.Extension -in @('.exe','.dll','.config','.json') } | Copy-Item -Destination $destination
}
& dotnet build (Join-Path $root 'tests/Regression/Regression.csproj') -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Regression build failed.' }
& (Join-Path $root 'tests/Regression/bin/Release/net48/Regression.exe')
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
& dotnet build (Join-Path $root 'tests/SqlSyntax/SqlSyntax.csproj') -c Release -v quiet
if ($LASTEXITCODE -ne 0) { throw 'SQL syntax validator build failed.' }
& (Join-Path $root 'tests/SqlSyntax/bin/Release/net48/SqlSyntax.exe') (Join-Path $root 'database/001-legacy-demo.sql') (Join-Path $root 'database/002-modern-security.sql')
if ($LASTEXITCODE -ne 0) { throw 'SQL syntax checks failed.' }
$payload = Join-Path $package 'payload'
foreach ($directory in @('scripts','database','Shared')) {
    New-Item -ItemType Directory (Join-Path $payload $directory) -Force | Out-Null
}
Copy-Item (Join-Path $root 'scripts/Set-EclipseDatabase.ps1'),(Join-Path $root 'scripts/Test-WindowsPrerequisites.ps1'),(Join-Path $root 'scripts/Uninstall-EclipsePOS.ps1') (Join-Path $payload 'scripts')
Copy-Item (Join-Path $root 'database/*.sql') (Join-Path $payload 'database')
Copy-Item (Join-Path $root 'Shared/PasswordHash.cs') (Join-Path $payload 'Shared')
Copy-Item (Join-Path $root 'LICENSE') $payload
Copy-Item (Join-Path $root 'docs/WINDOWS-SETUP.md') $payload
# Official SDK objects retain Microsoft's separate license.
$license = Join-Path $PosNetSdkDir 'Eula.rtf'
if (!(Test-Path $license)) { throw 'Place the original Microsoft POS for .NET Eula.rtf in the SDK directory before packaging.' }
Copy-Item $license (Join-Path $payload 'Microsoft-POS-for-NET-LICENSE.rtf')
Copy-Item (Join-Path $root 'scripts/Install-EclipsePOS.ps1') $package
$entries = @(Get-ChildItem $payload -File -Recurse | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring($payload.Length + 1); Hash=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
})
$entries | ConvertTo-Json | Set-Content (Join-Path $package 'payload-manifest.json') -Encoding UTF8
Compress-Archive -Path $package -DestinationPath ($package + '.zip')
Write-Host "Built $package.zip"
