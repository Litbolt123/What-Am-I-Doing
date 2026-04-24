#Requires -Version 5.1
<#
    Build src\WhatAmIDoing\app.ico from a source PNG (defaults to
    assets\app-icon-source.png — committed by the author, or regenerated
    through the AI image tool). Produces a proper multi-resolution ICO
    with 16/24/32/48/64/128/256 PNG-compressed entries so Windows
    Explorer, the taskbar, and shortcut icons all look crisp.

    Usage:
        powershell -ExecutionPolicy Bypass -File .\scripts\make-icon.ps1
#>

param(
    [string]$Source = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\app-icon-source.png'),
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\WhatAmIDoing\app.ico')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Source)) {
    throw "Source PNG not found: $Source"
}

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256

# A pixel is treated as padding if it is either transparent OR near-white AND
# near-neutral (low saturation). The image generator sometimes returns a 24-bit
# PNG with a solid white frame instead of real alpha, so we trim on color too.
$alphaThreshold   = 20
$lightnessCutoff  = 200   # channel min above this = "near white / light gray"
$saturationCutoff = 20    # channel max-min above this = "colorful, keep"

function Get-ContentBounds {
    param([System.Drawing.Bitmap]$Bitmap, [int]$AlphaThreshold, [int]$LightnessCutoff, [int]$SaturationCutoff)

    $w = $Bitmap.Width; $h = $Bitmap.Height
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $Bitmap.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $bytes  = New-Object byte[] ($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    } finally {
        $Bitmap.UnlockBits($data)
    }

    $minX = $w; $minY = $h; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $h; $y++) {
        $row = $y * $stride
        for ($x = 0; $x -lt $w; $x++) {
            # BGRA order in Format32bppArgb
            $b = $bytes[$row + $x * 4 + 0]
            $g = $bytes[$row + $x * 4 + 1]
            $r = $bytes[$row + $x * 4 + 2]
            $a = $bytes[$row + $x * 4 + 3]

            $isTransparent = $a -le $AlphaThreshold

            $chMin = [Math]::Min($r, [Math]::Min($g, $b))
            $chMax = [Math]::Max($r, [Math]::Max($g, $b))
            $isNearWhite = ($chMin -ge $LightnessCutoff) -and (($chMax - $chMin) -le $SaturationCutoff)

            if (-not $isTransparent -and -not $isNearWhite) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt 0) {
        return New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    }
    return New-Object System.Drawing.Rectangle($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1))
}

$sourceBmp = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
try {
    # The icon-builder needs a Format32bppArgb bitmap to read the pixel buffer; if the
    # source PNG is 24bpp we wrap it in a 32bpp copy first.
    $probeBmp = if ($sourceBmp -is [System.Drawing.Bitmap] -and
                     $sourceBmp.PixelFormat -eq [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) {
        $sourceBmp
    } else {
        New-Object System.Drawing.Bitmap($sourceBmp.Width, $sourceBmp.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    }
    if (-not [object]::ReferenceEquals($probeBmp, $sourceBmp)) {
        $pg = [System.Drawing.Graphics]::FromImage($probeBmp)
        try {
            $pg.Clear([System.Drawing.Color]::White)
            $pg.DrawImage($sourceBmp, 0, 0, $sourceBmp.Width, $sourceBmp.Height)
        } finally { $pg.Dispose() }
    }

    # Auto-crop transparent AND near-white padding so the artwork fills the icon tile.
    $bounds = Get-ContentBounds -Bitmap $probeBmp `
        -AlphaThreshold $alphaThreshold `
        -LightnessCutoff $lightnessCutoff `
        -SaturationCutoff $saturationCutoff

    # Expand the crop to a centered square so we never distort the aspect ratio.
    $side = [Math]::Max($bounds.Width, $bounds.Height)
    $cx = $bounds.X + $bounds.Width / 2
    $cy = $bounds.Y + $bounds.Height / 2
    $sqX = [int][Math]::Round($cx - $side / 2)
    $sqY = [int][Math]::Round($cy - $side / 2)
    $maxSideW = [Math]::Min($side, $sourceBmp.Width)
    $maxSideH = [Math]::Min($side, $sourceBmp.Height)
    $sqX = [Math]::Max(0, [Math]::Min($sqX, $sourceBmp.Width  - $maxSideW))
    $sqY = [Math]::Max(0, [Math]::Min($sqY, $sourceBmp.Height - $maxSideH))
    $cropRect = New-Object System.Drawing.Rectangle($sqX, $sqY, $maxSideW, $maxSideH)
    $side = $maxSideW

    Write-Host ("Source {0}x{1}; cropped to {2}x{3} at ({4},{5})" -f `
        $sourceBmp.Width, $sourceBmp.Height, $cropRect.Width, $cropRect.Height, $cropRect.X, $cropRect.Y)

    $cropped = New-Object System.Drawing.Bitmap($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $cg = [System.Drawing.Graphics]::FromImage($cropped)
    try {
        $cg.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $cg.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $cg.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $cg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $cg.Clear([System.Drawing.Color]::Transparent)
        $destRect = New-Object System.Drawing.Rectangle(0, 0, $side, $side)
        $cg.DrawImage($probeBmp, $destRect, $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
    } finally {
        $cg.Dispose()
    }

    if (-not [object]::ReferenceEquals($probeBmp, $sourceBmp)) {
        $probeBmp.Dispose()
    }

    $entries = foreach ($size in $sizes) {
        $target = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($target)
        try {
            $g.InterpolationMode   = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.SmoothingMode       = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.PixelOffsetMode     = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.CompositingQuality  = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($cropped, 0, 0, $size, $size)
        } finally {
            $g.Dispose()
        }
        $ms = New-Object System.IO.MemoryStream
        $target.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $target.Dispose()
        [PSCustomObject]@{ Size = $size; Bytes = $ms.ToArray() }
    }

    $cropped.Dispose()
}
finally {
    $sourceBmp.Dispose()
}

$totalHeader = 6 + (16 * $entries.Count)
$outStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($outStream)

$writer.Write([uint16]0)   # reserved
$writer.Write([uint16]1)   # type = icon
$writer.Write([uint16]$entries.Count)

$offset = $totalHeader
foreach ($e in $entries) {
    $dim = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
    $writer.Write([byte]$dim)            # width
    $writer.Write([byte]$dim)            # height
    $writer.Write([byte]0)               # color count
    $writer.Write([byte]0)               # reserved
    $writer.Write([uint16]1)             # planes
    $writer.Write([uint16]32)            # bits per pixel
    $writer.Write([uint32]$e.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $e.Bytes.Length
}
foreach ($e in $entries) {
    $writer.Write($e.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($Output, $outStream.ToArray())
$writer.Dispose()

Write-Host "Wrote $Output ($([math]::Round((Get-Item $Output).Length / 1KB, 1)) KB, sizes: $($sizes -join ', '))"
