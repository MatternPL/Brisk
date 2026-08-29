# Fjerner hvit bakgrunn rundt et ikon ved aa flyte innover fra kantene,
# slik at lyse piksler inne i motivet beholdes.
param(
    [Parameter(Mandatory = $true)][string]$Inn,
    [Parameter(Mandatory = $true)][string]$Ut,
    [int]$Toleranse = 26
)

Add-Type -AssemblyName System.Drawing

$src = if ($Inn.ToLower().EndsWith(".ico")) {
    $i = New-Object Drawing.Icon($Inn); $b = $i.ToBitmap(); $i.Dispose(); $b
} else {
    [Drawing.Bitmap]::FromFile($Inn)
}

$w = $src.Width; $h = $src.Height
$bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($src, 0, 0, $w, $h)
$g.Dispose()

function ErLys($c) {
    return ($c.R -ge (255 - $Toleranse) -and $c.G -ge (255 - $Toleranse) -and $c.B -ge (255 - $Toleranse))
}

# Flytefyll fra alle kantpiksler
$sett = New-Object 'bool[,]' $w, $h
$ko = New-Object System.Collections.Generic.Queue[int[]]
for ($x = 0; $x -lt $w; $x++) {
    foreach ($y in 0, ($h - 1)) { if (ErLys $bmp.GetPixel($x, $y)) { $ko.Enqueue(@($x, $y)) } }
}
for ($y = 0; $y -lt $h; $y++) {
    foreach ($x in 0, ($w - 1)) { if (ErLys $bmp.GetPixel($x, $y)) { $ko.Enqueue(@($x, $y)) } }
}

$fjernet = 0
while ($ko.Count -gt 0) {
    $p = $ko.Dequeue()
    $x = $p[0]; $y = $p[1]
    if ($x -lt 0 -or $y -lt 0 -or $x -ge $w -or $y -ge $h) { continue }
    if ($sett[$x, $y]) { continue }
    if (-not (ErLys $bmp.GetPixel($x, $y))) { continue }
    $sett[$x, $y] = $true
    $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb(0, 0, 0, 0))
    $fjernet++
    $ko.Enqueue(@(($x + 1), $y)); $ko.Enqueue(@(($x - 1), $y))
    $ko.Enqueue(@($x, ($y + 1))); $ko.Enqueue(@($x, ($y - 1)))
}

$bmp.Save($Ut, [Drawing.Imaging.ImageFormat]::Png)
Write-Output ("Fjernet {0} hvite piksler av {1}. Lagret {2}" -f $fjernet, ($w * $h), $Ut)
$bmp.Dispose(); $src.Dispose()
