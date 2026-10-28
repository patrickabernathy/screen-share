# Regenerates src/ScreenShare/app.ico, the icon embedded in ScreenShare.exe.
# It draws the same idle (grey) monitor as AppIcon.Create, so keep the two in sync if the design changes.
# Usage: powershell -File tools/New-AppIcon.ps1

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$out = Join-Path $PSScriptRoot '..\src\ScreenShare\app.ico'

# The design is laid out on a 32px grid; each rect is x, y, w, h on that grid.
$dark = [System.Drawing.Color]::FromArgb(36, 41, 47)
$grey = [System.Drawing.Color]::FromArgb(110, 118, 129)
$shapes = @(
    @{ Color = $dark; Rect = 1, 3, 30, 21 },   # frame
    @{ Color = $grey; Rect = 3, 5, 26, 17 },   # screen
    @{ Color = $dark; Rect = 13, 24, 6, 3 },   # stand neck
    @{ Color = $dark; Rect = 9, 27, 14, 3 }    # stand base
)

function Snap([double]$v) { [int][math]::Round($v, [MidpointRounding]::AwayFromZero) }

$images = foreach ($size in $sizes) {
    $scale = $size / 32.0
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    foreach ($s in $shapes) {
        $x, $y, $w, $h = $s.Rect
        # Snap edges to whole pixels so small sizes stay crisp instead of blurring.
        $left = Snap ($x * $scale); $top = Snap ($y * $scale)
        $right = Snap (($x + $w) * $scale); $bottom = Snap (($y + $h) * $scale)
        $brush = New-Object System.Drawing.SolidBrush $s.Color
        $g.FillRectangle($brush, $left, $top, $right - $left, $bottom - $top)
        $brush.Dispose()
    }
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $ms.ToArray() }
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$img.Bytes.Length); $bw.Write([uint32]$offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) { $bw.Write($img.Bytes) }
$bw.Dispose()

Write-Host "Wrote $((Resolve-Path $out).Path)"
