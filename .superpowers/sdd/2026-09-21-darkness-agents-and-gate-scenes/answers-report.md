# Answers report: Jeff's 2026-10-07 answers (thief draw, strike queue, fish and Holly)

Branch `story`, commits `cfbb79d..aae0e39` (on top of `f0e9d8b`), all pushed to origin/story.
Manifest Version untouched (0.18.15).

| Commit | What |
|---|---|
| cfbb79d | 3: fish keep the same word in the ask and the tainted name; Holly in sprigs |
| aeb0a1a | 1: while the thief scene is due, the thief only picks chests the scene can show; an unfilmable due scene postpones instead of landing bare |
| d893d53 | 2: a postponed strike is queued for the next free night |
| aae0e39 | docs: spec, HEADLESS_DRIVING, CHANGELOG Unreleased |

## 1. Thief only picks chests the scene can show, while its scene is due

- `src/TheLongestYear.Core/Sabotage/ChestDraw.cs:15` `ChestHost` gains `Filmable` (the group's host
  stands on a scene map; `ChestHosts` already prefers a Junimo Chest on the scene maps as host, so a
  Junimo group is filmable when any of its chests is there and is staged at that one).
- `ChestDraw.cs:61` `BlightRule.InThiefDraw(host, thiefSceneDue) => host.Host && (!thiefSceneDue || host.Filmable)`.
- `src/TheLongestYear/Loop/SpoilagePass.cs:132` the draw uses it; `StoredUnits` and `Plan` take
  `thiefSceneDue` (default false for the debug `tly_sabotage blight` and the status total).
- `src/TheLongestYear/Loop/SabotageService.Night.cs:180` `NightPlan.ThiefSceneDue` (= ChestBlight
  scene not yet played this loop) feeds `CanAct` (`:200`) and `Prepare` (`:239-240`). With nothing
  filmable to take, `CanAct(ChestBlight)` is false: the existing can't-act path (guarantee and queue
  keep him owed).
- The "lands at once without a scene" branch is gone: `Night.cs:33-36` lands at once only when no
  scene is due; a due scene that cannot show its pick (or whose test throws) now postpones
  (`PostponePendingIfAny`), so it is also queued (change 2). Placed machines need no filter: they are
  only ever at stake on the Farm map.
- `ModEntry.cs:2905/2908` the `tly_sabotage scene thief` preview draws filmable chests only.
- Stale comments and three scene log lines that still said "lands with no scene" (ThiefScene,
  CloudScene, HallScene staging failures, which have postponed since C1) now say the strike waits.
- `tly_sabotage status` adds `of which the thief can take tonight: M`.

## 2. A postponed strike is queued for the next free night

- `src/TheLongestYear.Core/Sabotage/StrikeQueue.cs` (new, pure): `Enqueue` (a kind once),
  `Tonight(run, canAct)` (oldest queued kind that can act, else null; unknown names skipped),
  `OnCommitted`, `IsQueued`.
- `src/TheLongestYear.Core/RunState.cs:331` `List<string> QueuedStrikes = new()`; cleared at
  `BeginNewRun` (`:608`). Old saves load it empty (default initialiser; a JSON null is handled by
  `??=` / null checks).
- `SabotageService.Pending.cs:62` every postponed strike is queued, except the guaranteed Winter tamper,
  which keeps its existing carry (`GuaranteedTamperPostponed`): it already fires on the next free night
  and also lifts the week-1 reservation, so folding it in would not simplify anything.
- `SabotageService.Night.cs:118-127` after the debug arm and before the every-loop guarantee: the queued
  kind that can act fires instead of the roll (dice ignored), through the same `Strike()` path, so it
  gets its scene and commits/records like any strike (chance drop, cap, spacing, `StruckEvents`).
- `Pending.cs:105` it leaves the queue when a strike of that kind commits (not the guaranteed tamper).
  A night whose slot is taken again postpones it and it stays queued.
