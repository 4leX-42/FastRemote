# Draws the Quaygate logo (vector, 64-unit grid) and writes logo-<size>.png and Quaygate.ico next to this script.
# Concept: one monoline screen whose corner breaks out into a stroke, a "Q" and a jump to a remote machine.
param([string]$Accent = '#C9A96E', [string]$Tile = '#141416', [string]$OutDir = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase, PresentationFramework

function Brush([string]$c, [double]$o = 1) { $b = New-Object Windows.Media.SolidColorBrush ([Windows.Media.ColorConverter]::ConvertFromString($c)); $b.Opacity = $o; $b.Freeze(); $b }
function Geo([string]$d) { $g = [Windows.Media.Geometry]::Parse($d); $g.Freeze(); $g }

function New-LogoVisual([int]$Size) {
    $dv = New-Object Windows.Media.DrawingVisual
    $dc = $dv.RenderOpen()
    $dc.PushTransform((New-Object Windows.Media.ScaleTransform ($Size / 64), ($Size / 64)))
    $gold = Brush $Accent; $tile = Brush $Tile
    $small = $Size -le 24
    # rounded charcoal tile, no frame
    $dc.DrawGeometry($tile, $null, (Geo 'M14,0 H50 A14,14 0 0 1 64,14 V50 A14,14 0 0 1 50,64 H14 A14,14 0 0 1 0,50 V14 A14,14 0 0 1 14,0 Z'))
    # monoline mark: a screen whose corner turns into an exit stroke (a "Q" and a remote jump)
    $t = if ($small) { 7 } else { 5.5 }
    $pen = New-Object Windows.Media.Pen $gold, $t
    $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
    $dc.DrawGeometry($null, $pen, (Geo 'M41,45 H21 A6,6 0 0 1 15,39 V21 A6,6 0 0 1 21,15 H43 A6,6 0 0 1 49,21 V37'))
    $dc.DrawGeometry($null, $pen, (Geo 'M36,36 L50,50'))
    $dc.Pop(); $dc.Close()
    $dv
}

function Get-Png([int]$Size) {
    $bmp = New-Object Windows.Media.Imaging.RenderTargetBitmap $Size, $Size, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render((New-LogoVisual $Size))
    $enc = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object IO.MemoryStream; $enc.Save($ms); , $ms.ToArray()
}

foreach ($s in 512, 256, 64, 32, 16) { [IO.File]::WriteAllBytes((Join-Path $OutDir "logo-$s.png"), (Get-Png $s)) }

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = foreach ($s in $sizes) { , @($s, (Get-Png $s)) }
$fs = [IO.File]::Create((Join-Path $OutDir 'Quaygate.ico')); $bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $d = if ($f[0] -ge 256) { 0 } else { $f[0] }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$f[1].Length); $bw.Write([uint32]$offset)
    $offset += $f[1].Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f[1]) }
$bw.Close()
'ok'
