#!/usr/bin/env bash
# Headless real-play simulation of one loop year (see docs/HEADLESS_DRIVING.md). No mouse, no
# keyboard, no foreground: every step goes through the file bridge, SMAPI's console input and
# the log. Usage: sim-year.sh <mode> <label> [seedLoop]   mode = minimal | goals
#   minimal = donate only the season's gate demand, a quarter of it per week
#   goals   = the same quarter-a-week donations plus every selected week's goal slots
#             (tly_playseason goalsonly), deposited after the pick
# Every week k of every season calls "tly_playseason quarter k" BEFORE the pick, so the board the
# hub sees has the donations a real player would have made by then; quarter 4 also pays the vault
# and prints the season gate line. seedLoop (optional) is passed to tly_reset so two runs share a
# board. Starts with tly_reset, plays Spring..Winter, stops after the Winter week-4 pick (the
# Winter day-28 win path is not exercised). Prints the offer, askable counts, goal counts and the
# pick for every week, then the 16-week askable table, the gate audit, the judgement rows and the
# unknown items; the full detail is in SMAPI-latest.txt.
set -u
MODE="${1:-goals}"; LABEL="${2:-sim}"; SEED="${3:-}"
REPO="C:/Users/Jeff/Documents/Projects/Stardee Valoo/TheLongestYear"
LOG="$APPDATA/StardewValley/ErrorLogs/SMAPI-latest.txt"
DUMP="$REPO/docs/board-availability.md"
drv() { pwsh -NoProfile -File "$REPO/tools/bridge.ps1" "$@"; }
con() { pwsh -NoProfile -File "$REPO/tools/send-smapi-command.ps1" "$@" >/dev/null; }
count() { drv -Action count; }
say() { echo "$@"; }
show() { tail -n +"$1" "$LOG" | grep -E "$2" | sed -E 's/^\[[^]]*\] //' | cut -c1-230; }

pick_and_goals() {  # hub is open: dump goals + pool, pick the left card
  local n; n=$(count)
  local hub; hub=$(tail -n +"$n" "$LOG" | grep -E "Opened planning hub" | tail -1)
  [ -z "$hub" ] && hub=$(grep -E "Opened planning hub" "$LOG" | tail -1)
  local left; left=$(echo "$hub" | sed -E 's/.*offer: ([A-Za-z]+)[,)].*/\1/')
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
    drv -Action send -Lines "tly_playseason goalsonly" >/dev/null
    drv -Action wait -Pattern "tly_playseason: .* gate WOULD|tly_playseason:" -TimeoutSec 90 -FromLine "$n" >/dev/null
    sleep 2
    show "$n" "tly_playseason|WARN|ERROR"
  fi
}
quarter_donations() {  # $1 = 1..4: this week's share of the season's gate demand
  local n; n=$(count)
  drv -Action send -Lines "tly_playseason quarter $1" >/dev/null
  drv -Action wait -Pattern "tly_playseason: .* quarter $1|tly_playseason: .* gate WOULD" -TimeoutSec 120 -FromLine "$n" >/dev/null
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
askable_table() {  # 16 rows: Foraging/Farming/Fishing/Mining/Mixed/Spelunking/Artisan/Kitchen
  tail -n +"$START" "$LOG" | sed -E 's/^\[[^]]*\] //' | awk '
    /^tly_themepool: / { season=$2; wk=$4; gsub(/,/, "", wk); n=0; vals=""; next }
    /askable [0-9]+/ && season != "" {
      v = ""
      for (i = 1; i <= NF; i++) if ($i == "askable") { v = $(i + 1); break }
      if (v == "") next
      vals = (n == 0 ? v : vals "/" v); n++
      if (n == 8) { print season " week " wk ": " vals; season = "" }
    }'
}

say "=== $LABEL ($MODE): reset to Spring 1${SEED:+ (seed loop $SEED)}"
RESET="tly_reset"; [ -n "$SEED" ] && RESET="tly_reset $SEED"
n=$(count); drv -Action send -Lines "$RESET" >/dev/null
drv -Action wait -Pattern "Opened planning hub \(week 1," -TimeoutSec 180 -FromLine "$n" >/dev/null; sleep 5
START=$(count)
for season in Spring Summer Fall Winter; do
  for k in 1 2 3 4; do
    say "=== $LABEL: $season week $k (quarter $k donations first)"
    quarter_donations "$k"
    pick_and_goals
    deposit_goals
    [ "$k" = 4 ] && break
    advance_to $((k * 7))
  done
  [ "$season" = "Winter" ] && break
  say "=== $LABEL: $season day 28"; cross_day28
done
n=$(count)
drv -Action send -Lines "tly_dumpavailability|tly_gatecheck" >/dev/null
drv -Action wait -Pattern "tly_gatecheck RESULT" -TimeoutSec 90 -FromLine "$n" >/dev/null
cp "/c/Program Files (x86)/Steam/steamapps/common/Stardew Valley/Mods/TheLongestYear/board-availability.md" "$DUMP"
say "=== $LABEL: askable by week (Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki)"
askable_table
# The per-bundle audit lines still carry SMAPI's "[time INFO ...]" prefix when grep sees them
# (show greps before it strips the prefix), so match the bracket AFTER that prefix, not at ^.
say "=== $LABEL: gate audit"; show "$n" "tly_gatecheck|\]\s+\[(ok|tight|IMPOSSIBLE)|RESULT|Vault gate|NOTE:|tly_dumpavailability"
say "=== $LABEL: judgement rows (Jeff's own rulings)"
sed -n '/^## Judgement rows/,/^## Unknown items/p' "$DUMP" | sed '$d'
say "=== $LABEL: unknown items (Jeff must confirm each one)"
sed -n '/^## Unknown items/,/^## Rejected/p' "$DUMP" | sed '$d'
say "=== $LABEL: done"
