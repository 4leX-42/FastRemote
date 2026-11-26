# Windows PowerShell 5.1. Only the process created below is automated.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $root '.work'
$instance = Join-Path $work ('ui-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($instance)
$utf8 = New-Object Text.UTF8Encoding($false)
$rows = @(for ($i=0; $i -lt 200; $i++) {
    $site = if ($i % 2) { 'West' } else { 'East' }
    $name = if ($i -eq 17) { 'Selected ' + [char]0xE9 + 'tage' } else { "Desk $i" }
    @{ Id="test-$i"; Folder=$site; Name=$name; Host='quaygate-selftest.invalid'; Port=3389; Domain='LAB'; Username='test'; Descr='Desk'; Source='manual'; Admin=$false; FullScreen=$false }
})
[IO.File]::WriteAllText((Join-Path $instance 'connections.json'),(ConvertTo-Json -InputObject $rows -Compress),$utf8)
[IO.File]::WriteAllText((Join-Path $instance 'settings.json'),'{"Language":"en","Scale":1.2,"Theme":"Carbon","SiteOrder":["East","West"]}',$utf8)
$A = [Windows.Automation.AutomationElement]
$TS = [Windows.Automation.TreeScope]
function Check([bool]$ok,[string]$name) {
    if (-not $ok) { throw "Failed: $name" }
    "PASS $name"
}
function Find-Id($parent,[string]$id) {
    $condition = New-Object Windows.Automation.PropertyCondition ($A::AutomationIdProperty), $id
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $element = $parent.FindFirst($TS::Descendants,$condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 25
    } while ($timer.ElapsedMilliseconds -lt 4000)
    throw "Missing control: $id"
}
function Invoke-Control($element) { $element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Set-Text($element,[string]$text) { $element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($text) }
function Select-Item($element) { $element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function List-Items($element) {
    $cond = New-Object Windows.Automation.PropertyCondition ($A::ControlTypeProperty), ([Windows.Automation.ControlType]::ListItem)
    $element.FindAll($TS::Descendants,$cond)
}
function Await-Name($element,[string]$name) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($element.Current.Name -ne $name -and $clock.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 10 }
    Check ($element.Current.Name -eq $name) $name
}
function Click([string]$id) { Invoke-Control (Find-Id $main $id) }
function Read-Settings { Get-Content -Raw (Join-Path $instance 'settings.json') | ConvertFrom-Json }
function Start-Isolated {
    $info = New-Object Diagnostics.ProcessStartInfo (Join-Path $root 'dist\Quaygate.exe')
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.EnvironmentVariables['QUAYGATE_HOME'] = $instance
    $info.EnvironmentVariables['QUAYGATE_OFFSCREEN'] = '1'
    [Diagnostics.Process]::Start($info)
}
$process = Start-Isolated
try {
    $cond = New-Object Windows.Automation.PropertyCondition ($A::ProcessIdProperty), $process.Id
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        if ($process.HasExited) { throw "Startup exit $($process.ExitCode)" }
        $main = $A::RootElement.FindFirst($TS::Children,$cond)
        if (-not $main) { Start-Sleep -Milliseconds 50 }
    } while (-not $main -and $timer.ElapsedMilliseconds -lt 12000)
    Check ($null -ne $main) 'isolated window'
    $count = Find-Id $main 'Count'
    Await-Name $count '200 connections'
    $search = Find-Id $main 'Search'
    $valuePattern = $search.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $valuePattern.SetValue('etage WEST')
    $setMs = $clock.Elapsed.TotalMilliseconds
    while ($count.Current.Name -ne '1 of 200 connections' -and $clock.ElapsedMilliseconds -lt 1000) {}
    $clock.Stop()
    $ms = $clock.Elapsed.TotalMilliseconds
    Check ($count.Current.Name -eq '1 of 200 connections') 'accent-insensitive multiword search'
    'Search {0:F2} ms (SetValue {1:F2} ms, 200 rows)' -f $ms,$setMs
    Set-Text $search 'no-results'
    Await-Name $count '0 of 200 connections'
    Await-Name (Find-Id $main 'EmptyList') 'No matching connections'
    Set-Text $search ''
    Click 'BtnNew'
    Click 'DlgOk'
    Await-Name (Find-Id $main 'DlgError') 'Enter a valid host name.'
    Set-Text (Find-Id $main 'FHost') 'quaygate-selftest.invalid'
    Set-Text (Find-Id $main 'FName') 'UI fixture'
    Set-Text (Find-Id $main 'FFolder') 'West'
    Click 'DlgOk'
    Await-Name $count '201 connections'
    Await-Name (Find-Id $main 'DName') 'UI fixture'
    Click 'BtnEdit'
    Set-Text (Find-Id $main 'FName') 'Renamed'
    Click 'DlgOk'
    Await-Name (Find-Id $main 'DName') 'Renamed'
    Click 'BtnEdit'
    Set-Text (Find-Id $main 'FName') 'Discard'
    Click 'DlgCancel'
    Await-Name (Find-Id $main 'DName') 'Renamed'
    Click 'BtnDel'
    Click 'DlgOk'
    Await-Name $count '200 connections'
    Click 'BtnBulk'
    Click 'DlgOk'
    Await-Name (Find-Id $main 'DlgError') 'Enter a password.'
    Click 'DlgCancel'
    Click 'BtnImport'
    [void](Find-Id $main 'IText')
    Click 'DlgCancel'
    $sites = Find-Id $main 'Sites'
    $items = @(List-Items $sites)
    Check ($items.Count -eq 3) 'two sites and All'
    Select-Item $items[0]
    Await-Name $count '100 of 200 connections'
    Select-Item $items[2]
    Await-Name $count '200 connections'
    $before = $search.Current.BoundingRectangle.Height
    Click 'BtnSettings'
    $scale = Find-Id $main 'Scale'
    $scale.GetCurrentPattern([Windows.Automation.RangeValuePattern]::Pattern).SetValue(1.4)
    $theme = Find-Id $main 'Theme'
    $options = @(List-Items $theme)
    Select-Item $options[2]
    Check ($search.Current.BoundingRectangle.Height -gt $before) 'live scale'
    Click 'MoveDown'
    Click 'DlgOk'
    $timer.Restart()
    do { $cfg = Read-Settings; if ($cfg.Theme -eq 'Light') { break }; Start-Sleep -Milliseconds 20 } while ($timer.ElapsedMilliseconds -lt 3000)
    if (-not ($cfg.Scale -eq 1.4 -and $cfg.Theme -eq 'Light' -and $cfg.SiteOrder[0] -eq 'West')) { 'settings now: ' + (Get-Content -Raw (Join-Path $instance 'settings.json')); $de = Find-Id $main 'DlgError'; if ($de) { 'dlg error: ' + $de.Current.Name }; $el = Join-Path $instance 'error.log'; if (Test-Path $el) { Get-Content $el -Tail 15 } }
    Check ($cfg.Scale -eq 1.4 -and $cfg.Theme -eq 'Light' -and $cfg.SiteOrder[0] -eq 'West') 'saved theme, scale and site order'
    $second = Start-Isolated
    try { Check ($second.WaitForExit(6000) -and $second.ExitCode -eq 0 -and -not $process.HasExited) 'second launch signals first' }
    finally { if (-not $second.HasExited) { $second.Kill(); $second.WaitForExit() }; $second.Dispose() }
    Click 'BtnClose'
    Check ($process.WaitForExit(4000) -and $process.ExitCode -eq 0) 'clean exit'
    Check (-not (Test-Path -LiteralPath (Join-Path $instance 'error.log'))) 'no error.log'
    Check ($ms -lt 50) 'search below 50 ms'
}
finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $process.Dispose()
    $target = [IO.Path]::GetFullPath($instance)
    $prefix = [IO.Path]::GetFullPath($work) + [IO.Path]::DirectorySeparatorChar
    if ($target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $target) -match '^ui-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