- Decision (asked to state): **a queued strike that cannot act tonight stays queued and the night rolls
  normally.** "Can act" is the kind's own test, so caps, the 5-day tamper spacing, the guaranteed
  tamper's week-1 reservation, wards, quiet days and "nothing fair" all keep it queued.
- Precedence: guaranteed Winter tamper, then a debug arm, then the queue, then the every-loop
  guarantee, then the roll.
- `tly_sabotage status` adds `Queued for the next free night (postponed strikes, oldest first)`.

## 3. Asks: fish keep the same word; Holly in sprigs

- `src/TheLongestYear.Core/Sabotage/AskPhrases.cs:103` Holly `(O)283` -> "sprigs" ("8 sprigs of Holly";
  the tainted name then reads "all the Holly" through the container rule).
- `AskPhrases.cs:142` `IsFishSameInThePlural(id, category)`: category -4
  (`FlavoredSlotRules.FishCategory`) and not one of the jellies. Checked after the container,
  same-word and countable tables (`:156`), so Roe / Aged Roe / Caviar / Smoked Fish keep theirs and the
  jellies stay "Sea Jellies".
- `ItemPlurals.cs:77` the tainted name uses the same rule ("all the Pike").
- `Ask` and `Tainted` take an optional `category` (default `AskPhrases.NoCategory`);
  `SabotageService.Tamper.cs:126` `CategoryOf` reads `ItemRegistry.GetData(id).Category`, passed at
  `SabotageService.Morning.cs:55,61`. No i18n lines changed.

## Tests

TDD: the new tests were run red first (fish/Holly: 9 failures after adding the parameter with no
behaviour; thief draw and queue: compile failures on the missing API), then green.

- `tests/TheLongestYear.Tests/AskPhrasesTests.cs`: `A_fish_keeps_the_same_word` (Pike, Salmon,
  Largemouth Bass, Bream, Pufferfish, Octopus; the three jellies stay plural),
  `The_fish_goods_keep_their_containers`, `A_thing_that_is_not_a_fish_still_takes_its_plural`,
  `Holly_comes_in_sprigs`.
- `tests/TheLongestYear.Tests/ItemPluralsTests.cs`: `The_tainted_name_of_a_fish_keeps_the_same_word`,
  `The_tainted_name_of_holly_is_bare`.
- `tests/TheLongestYear.Tests/ThiefDrawTests.cs` (5): scene-map only while due; every chest once
  played; Junimo group filmable via one chest and staged there; Junimo group with none waits; a
  non-host is never in.
- `tests/TheLongestYear.Tests/StrikeQueueTests.cs` (10): postponed fires next free night then leaves;
  slot taken again keeps it queued; cannot act keeps it queued (night rolls normally); oldest first;
  tamper spacing respected (real `StrikeLedger.Record` + `SabotageSchedule.WithinCaps`); a kind
  queued once; loop reset clears; old saves (Newtonsoft and System.Text.Json, missing and null) load
  empty; survives a save round trip; unknown names skipped.

```
dotnet build TheLongestYear.sln               -> Build succeeded, 0 Error(s)
dotnet test tests/TheLongestYear.Tests --no-build
Passed!  - Failed: 0, Passed: 4073, Skipped: 0, Total: 4073
```

## Live evidence (all launches mine, minimized, no mouse or keyboard)

`tools/deploy.ps1 -Minimized`, `tools/bridge.ps1`, `tools/send-smapi-command.ps1`. Throwaway farm
`standard_451091589` from `tly_newgame standard skipintro` (deleted after; the Saves listing matches the
one taken before). Log archived as `test-output/log-archive/SMAPI-v0.18.15-20261007-191851.txt`;
no ERROR lines. The deploy pruned one tracked log; restored with `git checkout -- test-output/log-archive`.
Mod `config.json` and `Default_options` unchanged; `startup_preferences` differed only in
`timesPlayed` and was restored byte for byte. Game closed.

Recipe: `tly_select Fishing`, `tly_setday 5`, `debug season summer`, `debug sleep` (Summer 6),
`tly_sabotage fixture scarecrow rows=3`.

