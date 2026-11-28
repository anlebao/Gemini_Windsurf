# Regenerate the 6 default KhachLink profile icons as VALID 512x512 PNGs
# (the 2026-10-15 originals were malformed — Chrome 155 rejected them:
#  IHDR colortype RGBA but RGB-sized IDAT → "not a valid image" → not installable)
Add-Type -AssemblyName System.Drawing
$dir = 'C:\VibeCoding\Gemini_Windsurf\2_Gateway\wwwroot\khachlink-icons'
$specs = @(
    @{ n = 'default.png';      c = [System.Drawing.Color]::FromArgb(44, 95, 45);    letter = 'V' },
    @{ n = 'directory.png';    c = [System.Drawing.Color]::FromArgb(44, 95, 45);    letter = 'T' },
    @{ n = 'fullcommerce.png'; c = [System.Drawing.Color]::FromArgb(37, 99, 235);   letter = 'V' },
    @{ n = 'logistics.png';    c = [System.Drawing.Color]::FromArgb(180, 83, 9);    letter = 'L' },
    @{ n = 'jobmarket.png';    c = [System.Drawing.Color]::FromArgb(124, 58, 237);  letter = 'J' },
    @{ n = 'reseller.png';     c = [System.Drawing.Color]::FromArgb(15, 118, 110);  letter = 'R' }
)
foreach ($s in $specs) {
    $bmp = New-Object System.Drawing.Bitmap 512, 512
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear($s.c)
    $font = New-Object System.Drawing.Font('Arial', 260, [System.Drawing.FontStyle]::Bold)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF 0, 0, 512, 512
    $g.DrawString($s.letter, $font, [System.Drawing.Brushes]::White, $rect, $fmt)
    $path = Join-Path $dir $s.n
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("OK " + $s.n)
}