#requires -Version 7
<#
.SYNOPSIS
    Unattended check that the opening cutscene lands on the farmhouse porch for a farm type:
    new game WITHOUT Skip intro, wait for the Lewis scene, print the farmer's tile, end the event
    (vanilla `debug ee`), confirm the driver still reaches the theme picker, exit to title.
#>
param([Parameter(Mandatory)][string]$FarmType)
$ErrorActionPreference = 'Stop'
$Tools = $PSScriptRoot
$Log = Join-Path $env:APPDATA 'StardewValley\ErrorLogs\SMAPI-latest.txt'
function Count { [int](pwsh -NoProfile -File "$Tools/bridge.ps1" -Action count) }
function Send([string]$lines) { pwsh -NoProfile -File "$Tools/bridge.ps1" -Action send -Lines $lines | Out-Null }
function WaitLog([string]$pattern, [int]$from, [int]$timeout = 120) { pwsh -NoProfile -File "$Tools/bridge.ps1" -Action wait -Pattern $pattern -FromLine $from -TimeoutSec $timeout }
function Debug([string]$cmd) { pwsh -NoProfile -File "$Tools/send-smapi-command.ps1" "debug $cmd" | Out-Null }
function Tail([int]$from) { (Get-Content $Log) | Select-Object -Skip $from }

$r = [ordered]@{ Farm = $FarmType }
$n = Count
Send "tly_newgame $FarmType"
$r.IntroStart = WaitLog 'Intro: starting the Lewis' $n 150
Start-Sleep -Seconds 6
$h = Count
Send 'tly_here'
WaitLog 'tly_here|Player is at|tile' $h 20 | Out-Null
$r.FarmerTile = (Tail $h | Select-String 'The Longest Year\].*(tile|Player)' | Select-Object -First 1).Line
$r.Location = (Tail $n | Select-String 'Warping|changeLocation|Farm' | Where-Object { $_ -match 'game\]' } | Select-Object -First 2 | % Line) -join ' || '
$e = Count
Debug 'ee'
$r.AfterEnd = WaitLog 'Opened planning hub \(week 1|forcing it to avoid a re-fire' $e 60
$r.Hub = WaitLog 'Opened planning hub \(week 1' $e 60
$r.Errors = ((Tail $n) | Where-Object { $_ -match '\bERROR\b' } | Select-Object -First 3) -join ' || '
$t = Count
Send 'tly_totitle'
WaitLog 'tly_totitle: exiting' $t 30 | Out-Null
Start-Sleep -Seconds 12
$r.GetEnumerator() | ForEach-Object { "{0,-11} {1}" -f $_.Key, $_.Value }
