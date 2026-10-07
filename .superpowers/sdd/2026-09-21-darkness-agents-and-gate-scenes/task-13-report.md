# Task 13 report: status, docs, the collision check, Jeff's pass

Status: DONE. Branch `story`. Commits `e313d5c..340f664` (code and docs) plus this report, all pushed
to `origin/story` (no GitHub 500). Manifest version untouched (0.18.15).

## Changes

### Step 1: `SabotageService.Status()`

"Scenes played this loop" and "Scenes seen on save" already existed (two lines); not duplicated.
Three lines added after them:

- `Struck this loop: <Run.StruckEvents or none>`
- `Owed tonight (every-loop guarantee, from day 15 of the debut season; forced only if it can act): <StrikeGuarantee.Owed(Run.Season, Run.DayOfMonth, Run.StruckEvents) or none>`
- `Witness lines pending: <Npc> (scene day <doy>, until day <doy+7>), ... or none` (unsaid records)

`Owed` is the pure list; it does not ask `CanAct`, so a warded Summer still lists CropBlight. The
label says so. No new tests (Status is game-side, untested before as well).

### Step 4: docs

- `docs/HEADLESS_DRIVING.md`: new section "Darkness strike scenes (story, verified 2026-10-07)": the
  `tly_sabotage scene|fixture|arm|status|aurachest` and `tly_witness` commands, the overnight slot
  rule (with a pointer to the existing night-order section from Task 5), the door-step rule for the
  Junimo scene, the collision recipe, the guarantee recipe, and how to predict a night's roll from
  the run seed.
- `2026-09-09-darkness-pushback-design.md`: top note, first-strike letters removed and presentation
  replaced by the 2026-09-21 spec.
- `2026-09-14-darkness-rework-design.md`: top note, a per-loop guarantee added by the 2026-09-21 spec.
- `CHANGELOG.md` Unreleased, Added: the darkness agents (four scenes as built: framed map cloud,
  Brute in the hall window, Shadow Brute thief, crows), the guarantee, witnesses, the door-step
  Junimo scene naming the exact item, the single-slot tamper and no tainted replacement, natural
  plurals ("3 Wild Honey"), the aura, the gate scenes. Darkness was not in the changelog at all
  before (it has never been released), so it is written as new, not as changes. Debug line for the
  new commands. No file names, no em dashes.
- `STATUS.md`: header updated (2026-10-07, tests 3914, last public release 0.19.0 tag, nothing on
  story released, story merges once when Jeff says) and a new top section: built, live checks,
  pass owed, the three saves.
- `TODO.md`: new top entry "Darkness agents and gate scenes: BUILT on story 2026-10-07, Jeff's pass
  owed" with the saves table, what to judge, and the two open designer questions (a) replacement
  skipping items asked elsewhere, (b) mass-noun asks. Not decided.

## Step 2: the slot collision (passed)

Throwaway farm, `tly_newgame standard skipintro`, `debug season summer`, `tly_setday 5`, sleep to
Summer 6, `tly_sabotage fixture scarecrow rows=3`, `tly_sabotage arm blight crops`,
`debug mft ccVault` (queues `WorldChangeEvent(7)`, the bus repair), `debug sleep`:

```
[15:36:23 INFO ] Darkness: Blight was armed; striking tonight as CropBlight.
[15:36:23 TRACE] Darkness: night pass done at tick 12459, leaving CropBlight waiting for its scene.
[15:36:23 TRACE] Darkness: applying tonight's CropBlight without its scene (WorldChangeEvent has the overnight slot) at tick 12459.
[15:36:23 INFO ] Darkness: 2 crop(s) struck down, 0 stored unit(s) spoiled, 0 gone missing on Summer 6.
```

The bus event played: that night ran 12 s from the pick to the save (15:36:23 to 15:36:35), against
2 s for a quiet night on the next farm (15:51:43 to 15:51:45). Morning status: live crops 30 -> 28,
`Scenes played this loop: none`, `Struck this loop: CropBlight`. Then `arm blight crops` again and
sleep:

