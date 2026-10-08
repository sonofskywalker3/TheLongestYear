# Conflict rule report: conflict pushes back, broken lands bare (Jeff, 2026-10-08)

Branch `story`, commits `c353637..aa8c18c` (on top of `5381c33`), all pushed to origin/story.
Manifest Version untouched (0.18.15). No em dashes written. Designer dialogue unchanged; the one new
line (`event.darkness.tamper-2-mass`) is the designer's verbatim text.

| Commit | What |
|---|---|
| c353637 | a conflict postpones and queues; a broken scene lands the strike bare (Core rule, wiring, debug switch, tests) |
| 44df2eb | each morning HUD line is also logged at Trace (for headless evidence) |
| 05461d3 | docs: spec, HEADLESS_DRIVING, CHANGELOG Unreleased |
| aa8c18c | add-on: the Junimos say "touched it" after an uncountable tainted item |

## Changes

Core (pure):
- `src/TheLongestYear.Core/Sabotage/StrikeStaging.cs` (new): `AtPick(sceneDue, canStage, canShowPick)` (:32)
  returns `LandNoSceneByDesign`, `WaitForScene` or `LandBare`; `CauseAtNet` (:42) maps `Replaced` to
  `SlotTaken` (queued) and `Postpone` to `NeverStaged` (not queued).
- `StrikeSlot.cs`: `StrikeNetAction` gains `LandBare` (:78, setUp ran but never staged) and `Replaced`
  (:82, handed the slot but setUp never ran). `StrikeLifecycle` gains `HandedSlot`/`SetUpRan`,
  `OnHandedSlot`/`OnSetUp` (:105), `LandBare` (:134, same steps as LandNow); `AtNet` (:145) is
  None / Land / LandBare / Replaced / Postpone in that order.
- `StrikeQueue.cs`: `PostponeCause.CannotStage` removed; only `SlotTaken` (:11, now also a replaced scene)
  and `NeverStaged` (:16) remain. `Queues` unchanged (SlotTaken only).

Mod:
- `SabotageService.Night.cs:27` `Strike()` asks `StrikeStaging.AtPick` (:33): a due scene that cannot
  stage or cannot show the pick (or whose check throws) lands bare at once. `NightPlan.CanAct` no
  longer asks about the scene (:200 comment; the `_stages` cache and `SceneStages` are gone). The
  thief's filmable-chest draw (`ThiefSceneDue`) is unchanged.
- `SabotageService.Pending.cs:50` `LandPendingNow(why, bare)` replaces `ApplyPendingIfAny`;
  `SettlePendingIfAny` (:103) handles Replaced (postpone, queued, :110), Postpone (unqueued),
  LandBare and Land.
- `PendingStrike.cs:107` `LandBare()` (commits so `OnStrikeCommitted` records it, then runs the effect
  once), `:115` `OnHandedSlot`, `:119` `OnSetUp`.
- `StrikeSceneBase.cs:189` `setUp` marks `OnSetUp` first; `Stage()` false or a throw before staging ends
  the scene, and `End` (:383) calls `LandWithoutScene` (:157) instead of `ApplyStrike` when not staged.
  `shown` stays false, so the scene is not marked played (it stays due).
- `ModEntry.cs:265` `SceneFor` marks the strike handed the slot when it returns a scene.
- Debug: `Scenes/SceneBreakSwitch.cs` (new) + `tly_sabotage breakscene <crows|thief|hall|cloud|off> [pick|setup]`
  (`ModEntry.cs:2823`); `StrikeSceneFactory.CanStage/CanPlay` (:17, :31) and `setUp` (:192) honour it.
- `SabotageService.Morning.cs:112` `Hud` logs each morning HUD line at Trace.
- Scene log lines and docs (Thief/Hall/Cloud) now say "lands without its scene".

Every path lands at most once (StrikeLifecycle): bare at pick (LandPendingNow clears Pending first),
bare in setUp (End runs once), staged (ApplyStrike / net Land), conflict (Postpone, never applied),
replaced (net postpones, never applied). Guaranteed Winter tamper: a bare landing commits, so
`GuaranteedTamper.OnCommitted` marks it done and `OnApplied(landed)` clears the carry; it is no
longer held back when the cloud cannot stage.

## Tests

