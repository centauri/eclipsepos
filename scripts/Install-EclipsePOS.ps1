#requires -Version 5.1
[CmdletBinding()]
param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs/EclipsePOS-0.2-preview'), [switch]$NoShortcuts)
$ErrorActionPreference = 'Stop'
$release = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
if (!$release -or $release.Release -lt 528040) { throw 'Install Microsoft .NET Framework 4.8 first.' }
$source = Join-Path $PSScriptRoot 'payload'
if (!(Test-Path $source)) { throw 'Run this installer from the extracted EclipsePOS Windows package.' }
$Destination = [IO.Path]::GetFullPath($Destination)
if (Test-Path $Destination) { throw 'The destination already exists. Choose a new directory; existing files will not be overwritten.' }
$manifest = Get-Content (Join-Path $PSScriptRoot 'payload-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $path = [IO.Path]::GetFullPath((Join-Path $source $entry.Path))
    if (!$path.StartsWith([IO.Path]::GetFullPath($source) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package path.' }
    if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $entry.Hash) { throw "Package checksum mismatch: $($entry.Path)" }
}
New-Item -ItemType Directory $Destination | Out-Null
foreach ($entry in $manifest) {
    $target = Join-Path $Destination $entry.Path
    New-Item -ItemType Directory (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $entry.Path) -Destination $target
    if ((Get-FileHash $target -Algorithm SHA256).Hash -ne $entry.Hash) { throw "Installation checksum mismatch: $($entry.Path)" }
}
Copy-Item (Join-Path $PSScriptRoot 'payload-manifest.json') (Join-Path $Destination 'installed-files.json')
if (!$NoShortcuts) {
    $start = Join-Path ([Environment]::GetFolderPath('Programs')) 'EclipsePOS Preview'
    New-Item -ItemType Directory $start -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $apps = @{
        'EclipsePOS Demo'='Demo/EclipsePOS.Modern.exe';
        'EclipsePOS Workbench'='EclipsePosWorkbench/EclipsePOS.WPF.PosWorkBench.exe';
        'EclipsePOS Management'='EclipsePosSystemManager/EclipsePOS.WPF.SystemManager.exe'
    }
    foreach ($name in $apps.Keys) {
        $link = $shell.CreateShortcut((Join-Path $start ($name + '.lnk')))
        $link.TargetPath = Join-Path $Destination $apps[$name]
        $link.WorkingDirectory = Split-Path $link.TargetPath -Parent
        $link.Save()
    }
}
Write-Host "Installed and verified: $Destination"
Write-Host 'The standalone demo can run immediately. Workbench and Management need SQL setup; real devices also need POS for .NET and vendor OPOS drivers. See WINDOWS-SETUP.md.'

