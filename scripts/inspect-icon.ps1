Add-Type -AssemblyName System.Drawing
$path = (Resolve-Path '.\assets\app-icon-source.png')
$img = [System.Drawing.Image]::FromFile($path)
try {
    Write-Host ("Size: {0}x{1}  PixelFormat: {2}" -f $img.Width, $img.Height, $img.PixelFormat)
    $bmp = New-Object System.Drawing.Bitmap($img)
    $corners = @(
        @{ x = 0;              y = 0 },
        @{ x = $bmp.Width - 1; y = 0 },
        @{ x = 0;              y = $bmp.Height - 1 },
        @{ x = $bmp.Width - 1; y = $bmp.Height - 1 },
        @{ x = [int]($bmp.Width / 2); y = [int]($bmp.Height / 2) }
    )
    foreach ($c in $corners) {
        $p = $bmp.GetPixel($c.x, $c.y)
        Write-Host ("Pixel ({0},{1}) = A={2} R={3} G={4} B={5}" -f $c.x, $c.y, $p.A, $p.R, $p.G, $p.B)
    }
    $bmp.Dispose()
}
finally {
    $img.Dispose()
}