```
[15:40:52 TRACE] Darkness: the strike scene takes tonight's empty overnight slot.
[15:40:52 INFO ] Darkness: the CrowsScene scene takes tonight's overnight slot for CropBlight (not skippable).
[15:40:58 INFO ] Darkness: 2 crop(s) struck down, 0 stored unit(s) spoiled, 0 gone missing on Summer 7.
[15:41:01 TRACE] Darkness: the CrowsScene scene ended (finished) at tick 29068.
[15:41:01 DEBUG] Witness: Linus saw the CropBlight scene on day 35.
```

Status after: `Scenes played this loop: CropBlight`, `Witness lines pending: Linus (scene day 35, until day 42)`.
Not checked directly: the bus stop map's repaired look (no read-only command for it); the evidence is
the slot line plus the night's length.

## Step 3: the guarantee, live (passed)

Throwaway farm, Summer via `debug season summer` + `tly_setday 13` + sleep (so `Run.Season` syncs),
fixture with crops and a chest. Summer 14 status: `Struck this loop: none`, `Owed tonight: none`.
Note: it takes three sleeps from 14, not two: night 14 is an ordinary roll, the force starts on the
night of day 15.

```
[15:51:43 TRACE] Darkness: night roll Summer 14 at 25 %: quiet; level Normal.
[15:52:10 INFO ] Darkness: CropBlight has not struck this loop by Summer 15, so it is forced tonight.
[15:52:10 INFO ] Darkness: the CrowsScene scene takes tonight's overnight slot for CropBlight (not skippable).
[15:52:15 INFO ] Darkness: 2 crop(s) struck down, 0 stored unit(s) spoiled, 0 gone missing on Summer 15.
(status on 16: Struck this loop: CropBlight; Owed tonight: ChestBlight; Witness lines pending: Linus (scene day 43, until day 50))
[15:52:40 INFO ] Darkness: ChestBlight has not struck this loop by Summer 16, so it is forced tonight.
[15:52:40 INFO ] Darkness: the ThiefScene scene takes tonight's overnight slot for ChestBlight (not skippable).
[15:52:43 INFO ] Darkness: 0 crop(s) struck down, 2 stored unit(s) spoiled, 1 gone missing on Summer 16.
(status on 17: Struck this loop: CropBlight, ChestBlight; Owed tonight: none)
```

With the ward, on a third throwaway farm (`tly_addjp 2000`, `tly_buyupgrade ward_crops_summer` on
Summer 14: "Purchased 'ward_crops_summer' (Ward of the Fields: Summer) for 250 JP"):

```
[15:54:23 TRACE] Darkness: night roll Summer 14 at 25 %: quiet; level Normal.
[15:54:41 INFO ] Darkness: ChestBlight has not struck this loop by Summer 15, so it is forced tonight.
[15:54:41 INFO ] Darkness: the ThiefScene scene takes tonight's overnight slot for ChestBlight (not skippable).
[15:55:05 TRACE] Darkness: night roll Summer 16 at 20 %: quiet; level Normal.
```

No crows on 15 or 16; live crops stayed 30. Status lists `Owed tonight: CropBlight` on the warded
farm (pure list; it cannot act).

## Step 5: the saves for Jeff's pass (prepared, NOT launched for him)

All three were built through real gate passes (`tly_playseason`, `tly_setday 27`, sleep, then
`tly_playseason` again on day 28 so a night-27 strike cannot fail the gate, sleep, porch scene
stepped with `tly_eventstep`, theme picked). All named "Rodger", farm name "standard".

| Folder | Date | State |
|---|---|---|
| `standard_451080087` | Summer 14 | `tly_sabotage fixture scarecrow rows=5` (50 corn, scarecrow, chest with 20 Parsnip + 10 Copper Ore), 5,000g added. Status: nothing struck, no scenes seen on the save. |
| `standard_451080418` | Fall 14 | Summer and Spring gates passed (the Summer 27 night forced the thief, so ChestBlight is seen on the save). Fall: fixture rows=3, `tly_playseason quarter 1` (filled slots in unfinished bundles for the reversion), 5,000g. Fall status: chance 35%, reversion owed from 15. |
| `standard_451080615` | Winter 1 | Spring, Summer and Fall gates passed; the Winter porch scene played (I stepped it) before the hub. Status: guaranteed Winter tamper pending, first Winter ever, 39 tamper targets. |

