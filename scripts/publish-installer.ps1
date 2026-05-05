#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes a self-contained, single-file win-x64 build for the Inno Setup installer.

.DESCRIPTION
  Bundles the .NET 8 runtime (including Windows Desktop / WPF) into WhatAmIDoing.exe so end users do not need
  a separate Microsoft .NET Desktop Runtime install. Larger than framework-dependent, but avoids partial or
  wrong shared-runtime installs on target PCs.

  Compression inside the single-file bundle is OFF (`EnableCompressionInSingleFile=false`): compressed bundles
  have caused startup crashes on some PCs (Event 1000, KERNELBASE, exception 0xc000041d) before any app logs exist.

  Output: src\WhatAmIDoing\bin\Publish\win-x64\WhatAmIDoing.exe
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
        -p:EnableCompressionInSingleFile=false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish (installer / self-contained single-file) failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host ""
$ver = & (Join-Path $repoRoot 'scripts\get-version.ps1')
Write-Host "Published (self-contained, single-file) to: $publishDir (Version $ver)"
