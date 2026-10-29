$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build ModernPOS.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $target = Join-Path $PSScriptRoot 'bin\Release\net48'
    Push-Location $target
    try {
        $check = Start-Process '.\EclipsePOS.Modern.exe' '--self-test' -WindowStyle Hidden -Wait -PassThru
        if ($check.ExitCode -ne 0) { throw 'Self-test failed; inspect self-test-failed.txt' }
    } finally { Pop-Location }
    $zip = Join-Path $PSScriptRoot 'EclipsePOS-Modern-Demo.zip'
    Compress-Archive -Path "$target\EclipsePOS.Modern.exe", "$target\EclipsePOS.Modern.exe.config", "$target\hardware.json", "$PSScriptRoot\README.md", "$PSScriptRoot\..\LICENSE" -DestinationPath $zip -Force
    Write-Output $zip
} finally { Pop-Location }