A save is always written after a night roll, and on my first two Summer farms the night-13 roll
struck (the thief) and spoiled the "nothing struck" state (both deleted). So I predicted the roll
from the run seed (`SabotageSchedule.Rng`, checked against the two observed strikes: draws 0.059 and
0.192 under 0.25) and only built on seeds whose draws were quiet: Summer farm night 13 0.819 and
night 14 0.858; Fall farm night 13 0.658 and night 14 0.703 (Fall chance 35%). So Jeff's night 14 is
quiet on both, and night 15 is the forced strike.

How to load (his launch, ask first): title screen Load menu (the three Rodger rows differ by date),
or headless `tools/bridge.ps1 -Action send -Lines "tly_loadsave standard_451080087"` (likewise
`standard_451080418`, `standard_451080615`). What to do on each is in `TODO.md`:

- Summer 14: sleep; on 15 pick a theme, sleep: crows; talk to Linus on 16; sleep: thief.
- Fall 14: sleep; on 15 pick a theme, sleep: hall; talk to Shane on 16 and later.
- Winter 1: sleep: cloud; on Winter 2 step out of the door: Junimos; aura (`tly_sabotage aurachest`).
  The Winter gate scene already played on this farm (a loaded save does not replay it), so the held
  glow is `tly_seasonturn winter`.

## Live check housekeeping

All launches mine: `tools/deploy.ps1 -Minimized` once, `tools/bridge.ps1`,
`tools/send-smapi-command.ps1`, no mouse or keyboard, no screenshots. Harmony and the mod loaded
with no ERROR line in the whole session. Game closed at the end. All other throwaway farms deleted;
the Saves folder is the listing from before plus the three farms above. `config.json` untouched
(window 1920x1080); `git status` showed no pruned tracked logs, so nothing to restore.

## Tests

```
dotnet build TheLongestYear.sln            -> 0 Error(s)
dotnet test tests/TheLongestYear.Tests --no-build
  Passed!  - Failed: 0, Passed: 3914, Skipped: 0, Total: 3914
```

## Open items

- Designer questions recorded in `TODO.md`, not decided: (a) tamper replacement skipping items asked
  elsewhere; (b) mass-noun asks.
- The brief's "sleep twice" from Summer 14 is three sleeps for the thief (14 quiet, 15 crows, 16
  thief). Docs say three.
- The Winter 1 farm cannot show the real gate scene on load; the replay command covers it. If Jeff
  wants the real porch scene, a Fall 28 save with the Fall gate complete can be made (reversion is
  quiet from day 25, so night 27 cannot undo a gate slot), but loading a save that sits on day 28 was
  not tried, so I did not hand that one over.
- Not checked live: the bus stop's repaired map after the collision night (inferred from the slot
  log and the night length).

## Final-review fix wave

Commits `c9bb44b..b549fe1` on `story` (code), then this docs commit; all pushed to `origin/story`.
Manifest Version untouched (0.18.15). No live run.

- **I1, strike scene vs Wildcard night_event** (c9bb44b). New pure rule
  `StrikeScenes.WildcardTwistHasTheSlot(twist, suppressed)` (src/TheLongestYear.Core/Sabotage/StrikeScenes.cs:33).
  `StrikeScenePatch.Postfix` asks it right after the fail-night check
  (src/TheLongestYear/Loop/StrikeScenePatch.cs:141) and, on a night_event night, calls
  `ApplyNow("the wildcard night event has the overnight slot")` and returns. It reads the run's
  twist, not `__result`, so the outcome is the same whichever Priority.Last postfix runs first: if the
  wildcard ran first, its forced fairy/witch/sound is no longer taken as a "random" event; if the
  strike runs first, the slot stays null for the wildcard to fill. The scene is not marked played, so
  it stays due. Tests: StrikeScenesTests (4 new cases).
