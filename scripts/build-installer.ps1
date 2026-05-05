#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes a self-contained single-file EXE and compiles the Inno Setup installer.

.DESCRIPTION
  1. Optionally installs .NET 8 SDK + Inno Setup 6 (developer machine only) when -InstallPrerequisites is set.
  2. Runs scripts\publish-installer.ps1 (self-contained single-file — .NET 8 Desktop stack is embedded in the EXE; no separate runtime bundle).
  3. Invokes Inno Setup 6 ISCC.exe with /DAppVersion=<Version> from MSBuild (Directory.Build.props).

.PARAMETER SkipPublish
  Skip dotnet publish (reuse existing src\WhatAmIDoing\bin\Publish\win-x64\WhatAmIDoing.exe).

.PARAMETER SkipFetch
  Deprecated no-op (kept so existing scripts that pass -SkipFetch do not break).

.PARAMETER InstallPrerequisites
  Run scripts\install-build-prerequisites.ps1 first (winget or Chocolatey). For machines that build the installer.

.EXAMPLE
  .\scripts\build-installer.ps1

.EXAMPLE
  .\scripts\build-installer.ps1 -InstallPrerequisites
#>

param(
    [switch] $SkipPublish,
    [switch] $SkipFetch,
    [switch] $InstallPrerequisites
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptsDir = $PSScriptRoot

if ($InstallPrerequisites) {
    & (Join-Path $scriptsDir 'install-build-prerequisites.ps1')
}

if ($SkipFetch) {
    Write-Host "Note: -SkipFetch is deprecated (installer no longer bundles a separate Desktop runtime)."
}

if (-not $SkipPublish) {
    & (Join-Path $scriptsDir 'publish-installer.ps1')
}
else {
    $exe = Join-Path $repoRoot 'src\WhatAmIDoing\bin\Publish\win-x64\WhatAmIDoing.exe'
    if (-not (Test-Path $exe)) {
        throw "SkipPublish was set but EXE not found: $exe — run publish-installer.ps1 first or omit -SkipPublish."
    }
    Write-Host "SkipPublish: using existing $exe"
}

function Find-IsccPath {
    $roots = @(
        $env:INNO_SETUP_ROOT
        "${env:ProgramFiles(x86)}\Inno Setup 6"
        "$env:ProgramFiles\Inno Setup 6"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($r in $roots) {
        $p = Join-Path $r 'ISCC.exe'
        if (Test-Path $p) { return $p }
    }
    return $null
}

$iscc = Find-IsccPath
if (-not $iscc) {
    throw @"
Inno Setup 6 (ISCC.exe) not found.

Run:
  powershell -ExecutionPolicy Bypass -File .\scripts\install-build-prerequisites.ps1
or install from https://jrsoftware.org/isdl.php and set INNO_SETUP_ROOT if needed.

Then re-run build-installer.ps1 (use -InstallPrerequisites to try automatic install).
"@
}

$appVersion = & (Join-Path $scriptsDir 'get-version.ps1')
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "Could not read Version from MSBuild (get-version.ps1)."
}

$issDir = Join-Path $repoRoot 'installer'
Write-Host ""
Write-Host "Compiling installer with: $iscc"
Write-Host "  Script: $(Join-Path $issDir 'WhatAmIDoing.iss')"
Write-Host "  AppVersion (from MSBuild / Directory.Build.props): $appVersion"
Push-Location $issDir
try {
    & $iscc "/DAppVersion=$appVersion" ".\WhatAmIDoing.iss"
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC.exe failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$outDir = Join-Path $repoRoot 'installer\Output'
Write-Host ""
Write-Host "Done. Installer output directory: $outDir"
Get-ChildItem $outDir -Filter *.exe -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $($_.FullName)" }
