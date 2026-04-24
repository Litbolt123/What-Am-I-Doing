# Extract each embedded frame from app.ico into assets\icon-preview-<size>.png
# so you can visually confirm the icon at every Windows presentation size.
param(
    [string]$Icon    = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\WhatAmIDoing\app.ico'),
    [string]$OutDir  = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets')
)

$ErrorActionPreference = 'Stop'

$bytes = [System.IO.File]::ReadAllBytes($Icon)
if ($bytes.Length -lt 6 -or $bytes[0] -ne 0 -or $bytes[1] -ne 0 -or $bytes[2] -ne 1 -or $bytes[3] -ne 0) {
    throw "Not a valid .ico: $Icon"
}

$count = [BitConverter]::ToUInt16($bytes, 4)
Write-Host "Frames: $count"

for ($i = 0; $i -lt $count; $i++) {
    $e = 6 + $i * 16
    $w = $bytes[$e];     if ($w -eq 0) { $w = 256 }
    $h = $bytes[$e + 1]; if ($h -eq 0) { $h = 256 }
    $size     = [BitConverter]::ToUInt32($bytes, $e + 8)
    $offset   = [BitConverter]::ToUInt32($bytes, $e + 12)
    $frame    = New-Object byte[] $size
    [Array]::Copy($bytes, $offset, $frame, 0, $size)

    # PNG signature starts with 0x89 0x50 0x4E 0x47 — modern ICO embeds PNG directly.
    $isPng = $frame.Length -ge 4 -and $frame[0] -eq 0x89 -and $frame[1] -eq 0x50 -and $frame[2] -eq 0x4E -and $frame[3] -eq 0x47
    $outPath = Join-Path $OutDir ("icon-preview-{0}.png" -f $w)
    if ($isPng) {
        [System.IO.File]::WriteAllBytes($outPath, $frame)
        Write-Host "  ${w}x${h}: PNG -> $outPath"
    } else {
        Write-Host "  ${w}x${h}: DIB frame (skipping; preview only covers PNG frames)"
    }
}
