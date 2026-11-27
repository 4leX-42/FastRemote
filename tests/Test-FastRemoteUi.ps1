# Windows PowerShell 5.1. Only the process created below is automated, with its own data folder.
param([string]$Exe)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root = Split-Path -Parent $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path $root 'src\FastRemote\bin\Release\net48\FastRemote.exe' }
$work = Join-Path $root '.work'
$instance = Join-Path $work ('ui-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($instance)
$utf8 = New-Object Text.UTF8Encoding($false)
$rows = @(for ($i=0; $i -lt 200; $i++) {
    $site = if ($i % 2) { 'West' } else { 'East' }
    $name = if ($i -eq 17) { 'Selected ' + [char]0xE9 + 'tage' } else { "Desk $i" }
    @{ Id="test-$i"; Folder=$site; Name=$name; Host='fastremote-selftest.invalid'; Port=3389; Domain='LAB'; Username='test'; Descr='Desk'; Source='manual'; Admin=$false; FullScreen=$false }
})
[IO.File]::WriteAllText((Join-Path $instance 'connections.json'),(ConvertTo-Json -InputObject $rows -Compress),$utf8)
# no automatic checks: the test hosts do not exist and nothing should touch the network
[IO.File]::WriteAllText((Join-Path $instance 'settings.json'),'{"Language":"en","Scale":1.2,"Theme":"Carbon","SiteOrder":["East","West"],"CheckAllOnStart":false,"AutoCheck":false,"LayoutVersion":4}',$utf8)
$A = [Windows.Automation.AutomationElement]
$TS = [Windows.Automation.TreeScope]
function Check([bool]$ok,[string]$name) {
    if (-not $ok) { throw "Failed: $name" }
    "PASS $name"
}
function Windows-Of {
    $cond = New-Object Windows.Automation.PropertyCondition ($A::ProcessIdProperty), $process.Id
    @($A::RootElement.FindAll($TS::Children,$cond))
}
# dialogs and the settings window are separate top-level windows of the same process
function Find-Id([string]$id) {
    $condition = New-Object Windows.Automation.PropertyCondition ($A::AutomationIdProperty), $id
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        foreach ($w in Windows-Of) { $element = $w.FindFirst($TS::Descendants,$condition); if ($element) { return $element } }
        Start-Sleep -Milliseconds 25
    } while ($timer.ElapsedMilliseconds -lt 4000)
    throw "Missing control: $id"
}
function Invoke-Control($element) { $element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Set-Text($element,[string]$text) { $element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($text) }
function Select-Item($element) { $element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function List-Items($element) {
    $cond = New-Object Windows.Automation.PropertyCondition ($A::ControlTypeProperty), ([Windows.Automation.ControlType]::ListItem)
    @($element.FindAll($TS::Children,$cond))
}
function Await-Name($element,[string]$name) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($element.Current.Name -ne $name -and $clock.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 10 }
    Check ($element.Current.Name -eq $name) "$name (got '$($element.Current.Name)')"
}
function Click([string]$id) { Invoke-Control (Find-Id $id) }
function Read-Settings { Get-Content -Raw (Join-Path $instance 'settings.json') | ConvertFrom-Json }
function Start-Isolated {
    $info = New-Object Diagnostics.ProcessStartInfo $Exe
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.EnvironmentVariables['FASTREMOTE_HOME'] = $instance
    $info.EnvironmentVariables['FASTREMOTE_OFFSCREEN'] = '1'
    [Diagnostics.Process]::Start($info)
}
$process = Start-Isolated
try {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        if ($process.HasExited) { throw "Startup exit $($process.ExitCode)" }
        $main = @(Windows-Of) | Select-Object -First 1
        if (-not $main) { Start-Sleep -Milliseconds 50 }
    } while (-not $main -and $timer.ElapsedMilliseconds -lt 12000)
    Check ($null -ne $main) 'isolated window'
    $count = Find-Id 'ViewTitle'
    Await-Name $count 'Connections (200)'
    # search: accent-insensitive, every word must match
    $search = Find-Id 'FilterBox'
    $valuePattern = $search.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
    # warm-up: the first filter pays JIT, the measured one is a normal keystroke
    $valuePattern.SetValue('warm-up'); Await-Name $count 'Connections (0)'; $valuePattern.SetValue('')
    Await-Name $count 'Connections (200)'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $valuePattern.SetValue('etage WEST')
    $setMs = $clock.Elapsed.TotalMilliseconds
    while ($count.Current.Name -ne 'Connections (1)' -and $clock.ElapsedMilliseconds -lt 1000) {}
    $clock.Stop()
    $ms = $clock.Elapsed.TotalMilliseconds
    Check ($count.Current.Name -eq 'Connections (1)') 'accent-insensitive multiword search'
    'Search {0:F2} ms (SetValue {1:F2} ms, 200 rows)' -f $ms,$setMs
    Set-Text $search 'no-results'
    Await-Name $count 'Connections (0)'
    Await-Name (Find-Id 'EmptyList') 'No matching connections'
    Set-Text $search ''
    # new, edit, cancel, delete
    Click 'BtnNew'
    Click 'DlgOk'
    Await-Name (Find-Id 'DlgError') 'Enter a valid host name.'
    Set-Text (Find-Id 'FHost') 'fastremote-selftest.invalid'
    Set-Text (Find-Id 'FName') 'UI fixture'
    Set-Text (Find-Id 'FFolder') 'West'
    Click 'DlgOk'
    Await-Name $count 'Connections (201)'
    Await-Name (Find-Id 'DName') 'UI fixture'
    Click 'BtnEdit'
    Set-Text (Find-Id 'FName') 'Renamed'
    Click 'DlgOk'
    Await-Name (Find-Id 'DName') 'Renamed'
    Click 'BtnEdit'
    Set-Text (Find-Id 'FName') 'Discard'
    Click 'DlgCancel'
    Await-Name (Find-Id 'DName') 'Renamed'
    Click 'BtnDelTool'
    Click 'DlgOk'
    Await-Name $count 'Connections (200)'
    # bulk credential needs a password; nothing is written to the credential store
    Click 'NavCred'
    Click 'DlgOk'
    Await-Name (Find-Id 'DlgError') 'Enter a password.'
    Click 'DlgCancel'
    # sites in the sidebar filter the table
    $nav = Find-Id 'Nav'
    $east = List-Items $nav | Where-Object { $_.Current.Name -eq 'East' } | Select-Object -First 1
    Check ($null -ne $east) 'site East in the sidebar'
    Select-Item $east
    Await-Name $count 'East (100)'
    $all = List-Items $nav | Where-Object { $_.Current.Name -eq 'All connections' } | Select-Object -First 1
    Select-Item $all
    Await-Name $count 'Connections (200)'
    # settings: scale and theme preview live and are saved with OK
    $before = $search.Current.BoundingRectangle.Height
    Click 'BtnSettings'
    $pages = Find-Id 'SettingsPages'
    Select-Item ((List-Items $pages) | Where-Object { $_.Current.Name -eq 'Appearance' } | Select-Object -First 1)
    (Find-Id 'Scale').GetCurrentPattern([Windows.Automation.RangeValuePattern]::Pattern).SetValue(1.4)
    Select-Item (Find-Id 'Theme_Light')
    Check ($search.Current.BoundingRectangle.Height -gt $before) 'live scale'
    Click 'DlgOk'
    $timer.Restart()
    do { $cfg = Read-Settings; if ($cfg.Theme -eq 'Light') { break }; Start-Sleep -Milliseconds 20 } while ($timer.ElapsedMilliseconds -lt 3000)
    Check ($cfg.Scale -eq 1.4 -and $cfg.Theme -eq 'Light') 'saved theme and scale'
    # a second start with the same data folder only brings the first one forward
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