TDD: `ConflictRuleTests.cs` written first; red step was compile failures on the missing API
(`StrikeStaging`, `StrikePickAction`, `OnHandedSlot`, `OnSetUp`, `LandBare`, `StrikeNetAction.LandBare`).

- `tests/TheLongestYear.Tests/ConflictRuleTests.cs` (14): by-design / wait / cannot stage (pick never
  asked) / cannot show the pick; broken lands bare and records (StruckEvents, cap, chance, guarantee
  satisfied); bare leaves the scene due; conflict postpones and queues; replaced before setUp is a
  conflict and queues; setUp throw lands bare; setUp ran but uncommitted is landed bare by the net;
  never handed is postponed unqueued; staged lands exactly once; guaranteed tamper satisfied by a bare
  landing; `PostponeCause` has no staging cause.
- Updated (old "postpone without queue" behaviour): `StrikeSlotTests`
  `A_scene_that_cannot_stage_lands_bare_and_the_net_has_nothing_left` (was "never lands and the net
  postpones"), `Another_mod_replacing_our_scene_after_the_postfix_is_a_conflict_the_net_queues` (was
  "net postpones"); `StagingAtPickTests.Only_a_slot_collision_queues_a_postponed_strike` (CannotStage gone).
- "CanAct no longer depends on CanStage": NightPlan is in the mod project (not testable); covered by
  the pick-time tests (a kind whose scene cannot stage gets `LandBare`, i.e. it acts) and live (night A below).
- Add-on: `TamperPronounTests.cs` (54 rows), `I18nGuardTests.Both_tamper_middle_lines_exist_and_are_referenced`.

```
dotnet build TheLongestYear.sln               -> Build succeeded, 0 Error(s)
dotnet test tests/TheLongestYear.Tests --no-build
Passed!  - Failed: 0, Passed: 4184, Skipped: 0, Total: 4184
```

## Live evidence (all launches mine, minimized, no mouse or keyboard)

`tools/deploy.ps1 -Minimized`, `tools/bridge.ps1`, `tools/send-smapi-command.ps1`. Throwaway farm
`standard_451140110` from `tly_newgame standard skipintro`, deleted after. `tly_select Fishing`,
`tly_setday 5`, `debug season summer`, `debug sleep`, `tly_sabotage fixture scarecrow rows=3`.
Build was c353637 + 44df2eb (before the add-on; the add-on was not run live).

