#requires -Version 7
<#
.SYNOPSIS
    Unattended farm-type verification: new TLY game on a farm type, build + keep coop/barn/silo,
    reset, and confirm every kept building comes back on its own tile.
.DESCRIPTION
    Runs from the title screen (tly_totitle first if needed). Prints a PASS/FAIL line per farm.
    Building spots are searched from a candidate list per farm type; the first tile the vanilla
    `debug build` accepts wins (it uses the normal placement rules, so a spot that works is a
    spot a player could have used). Usage: farmtype-cycle.ps1 -FarmType riverland [-SkipIntro]
#>
param(
    [Parameter(Mandatory)][string]$FarmType,
    [switch]$SkipIntro
)
$ErrorActionPreference = 'Stop'
$Tools = $PSScriptRoot
$Log = Join-Path $env:APPDATA 'StardewValley\ErrorLogs\SMAPI-latest.txt'

function Count { [int](pwsh -NoProfile -File "$Tools/bridge.ps1" -Action count) }
function Send([string]$lines) { pwsh -NoProfile -File "$Tools/bridge.ps1" -Action send -Lines $lines | Out-Null }
function WaitLog([string]$pattern, [int]$from, [int]$timeout = 120) {
    pwsh -NoProfile -File "$Tools/bridge.ps1" -Action wait -Pattern $pattern -FromLine $from -TimeoutSec $timeout
}
function Debug([string]$cmd) { pwsh -NoProfile -File "$Tools/send-smapi-command.ps1" "debug $cmd" | Out-Null }
function Tail([int]$from) { (Get-Content $Log) | Select-Object -Skip $from }
function Tly([int]$from) { Tail $from | Where-Object { $_ -match 'The Longest Year\]' } | ForEach-Object { $_ -replace '^\[(\S+) (\S+)\s+The Longest Year\] ', '$1 $2 ' } }

$result = [ordered]@{ Farm = $FarmType }

# 1. New game
$n = Count
$cmd = "tly_newgame $FarmType" + ($(if ($SkipIntro) { ' skipintro' } else { '' }))
Send $cmd
$hub = WaitLog 'Opened planning hub \(week 1' $n 150
if ($hub -notmatch '^FOUND') {
    $intro = WaitLog 'Intro: starting the Lewis' $n 5
    if ($intro -match '^FOUND') {
        # Cutscene playing: not skippable, wait for the picker after it.
        $hub = WaitLog 'Opened planning hub \(week 1' $n 240
    }
}
$result.NewGame = $hub
$result.FarmTypeLine = (Tail $n | Select-String 'Farm type:' | Select-Object -First 1).Line
$result.Stash = (Tail $n | Select-String 'JunimoStashService: placed stash' | Select-Object -First 1).Line
$result.Shrine = (Tail $n | Select-String 'PlanningShrineService: placed' | Select-Object -First 1).Line
$offer = (Tail $n | Select-String 'Opened planning hub \(week 1, offer: ([^,\)]+)' | Select-Object -First 1)
$theme = if ($offer) { $offer.Matches[0].Groups[1].Value } else { 'Mixed' }
Start-Sleep -Seconds 5
$m = Count; Send "tly_select $theme"; WaitLog "Selected $theme" $m 30 | Out-Null

# 2. Buildings. Warp to the farm, then try candidates until each building lands.
Debug 'warp Farm 64 20'
Start-Sleep -Seconds 3
# The fresh farm is covered in weeds/stones/trees, which block every legal placement; clear
# them (a player would) so the build obeys only the map's own buildable rules.
Debug 'clearfarm'
Start-Sleep -Seconds 2
$candidates = @(
    @(52,20), @(44,20), @(36,20), @(52,28), @(44,28), @(36,28), @(60,28), @(68,28), @(28,20), @(28,28),
    @(52,36), @(44,36), @(36,36), @(60,36), @(68,36), @(20,20), @(20,28), @(60,44), @(52,44), @(44,44),
    @(76,28), @(76,36), @(30,12), @(40,12), @(72,44), @(64,52), @(48,52), @(32,44), @(24,36), @(16,44)
)
$placed = @{}
foreach ($bt in 'Coop','Barn','Silo') {
    foreach ($c in $candidates) {
        $key = "$($c[0]),$($c[1])"
        if ($placed.ContainsValue($key)) { continue }
        $b = Count
        Debug "build $bt $($c[0]) $($c[1])"
        Start-Sleep -Milliseconds 900
        $warn = Tail $b | Select-String "Couldn't place a '$bt'"
        if (-not $warn) { $placed[$bt] = $key; break }
    }
}
$result.Placed = ($placed.GetEnumerator() | ForEach-Object { "$($_.Key)@($($_.Value))" }) -join ' '

# 3. Keeps + snapshot
$k = Count
Send 'tly_addjp 20000|tly_buyupgrade keep_coop|tly_buyupgrade keep_barn|tly_buyupgrade keep_silo|tly_buildings'
WaitLog 'Buildings farm=' $k 30 | Out-Null
$before = (Tail $k | Select-String 'Buildings farm=' | Select-Object -Last 1).Line -replace '^.*Buildings ', ''
$result.Purchases = ((Tly $k) | Where-Object { $_ -match 'keep_|purchase|Purchase' }) -join ' || '
$result.Before = $before

# 4. Reset and compare
$r = Count
Send 'tly_reset'
$done = WaitLog 'Loop reset complete' $r 240
$result.Reset = $done
Start-Sleep -Seconds 5
$a = Count
Send 'tly_buildings'
WaitLog 'Buildings farm=' $a 30 | Out-Null
$after = (Tail $a | Select-String 'Buildings farm=' | Select-Object -Last 1).Line -replace '^.*Buildings ', ''
$result.After = $after
$result.KeptLines = ((Tly $r) | Where-Object { $_ -match "kept building|Keep Greenhouse|stash chest|planning shrine" }) -join ' || '

function BuildingSet([string]$line) {
    ($line -replace '^farm=\S+ n=\d+: ', '') -split ' ' | Where-Object { $_ -match '^(Coop|Barn|Silo)@' } | Sort-Object
}
$as = BuildingSet $after
# Compare the three buildings THIS run placed (a farm type's own starter building, e.g. the
# Meadowlands coop, is walked onto the kept spot rather than duplicated, so it is not counted).
$want = @($placed.GetEnumerator() | ForEach-Object { "$($_.Key)@($($_.Value))" } | Sort-Object)
$missing = @($want | Where-Object { $_ -notin $as })
$result.Verdict = if ($missing.Count -eq 0 -and $want.Count -eq 3) { 'PASS' } else { "FAIL missing: $($missing -join ' ') (placed $($want.Count))" }
$result.Errors = ((Tail $n) | Where-Object { $_ -match '\bERROR\b|Exception' -and $_ -notmatch 'without exceptions' } | Select-Object -First 5) -join ' || '

# 5. Back to title for the next farm
$t = Count
Send 'tly_totitle'
WaitLog 'tly_totitle: exiting' $t 30 | Out-Null
Start-Sleep -Seconds 12

$result.GetEnumerator() | ForEach-Object { "{0,-12} {1}" -f $_.Key, $_.Value }