**Postponed strike fires with its scene on the next free night.** `tly_sabotage arm blight crops`,
`debug mft ccVault`, `debug sleep`:
```
19:16:14 Darkness: night pass done at tick 10306, leaving CropBlight waiting for its scene.
19:16:14 Darkness: tonight's CropBlight is postponed (WorldChangeEvent has the overnight slot): no effect and no scene tonight; the night counts as no strike, and it is queued for the next free night.
status Summer 7: chance tonight 25 %; blight week -1 nights 0; live crops 30; Scenes played: none;
                 Queued for the next free night: CropBlight; Struck: none
```
Then `debug sleep` with nothing armed:
```
19:16:59 Darkness: night roll Summer 7 at 25 %: strike; level Normal.
19:16:59 Darkness: the postponed CropBlight fires tonight (Summer 7) instead of the roll.
19:16:59 Darkness: the crows are staged on 3 bird(s) at (60,22), scarecrow in frame, Linus walking in.
19:16:59 Darkness: tonight's CropBlight is committed and recorded.
19:16:59 Darkness: the postponed CropBlight has struck, so it leaves the queue.
19:16:59 Darkness: the CrowsScene scene takes tonight's overnight slot for CropBlight (not skippable).
19:17:04 Darkness: 2 crop(s) struck down, 0 stored unit(s) spoiled, 0 gone missing on Summer 7.
status Summer 8: blight week 5 nights 1; live crops 28; Scenes played: CropBlight; Queued: none; Struck: CropBlight
```

**Thief chooses a farm chest over a barn chest while his scene is due.** `debug warp Farm 64 15`,
`debug forcebuild Barn 42 14`, `debug warp Barn 8 10`, `tly_sabotage fixture here` (chest on the Barn
with 20 Parsnip + 10 Copper Ore):
```
status: units in chests (stash excluded): 75, of which the thief can take tonight: 45
```
`debug warp Farm 64 15`, `tly_sabotage arm blight chest`, `debug sleep`:
```
19:18:25 Darkness: the thief is staged on a chest at (69,21) on Farm, walking 2 tile(s) in from (71,22), lid driven ...
19:18:25 Darkness: tonight's ChestBlight is committed and recorded.
19:18:27 Darkness: 0 crop(s) struck down, 2 stored unit(s) spoiled, 1 gone missing on Summer 8.
status Summer 9: units in chests: 72, of which the thief can take tonight: 72; Scenes played: CropBlight, ChestBlight
```
The barn's 30 units were out of the draw while the scene was due (75 vs 45), all 3 units came from the
farm chest (barn kept its 30), and once the scene had played the barn chest is back in (72 = 72).

## Handoff farms

`standard_451080087`, `standard_451080418`, `standard_451080615`: not loaded or touched (SHA-1 of every
file before and after matches). Read-only scan of the save XML: every chest on all three is on the Farm
or in the FarmHouse ((69,21), (67,17), FarmHouse (3,7)), so the thief draw is unchanged; their queue is
empty on load, so their nights roll as before unless a collision happens (then the strike now comes
back the next free night). The Winter 1 tamper pick on `standard_451080615` is unchanged; only if its
old or new item is a fish or Holly does the porch line now read the new way ("all the Pike", "8 sprigs
of Holly"). All three remain valid.

## Open items

- A due scene whose pick-time test keeps failing (only reachable now through a throwing scene test, or
  a thief pick whose scene target is somehow off the scene maps) would be postponed and queued every
  night, and because the queue fires before the roll, that kind would block the normal roll while it
  can act. Not reachable with the current draw; flagged in case a scene test starts throwing.
- A queued kind is held once: two postponed strikes of the same kind before either fires collapse into
  one (in practice they cannot both occur, since the queue fires first whenever the kind can act).
- A debug arm or the roll committing a queued kind also clears it from the queue (the owed strike
  happened).
- Not checked live: a queued tamper held by the 5-day spacing (unit-tested), a queued thief with no
  filmable stock (unit-tested via `InThiefDraw`), a Junimo group across a barn and the farm.
- Still waiting on Jeff from the earlier round: the "touched them" line after a mass noun.
