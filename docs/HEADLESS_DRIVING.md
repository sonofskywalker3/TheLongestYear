# Headless driving: verify The Longest Year without touching Jeff's desktop

Since 0.16.69 an unattended verification run needs no mouse, no keyboard injection and no
foreground window. Everything goes through SMAPI: the file bridge for `tly_*` commands, the SMAPI
console input buffer for the game's own `debug` commands, and the SMAPI log for results.

## Why it used to need the desktop

- The game pauses its update loop whenever its window is not in front (`Game1.cs:4693`, unless
  the vanilla option "pause when window is inactive" is off). A queued command looked like it had
  failed when the game was simply asleep. **Fixed:** with `EnableDebugCommandBridge: true` in
  `config.json` the mod switches that option off at launch and on every save load (log line
  `Debug bridge: 'pause when window is inactive' switched off`).
- The planning hub ("Pick a theme") had to be clicked. **Fixed:** `tly_select <theme>` with the
  hub open is the card click (any theme, current week or the day-28 next-month pre-pick) and the
  hub closes itself.
- Launching the game brought its window to the front. **Mitigated:** `tools/deploy.ps1 -Minimized`
  starts SMAPI minimized. The window may still flash once on creation; nothing else takes focus.

Never use `tools/game.ps1` (mouse and keyboard) or `tools/screenshot.ps1` in this mode.

## Prerequisites

- `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\config.json`
  has `"EnableDebugCommandBridge": true` (developer-only; a shipped build never reads the bridge).
- Repo: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`. Run scripts with
  `pwsh -NoProfile -File tools/<script>`.
- Log: `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. Read it; never screenshot.

## The three tools

| Need | Tool | Notes |
|---|---|---|
| Build, close, relaunch | `tools/deploy.ps1 -Minimized` | Archives the old log first. `-NoLaunch` to build only. Do NOT `git add` the pruned archives; run `git checkout -- test-output/log-archive` afterwards. |
| Any `tly_*` command | `tools/bridge.ps1 -Action send -Lines "cmd1|cmd2"` | Whole batch runs in one tick, in order. Works at the title screen (`tly_loadsave`) and in-world. |
| Wait for a log line | `tools/bridge.ps1 -Action wait -Pattern "<regex>" -TimeoutSec 60 -FromLine <n>` | Take `<n>` from `-Action count` BEFORE sending, so you only match new lines. Returns `FOUND: ...` or `TIMEOUT`. |
| The game's own `debug` commands | `tools/send-smapi-command.ps1 "debug sleep"` | Writes into SMAPI's console input buffer, focus-independent. `debug season summer`, `debug sleep`, `debug time 1000`, `debug warp ...`. |

## Standard sequence

```
n = bridge.ps1 -Action count
deploy.ps1 -Minimized                      # wait for "SMAPI" banner / mod list in the log
bridge.ps1 -Action wait -Pattern "Debug bridge: 'pause when window is inactive'" -FromLine n -TimeoutSec 180
n = count; bridge.ps1 -Action send -Lines "tly_loadsave <SaveFolder>"
bridge.ps1 -Action wait -Pattern "Run \d+ ready" -FromLine n -TimeoutSec 120   # then wait ~45 s more before mutating commands
n = count; bridge.ps1 -Action send -Lines "tly_reset"      # tly_reset 5 pins seed loop 5 (same board twice)
bridge.ps1 -Action wait -Pattern "Opened planning hub" -FromLine n -TimeoutSec 150
n = count; bridge.ps1 -Action send -Lines "tly_select Farming"      # the card click
bridge.ps1 -Action wait -Pattern "Selected Farming" -FromLine n
```

Advance a season without tripping the day-28 gate: `tly_setday 7` (bridge), then
`send-smapi-command.ps1 "debug season summer"` and `send-smapi-command.ps1 "debug sleep"`; the
hub re-opens on day 8 (`Opened planning hub (week N, offer: A,B)`).

## Read-only diagnostics (no world change)

`tly_themepool [theme]`, `tly_goals [season] [week]`, `tly_gatecheck`,
`tly_genbundles [loop] [custom|standard|remixed]` (custom = the TLY engine board; standard and
remixed audit the board vanilla would build for that Advanced Options choice),
`tly_itemmodel <id|bundle>`, `tly_dumpeffort` (writes `item-effort-model.md` in the mod folder;
copy to `docs/`, it is gitignored), `tly_dumpbundles`, `tly_meta`, `tly_runstate`.

## Rules

- Throwaway save only: the Rodger lineage (`None_447610463` at the time of writing; a reset
  rotates the folder, read the new name from the log). Never `PuffPuff_*`, never `Cheatside_*`,
  never the original `None_443632257`.
- Ask Jeff before the first launch of a session (memory `ask-before-driving-desktop`); one yes
  covers the session. Do not use any mouse or keyboard tool even after a yes.
- Report from the log, quote the lines, say what is committed, what is deployed, what is pushed.

## Year sims (`tools/sim-year.sh`)

- Usage: `bash tools/sim-year.sh <mode> <label> [seedLoop]`. `minimal` plays a loop year meeting
  only the gates; `goals` also deposits every selected week's goal slots (`tly_playseason
  goalsonly`) after the pick.
- **Donations land a quarter a week, not all at once.** Week k of every season calls
  `tly_playseason quarter k` *before* the pick, so the hub sees the board a real player would
  have by then. The share is global and cumulative (quarter 3 means three quarters of the
  season's plan donated in total), and quarter 4 also pays the vault and prints the
  `gate WOULD PASS/FAIL` ledger line. Only quarter 4 prints that line, so do not wait on it for
  the earlier weeks.
- **`[seedLoop]` pins the board**: it is passed to `tly_reset <seedLoop>` so two sims (say a
  `minimal` and a `goals` run) compare like for like. Omit it for a random board.
- Related: `tly_genbundles <seedLoop> [custom|standard|remixed]` rolls a board through the same
  audit without playing; `custom` is the mod's own board, `standard` and `remixed` the vanilla
  Community Center sets.
- Every run ends with `tly_dumpavailability` and `tly_gatecheck`, copies the board listing to
  `docs/board-availability.md` (gitignored), and prints, in order: `=== <label>: askable by week`
  as 16 rows `Season week N: Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki` (Foraging, Farming, Fishing, Mining, Mixed,
  Spelunking, Artisan, Kitchen, the order `tly_themepool` prints them), the gate audit, the
  Judgement rows and the Unknown items. The Judgement and Unknown lists go to Jeff after every
  run (memory `tly-sim-list-unknowns-each-run`).
- **Never stop a running sim with the harness's task stop.** It kills the wrapper shell only; the
  inner script keeps sending bridge commands and poisons the next run (2026-08-28, sims I, J and
  K). Kill the script's own process (`Get-CimInstance Win32_Process` filtered on the script
  name, then `Stop-Process`), then redeploy so the bridge queue is cleared, then start again.
- Two sims never overlap: one game, one bridge queue, one log.
- The hub can show a single card (`offer: Foraging`) when only one theme can ask for two goals;
  the script handles it since 2026-08-28.

