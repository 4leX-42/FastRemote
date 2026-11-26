# Draws the Quaygate logo (vector, 64-unit grid) and writes logo-<size>.png and Quaygate.ico next to this script.
# Concept: a local screen sending an arrow to a remote one behind it, cut corners like the app buttons.
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
    # tile with two cut corners
    $dc.DrawGeometry($tile, $null, (Geo 'M12,0 H64 V52 L52,64 H0 V12 Z'))
    if (-not $small) {
        $edge = New-Object Windows.Media.Pen ((Brush $Accent 0.35)), 1.2
        $dc.DrawGeometry($null, $edge, (Geo 'M12.6,0.6 H63.4 V51.8 L51.8,63.4 H0.6 V12.6 Z'))
    }
    # back screen (the remote one), dimmed outline partly behind the front screen
    $back = New-Object Windows.Media.Pen ((Brush $Accent 0.5)), $(if ($small) { 4.5 } else { 3.2 })
    $back.LineJoin = 'Miter'
    $dc.DrawGeometry($null, $back, (Geo 'M30,10.5 H54 V32 H45'))
    $dc.DrawGeometry($null, $back, (Geo 'M30,10.5 V18'))
    # front screen: gold frame with a cut corner, dark display
    $dc.DrawGeometry($gold, $null, (Geo 'M15,19 H45 V45 H9 V25 Z'))
    $dc.DrawGeometry($tile, $null, (Geo 'M16.8,23 H41 V41 H13 V26.8 Z'))
    # arrow from the display towards the remote screen
    $w = if ($small) { 4.2 } else { 3.2 }
    $pen = New-Object Windows.Media.Pen $gold, $w
    $pen.StartLineCap = 'Flat'; $pen.EndLineCap = 'Flat'
    $dc.DrawGeometry($null, $pen, (Geo 'M18.5,36.5 L31,27'))
    $dc.DrawGeometry($gold, $null, (Geo 'M35.5,23.5 L34.2,33 L25.6,24.8 Z'))
    # stand
    $dc.DrawGeometry($gold, $null, (Geo 'M24,45 H30 V49.5 H24 Z'))
    $dc.DrawGeometry($gold, $null, (Geo 'M17,49.5 H37 V52.5 H17 Z'))
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
