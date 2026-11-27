# Draws the FastRemote logo (a screen whose top right corner is broken by a lightning bolt) and writes
# logo-<size>.png and FastRemote.ico next to this script. Flat colours, 64-unit grid; .ico frames under 256 px are DIB.
param([string]$OutDir = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase, PresentationFramework
function B([string]$h) { $b = New-Object Windows.Media.SolidColorBrush ([Windows.Media.ColorConverter]::ConvertFromString($h)); $b.Freeze(); $b }
function G([string]$d) { [Windows.Media.Geometry]::Parse($d) }
function P([string]$c, [double]$w) { $p = New-Object Windows.Media.Pen (B $c), $w; $p.LineJoin = 'Round'; $p.StartLineCap = 'Round'; $p.EndLineCap = 'Round'; $p }
$INK = '#131315'; $GOLD = '#D2AE6D'; $PALE = '#ECE6DA'

function Render([int]$Size) {
    $s = $Size -le 24; $w = if ($s) { 5.5 } else { 4 }
    $dv = New-Object Windows.Media.DrawingVisual; $dc = $dv.RenderOpen()
    $dc.PushTransform((New-Object Windows.Media.ScaleTransform ($Size / 64), ($Size / 64)))
    $dc.DrawGeometry((B $INK), $null, (G 'M14,0 H50 A14,14 0 0 1 64,14 V50 A14,14 0 0 1 50,64 H14 A14,14 0 0 1 0,50 V14 A14,14 0 0 1 14,0 Z'))
    $dc.DrawGeometry($null, (P $GOLD $w), (G 'M33,13 H15 A4,4 0 0 0 11,17 V41 A4,4 0 0 0 15,45 H49 A4,4 0 0 0 53,41 V29'))
    $dc.DrawGeometry((B $PALE), $null, (G 'M47,4 L36,22 H44 L40,36 L54,16 H46 L50,4 Z'))
    $dc.DrawGeometry($null, (P $GOLD $w), (G 'M25,52 H39'))
    $dc.Pop(); $dc.Close()
    $bmp = New-Object Windows.Media.Imaging.RenderTargetBitmap $Size, $Size, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32); $bmp.Render($dv); $bmp
}
function Get-Png($bmp) { $e = New-Object Windows.Media.Imaging.PngBitmapEncoder; $e.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp)); $m = New-Object IO.MemoryStream; $e.Save($m); , $m.ToArray() }
function Get-Dib($bmp) {
    $n = $bmp.PixelWidth; $stride = $n * 4; $px = New-Object byte[] ($stride * $n)
    (New-Object Windows.Media.Imaging.FormatConvertedBitmap $bmp, ([Windows.Media.PixelFormats]::Bgra32), $null, 0).CopyPixels($px, $stride, 0)
    $ms = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $ms
    $w.Write([uint32]40); $w.Write([int32]$n); $w.Write([int32]($n * 2)); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]0); $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $n - 1; $y -ge 0; $y--) { $w.Write($px, $y * $stride, $stride) }
    $w.Write((New-Object byte[] ([int]([Math]::Ceiling($n / 32.0) * 4) * $n))); $w.Flush(); , $ms.ToArray()
}

foreach ($n in 512, 256, 64, 32, 16) { [IO.File]::WriteAllBytes((Join-Path $OutDir "logo-$n.png"), (Get-Png (Render $n))) }
$frames = foreach ($n in 16, 20, 24, 32, 40, 48, 64, 128, 256) { $b = Render $n; , @($n, $(if ($n -ge 256) { Get-Png $b } else { Get-Dib $b })) }
$fs = [IO.File]::Create((Join-Path $OutDir 'FastRemote.ico')); $bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $d = if ($f[0] -ge 256) { 0 } else { $f[0] }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$f[1].Length); $bw.Write([uint32]$offset); $offset += $f[1].Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f[1]) }
$bw.Close()
'ok'
