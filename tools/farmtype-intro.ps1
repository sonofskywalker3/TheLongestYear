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
    Send "tly_newgame $FarmType arrival"
    # `arrival` mirrors the bus ride's handoff: the save loads at dayOfMonth==0 and the arrival
    # event fires on its own vanilla precondition (60367/u 0) right away — vanilla's checkForEvents
    # runs every frame regardless of the mod's own activation state. SMAPI's Context.IsWorldReady
    # (and so RunActivation.IsActive, and the mod's own SaveLoaded/"Run N ready" line) stays false
    # until dayOfMonth leaves 0, which only happens once the event's "end beginGame" runs — so the
    # event necessarily comes BEFORE "Run N ready" and the stash/shrine placements, not after, and
    # nothing the mod itself logs can mark "the event started" (every mod driver is gated on
    # RunActivation, which isn't true yet either). Detect it from the eventstep loop instead: the
    # first real cmd[N] (as opposed to "no event") is the start.
    # Step the event: each tly_eventstep clicks the open speech box on and logs the command index.
    # Hang rule: the same cmd[N] on three consecutive polls (30s) is a hang, not a wait — record the
    # index/text/actors/location, `debug ee`, `tly_totitle`, and report DONE_WITH_CONCERNS instead of
    # patching the script.
    $deadline = (Get-Date).AddMinutes(8)
    $lastCmd = $null
    $repeatCount = 0
    $hangCmd = $null
    do {
        Start-Sleep -Seconds 3
        $s = Count
        Send 'tly_eventstep'
        # Match the result line itself, not the "Debug bridge: executing 'tly_eventstep'." echo
        # that's queued first and would otherwise satisfy a bare 'tly_eventstep' pattern.
        $line = (WaitLog "tly_eventstep: (cmd\[|no event)" $s 10)
        if (-not $r.EventStart -and $line -match "cmd\[") { $r.EventStart = $line }
        if ($line -match 'Morris' -and -not $r.MorrisSeen) { $r.MorrisSeen = $line }
        if ($line -match 'CommunityCenter' -and -not $r.HallReached) { $r.HallReached = $line }
        if ($line -match "cmd\[(\d+)\]='([^']*)'") {
            $thisCmd = "$($matches[1])=$($matches[2])"
            if ($thisCmd -eq $lastCmd) { $repeatCount++ } else { $repeatCount = 1; $lastCmd = $thisCmd }
            # A long multi-page `speak` command (multiple #$b# page breaks) legitimately repeats the
            # same cmd[N] for 3 consecutive polls while tly_eventstep pages through it one click at a
            # time (confirmed live 2026-09-17 on the Junimo Community Center speech) — that is not a
            # hang. Only declare one past that, with real margin.
            if ($repeatCount -ge 6 -and -not $hangCmd) { $hangCmd = $line }
        }
    } while ((Get-Date) -lt $deadline -and -not $hangCmd -and -not ((Tail $n) -match 'Opened planning hub \(week 1'))
    if (-not $r.EventStart) { $r.EventStart = 'FAIL: never saw a cmd[N] line' }
    if ($hangCmd) {
        $r.Hang = $hangCmd
        Debug 'ee'
    } else {
        # By the time the event finishes, "Run N ready" and the stash/shrine placements have
        # already been logged during the step loop above — check retroactively, no more waiting.
        $r.SaveLoaded = if ((Tail $n) -match 'Run \d+ ready') { 'ok (seen during event step)' } else { 'FAIL: never appeared' }
        $r.StashPlaced = if ((Tail $n) -match 'JunimoStashService: placed|Junimo Stash anchored|PlanningShrine') { 'ok (seen during event step)' } else { 'FAIL: never appeared' }
        $r.Hub = WaitLog 'Opened planning hub \(week 1' $n 30
    }
    $r.MorrisGoneBeforeHall = if ($r.HallReached -and $r.MorrisSeen) { 'checked by OpeningScriptTests' } else { 'n/a' }
}
$r.Errors = ((Tail $n) | Where-Object { $_ -match '\bERROR\b' } | Select-Object -First 3) -join ' || '
$t = Count
Send 'tly_totitle'
WaitLog 'tly_totitle: exiting' $t 30 | Out-Null
Start-Sleep -Seconds 12
$r.GetEnumerator() | ForEach-Object { "{0,-20} {1}" -f $_.Key, $_.Value }
