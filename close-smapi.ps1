# Close SMAPI and pull the PC log into TheLongestYear's test-output/ for analysis (git-ignored).
$logSource = "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $projectRoot "test-output"
New-Item -ItemType Directory -Force $logDir | Out-Null
$logDest = Join-Path $logDir "SMAPI-latest.txt"

$procs = Get-Process -Name "StardewModdingAPI", "Stardew Valley" -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Stopping SMAPI..."
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 2
} else {
    Write-Host "SMAPI not running."
}

if (Test-Path $logSource) {
    Copy-Item $logSource $logDest -Force
    Write-Host "Log copied to $logDest"
} else {
    Write-Error "Log not found at $logSource"
    exit 1
}
