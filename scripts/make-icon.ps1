#Requires -Version 5.1
<#
    Build src\WhatAmIDoing\app.ico for Windows (taskbar, tray, shortcuts, installer).

    Default: render the built-in clock + magnifier artwork — transparent outside the
    clock face, blue-to-teal fill inside the dial only (no white square corners).

    Optional: pass -FromPng to build from assets\app-icon-source.png instead.

    Usage:
        powershell -ExecutionPolicy Bypass -File .\scripts\make-icon.ps1
        powershell -ExecutionPolicy Bypass -File .\scripts\make-icon.ps1 -FromPng
#>

param(
    [switch]$FromPng,
    [string]$Source = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\app-icon-source.png'),
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\WhatAmIDoing\app.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256

function New-ClockMagnifierBitmap {
    param([int]$Side = 1024)

    $bmp = New-Object System.Drawing.Bitmap($Side, $Side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $cx = $Side / 2.0
        $cy = $Side / 2.0
        $radius = $Side * 0.495
        $ring = [Math]::Max(2.0, $Side * 0.03)

        $blue  = [System.Drawing.Color]::FromArgb(255, 43, 127, 212)
        $teal  = [System.Drawing.Color]::FromArgb(255, 30, 200, 200)
        $white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)

        $gradRect = New-Object System.Drawing.RectangleF(
            [single]($cx - $radius), [single]($cy - $radius),
            [single]($radius * 2), [single]($radius * 2))
        $fillBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $gradRect, $blue, $teal, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
        try {
            $g.FillEllipse($fillBrush, [single]($cx - $radius), [single]($cy - $radius),
                [single]($radius * 2), [single]($radius * 2))
        } finally {
            $fillBrush.Dispose()
        }

        $ringPen = New-Object System.Drawing.Pen($white, [single]$ring)
        $ringPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        try {
            $g.DrawEllipse($ringPen, [single]($cx - $radius), [single]($cy - $radius),
                [single]($radius * 2), [single]($radius * 2))
        } finally {
            $ringPen.Dispose()
        }

        $tickLen = $radius * 0.11
        $tickPen = New-Object System.Drawing.Pen($white, [single]([Math]::Max(1.5, $Side * 0.016)))
        $tickPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $tickPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        try {
            for ($i = 0; $i -lt 12; $i++) {
                $angle = ($i * 30.0 - 90.0) * [Math]::PI / 180.0
                $inner = $radius * 0.82
                $outer = $radius * 0.92
                $x1 = $cx + $inner * [Math]::Cos($angle)
                $y1 = $cy + $inner * [Math]::Sin($angle)
                $x2 = $cx + $outer * [Math]::Cos($angle)
                $y2 = $cy + $outer * [Math]::Sin($angle)
                $g.DrawLine($tickPen, [single]$x1, [single]$y1, [single]$x2, [single]$y2)
            }
        } finally {
            $tickPen.Dispose()
        }

        $handPen = New-Object System.Drawing.Pen($white, [single]([Math]::Max(2.0, $Side * 0.025)))
        $handPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $handPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        try {
            $hourAngle = (10.0 * 30.0 - 90.0) * [Math]::PI / 180.0
            $minAngle  = (10.0 * 6.0  - 90.0) * [Math]::PI / 180.0
            $hourLen = $radius * 0.48
            $minLen  = $radius * 0.66
            $g.DrawLine($handPen, [single]$cx, [single]$cy,
                [single]($cx + $hourLen * [Math]::Cos($hourAngle)),
                [single]($cy + $hourLen * [Math]::Sin($hourAngle)))
            $g.DrawLine($handPen, [single]$cx, [single]$cy,
                [single]($cx + $minLen * [Math]::Cos($minAngle)),
                [single]($cy + $minLen * [Math]::Sin($minAngle)))
        } finally {
            $handPen.Dispose()
        }

        $hub = [Math]::Max(3.0, $Side * 0.028)
        $hubBrush = New-Object System.Drawing.SolidBrush($white)
        try {
            $g.FillEllipse($hubBrush, [single]($cx - $hub / 2), [single]($cy - $hub / 2), [single]$hub, [single]$hub)
        } finally {
            $hubBrush.Dispose()
        }

        # Magnifying glass — stroke only, transparent outside the clock fill.
        $glassCx = $cx + $radius * 0.42
        $glassCy = $cy + $radius * 0.42
        $glassR  = $radius * 0.34
        $glassStroke = [Math]::Max(2.0, $Side * 0.029)
        $glassPen = New-Object System.Drawing.Pen($white, [single]$glassStroke)
        $glassPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $glassPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $glassPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        try {
            $g.DrawEllipse($glassPen,
                [single]($glassCx - $glassR), [single]($glassCy - $glassR),
                [single]($glassR * 2), [single]($glassR * 2))
            $handleLen = $glassR * 0.95
            $handleAngle = 45.0 * [Math]::PI / 180.0
            $hx1 = $glassCx + ($glassR * 0.72) * [Math]::Cos($handleAngle)
            $hy1 = $glassCy + ($glassR * 0.72) * [Math]::Sin($handleAngle)
            $hx2 = $hx1 + $handleLen * [Math]::Cos($handleAngle)
            $hy2 = $hy1 + $handleLen * [Math]::Sin($handleAngle)
            $g.DrawLine($glassPen, [single]$hx1, [single]$hy1, [single]$hx2, [single]$hy2)
        } finally {
            $glassPen.Dispose()
        }
    } finally {
        $g.Dispose()
    }

    return $bmp
}