Night A (Summer 6), forced staging failure at the pick: `breakscene crows`, `arm blight crops`, sleep.
```
Darkness: CropBlight's scene cannot stage tonight.
Darkness: tonight's CropBlight lands now without its scene (its scene cannot stage or cannot show tonight's pick); the scene stays due for a later strike, at tick 4118.
Darkness: tonight's CropBlight is committed and recorded.
Darkness: 2 crop(s) struck down, 0 stored unit(s) spoiled, 0 gone missing on Summer 6.
Darkness: morning HUD line: 2 crops were struck down by the darkness overnight.
status Summer 7: chance tonight 20 % (was 25); live crops 28; Scenes played: none; Queued: none; Struck: CropBlight
```
Night B (Summer 7), collision: `breakscene off`, `arm blight crops`, `debug mft ccVault`, sleep.
```
Darkness: tonight's CropBlight is postponed (WorldChangeEvent has the overnight slot): no effect and no scene tonight; the night counts as no strike, and it is queued for the next free night.
status Summer 8: live crops 28 (unchanged); Scenes played: none; Queued for the next free night: CropBlight
```
Night C (Summer 8), nothing armed:
```
Darkness: the postponed CropBlight fires tonight (Summer 8) instead of the roll.
Darkness: the crows are staged on 3 bird(s) at (60,22), scarecrow in frame, Linus walking in.
Darkness: tonight's CropBlight is committed and recorded.
Darkness: the postponed CropBlight has struck, so it leaves the queue.
Darkness: the CrowsScene scene takes tonight's overnight slot for CropBlight (not skippable).
Darkness: 2 crop(s) struck down ... on Summer 8.
Darkness: morning HUD line: 2 crops were struck down by the darkness overnight.
status Summer 9: live crops 26; Scenes played: CropBlight; Queued: none
```
Night D (Summer 9), setUp failure: `breakscene thief setup`, `arm blight chest`, sleep.
```
Darkness: the strike scene takes tonight's empty overnight slot.
ERROR Darkness: the ThiefScene scene failed and was ended. The strike still lands, without its scene. System.InvalidOperationException: debug: the ThiefScene scene is broken on purpose (tly_sabotage breakscene).
Darkness: tonight's ChestBlight is committed and recorded.
Darkness: 0 crop(s) struck down, 2 stored unit(s) spoiled, 1 gone missing on Summer 9.
Darkness: morning HUD line: Some of your things have spoiled in the night. (2)
Darkness: morning HUD line: Some of your things have gone missing overnight. (1)
status Summer 10: units in chests 45 -> 42; Scenes played: CropBlight (thief still due); Struck: CropBlight, ChestBlight
```
The only ERROR line in the log is that deliberate one. Log copy kept in my scratchpad (not
committed). Cleanup: game closed; throwaway save deleted, Saves listing identical to before;
`Default_options` and mod `config.json` unchanged (SHA-1); `startup_preferences` restored byte for
byte; no tracked log pruned (`git status` clean apart from the controller's progress.md).

Not run live: replaced-before-setUp (no headless way to make another mod replace the event; unit
tested), a bare guaranteed Winter tamper (unit tested), the "touched it" line.

## Handoff farms

`standard_451080087`, `standard_451080418`, `standard_451080615`: not loaded or touched (SHA-1 of
every file before and after the live run matches). Effect on them: a collision night still queues as
before; a scene that cannot stage would now land its strike bare instead of being held back (not
expected: their maps are vanilla, and earlier runs staged every scene on them). The Winter 1 tamper
on `standard_451080615` lands the same; its porch scene's middle line now reads "touched it" if its
old item is uncountable (see the list below). All three remain valid.

## Add-on: "touched it" vs "touched them"

Rule `ItemPlurals.TaintedReadsAsMass` (`src/TheLongestYear.Core/Sabotage/ItemPlurals.cs:108`): "it" when
the tainted name stays singular and is not a fish/shellfish/jelly, not a counted good that keeps one
word, and not an already plural name. Wired at `SabotageService.Morning.cs:60` ->
`SeasonTurnDriver.StartTamperAtPorch` -> `SeasonTurnEventInjector.cs:79`; key at `default.json:909`.
Debug replay: `tly_sabotage scene [old] [new] mass`.

"it": Wood, Hardwood, Clay, Hay, Wool, Cloth, Fiber, Sap, Moss, Slime, Bug Meat, Seaweed, Green and
White Algae, the ores, Refined Quartz, Driftwood, Bok Choy; every container good: Holly, Honey (and
Wild Honey etc.), Wine, Beer, Pale Ale, Mead, Juice, Coffee, Green Tea, Milk, Goat Milk, Oil, Truffle
Oil, Vinegar, Maple Syrup, Oak Resin, Pine Tar, Mayonnaises, Jelly (flavoured too), Roe, Aged Roe,
Caviar, Squid Ink, Sugar, Wheat Flour, Rice, Bread, the soups and plated dishes, Joja Cola, flavoured
pickles ("Pickled Beet"); Dish O' The Sea.

"them": everything the rule pluralises ("Parsnips", "Potatoes", "Salads"), the game's phrase plurals
("lumps of Coal", "bushels of Wheat", "bulbs of Garlic", "pieces of Salt"), every fish ("Pike",
"Bream", "Largemouth Bass", Shrimp, Crayfish), shellfish ("Oysters"), the jellies ("Sea Jellies"),
Smoked Fish and its flavours, Baked Fish, Dried Fruit ("Dried Apples"), Dried Mushrooms, Raisins,
Cookies, Pickles (unflavoured), Hops, Tea Leaves and the game's other already plural names.

## Open items

- Coal and Wheat: the coordinator listed Coal as "it", but the existing tainted-name rule writes
  "all the lumps of Coal" (the game's own plural; Coal is not on the bulk list), which reads counted,
  so it gets "them". If Jeff wants "all the Coal ... touched it", Coal (and Wheat) go on the bulk list;
  that also changes the ask from "3 lumps of Coal" to "3 Coal". Asked, not changed.
- `StrikeSceneBase.cs` is 401 lines (was 380); right at the split guideline.
- A queued strike whose scene broke since it was queued now fires bare on its night (it is not held).
- `tly_sabotage breakscene` is in memory only; it survives a sleep, not a relaunch.
