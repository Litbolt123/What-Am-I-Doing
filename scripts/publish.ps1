#Requires -Version 5.1
<#
    Build a single-file, self-contained Windows x64 publish of "What Am I Doing".

    Usage (from the repo root):
        powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1

    Output:
        src\WhatAmIDoing\bin\Publish\win-x64\WhatAmIDoing.exe
#>

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'src\WhatAmIDoing'
$publishDir = Join-Path $projectDir 'bin\Publish\win-x64'

if (Test-Path $publishDir) {
    Write-Host "Cleaning $publishDir"
    Remove-Item $publishDir -Recurse -Force
}

Push-Location $projectDir
try {
    & dotnet publish `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Published to: $publishDir"
Write-Host "Next: open installer\WhatAmIDoing.iss in Inno Setup Compiler to build the installer."
