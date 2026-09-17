#requires -Version 7
<#
.SYNOPSIS
    Unattended run of the whole opening for a farm type: with the Skip intro checkbox OFF, plays
    the deathbed/cubicle minigames, the bus ride, and the arrival event (stepped with
    tly_eventstep) through to the planning hub; with -SkipIntro, takes the bed shortcut straight
    to the hub with no event. Ends by exiting to title.
#>
param([Parameter(Mandatory)][string]$FarmType, [switch]$SkipIntro)
$ErrorActionPreference = 'Stop'
$Tools = $PSScriptRoot
$Log = Join-Path $env:APPDATA 'StardewValley\ErrorLogs\SMAPI-latest.txt'
function Count { [int](pwsh -NoProfile -File "$Tools/bridge.ps1" -Action count) }
function Send([string]$lines) { pwsh -NoProfile -File "$Tools/bridge.ps1" -Action send -Lines $lines | Out-Null }
function WaitLog([string]$pattern, [int]$from, [int]$timeout = 120) { pwsh -NoProfile -File "$Tools/bridge.ps1" -Action wait -Pattern $pattern -FromLine $from -TimeoutSec $timeout }
function Debug([string]$cmd) { pwsh -NoProfile -File "$Tools/send-smapi-command.ps1" "debug $cmd" | Out-Null }
function Tail([int]$from) { (Get-Content $Log) | Select-Object -Skip $from }

$r = [ordered]@{ Farm = $FarmType; Skip = [bool]$SkipIntro }
$n = Count
if ($SkipIntro) {
    Send "tly_newgame $FarmType skipintro"
    $r.Skipped = WaitLog 'Intro: skipped by the character-creation checkbox' $n 150
    $r.Hub = WaitLog 'Opened planning hub \(week 1' $n 120
    $r.NoEvent = if ((Tail $n) -match 'Event started: 60367|busDoorOpen') { 'FAIL: arrival played' } else { 'ok' }
} else {
    Send "tly_newgame $FarmType"
    # The deathbed and cubicle are minigames with no log lines of their own; the first thing the
    # log shows is the save load during the bus ride, then the stash placement, then the event.
    $r.SaveLoaded = WaitLog 'Run \d+ ready' $n 240
    $r.StashPlaced = WaitLog 'JunimoStashService: placed|Junimo Stash anchored|PlanningShrine' $n 60
    $r.EventStart = WaitLog 'Opening: arrival event running' $n 240
    # Step the event: each tly_eventstep clicks the open speech box on and logs the command index.
    $deadline = (Get-Date).AddMinutes(8)
    do {
        Start-Sleep -Seconds 3
        $s = Count
        Send 'tly_eventstep'
        $line = (WaitLog 'tly_eventstep' $s 10)
        if ($line -match 'Morris' -and -not $r.MorrisSeen) { $r.MorrisSeen = $line }
        if ($line -match 'CommunityCenter' -and -not $r.HallReached) { $r.HallReached = $line }
    } while ((Get-Date) -lt $deadline -and -not ((Tail $n) -match 'Opened planning hub \(week 1'))
    $r.Hub = WaitLog 'Opened planning hub \(week 1' $n 30
    $r.MorrisGoneBeforeHall = if ($r.HallReached -and $r.MorrisSeen) { 'checked by OpeningScriptTests' } else { 'n/a' }
}
$r.Errors = ((Tail $n) | Where-Object { $_ -match '\bERROR\b' } | Select-Object -First 3) -join ' || '
$t = Count
Send 'tly_totitle'
WaitLog 'tly_totitle: exiting' $t 30 | Out-Null
Start-Sleep -Seconds 12
$r.GetEnumerator() | ForEach-Object { "{0,-20} {1}" -f $_.Key, $_.Value }
