#requires -Version 7
<#
.SYNOPSIS
    One-command deploy cycle for TheLongestYear (PC): archive log -> close game -> build -> relaunch.

.DESCRIPTION
    The game LOCKS TheLongestYear.dll while running, so a rebuild requires closing it first; and
    relaunching SMAPI overwrites SMAPI-latest.txt, destroying any repro log. This wraps the whole
    sequence so the log is ALWAYS archived before it can be lost (the step that was missing when a
    #1b repro log got overwritten on 2026-06-08).

    Order:
      1. Archive the current live SMAPI log  (tools/pull-logs.ps1)
      2. Stop StardewModdingAPI if running
      3. dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release  (auto-deploys to Mods)
      4. Relaunch StardewModdingAPI.exe  (NOT steam://rungameid — that skips SMAPI)

    Mirrors AndroidConsolizer's deploy practice (SyncdewValley/sync.ps1) for the PC target.

.PARAMETER NoLaunch
    Build + archive but leave the game closed (use when there's nothing to test in-game yet).

.PARAMETER NoBuild
    Skip the build (just archive + close, or archive + relaunch the existing DLL).

.EXAMPLE
    pwsh -NoProfile -File tools/deploy.ps1
.EXAMPLE
    pwsh -NoProfile -File tools/deploy.ps1 -NoLaunch
#>
param(
    [switch]$NoLaunch,
    [switch]$NoBuild,
    [string]$SmapiExe = 'C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Csproj   = Join-Path $RepoRoot 'src\TheLongestYear\TheLongestYear.csproj'

# 1. Archive the live log BEFORE anything can overwrite it.
Write-Host '=== [1/4] Archiving live SMAPI log ===' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'pull-logs.ps1')

# 2. Close the game so the DLL unlocks (and so the relaunch starts clean).
Write-Host ''
Write-Host '=== [2/4] Closing game ===' -ForegroundColor Cyan
$proc = Get-Process -Name 'StardewModdingAPI', 'Stardew Valley' -ErrorAction SilentlyContinue
if ($proc) {
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host "Stopped: $($proc.Name -join ', ')"
} else {
    Write-Host 'Game not running.'
}

# 3. Build (auto-deploys to the Mods folder via the csproj).
if (-not $NoBuild) {
    Write-Host ''
    Write-Host '=== [3/4] Building (Release) ===' -ForegroundColor Cyan
    dotnet build $Csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE) — not relaunching." }
} else {
    Write-Host ''
    Write-Host '=== [3/4] Build skipped (-NoBuild) ===' -ForegroundColor DarkGray
}

# 4. Relaunch SMAPI (unless suppressed).
Write-Host ''
if ($NoLaunch) {
    Write-Host '=== [4/4] Launch skipped (-NoLaunch) ===' -ForegroundColor DarkGray
    return
}
Write-Host '=== [4/4] Relaunching SMAPI ===' -ForegroundColor Cyan
if (-not (Test-Path -LiteralPath $SmapiExe)) { throw "SMAPI exe not found: $SmapiExe" }
Start-Process -FilePath $SmapiExe
Write-Host 'Launched. New SMAPI-latest.txt will start fresh; the prior log is archived.'
