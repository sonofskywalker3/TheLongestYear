#requires -Version 7
# File-bridge helper for TLY debug commands: queue tly_* lines and wait for log patterns. Usage: bridge.ps1 -Action send -Lines "a|b" ; -Action wait -Pattern re -TimeoutSec n -FromLine n ; -Action count
param(
    [string]$Action = "",
    [string]$Lines = "",
    [string]$Pattern = "",
    [int]$TimeoutSec = 120,
    [int]$FromLine = 0
)
$ModDir = 'C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear'
$Bridge = Join-Path $ModDir 'tly_commands.txt'
$Log = Join-Path $env:APPDATA 'StardewValley\ErrorLogs\SMAPI-latest.txt'
$Repo = 'C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear'

function Send-Bridge([string[]]$cmds) {
    # Append-safe: if a previous batch is still unconsumed, merge rather than clobber.
    $existing = @()
    if (Test-Path $Bridge) { $existing = Get-Content $Bridge }
    Set-Content -Path $Bridge -Value (@($existing) + $cmds) -Encoding UTF8
    "queued: " + ($cmds -join ' | ')
}

function Get-LogLineCount { if (Test-Path $Log) { (Get-Content $Log).Count } else { 0 } }

function Wait-Log([string]$pattern, [int]$timeoutSec, [int]$fromLine) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $Log) {
            $lines = Get-Content $Log
            if ($lines.Count -gt $fromLine) {
                $tail = $lines[$fromLine..($lines.Count - 1)]
                $hit = $tail | Select-String -Pattern $pattern | Select-Object -First 1
                if ($hit) { return "FOUND: " + $hit.Line }
            }
        }
        Start-Sleep -Milliseconds 1500
    }
    return "TIMEOUT waiting for '$pattern'"
}

switch ($Action) {
    "send"  { Send-Bridge ($Lines -split "\|" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }) }
    "wait"  { Wait-Log $Pattern $TimeoutSec $FromLine }
    "count" { Get-LogLineCount }
    "bridge-exists" { Test-Path $Bridge }
    default { "actions: send -Lines ..., wait -Pattern ... [-TimeoutSec] [-FromLine], count, bridge-exists" }
}
