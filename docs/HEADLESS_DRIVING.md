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

## Farm-type runs

`tly_newgame <standard|riverland|forest|hilltop|wilderness|fourcorners|beach|meadowlands> [skipintro] [name]`
starts a farm of that type from the title screen with no character screen (same SaveCreating /
SaveLoaded path as a real new game, TLY Custom bundles). `tly_totitle` exits to the title without
saving so the next run can start. `tly_buildings` lists every farm building with its tile.
`tools/farmtype-cycle.ps1 -FarmType <type> [-SkipIntro]` is the whole keep-and-rewind check (new
game, `debug clearfarm`, coop + barn + silo via `debug build` on the first legal tiles, the three
keeps, `tly_reset`, PASS when all three come back on their tiles); `tools/farmtype-intro.ps1
-FarmType <type> [-SkipIntro]` plays the whole opening on a type. With `-SkipIntro` it takes the
character-creation checkbox's bed shortcut straight to the planning hub with no arrival event; the
result table's `NoEvent` should read `ok`. Without it, the deathbed and cubicle are a vanilla
minigame (`GrandpaStory`) that logs nothing of its own; the first mod lines appear when the bus
ride loads the save (`Run N ready`, then the Junimo Stash and planning shrine placements), then the
arrival event, which is vanilla's `60367` replaced with the mod's own script — step it with
`tly_eventstep` (it clicks an open dialogue box on) until `Opened planning hub (week 1` appears.
Both exit to title when done. Delete the `<type>_<id>` save folders afterwards.

**Known gap: the no-`-SkipIntro` path is not headless-capable today.** `GrandpaStory`'s scene 6
sets `mouseActive = true` and waits for a real `receiveLeftClick` on Grandpa's letter
(`GrandpaStory.cs`); nothing before that point is time-gated past it, and no `tly_*` or vanilla
`debug` command exists to click it. A headless `tly_newgame <type>` (no `skipintro`) hangs there
indefinitely — every `tly_eventstep` after it logs `Load a save first.` because `loadForNewGame`
is never reached. Confirmed live 2026-09-17: 8+ minutes with no progress past
`EnsureManifestInitialized() finished`. Recovering needs `deploy.ps1 -Minimized` (its
`Stop-Process` closes the stuck game) or the desktop; there is no bridge-only way out. Do not spend
time re-testing this path headless until a bridge command exists to fire that click.

`tly_replayintro` clears the intro flags and warps to the bus stop, but the arrival event's vanilla
key is `60367/u 0` — precondition `u` is `DayOfMonth`, so it only fires while
`Game1.dayOfMonth == 0`, a state that exists only during the pre-Day-1 setup before `NewDay` runs.
On an already-loaded save `Game1.dayOfMonth` is 1 or higher, so the event does not re-fire from
`tly_replayintro` alone or after a follow-up `tly_reset`; confirmed live 2026-09-17,
`tly_eventstep` logged `no event.` both times. Treat `tly_replayintro` as a flag-clearing helper for
a save that has never advanced past the pre-game setup, not a way to replay the cutscene on a
running save.

## The Year One Ending

Arm, sleep, step outside, watch. Use the throwaway save (memory: the Rodger save is disposable).
Before the first launch: Jeff's yes (see Rules).

    n = count; send "tly_win"                          # arms the ending; log: "Win night (tly_win): ending armed"
    send "debug sleep"  (or walk to bed)                # log next morning: "Ending: starting (speaker=..."
    send "debug warp Farm 64 16"                        # step outside if the wake put you indoors
    wait -Pattern "Ending: event finished" -TimeoutSec 240

The shrine opens, then the choice. Answer a question box only with `tly_answer`: `tly_answer 1`
takes Keep playing, `tly_answer 0` takes Loop again, and `tly_answer 0` answers the Year 2 wall.
Wait about 2 seconds after the log shows the choice opened before sending it. Do NOT use
`tly_dismiss` on a question box: it calls `exitThisMenu`, which never runs `answerDialogue`, so it
closes the box without taking any response.

