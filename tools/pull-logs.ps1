#requires -Version 7
<#
.SYNOPSIS
    Archive the live SMAPI log so it survives the next relaunch (PC / TheLongestYear).

.DESCRIPTION
    SMAPI overwrites %APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt every time the game
    relaunches. A repro log is therefore destroyed the instant you rebuild + relaunch unless it
    is copied first. This is the PC equivalent of AndroidConsolizer's `sync.ps1 logs` archive
    step (SyncdewValley/sync.ps1 Invoke-Logs) — same naming + 30-file prune — but TheLongestYear
    runs on PC, so the log is read straight off disk instead of pulled over ADB.

    Run this BEFORE every relaunch (tools/deploy.ps1 calls it automatically). Safe to run any
    time; a no-op if no live log exists.

    Archive name: SMAPI-v<manifestVersion>-<yyyyMMdd-HHmmss>.txt  (matches AC's convention).
    Archives land in test-output/log-archive/ and are pruned to the 30 most recent.

.EXAMPLE
    pwsh -NoProfile -File tools/pull-logs.ps1

.NOTES
    Keep this aligned with the workspace "I deploy + pull logs; the user playtests" rule.
#>
param(
    # Where SMAPI writes the live log on PC.
    [string]$LogPath = (Join-Path $env:APPDATA 'StardewValley\ErrorLogs\SMAPI-latest.txt'),

    # How many archived logs to keep.
    [int]$Keep = 30
)

$ErrorActionPreference = 'Stop'

# Repo-root-relative paths (this script lives in <repo>/tools/).
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ArchiveDir = Join-Path $RepoRoot 'test-output\log-archive'
$Manifest   = Join-Path $RepoRoot 'src\TheLongestYear\manifest.json'

if (-not (Test-Path -LiteralPath $LogPath)) {
    Write-Host "No live SMAPI log at: $LogPath (nothing to archive)."
    return
}

if (-not (Test-Path -LiteralPath $ArchiveDir)) {
    New-Item -ItemType Directory -Path $ArchiveDir -Force | Out-Null
}

# Version for the filename (so archives sort + identify by mod build).
$version = 'unknown'
if (Test-Path -LiteralPath $Manifest) {
    try { $version = (Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).Version } catch { }
}

$timestamp   = Get-Date -Format 'yyyyMMdd-HHmmss'
$archiveName = "SMAPI-v${version}-${timestamp}.txt"
$archivePath = Join-Path $ArchiveDir $archiveName

Copy-Item -LiteralPath $LogPath -Destination $archivePath -Force
$size = (Get-Item -LiteralPath $archivePath).Length
Write-Host "Archived live log -> log-archive/$archiveName ($size bytes)"

# Prune to the most recent $Keep (newest mtime wins; name sort is ambiguous across versions).
$all = Get-ChildItem -LiteralPath $ArchiveDir -Filter 'SMAPI-*.txt' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending
if ($all.Count -gt $Keep) {
    $all | Select-Object -Skip $Keep | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    Write-Host "Pruned old log archives (kept $Keep)."
}

Write-Host ''
Write-Host '--- last 12 lines of archived log ---'
Get-Content -LiteralPath $archivePath -Tail 12 | ForEach-Object { Write-Host "  $_" }
