<#
.SYNOPSIS
    Helpers for driving Casino Tracker on the emulator from PowerShell (adb + uiautomator).

.DESCRIPTION
    Dot-source this file, then use e.g.
        Show-Ui                     # list visible texts with tap coordinates
        Tap-Text "Start session"    # tap an element by its text
        Type-Text "100"             # type into the focused field
        Shot "after-buyin"          # screenshot into tools\shots\
    Note: while a session is running, the 1-second clock on the Session tab keeps the UI busy, so
    uiautomator cannot take a dump there ("could not get idle state"). Use Shot + Tap x y on that tab;
    every other page dumps fine.
#>
$script:Adb = if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe' } else { 'C:\Android\android-sdk\platform-tools\adb.exe' }
$script:Shots = Join-Path $PSScriptRoot 'shots'
if (-not (Test-Path $script:Shots)) { New-Item -ItemType Directory $script:Shots | Out-Null }

function adb { & $script:Adb @args }

function Get-UiNodes {
    # Dump the current accessibility tree and return flat nodes with centre coordinates.
    $null = & $script:Adb shell uiautomator dump /sdcard/ui.xml 2>&1
    $xml = (& $script:Adb exec-out cat /sdcard/ui.xml) -join ''
    if (-not $xml.StartsWith('<')) { $xml = $xml.Substring($xml.IndexOf('<')) }
    [xml]$doc = $xml
    $nodes = $doc.SelectNodes('//node')
    foreach ($n in $nodes) {
        if ($n.bounds -match '\[(\d+),(\d+)\]\[(\d+),(\d+)\]') {
            [pscustomobject]@{
                Text    = $n.text
                Desc    = $n.'content-desc'
                Class   = $n.class
                Click   = $n.clickable
                Checked = $n.checked
                Enabled = $n.enabled
                X       = [int](([int]$Matches[1] + [int]$Matches[3]) / 2)
                Y       = [int](([int]$Matches[2] + [int]$Matches[4]) / 2)
                L       = [int]$Matches[1]; T = [int]$Matches[2]; R = [int]$Matches[3]; B = [int]$Matches[4]
            }
        }
    }
}

function Show-Ui([string] $Filter = '') {
    Get-UiNodes | Where-Object { ($_.Text -or $_.Desc) -and ($_.Text -like "*$Filter*" -or $_.Desc -like "*$Filter*") } |
        ForEach-Object { "{0,-24} {1,4},{2,-4} click={3} chk={4} en={5} | {6}" -f ($_.Class -replace '^android\.widget\.',''), $_.X, $_.Y, $_.Click, $_.Checked, $_.Enabled, ($(if ($_.Text) { $_.Text } else { "[desc] $($_.Desc)" })) }
}

function Find-Node([string] $Text, [string] $Class = '', [switch] $Exact, [int] $Index = 0) {
    $all = Get-UiNodes
    $hits = $all | Where-Object {
        (($Exact -and ($_.Text -eq $Text -or $_.Desc -eq $Text)) -or (-not $Exact -and ($_.Text -like "*$Text*" -or $_.Desc -like "*$Text*"))) -and
        ($Class -eq '' -or $_.Class -like "*$Class*")
    }
    $hits = @($hits)
    if ($hits.Count -le $Index) { return $null }
    return $hits[$Index]
}

function Tap([int] $X, [int] $Y) { $null = & $script:Adb shell input tap $X $Y; Start-Sleep -Milliseconds 600 }

function Tap-Text([string] $Text, [string] $Class = '', [switch] $Exact, [int] $Index = 0, [int] $Retries = 5) {
    for ($i = 0; $i -lt $Retries; $i++) {
        $n = Find-Node -Text $Text -Class $Class -Exact:$Exact -Index $Index
        if ($n) { Tap $n.X $n.Y; return $n }
        Start-Sleep -Milliseconds 700
    }
    throw "UI element not found: '$Text' (class '$Class')"
}

function Type-Text([string] $Text) {
    # adb input text needs spaces escaped as %s and a few shell metacharacters escaped.
    $escaped = $Text -replace ' ', '%s' -replace '([&|<>()\\;"''])', '\$1'
    $null = & $script:Adb shell input text $escaped
    Start-Sleep -Milliseconds 400
}

function Clear-Field {
    $null = & $script:Adb shell input keyevent KEYCODE_MOVE_END
    $null = & $script:Adb shell input keyevent --longpress $(1..40 | ForEach-Object { 'KEYCODE_DEL' })
    Start-Sleep -Milliseconds 300
}

function Type-Into([string] $FieldText, [string] $Value, [int] $Index = 0) {
    # Tap the field (found by its hint/current text), select all, type the value.
    $n = Tap-Text -Text $FieldText -Class 'EditText' -Index $Index
    $null = & $script:Adb shell input keyevent KEYCODE_CTRL_LEFT KEYCODE_A 2>$null
    Clear-Field
    Type-Text $Value
    return $n
}

function Press-Back { $null = & $script:Adb shell input keyevent KEYCODE_BACK; Start-Sleep -Milliseconds 700 }
function Test-Keyboard { ((& $script:Adb shell dumpsys input_method) -join "`n") -match 'mInputShown=true' }
function Hide-Keyboard {
    if (Test-Keyboard) { $null = & $script:Adb shell input keyevent KEYCODE_BACK; Start-Sleep -Milliseconds 500 }
}
function Scroll-Down([int] $Times = 1) { for ($i = 0; $i -lt $Times; $i++) { $null = & $script:Adb shell input swipe 540 1700 540 700 300; Start-Sleep -Milliseconds 500 } }
function Scroll-Up([int] $Times = 1) { for ($i = 0; $i -lt $Times; $i++) { $null = & $script:Adb shell input swipe 540 700 540 1700 300; Start-Sleep -Milliseconds 500 } }

function Shot([string] $Name) {
    $file = Join-Path $script:Shots "$Name.png"
    # Binary data must not pass through the PowerShell pipeline; let cmd redirect it.
    $null = & cmd.exe /c "`"$script:Adb`" exec-out screencap -p > `"$file`""
    return $file
}

function Wait-Text([string] $Text, [int] $Seconds = 10) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        if (Find-Node -Text $Text) { return $true }
        Start-Sleep -Milliseconds 700
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Assert-Text([string] $Text, [string] $Because = '') {
    if (-not (Wait-Text $Text 6)) { throw "ASSERT FAILED: expected text '$Text' on screen. $Because" }
    Write-Host "  ok: '$Text'" -ForegroundColor Green
}

function Launch-App {
    $null = & $script:Adb shell monkey -p com.moritz.casinotracker -c android.intent.category.LAUNCHER 1 2>&1
    Start-Sleep -Seconds 3
}

function Stop-App { $null = & $script:Adb shell am force-stop com.moritz.casinotracker }

function Get-Crashes {
    & $script:Adb logcat -d -b crash 2>&1 | Select-Object -Last 60
}