**An event's `speak` waits for a click, so the ending never advances on its own.** Poll
`tly_eventstep` about every 4 s for the whole event: it logs the current command index and text,
every actor's tile and whether it is moving, the open menu, and it clicks an open dialogue box on.
It is also how you tell a hang from a wait: `tly_eventstep: no event` plus
`tly_ending: the game is busy (eventUp,locationRequest)` is a stuck location change, not a pause.
A vanilla farm event (Evelyn's Garden Pot on this save) counts as busy and defers the ending until
it is stepped out; step it the same way.

Replay the event alone: `tly_ending` (current location, no continuation); force a voice:
`tly_ending speaker Shane`. Wall: `tly_year2wall` on any keep-playing save.

Verified live 2026-09-06: Morris's `move` path along the Town row `HallY + 2` walks clear, and the
speaker's one-tile move at the crack scene lands (Shane went 52,22 -> 52,23). No warp workaround is
needed. Never wrap a `changeLocation` in `globalFade` / `globalFadeToClear`: `changeLocation` warps
through `Game1.warpFarmer`, which fades on its own, and the extra global fade leaves
`Game1.locationRequest` pending forever.

Checks before a release: whole flow once on Standard; scenes 1 and 6 once on Meadowlands
(`tly_newgame meadowlands skipintro`, then `tly_win`); the log line "Morris_Dark: recoloured N"
with N > 0; every crowd member present on the steps; both branches of the choice; the wall on
Spring 1 year 2 of a keep-playing save; a loop-again reset leaves the shrine dark.

## The rewind (Fail night)

`tly_failreset` queues it (close the planning hub first with `tly_select`). `tly_skipscene` finishes
one beat per call (bedroom, Town pan, morning); `tly_skipscene all` presses the player's skip button,
which is offered from a save's second rewind (or any save with a finished loop) and runs to the
keep-or-release question. Answer with `tly_answer`, close the shrine with `tly_dismiss`. The pan's
walkers log as `RewindReversedExtras: <name> (<n> tiles, (x,y) back to (x,y))`; the second point is
always a door or the map edge. Right after a reset nobody has a schedule yet, so a rewind queued
before sleeping plays with no walkers.

## Season turns

`tly_seasonturn <summer|fall|winter>` replays the porch scene alone (no continuation) from
wherever the farmer stands; it starts under black and moves to the doorstep. Step it with
`tly_eventstep` (it clicks the speech box on); `tly_eventstep` reports "no event" for a tick or
two during the location change, so wait for two in a row before calling the scene over. The real
path: `tly_playseason quarter 4` (the gate would pass), `tly_setday 28`, `debug sleep`; the log
shows `Season turn: starting Summer`, then `scene finished`, then `Opened planning hub`.

## Read-only diagnostics (no world change)

`tly_themepool [theme]`, `tly_goals [season] [week]`, `tly_gatecheck`, `tly_gateneeds` (per-bundle remaining demand for the current season's gate, the same numbers as the Season Goals page; run it after any donation to see what the gate still wants),
`tly_seasongoals` (opens the Season Goals page; close it with its X button, Escape does not close it, and a page left open across a `tly_reset` keeps showing the OLD run's numbers until reopened),
`tly_genbundles [loop] [custom|standard|remixed]` (custom = the TLY engine board; standard and
remixed audit the board vanilla would build for that Advanced Options choice),
`tly_boost list` (the boost roster with each row's state and price), `tly_activeeffects` (running boosts, stacks per modifier), `tly_itemmodel <id|bundle>`, `tly_dumpeffort` (writes `item-effort-model.md` in the mod folder;
copy to `docs/`, it is gitignored), `tly_dumpbundles`, `tly_meta`, `tly_runstate`.

## Menus that block a sleep

Vanilla finishes a new day only after its end-of-night menus are dismissed. A `debug sleep` with a
level-up queued (Crash Course, `debug experience`) sits on the LevelUpMenu forever and every later
bridge command runs against the stale day. `tly_dismiss` clicks the menu's OK button (profession
picks take their default); send it after each sleep that could have queued one. `tly_openshrine
[active|boosts|plan]` opens the planning shrine on a tab without the statue, `tly_boost <id> [skill]`
buys any boost, `tly_boostexpire` forces the boosts' day-start pass.

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
- **Donations land a quarter a week, not all at once, and they land AFTER the pick.** Week k of
  every season picks the card, deposits the week's goals, and only then calls
  `tly_playseason quarter k`: the player chooses a card and then spends the week donating.
  Donating before the pick finished the season by the week-4 hub and left every week-4 goal pool
  empty. The share is global and cumulative (quarter 3 means three quarters of the season's plan
  donated in total), and quarter 4 also pays the vault and prints the `gate WOULD PASS/FAIL`
  ledger line. Only quarter 4 prints that line, so do not wait on it for the earlier weeks.
- The quarter's plan is flattened **round-robin across bundles**, not bundle by bundle: pass 1
  takes each bundle's first demanded slot in board order, pass 2 each bundle's second. A flat
  bundle-by-bundle list put the whole Boiler Room share inside quarter 1 and left Mining with no
  askable weekly goal for the rest of every season.
- **Check the deployed `config.json` before a sim.** It is Jeff's live file and it does not get
  overwritten by a deploy, so a stale value silently changes what the sim measures.
  `ThemeFillerBySeason` must be `[99, 99, 99, 99]` (the current default); the pre-0.16.82
  `[0, 1, 2, 99]` starves the early seasons.
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

