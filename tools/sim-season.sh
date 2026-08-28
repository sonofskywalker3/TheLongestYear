#!/usr/bin/env bash
# Play ONE season of the real-play simulation. Usage: season.sh <mode> <label>
#   mode = minimal | goals   (goals = deposit each week's goal slots as well)
# Precondition: the planning hub is open on day 1 of the season.
set -u
MODE="$1"; LABEL="$2"
REPO="C:/Users/Jeff/Documents/Projects/Stardee Valoo/TheLongestYear"
SP="$REPO/tools"
LOG="$APPDATA/StardewValley/ErrorLogs/SMAPI-latest.txt"
drv() { pwsh -NoProfile -File "$REPO/tools/bridge.ps1" "$@"; }
game() { pwsh -NoProfile -File "$REPO/tools/game.ps1" "$@" | tail -1; }
con() { pwsh -NoProfile -File "$REPO/tools/send-smapi-command.ps1" "$@" | tail -1; }
count() { drv -Action count; }

pick_and_goals() {  # hub is open: log goals for the current week, pick the left card
  local n; n=$(count)
  game -Focus >/dev/null
  drv -Action send -Lines "tly_goals" >/dev/null
  drv -Action wait -Pattern "tly_goals:" -TimeoutSec 60 -FromLine "$n"
  sleep 2
  game -Click 707,530 >/dev/null
  drv -Action wait -Pattern "added quest|Weekly goal pool for .* is empty" -TimeoutSec 60 -FromLine "$n"
}
deposit_goals() {
  if [ "$MODE" = "goals" ]; then
    local n; n=$(count)
    drv -Action send -Lines "tly_playseason goals" >/dev/null
    drv -Action wait -Pattern "tly_playseason: .* gate WOULD" -TimeoutSec 60 -FromLine "$n"
  fi
}
advance_to() {  # $1 = day before the target week start (7, 14, 21) or 28
  local n; n=$(count)
  drv -Action send -Lines "tly_setday $1" >/dev/null
  drv -Action wait -Pattern "tly_setday: date set" -TimeoutSec 60 -FromLine "$n"
  con "debug sleep" >/dev/null
  drv -Action wait -Pattern "Opened planning hub|FailReset|Loop reset complete|Win|Month cleared" -TimeoutSec 150 -FromLine "$n"
  sleep 4
}

echo "=== $LABEL: week 1"; pick_and_goals; deposit_goals
advance_to 7
echo "=== $LABEL: week 2"; pick_and_goals; deposit_goals
advance_to 14
echo "=== $LABEL: week 3 (gate donations)"
n=$(count); game -Focus >/dev/null
drv -Action send -Lines "tly_playseason" >/dev/null
drv -Action wait -Pattern "tly_playseason: .* gate WOULD" -TimeoutSec 60 -FromLine "$n"
pick_and_goals; deposit_goals
advance_to 21
echo "=== $LABEL: week 4"; pick_and_goals; deposit_goals
echo "=== $LABEL: day 28"
n=$(count)
drv -Action send -Lines "tly_setday 28" >/dev/null
drv -Action wait -Pattern "tly_setday: date set" -TimeoutSec 60 -FromLine "$n"
con "debug sleep" >/dev/null
drv -Action wait -Pattern "Month cleared|Advancing|FailReset|resetting|Loop reset complete|Win|Victory" -TimeoutSec 150 -FromLine "$n"
sleep 6
for i in 1 2 3 4 5 6 7 8; do   # the day-28 Junimo scene is click-to-continue
  r=$(drv -Action wait -Pattern "Opened planning hub|VictoryMenu|Win screen|keep playing|Victory" -TimeoutSec 8 -FromLine "$n")
  case "$r" in FOUND*) echo "$r"; break;; esac
  game -Click 960,540 >/dev/null
done
grep -n "Month cleared\|Season checkpoint\|FailReset\|gate WOULD\|Opened planning hub\|WIN\|Win \|Victory" "$LOG" | tail -6 | cut -c1-160
