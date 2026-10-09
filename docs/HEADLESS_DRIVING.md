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

With the hub open, three more commands check the Randomizer cards (all log to the SMAPI console):

- `tly_select <theme> left|right` is the real card click for that side (goal multiplier and
  mystery card included); it refuses a theme that is not on that side. Logs
  `Selected <theme> (slot N, goal JP xM)`. Without a side it is the old forced 1x pick.
- `tly_reroll paid` presses the reroll button's own code (price, JP spend, "cannot change"
  gate); logs `Reroll paid <cost> JP (JP a -> b)` or `Reroll refused: <reason>`. `tly_reroll`
  alone stays the free debug reroll.
- `tly_hubcards` logs each card: slot, theme (`?` when face down, real theme at Trace), drawback
  id and goal multiplier.

Wildcard days: `tly_wildcard` logs the week's planned day, the stored twist and today's
twist. `tly_wildcard <twistId>` makes today the wildcard day with that twist (any of the 13 ids
in `WildcardSchedule`), so a twist patch can be checked without waiting for its day;
`tly_wildcard clear` drops today's twist.

Shrine donations: `tly_shrinegoals` logs this week's shrine goals (`[index] list L <id> xN
deposited= paid=`; the roll itself logs `Shrine goals for <theme> (list L, week W)` at pick time).
`tly_shrinedonate <index>` spawns the goal's stack if no inventory stack can fill it and donates
through `ShrineDonationService.Donate`, the Donate tab's own path; logs
`goal N donated (JP a -> b)` or `refused`.

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
keeps, `tly_reset`, PASS when all three come back on their tiles); `tools/farmtype-intro.ps1`
plays the opening on a type, prints the farmer's porch tile and ends the event with `debug ee`.
Both exit to title when done. Delete the `<type>_<id>` save folders afterwards.

**Animal powers (2026-10-09).** `tly_animalpowers` lists the owned powers and every farm animal (age, daysOwned,
daysSinceLastLay, produce days with Busy Coop / Busy Barn, friendship, happiness, wasAutoPet). Subcommands: `produce <type>
<n>` (GetProduceID on n throwaway adults: Lucky Rabbits, Molting Season), `dig <n>` (n throwaway pigs dig on the
current map: Truffle Hog), `speed` (mounts the farm horse, then prints the riding speed against vanilla's sum:
Swift Horse; headless the farmer walks at speed 2, not the auto-run 5, so the percentage reads high), `pets`
(Data/Pets as loaded: Loyal Pet), `incubate` (an Egg into the first empty incubator: Fast Hatch), `grow`, `feed`,
`sethappy <n>`, `setfriend <n>`, `births <n>` (runs the barn-birth roll n times and counts births by parent: Growing Herd; compare before and after buying), `birthnight` (tonight's farm event is a certain barn birth), `doors` (opens every animal door), `truffles` (truffles found and on the farm, pigs outdoors: Truffle Hog over a real day on the farm), `enter coop|barn [i]` (warps inside; a ready incubator hatches on entry), `incubators`, `grow new|<name>`, `revoke <id>` (debug un-own, for a control run). `pet` (pets every pet once through Pet.checkAction, the click path; vanilla rolls the present on the day's first petting and drops it beside the pet, so the line lists the items lying there before and after: Loyal Pet). `tly_dismiss` closes the birth or hatch dialogue and names a NamingMenu through its own Enter path. `debug animal <type>` (vanilla, on the Farm) adds an animal through adoptAnimal; building names
with spaces need quotes: `debug forcebuild "Deluxe Coop" 52 20`.

**Bundles per room (2026-10-09).** `tly_bundlecount` lists the live bundles per room and says whether the CC's own
room lookup knows every bundle (a missing one throws KeyNotFound in `checkForMissedRewards`). `tly_bundlecount set
<step>` sets the dial in memory only (config.json untouched; the next new board uses it), `tly_bundlecount open
<area 0-5>` opens that room's page and logs each bag and its spot (the Vault is area 4), `tly_bundlecount missed` runs
`checkForMissedRewards` and logs whether it threw. `tly_bundlecount buy` opens the Vault page; `tly_bundlecount buy
[cheapest|<index>]` in a LATER batch pays that bundle through the page's own purchase button (gives the farmer the
gold first) and logs the price, the gold taken and whether ccVault was queued; TLY's observer logs `Vault bundle N
paid` on the next tick. The page stays open so the observer sees it: send `tly_bundlecount close` (closes any menu
without its exit action, so no restore cutscene) when done. After the purchase that finishes the room, `tly_bundlecount exit` closes the page with
vanilla's own exit action instead (the Vault restore cutscene), so the bus repair follows that night as in play.
`tly_bundlecount bus` logs Keep Bus Unlocked's reach with each path on its own (every Vault bundle on the board paid
this run; the Vault room repaired: area flag or the ccVault mail) and the live result.

**Upgrade menu screenshots.** `tly_openshop <tab> [hoverRow] [scroll]` opens the upgrade menu on a tab, scrolled,
with the tooltip pinned to a row (drawn under that row, not at the mouse), and logs the visible rows and the tooltip
text.

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

## Festival memories (`tly_festmem`)

Besides `status`, `set`, `partner`, `recipient`, `clear`, `commit` and `force`, these drive a festival headlessly
(date with `debug season`/`tly_setday` and a sleep, then `debug time 900` and `debug warp <map>` enters it):
`talk <npc>` (the festival's own checkAction on that villager), `start` (the host's "yes"), `click` (one click on
the open dialogue box), `pot` (acts on the Luau soup pot tile, which opens vanilla's ingredient menu), `give <item>
[quality]` (picks that item in the open item menu through the menu's own callback: the Luau pot or the secret gift
after `talk <secret friend>` and `tly_answer 0`), and `score <n>` (sets the eggs or fish found, e.g. 9 wins the egg
hunt).
