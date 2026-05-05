#Requires -Version 5.1
<#
.SYNOPSIS
  Installs tools needed on YOUR machine to *build* the app and Inno installer (developers only).

.DESCRIPTION
  The published What Am I Doing EXE is self-contained — end users do NOT need the .NET SDK or Inno Setup.

  This script tries, in order:
    1. winget (Windows Package Manager) — preferred, often no admin for user-scope installs
    2. Chocolatey — if winget is missing and choco is available (may require elevation)

  After installs, environment PATH is refreshed in this session so `dotnet` / Inno may work immediately.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\install-build-prerequisites.ps1
#>

$ErrorActionPreference = 'Stop'

function Refresh-SessionPath {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
}

function Test-DotNet8Sdk {
    try {
        $raw = & dotnet --list-sdks 2>&1
        if ($LASTEXITCODE -ne 0) { return $false }
        foreach ($line in $raw) {
            if ($line -match '^8\.') { return $true }
        }
    }
    catch {
        return $false
    }
    return $false
}

function Test-InnoSetup {
    $candidates = @(
        $env:INNO_SETUP_ROOT
        "${env:ProgramFiles(x86)}\Inno Setup 6"
        "$env:ProgramFiles\Inno Setup 6"
    ) | Where-Object { $_ -and (Test-Path $_) }
    foreach ($r in $candidates) {
        if (Test-Path (Join-Path $r 'ISCC.exe')) { return $true }
    }
    return $false
}

function Install-WingetId {
    param([Parameter(Mandatory)][string] $Id, [string] $Label)
    Write-Host "Installing $Label via winget ($Id)..."
    & winget install --id $Id -e --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget install $Id failed with exit code $LASTEXITCODE"
    }
}

Write-Host "=== Build prerequisites (developer machine) ===" -ForegroundColor Cyan
Write-Host "Note: people who only install the Releases EXE do not need any of this.`n"

$needDotNet = -not (Test-DotNet8Sdk)
$needInno = -not (Test-InnoSetup)

if (-not $needDotNet -and -not $needInno) {
    Write-Host ".NET 8 SDK and Inno Setup 6 already look installed. Nothing to do."
    exit 0
}

$winget = Get-Command winget -ErrorAction SilentlyContinue
$choco = Get-Command choco -ErrorAction SilentlyContinue

if ($needDotNet) {
    if ($winget) {
        Install-WingetId -Id 'Microsoft.DotNet.SDK.8' -Label '.NET 8 SDK'
        Refresh-SessionPath
    }
    elseif ($choco) {
        Write-Host "Installing .NET 8 SDK via Chocolatey..."
        & choco install dotnet-8.0-sdk -y --no-progress
        if ($LASTEXITCODE -ne 0) { throw "choco install dotnet-8.0-sdk failed with exit code $LASTEXITCODE" }
        Refresh-SessionPath
    }
    else {
        throw @"
Could not find winget or Chocolatey to install the .NET 8 SDK.

Install manually:
  https://dotnet.microsoft.com/download/dotnet/8.0

Then re-run this script or build-installer.ps1
"@
    }
    if (-not (Test-DotNet8Sdk)) {
        throw ".NET 8 SDK still not detected after install. Open a new PowerShell window and run this script again, or verify PATH."
    }
    Write-Host ".NET 8 SDK: OK" -ForegroundColor Green
}

if ($needInno) {
    if ($winget) {
        Install-WingetId -Id 'JRSoftware.InnoSetup' -Label 'Inno Setup 6'
        Refresh-SessionPath
    }
    elseif ($choco) {
        Write-Host "Installing Inno Setup 6 via Chocolatey..."
        & choco install innosetup -y --no-progress
        if ($LASTEXITCODE -ne 0) { throw "choco install innosetup failed with exit code $LASTEXITCODE" }
        Refresh-SessionPath
    }
    else {
        throw @"
Could not find winget or Chocolatey to install Inno Setup 6.

Install manually:
  https://jrsoftware.org/isdl.php

Then re-run this script or build-installer.ps1
"@
    }
    if (-not (Test-InnoSetup)) {
        throw "Inno Setup 6 still not detected after install. Open a new PowerShell window, or set INNO_SETUP_ROOT to the folder that contains ISCC.exe."
    }
    Write-Host "Inno Setup 6: OK" -ForegroundColor Green
}

Write-Host "`nDone. You can run scripts\build-installer.ps1 next." -ForegroundColor Cyan
