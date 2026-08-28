#!/usr/bin/env bash
# Headless real-play simulation of one loop year (see docs/HEADLESS_DRIVING.md). No mouse, no
# keyboard, no foreground: every step goes through the file bridge, SMAPI's console input and
# the log. Usage: sim-year.sh <mode> [label]   mode = minimal | goals
#   minimal = donate only what each season gate demands (tly_playseason in week 3)
#   goals   = also deposit every selected week's goal slots (tly_playseason goals)
# Starts with tly_reset (fresh Spring 1 board), plays Spring..Winter, stops after the Winter
# week-4 pick (the Winter day-28 win path is not exercised). Prints the offer, askable counts,
# goal counts and the pick for every week; the full detail is in SMAPI-latest.txt.
set -u
MODE="${1:-goals}"; LABEL="${2:-sim}"
REPO="C:/Users/Jeff/Documents/Projects/Stardee Valoo/TheLongestYear"
LOG="$APPDATA/StardewValley/ErrorLogs/SMAPI-latest.txt"
drv() { pwsh -NoProfile -File "$REPO/tools/bridge.ps1" "$@"; }
con() { pwsh -NoProfile -File "$REPO/tools/send-smapi-command.ps1" "$@" >/dev/null; }
count() { drv -Action count; }
say() { echo "$@"; }
show() { tail -n +"$1" "$LOG" | grep -E "$2" | sed -E 's/^\[[^]]*\] //' | cut -c1-230; }

pick_and_goals() {  # hub is open: dump goals + pool, pick the left card
  local n; n=$(count)
  local hub; hub=$(tail -n +"$n" "$LOG" | grep -E "Opened planning hub" | tail -1)
  [ -z "$hub" ] && hub=$(grep -E "Opened planning hub" "$LOG" | tail -1)
  local left; left=$(echo "$hub" | sed -E 's/.*offer: ([A-Za-z]+),.*/\1/')
  drv -Action send -Lines "tly_goals|tly_themepool" >/dev/null
  drv -Action wait -Pattern "executing 'tly_themepool'" -TimeoutSec 60 -FromLine "$n" >/dev/null
  sleep 3
  local m; m=$(count)
  drv -Action send -Lines "tly_select $left" >/dev/null
  drv -Action wait -Pattern "Selected $left|Weekly goal pool for .* is empty" -TimeoutSec 60 -FromLine "$m" >/dev/null
  sleep 2
  show "$n" "Opened planning hub|tly_goals:|goal\(s\)|askable|Selected |added quest|pool for .* is empty|WARN|ERROR"
}
deposit_goals() {
  if [ "$MODE" = "goals" ]; then
    local n; n=$(count)
    drv -Action send -Lines "tly_playseason goals" >/dev/null
    drv -Action wait -Pattern "tly_playseason: .* gate WOULD|tly_playseason:" -TimeoutSec 90 -FromLine "$n" >/dev/null
    sleep 2
    show "$n" "tly_playseason|WARN|ERROR"
  fi
}
gate_donations() {
  local n; n=$(count)
  drv -Action send -Lines "tly_playseason" >/dev/null
  drv -Action wait -Pattern "tly_playseason: .* gate WOULD" -TimeoutSec 90 -FromLine "$n" >/dev/null
  sleep 2
  show "$n" "tly_playseason|WARN|ERROR"
}
advance_to() {  # $1 = 7, 14, 21: sleep into the next week's hub
  local n; n=$(count)
  drv -Action send -Lines "tly_setday $1" >/dev/null
  drv -Action wait -Pattern "tly_setday: date set" -TimeoutSec 60 -FromLine "$n" >/dev/null
  sleep 2; con "debug sleep"
  drv -Action wait -Pattern "Opened planning hub|Loop reset complete" -TimeoutSec 150 -FromLine "$n" >/dev/null
  sleep 4
}
cross_day28() {
  local n; n=$(count)
  drv -Action send -Lines "tly_setday 28" >/dev/null
  drv -Action wait -Pattern "tly_setday: date set" -TimeoutSec 60 -FromLine "$n" >/dev/null
  sleep 2; con "debug sleep"
  local r; r=$(drv -Action wait -Pattern "Day-28 cutscene: opening|Loop reset complete" -TimeoutSec 150 -FromLine "$n")
  say "$r" | cut -c1-200
  sleep 4
  local m; m=$(count)
  drv -Action send -Lines "tly_skipscene" >/dev/null
  drv -Action wait -Pattern "Opened planning hub|Loop reset complete|Junimo Shrine" -TimeoutSec 150 -FromLine "$m" >/dev/null
  sleep 4
  show "$n" "Month cleared|Season checkpoint|gate|FailReset|Loop reset complete|tly_skipscene|Opened planning hub|JP|WARN|ERROR"
}

say "=== $LABEL ($MODE): reset to Spring 1"
n=$(count); drv -Action send -Lines "tly_reset" >/dev/null
drv -Action wait -Pattern "Opened planning hub \(week 1," -TimeoutSec 180 -FromLine "$n" >/dev/null; sleep 5
for season in Spring Summer Fall Winter; do
  say "=== $LABEL: $season week 1"; pick_and_goals; deposit_goals
  advance_to 7
  say "=== $LABEL: $season week 2"; pick_and_goals; deposit_goals
  advance_to 14
  say "=== $LABEL: $season week 3 (gate donations first)"; gate_donations; pick_and_goals; deposit_goals
  advance_to 21
  say "=== $LABEL: $season week 4"; pick_and_goals; deposit_goals
  [ "$season" = "Winter" ] && break
  say "=== $LABEL: $season day 28"; cross_day28
done
say "=== $LABEL: done"
