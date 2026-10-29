#requires -Version 5.1
[CmdletBinding()]
param([string]$Installation = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Installation).TrimEnd('\')
$manifest = Join-Path $root 'installed-files.json'
if (!(Test-Path $manifest)) { throw 'No EclipsePOS installation manifest found. Nothing was removed.' }
$entries = Get-Content $manifest -Raw | ConvertFrom-Json
foreach ($entry in $entries) {
    $path = [IO.Path]::GetFullPath((Join-Path $root $entry.Path))
    if (!$path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation manifest.' }
}
foreach ($entry in $entries) {
    $path = Join-Path $root $entry.Path
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $entry.Hash) { Remove-Item -LiteralPath $path }
}
$start = Join-Path ([Environment]::GetFolderPath('Programs')) 'EclipsePOS Preview'
if (Test-Path $start) {
    $shell = New-Object -ComObject WScript.Shell
    foreach ($link in Get-ChildItem $start -Filter *.lnk) {
        if ($shell.CreateShortcut($link.FullName).TargetPath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $link.FullName }
    }
}
Remove-Item -LiteralPath $manifest
Write-Host 'Unmodified application files removed. Settings, receipts, databases and modified files are preserved. Empty installation folders may be removed manually.'

