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
