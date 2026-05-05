#Requires -Version 5.1
<#
.SYNOPSIS
  Prints the app/installer version from MSBuild (Directory.Build.props + project).

.EXAMPLE
  $v = powershell -NoProfile -File .\scripts\get-version.ps1
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'src\WhatAmIDoing\WhatAmIDoing.csproj'

if (-not (Test-Path $proj)) {
    throw "Project not found: $proj"
}

$v = dotnet msbuild $proj -nologo -getProperty:Version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet msbuild -getProperty:Version failed with exit code $LASTEXITCODE"
}

$v = ($v | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($v)) {
    throw "MSBuild returned an empty Version. Check Directory.Build.props."
}

Write-Output $v