- **I2, Mini-Shipping Bin / Junimo Chest** (79ee1d9). `BlightRule.ChestInDraw(shipsOvernight, sharedInventory)`
  (src/TheLongestYear.Core/Sabotage/SabotageRules.cs:55); `SpoilagePass.Entries` skips
  `SpecialChestTypes.MiniShippingBin` and `JunimoChest` (src/TheLongestYear/Loop/SpoilagePass.cs:70).
  Tests: DarknessLevelsTests `The_chest_draw_skips_shipping_bins_and_junimo_chests` (3 cases).
- **M2, first-Winter flag** (d785f7a). `Meta.FirstWinterTamperSeen` is no longer set at the pick; it is
  set in `OnStrikeApplied` when the guaranteed tamper has landed
  (src/TheLongestYear/Loop/SabotageService.Night.cs:60). The day-7 "week 1 is spent" line (Night.cs:134)
  is unchanged, so a first Winter with no fair target still counts as reached. A failed apply now
  retries tomorrow on the first-Winter schedule. Not unit-tested (service state); covered by build.
- **M1 + carried reports, M7** (f88e3ed). New pure `MorningReports.TakeShownNow(reports, tampersWait)`
  (src/TheLongestYear.Core/Sabotage/MorningReports.cs). `ShowMorningReports` shows and removes every
  non-tamper report and leaves tamper reports while the tamper scene is wired
  (src/TheLongestYear/Loop/SabotageService.Morning.cs:75); `TryStartTamperScene` removes only the report
  it told (Morning.cs:63). `ShowMorning()` is now `void` with no parameter and always shows the non-tamper
  reports on waking (Morning.cs:25); callers RunController.cs:984 and ModEntry.cs:2641 updated, stale
  "porch scene first" comment replaced. Tests: MorningReportsTests (3, written first, failed to compile).
- **M3, aura robustness** (026c9ed). src/TheLongestYear/Loop/TaintedAuraPatch.cs: `RunActivation.IsActive`
  guard in `IsTainted` (:106); both prefixes wrapped, first failure logged via PatchLog.Warn then the aura
  stays off (:126); `Glow()` rebuilds when the graphics device changes (:97), disposing the old texture.
- **M4** (5cb77d9). SceneCamera.cs:183, SceneGlow.cs:100 and :121 now log via PatchLog.Warn.
- **M5** (25335f4). CloudScene.cs:337 indexed loop over `_cloud`.
- **M6** (c9bb44b). StrikeScenePatch.cs:109 comment: `canGetPregnant` writes defaultMap, idempotently
  (verified in decompile NPC.cs:6186).
- **Docs** (this commit). TODO.md: both designer questions marked answered and built, pointing at
  asks-report.md. STATUS.md: last public release 0.19.1 (tag v0.19.1), "Details are in" sentence fixed,
  range `c1313a1..b549fe1`, test count 4007, fix-wave summary. CHANGELOG Unreleased: I1 (wildcard night
  keeps the night, scene waits) and I2 (thief never takes from a Mini-Shipping Bin or Junimo Chest) in
  player words; also corrected the stale ask examples ("3 jars of Honey", "3 bottles of Wine") and the
  tamper line to say the replacement is new to the board.
- **Split** (b549fe1). SabotageService.cs (state, ctor, Enabled, KindOf), .Night.cs (night pass,
  OnStrikeApplied, Strike, RunNight, NightPlan, TamperPlan, CropsWarded), .Strikes.cs (Blight, Revert,
  PrepareRevert, reversion picks; named Strikes since blight and reversion live there), .Tamper.cs
  (Tamper, PrepareTamper, PlanTamper, BoardFlavor, CreateExact, ExactName, Candidates, HeldItemIds,
  WriteTamper), .Morning.cs, .Debug.cs (Arm, TakeArmed, Explain, TargetLine, TargetSummary, Status).
  Members moved verbatim by line range; largest file 344 lines. PrepareRevert/PrepareTamper stayed beside
  their effects rather than in Debug.

Tests:
```
dotnet build TheLongestYear.sln            -> Build succeeded, 0 Error(s) (10 pre-existing nullable warnings, none in touched files)
dotnet test tests/TheLongestYear.Tests --no-build
Passed!  - Failed:     0, Passed:  4007, Skipped:     0, Total:  4007
```