function Test-IsPaddingPixel {
    param([byte]$R, [byte]$G, [byte]$B, [byte]$A,
          [int]$AlphaThreshold, [int]$LightnessCutoff, [int]$SaturationCutoff)

    if ($A -le $AlphaThreshold) { return $true }
    $chMin = [Math]::Min($R, [Math]::Min($G, $B))
    $chMax = [Math]::Max($R, [Math]::Max($G, $B))
    return ($chMin -ge $LightnessCutoff) -and (($chMax - $chMin) -le $SaturationCutoff)
}

function Convert-PaddingToTransparent {
    param([System.Drawing.Bitmap]$Bitmap,
          [int]$AlphaThreshold = 20,
          [int]$LightnessCutoff = 200,
          [int]$SaturationCutoff = 20)

    $w = $Bitmap.Width; $h = $Bitmap.Height
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $bytes  = New-Object byte[] ($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        for ($y = 0; $y -lt $h; $y++) {
            $row = $y * $stride
            for ($x = 0; $x -lt $w; $x++) {
                $i = $row + $x * 4
                $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]; $a = $bytes[$i + 3]
                if (Test-IsPaddingPixel -R $r -G $g -B $b -A $a `
                    -AlphaThreshold $AlphaThreshold -LightnessCutoff $LightnessCutoff `
                    -SaturationCutoff $SaturationCutoff) {
                    $bytes[$i + 3] = 0
                }
            }
        }
        [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    } finally {
        $Bitmap.UnlockBits($data)
    }
}

