#Requires -Version 5.1
<#
.SYNOPSIS
  Downloads the Microsoft .NET 8 Desktop Runtime (x64) offline installer for bundling into the Inno Setup wizard.

.DESCRIPTION
  Uses winget to download the latest 8.x desktop runtime matching the app TFM (net8.0-windows).
  Output is always renamed to installer\prereq\DesktopRuntime-8-x64.exe so WhatAmIDoing.iss can reference a stable name.

  Requires winget (Windows 10 1809+ / 11). Run install-build-prerequisites.ps1 if needed.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$prereqDir = Join-Path $repoRoot 'installer\prereq'
$targetName = 'DesktopRuntime-8-x64.exe'
$targetPath = Join-Path $prereqDir $targetName

New-Item -ItemType Directory -Path $prereqDir -Force | Out-Null

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw "winget is required to download the .NET Desktop Runtime bundle. Install App Installer / winget, or place $targetName manually in installer\prereq\"
}

Write-Host "Downloading Microsoft .NET 8 Desktop Runtime (x64) via winget..."
$tempDl = Join-Path $prereqDir '_winget_download'
if (Test-Path $tempDl) { Remove-Item $tempDl -Recurse -Force }
New-Item -ItemType Directory -Path $tempDl -Force | Out-Null

& winget download --id Microsoft.DotNet.DesktopRuntime.8 -e --architecture x64 `
    --accept-package-agreements --accept-source-agreements --disable-interactivity `
    -d $tempDl
if ($LASTEXITCODE -ne 0) {
    throw "winget download failed with exit code $LASTEXITCODE"
}

$downloaded = Get-ChildItem -Path $tempDl -Filter *.exe -File | Sort-Object Length -Descending | Select-Object -First 1
if (-not $downloaded) {
    throw "winget did not produce an .exe in $tempDl"
}

Copy-Item -LiteralPath $downloaded.FullName -Destination $targetPath -Force
Remove-Item $tempDl -Recurse -Force

Write-Host "Saved: $targetPath ($([math]::Round((Get-Item $targetPath).Length / 1MB, 1)) MB)"
