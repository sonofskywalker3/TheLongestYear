# Handoff — 2026-05-26 evening playtest session

You are picking up a long iterative playtest-and-fix loop on The Longest
Year. The user wrapped for the night with **two known-open bugs** and a
**design item awaiting sign-off**. Read this file FIRST.

## Working directory + branch

- `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`
- Branch: `feat/v1-plan-05-ui` (many commits past `bfeb221`; latest is
  `6a4bfc0` "row + icon-gap a touch bigger").
- 223 unit tests passing. Build clean.

## Workspace rules (NON-NEGOTIABLE — same as prior sessions)

- **Local commits only. NEVER push without explicit user "yes, push."**
- **Co-Authored-By footer on every commit:**
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- **`/sdcard/` paths are forbidden** (Android workspace rule — irrelevant
  here since TLY is PC-only, but be aware).
- **Playwright MCP tools are broken** — use PowerShell for screenshots.
- **Test before each commit:** `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
- **Build is `dotnet build src/TheLongestYear/TheLongestYear.csproj`**.
  EnableModDeploy=true auto-copies to `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\`.
- **The game's running session locks the DLL** — you can't build/deploy
  while it's running. Use `pwsh -NoProfile -File close-smapi.ps1` to kill
  and pull the log, or `Stop-Process -Name StardewModdingAPI -Force`.
- **Launch via** `Start-Process "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe"`.
- **Debug-command bridge:** writing a single tly_ command to
  `…\Mods\TheLongestYear\tly_commands.txt` makes the running game execute
  it on the next 30-tick poll (~0.5s). Used heavily this session to auto-fire
  `tly_reset` and `tly_setboard` without console typing.
- **Workflow division of labor:** I deploy, user tests, I pull logs.
  Never tell the user to run commands themselves unless absolutely needed
  (the bridge usually beats it).

## Background — what TLY is

The Longest Year is a roguelite mod for Stardew Valley. The player has 16
weeks (one year) to restore the Community Center; failing the season-end
bundle gate triggers `tly_reset` which rebuilds the world via vanilla's
`Game1.game1.loadForNewGame(loadedGame: false)` + custom RunState reset.

The current branch executes the **bundle-gate refactor** (`docs/superpowers/specs/2026-05-26-bundle-gate-handoff.md`)
+ playtest cleanup (`docs/superpowers/specs/2026-05-26-playtest-fixes-handoff.md`).
Most of that work is shipped. Open items are below.

## Read these next (after this file)

- `TODO.md` — current item: weekly theme journal entry feature (idea
  captured, not planned). Plan 06 sketches for the effects layer +
  liability design table (sign-off pending).
- `docs/superpowers/specs/2026-05-26-playtest-fixes-handoff.md` — the
  prior session's spec; most items shipped this session.
- `test-output/log-archive/SMAPI-playtest-2026-05-26-2300-FINAL.txt` —
  the final session log. The relevant lines for both open bugs are in
  here.
- `test-output/goals-menu-2026-05-26.png` — screenshot showing the
  pre-fix Season Goals menu (icons tiny, "needs N" wrong for Quality
  Crops). The fix is in `684c4d6` + `6a4bfc0` but unverified in-game.

## Known-open bugs (priority order)

### Bug A — Joja Mart shows as destroyed/abandoned on Spring 1 of a fresh run

**Symptom:** Player loads puffpuff, runs `tly_reset`, lands on Spring 1
day 1 of a brand-new run. JojaMart visual is the destroyed/abandoned
version with the cracked door — even with `mailReceived` flags cleared
+ areasComplete = [FFFFFF].

**Root cause (identified, NOT yet fixed):**
`Town.cs` line 577-583:
```csharp
else if (Utility.HasAnyPlayerSeenEvent("191393"))
{
    showDestroyedJoja();
    if (Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("abandonedJojaMartAccessible"))
    {
        crackOpenAbandonedJojaMartDoor();
    }
}
```

`showDestroyedJoja()` (Town.cs:391) modifies map tiles at (90..100,
42..50) to the destroyed-Joja look. The trigger is **event 191393 having
been seen**. That event is exactly what `CommunityCenterUnlock.Apply`
adds to `MasterPlayer.eventsSeen` to skip the Demetrius unlock cutscene
and make the CC accessible (so the player can enter on day 1).

Vanilla intent: 191393 = "Demetrius revealed the CC." After that, the
"destroyed Joja" backdrop is shown to signal the world has moved on
from the Joja-vs-CC pre-decision state. Our pre-unlock inadvertently
puts the world into "post-CC-discovery" state, which destroys Joja.

**Fix options (pick one, NOT yet attempted):**
1. **Don't set 191393.** Find another route to make the CC accessible.
   `Game1.cs:13121-13127` + `GameLocation.cs:6642` gate CC entry on
   `eventsSeen.Contains("191393")`. Need a Harmony patch on
   `Game1.isLocationAccessible` to force true for CommunityCenter, then
   audit the other 191393 checks for things that'd break.
2. **Harmony patch the Town method** that contains lines 577-583. Find
   the method (probably `resetForPlayerEntry` or a map-mod method); add
   a prefix that skips the showDestroyedJoja call when our mod is
   active. **Probably cleanest.**
3. **Harmony patch `Town.showDestroyedJoja` directly** — Prefix returns
   false, no-op. Even simpler. Locks out vanilla's destroyed-Joja in
   ALL contexts; might break the genuine Joja-path victory ending, but
   in v1 we're explicitly blocking Joja membership anyway
   (`JojaMembershipBlock`), so there's no Joja-route win to preserve.

I'd go with option 3 (simplest, lowest risk in v1).

**To investigate further:** read `Town.cs` lines 500-600 to find the
method containing 577-583. That's the enclosing method context. Also
search for `showDestroyedJoja` other callers — there's only one (line
579, already shown).

### Bug B — Day-8 Monday-morning hub still blocked

**Symptom:** Player slept through to day 8 (Monday of week 2). Hub
didn't open.

**Log evidence (line 278-279 of the final log):**
```
[22:35:46 INFO ] Week 2 selection offer: Mixed OR Mining (opening planning hub).
[22:35:46 TRACE] Cannot open menu: cutscene or input lock.
```

`OnDayStarted` IS firing on day 8 + IS calling `PresentOffer(week=2)` +
PresentOffer IS calling `_launcher?.OpenWeeklyHub`. The launcher's
`CanOpen()` check blocks because at that moment in the day-start
sequence, `Game1.eventUp` is true OR `Game1.player.CanMove` is false
(SMAPI fires `DayStarted` before the wake-up cutscene fully clears).

**Fix options (not yet attempted):**
1. **Defer the hub open** — in `OnDayStarted`, queue a flag, then in
   `OnUpdateTicked` open the hub when CanOpen finally returns true.
2. **Drop the CanMove check from `MenuLauncher.CanOpen`** — Game1.eventUp
   should be sufficient. CanMove was overly defensive (the prior session
   added it to dodge a different edge case).

Option 2 is simpler. Verify in-game that day-1 fresh-run hub still
opens cleanly after the change (that case worked because CanMove was
already true on a fresh load).

### Item C — Liability design table awaiting user sign-off

Captured in `TODO.md` under "Plan 06 — effects layer":

| Theme | Bonus | Liability |
|---|---|---|
| Foraging | 25% chance +1 on any forage drop | **Mines Closed** — elevator + ladder from entrance don't function |
| Farming | +25% Crop Growth | -30% Fish Bite Rate |
| Fishing | +30% Fish Bite Rate | -25% Crop Growth |
| Mining | 30% chance +1 on any mine drop | **Forage Off** — no wild produce / mushrooms (incl. mines) / fiddleheads spawn |
| Mixed | 10% chance +1 on any drop | -50% All Sell Prices |

User confirmed the bonus mechanics ("+1 chance" everywhere, no
exclusions for stone/wood since it's not "double"). Liabilities are still
provisional pending one more pass with the user.

For Plan 06 implementation hooks — see `TODO.md` § "Implementation notes
for the effects layer."

## What got shipped in this session

In commit order:

- `f692955` CB1+CB2: classifier accepts X>=Y for PerItem; Percentage only when X<Y.
- `95003a8` CB3: DonationObserver replaces failing Harmony patch on Bundle.tryToDepositThisItem.
- `758e87f` UX1+UX3: dropped U+2190 / U+00D7 / U+2212; unified "needs N before {NextSeason} 1".
- `53bb34f` UX4: rarity-weighted bonus sampling.
- `bfeb221` UX2: split WeeklyHubMenu from new SeasonGoalsMenu.
- `848bd57` In-CC interactable replaces F8 hotkey.
- `2ad96aa` Board fires via Harmony patch + draws sign.
- `29dedc6` Hub fires on Monday morning + diagnostics + targeted reset.
- `0bd0fec` Revert 0-quota Percentage exclusion (keep rarity weighting).
- `848bd57`/`992c000`/`a6899d0`/`43756d6` series — board fixes, reset
  clears Joja mail, JojaMembershipBlock, debug-bridge missing commands.
- `d495b8a`/`7667f38` Reset must repopulate Bundles dict via SetBundleData
  + use FieldDict to mutate NetArrays.
- `41a4d64` FarmerReset preserves slot count + bonus icons show
  donation quantity.
- `3ab38f6` SeasonGoalsMenu filters by current-season relevance.
- `684c4d6` SeasonGoalsMenu — bigger rows + 64px icons w/ stack & quality,
  fix Percentage 'needs N' count.
- `6a4bfc0` Row + icon-gap tweak.

## Confirmed-working from the final log

- `tly_reset` queued via tly_commands.txt fires + reset succeeds (line
  146-153 of the log).
- CC state on fresh reset: `areasComplete=[FFFFFF]`, all completion
  flags false (line 138 + 150).
- Week 1 hub opens cleanly on Spring 1 after reset (line 154-155).
- CB3 still working: `Donated 1x (O)18 (Uncommon) -> +3 JP` (line 174-175).
- Season Goals menu opens (line 176-177).
- Save/load cycle persists across 6+ day sleeps (lines 186-271).

## Open user requests / TODO items not yet shipped

- **Weekly Theme Journal feature** (captured in TODO.md). Donating all
  4 bonus items via an in-game quest entry completes the entry and
  suppresses that week's liability for the remaining days. Plan 06
  scope; design captured but not yet planned.
- **Liability sign-off** — see Item C above.

## Strong recommendations for your first hour

1. **Start with Bug A.** It's the most user-visible (every fresh reset
   shows it) and the root cause is documented. Pick fix option 3
   (Harmony prefix on `Town.showDestroyedJoja` → return false). Should
   be ~10 lines of new code. Build, deploy, queue `tly_reset` via
   `tly_commands.txt`, ask user to launch + load + observe Joja state.
2. **Then Bug B.** Try dropping CanMove from CanOpen first (simplest).
   If that breaks the day-1 hub, fall back to deferring via
   UpdateTicked.
3. Once both are shipped + verified, get the liability sign-off and
   start sketching Plan 06's effects layer (probably its own plan doc
   under `docs/superpowers/plans/`).

## Pulling the user's log

When the user reports something behaved wrong, the log answers most
questions. Pull via:
```
pwsh -NoProfile -File close-smapi.ps1
```
(That script kills the game AND copies `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`
to the project root.) The user is OK with you killing their session
when it's done — but ALWAYS pull the log first before launching the next
build, even if there was no crash. The log captures `tly_*` info + diagnostic
lines + the "Cannot open menu: cutscene or input lock" type warnings
that have been the deciding evidence for several bugs this session.

Archived logs go in `test-output/log-archive/SMAPI-playtest-YYYY-MM-DD-HHMM[-tag].txt`.

## Quick reference — useful tly_ debug commands

- `tly_reset` — full reset (rebuild world + clear RunState + open hub).
- `tly_resetif <name>` — reset only if `Game1.player.Name` matches.
- `tly_here` — print player's current tile coords.
- `tly_setboard` — anchor the Season Goals board to the tile the player
  is FACING. Writes config.json.
- `tly_meta` — print MetaStore state (banked JP, upgrades, etc.).
- `tly_donate (O)X` — record a synthetic donation in the run ledger.
- `tly_testdonate (O)X N` — feed N donations through DonationService.
  (Verified path — uses the real JP economy.)
- `tly_runstate` — current run state dump.
- `tly_catalog` — bundle-derived CC catalog summary.
- `tly_openhub` — manually open the planning hub.

All commands work via the SMAPI console OR via `tly_commands.txt` in the
mod folder (one command per line).

## User's preferred working style

- Doesn't want to type console commands himself unless absolutely
  necessary. Use `tly_commands.txt` bridge for everything we control.
- He DEPLOYS — I close the game, deploy, queue commands, ask him to
  launch + load.
- Wants concise progress updates after each iteration, not running
  commentary.
- Will correct design assumptions when I get them wrong. Listen carefully
  to corrections (the UX4 "include items but weight them" lesson +
  the "doesn't hurt the thing you're doing" liability principle were
  both critical re-framings from him).
- Cares about user-facing string quality (caught U+2190 / U+00D7 /
  U+2212 missing glyphs, caught the "needs 4 not 1" Quality Crops bug,
  caught the missing stack count).