function Get-ContentBounds {
    param([System.Drawing.Bitmap]$Bitmap, [int]$AlphaThreshold, [int]$LightnessCutoff, [int]$SaturationCutoff)

    $w = $Bitmap.Width; $h = $Bitmap.Height
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
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
            $i = $row + $x * 4
            $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]; $a = $bytes[$i + 3]
            if (-not (Test-IsPaddingPixel -R $r -G $g -B $b -A $a `
                -AlphaThreshold $AlphaThreshold -LightnessCutoff $LightnessCutoff `
                -SaturationCutoff $SaturationCutoff)) {
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

function Get-IconArtworkBitmap {
    param([switch]$FromPng, [string]$Source)

    if (-not $FromPng) {
        Write-Host 'Rendering built-in clock + magnifier (transparent outside dial).'
        return New-ClockMagnifierBitmap -Side 1024
    }

    if (-not (Test-Path $Source)) {
        throw "Source PNG not found: $Source"
    }

    $alphaThreshold = 20
    $lightnessCutoff = 200
    $saturationCutoff = 20

    $sourceBmp = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
    try {
        $probeBmp = if ($sourceBmp -is [System.Drawing.Bitmap] -and
                         $sourceBmp.PixelFormat -eq [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) {
            $sourceBmp
        } else {
            New-Object System.Drawing.Bitmap($sourceBmp.Width, $sourceBmp.Height,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        }
        if (-not [object]::ReferenceEquals($probeBmp, $sourceBmp)) {
            $pg = [System.Drawing.Graphics]::FromImage($probeBmp)
            try {
                $pg.Clear([System.Drawing.Color]::Transparent)
                $pg.DrawImage($sourceBmp, 0, 0, $sourceBmp.Width, $sourceBmp.Height)
            } finally { $pg.Dispose() }
        }

        Convert-PaddingToTransparent -Bitmap $probeBmp `
            -AlphaThreshold $alphaThreshold -LightnessCutoff $lightnessCutoff `
            -SaturationCutoff $saturationCutoff

        $bounds = Get-ContentBounds -Bitmap $probeBmp `
            -AlphaThreshold $alphaThreshold -LightnessCutoff $lightnessCutoff `
            -SaturationCutoff $saturationCutoff

        $side = [Math]::Max($bounds.Width, $bounds.Height)
        $cx = $bounds.X + $bounds.Width / 2
        $cy = $bounds.Y + $bounds.Height / 2
        $sqX = [int][Math]::Round($cx - $side / 2)
        $sqY = [int][Math]::Round($cy - $side / 2)
        $maxSide = [Math]::Min($side, $sourceBmp.Width, $sourceBmp.Height)
        $sqX = [Math]::Max(0, [Math]::Min($sqX, $sourceBmp.Width - $maxSide))
        $sqY = [Math]::Max(0, [Math]::Min($sqY, $sourceBmp.Height - $maxSide))
        $cropRect = New-Object System.Drawing.Rectangle($sqX, $sqY, $maxSide, $maxSide)

        Write-Host ("Source {0}x{1}; cropped to {2}x{3} at ({4},{5})" -f `
            $sourceBmp.Width, $sourceBmp.Height, $cropRect.Width, $cropRect.Height, $cropRect.X, $cropRect.Y)

        $cropped = New-Object System.Drawing.Bitmap($maxSide, $maxSide,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $cg = [System.Drawing.Graphics]::FromImage($cropped)
        try {
            $cg.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $cg.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $cg.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $cg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $cg.Clear([System.Drawing.Color]::Transparent)
            $cg.DrawImage($probeBmp, 0, 0, $maxSide, $maxSide, $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
        } finally {
            $cg.Dispose()
        }

        if (-not [object]::ReferenceEquals($probeBmp, $sourceBmp)) {
            $probeBmp.Dispose()
        }

        return $cropped
    } finally {
        $sourceBmp.Dispose()
    }
}

function Write-IconFile {
    param([System.Drawing.Bitmap]$Artwork, [string]$Output, [int[]]$Sizes)

    $side = $Artwork.Width
    $entries = foreach ($size in $Sizes) {
        $target = New-Object System.Drawing.Bitmap($size, $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($target)
        try {
            $g.InterpolationMode   = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.SmoothingMode       = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.PixelOffsetMode     = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.CompositingQuality  = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($Artwork, 0, 0, $size, $size)
        } finally {
            $g.Dispose()
        }
        $ms = New-Object System.IO.MemoryStream
        $target.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $target.Dispose()
        [PSCustomObject]@{ Size = $size; Bytes = $ms.ToArray() }
    }

    $totalHeader = 6 + (16 * $entries.Count)
    $outStream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($outStream)

    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$entries.Count)

    $offset = $totalHeader
    foreach ($e in $entries) {
        $dim = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
        $writer.Write([byte]$dim)
        $writer.Write([byte]$dim)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
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

    Write-Host "Wrote $Output ($([math]::Round((Get-Item $Output).Length / 1KB, 1)) KB, artwork ${side}px, sizes: $($Sizes -join ', '))"
}

$artwork = Get-IconArtworkBitmap -FromPng:$FromPng -Source $Source
try {
    if (-not $FromPng) {
        $artwork.Save($Source, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Updated source PNG: $Source"
    }
    Write-IconFile -Artwork $artwork -Output $Output -Sizes $sizes
} finally {
    $artwork.Dispose()
}
