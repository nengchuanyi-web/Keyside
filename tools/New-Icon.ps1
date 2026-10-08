$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null
$sizes = @(16, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $scale = $size / 64.0
    $graphics.ScaleTransform($scale, $scale)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(2, 2, 22, 22, 180, 90); $path.AddArc(40, 2, 22, 22, 270, 90)
    $path.AddArc(40, 40, 22, 22, 0, 90); $path.AddArc(2, 40, 22, 22, 90, 90); $path.CloseFigure()
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new(64, 64)), ([System.Drawing.Color]::FromArgb(90, 139, 224)), ([System.Drawing.Color]::FromArgb(45, 80, 145))
    $graphics.FillPath($fill, $path)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(242, 247, 255)), 3
    $graphics.DrawRectangle($pen, 14, 16, 34, 29)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(242, 247, 255))
    foreach ($x in @(20, 29, 38)) { $graphics.FillRectangle($brush, $x, 23, 5, 5) }
    $graphics.FillRectangle($brush, 20, 34, 23, 4)
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$memory.ToArray()
    if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $assetDirectory 'Keyside.png'), $memory.ToArray()) }
    $memory.Dispose(); $brush.Dispose(); $pen.Dispose(); $fill.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$stream = [IO.File]::Create((Join-Path $assetDirectory 'Keyside.ico'))
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output 'Created original Keyside icon (16–256 px).'
