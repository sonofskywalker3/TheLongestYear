# Jeff's corrections (2026-10-07): implementer report

Branch `story`, base 7b24951, commits `bf0c33e..dec5733` (plus this report's commit), all pushed to
`origin/story`. Manifest Version untouched (0.18.15). No em dashes written; designer dialogue
unchanged.

| Commit | What |
|---|---|
| bf0c33e | 1: Junimo Chests are thief targets, shared stock drawn once |
| 20101fe | 4: per-bundle tamper target, every open slot rewritten |
| 84a3473 | 2: a strike whose scene cannot have the night is postponed (also the spec text for 1, 3, 4) |
| f089c92 | 3: tamper scene on any Farm arrival, staged at the porch |
| f187ca5 | 3 fix from the live run (no row-64 nudge), "all the Wood" plural, debug `edge` and `fixture here junimo` |
| dec5733 | CHANGELOG, STATUS, TODO |

## 1. Junimo Chests are thief targets

- `src/TheLongestYear.Core/Sabotage/SabotageRules.cs:64` `BlightRule.ChestInDraw(shipsOvernight)`:
  only Mini-Shipping Bins stay out (79ee1d9's `sharedInventory` exclusion reverted).
- `SabotageRules.cs:33` `ChestSeat`, `:74` `BlightRule.ChestHosts`: chests that show the same
  inventory are ONE chest in the draw. The group is hosted by its first chest on a scene map (Farm,
  FarmHouse, Cellar, Shed), else its first chest, so the thief scene stages at a Junimo Chest on the
  farm when there is one; the group is warded if any of its chests is on a Circle of Warding (my
  call: the player sees those items in a warded chest).
- `src/TheLongestYear/Loop/SpoilagePass.cs:77` `Entries`: two passes, same walk order as before for
  ordinary chests (so existing seeds roll the same). It reads `chest.GetItemsForPlayer()`: a Junimo
  Chest's own `Items` is empty, its stock is the team's global inventory (Chest.cs:990). `Hit` carries
  the inventory and `Apply` (`:232`) takes the unit from it, so it is gone from every Junimo Chest.
  `:74` `OnSceneMap` is now the one scene-map test (ThiefScene uses it).
- `SabotageService.Tamper.cs` `HeldItemIds` also reads `GetItemsForPlayer` (Junimo stock counts as held).

## 2. Never apply a strike's effect without its scene

- `src/TheLongestYear.Core/Sabotage/StrikeSlot.cs:53` `StrikeSlot.Decide` (LeaveAlone on a fail
  night; Postpone for a Wildcard night_event, a scripted event such as a WorldChangeEvent, or an
  empty slot that a wedding, farmEventOverride or personal farm event reads again; else PlayScene).
  `:70` `LandsAtNet(sceneHadTheSlot)`. `:85` `StrikeLedger.Record` (chance drop + cap/spacing +
  StruckEvents in one place).
- `src/TheLongestYear/Loop/PendingStrike.cs:49,76` `Committed` / `Commit()`; `Apply()` commits first.
- `src/TheLongestYear/Loop/SabotageService.Night.cs:135` `Strike()` no longer records at the pick;
  `:88` `OnStrikeCommitted` records (and marks the guaranteed Winter tamper done). `:45`
  `PostponePendingIfAny` drops an uncommitted strike unapplied; `:63` `SettlePendingIfAny` is the net
  (save, morning, next night pass, replaced): lands only if committed, else postpones; `:76`
  `CommitPendingScene`. The private `ApplyPendingIfAny` remains only for strikes with no scene by
  design (kind already played this loop, or a target the scene cannot show: unchanged exception).
- `src/TheLongestYear/Loop/StrikeScenePatch.cs:139` Postfix reads the game and asks `StrikeSlot.Decide`;
  Postpone calls `Postpone`, a scene taking the slot calls `SceneTookSlot` (commit). It returns early
  when no strike waits (so the personal-event probe no longer runs on quiet nights).
- ModEntry: `:1095` the Saving net and `SabotageService.Morning` use `SettlePendingIfAny`, which covers
  the first night of a save (DaysPlayed 1: no pickFarmEvent, so never committed, so postponed).
- Ruling on "guarantee owed": a postponed guaranteed Winter tamper would lose its week-1 window if the
  collision fell on Winter 7, so `RunState.GuaranteedTamperPostponed` (RunState.cs:327, cleared on a
  new run :603) keeps `NightRoll.IsGuaranteedTamperNight` true past week 1 (NightRoll.cs:79) and the
  ordinary roll's reservation with it (Night.cs:365) until it lands.
- Kept as before: a scene that took the slot but fails to stage (no ground beside the thief's
  target) still lands without a scene, the same "cannot film this target" category as
  `SceneCanPlay` false. The debug arm is spent on a postponed night (re-arm, or the guarantee forces it).
- Docs: spec "Picking at dusk" and "The overnight slot"; HEADLESS_DRIVING overnight-slot paragraph and
  the collision recipe (now expects `is postponed`); CHANGELOG line on wildcard nights.

## 3. Any arrival on the Farm map shows the tamper scene

Mechanism: vanilla's own event end, the one the Community Center cutscene on entering Town uses.
`Game1.eventFinished` warps the farmer back to `Game1.player.positionBeforeEvent` with
`orientationBeforeEvent` (decompile Game1.cs:6849); vanilla's `end position x y` writes the same
field. A new event command `tlyReturnTo x y facing`
(`src/TheLongestYear/Integration/EndingEventCommands.cs:185`) sets them to the arrival tile and facing
as the script's first command after the black, so a skip returns him the same way.

- `src/TheLongestYear.Core/Sabotage/TamperPorchRule.cs:29` `ShouldStart(pending, entered, local,
  busy, porch)`: any arrival on "Farm"; still blocked while an event, farm event, menu or season turn
  is up (report kept for the next arrival). `:40` `PorchTile`: the farmhouse's door warp target, else
  the farm's main-house entry (both (64,15) on the standard farm).
- `src/TheLongestYear/Integration/SeasonTurnDriver.cs:60` `StartTamperAtPorch` (records the arrival
  tile, starts the event, `HoldBlack()` the same frame), `:85` `FarmPorch()`.
- `SeasonTurnEventInjector.cs:46` `BuildTamper(porch.., return.., facing, ...)`: farmer on the porch
  facing down, Junimo marks round the porch as before, then `tlyReturnTo`.
- ModEntry `:1077` `OnWarpedForTamperScene` uses the new rule.
- Live finding: the decompile's row-64 nudge in `eventFinished` (X+1 when the saved tile is on row 64
  of the Farm) did NOT happen in the running game (handed (39,64), landed (39,64)), so the first
  version's pre-compensation was removed (f187ca5): the arrival tile is handed as is.

## 4. "Only one bundle" means per bundle

- `SabotageRules.cs:262` `TamperTarget.IngredientIndices` (defaults to the one slot);
  `:295` `TamperRule.Targets`: one target per exact item per bundle; `:324` every slot on the board
  that overlaps it (the any-flavour overlap kept) must be in this bundle with the exact same flavour,
  else no target; the target carries every OPEN slot of it there. `:369` `BundlesAsking` for the
  debug readout.
- `src/TheLongestYear.Core/Sabotage/BundleDataTamper.cs:57` `ApplyAll`: all slots rewritten to the same
  item, stack, quality, all or nothing.
- `SabotageService.Tamper.cs:192` `WriteTamper`: one board write, flavour cleared per slot, goal cards
  dropped for every rewritten slot, ONE morning report (one Junimo scene, `Count` = the per-slot
  stack), and one `TamperRecord` per rewritten slot. Justification: the record's shape and the save
  stay as they were, every reader keyed on (bundle, slot) (status, aura, tainted bar) works unchanged,
  and the aura/tainted bar read the same exact item from each record.
- The new-to-board and tainted bars are unchanged (PickReplacement); tested for a doubled target.
- Follow-on: Wood can now be a target, and the first live line read "all the Woods".
  `ItemPlurals.Tainted` (`ItemPlurals.cs:67`) now follows the ask's word lists (container goods and
  bulk stuff stay bare, the jellyfish are counted): "all the Wood", "all the Copper Ore".

## Tests

```
dotnet build TheLongestYear.sln            -> 0 Error(s) (33 warnings, all pre-existing kinds)
dotnet test tests/TheLongestYear.Tests --no-build
Passed!  - Failed: 0, Passed: 4032, Skipped: 0, Total: 4032
```

New or changed: `DarknessLevelsTests` (chest draw, Junimo seats, farm host, ward, single weighting
over 2000 seeded nights), `StrikeSlotTests` (new: Decide table, probe laziness, nets, ledger, a
postponed night leaves the kind owed and the chance whole, postponed guaranteed tamper owed past
week 1), `TamperPorchRuleTests` (rewritten: any arrival, porch tile), `TamperExactItemTests`
(doubled Wood is one target over both slots, filled copy stays, Wood elsewhere rules it out,
flavour overlap inside a bundle, ApplyAll, replacement bars), `ItemPluralsTests` (tainted names).
Tests were written before the code for each rule (compile failures were the red step).

## Live check (all launches mine, minimized, no mouse or keyboard)

`tools/deploy.ps1 -Minimized`, `tools/bridge.ps1`, `tools/send-smapi-command.ps1`. Two throwaway
farms from `tly_newgame standard skipintro`, both deleted after; the Saves folder matches the listing
from before; `config.json` unchanged (1920x1080); `git status` clean after every deploy (no pruned
tracked logs). No ERROR lines. Game closed. Logs: `test-output/log-archive/SMAPI-v0.18.15-20261007-172651.txt`
(farm 1), `-172955.txt` (double Wood night), `-173632.txt` (arrivals), plus the last run (Forest, exact tile).

**Collision postpones, later night lands with its scene** (Summer 6, fixture rows=3, arm crops, `debug mft ccVault`):
```
17:22:25 Darkness: night pass done at tick 14330, leaving CropBlight waiting for its scene.
17:22:25 Darkness: tonight's CropBlight is postponed (WorldChangeEvent has the overnight slot): no effect and no scene tonight; ...
status Summer 7: chance tonight 25 %; blight week -1 nights 0; live crops 30; Scenes played: none; Struck: none
```
(bus event ran 17:22:25 to 17:22:37.) Re-armed, slept:
```
17:23:05 Darkness: tonight's CropBlight is committed: its scene has the overnight slot.
17:23:05 Darkness: the CrowsScene scene takes tonight's overnight slot for CropBlight (not skippable).
17:23:11 Darkness: 2 crop(s) struck down ... on Summer 7.
status Summer 8: blight week 5 nights 1; live crops 28; Scenes played: CropBlight; Struck: CropBlight
```

**Junimo Chest theft.** Two Junimo Chests (FarmHouse (8,10), Farm (60,18)) with one shared stock of
20 Parsnip + 10 Copper Ore: units in chests went 45 -> 75 (+30, counted once). The armed overnight
thief happened to land on the plain chest (scene staged there, so the loop's thief scene was used);
the Junimo theft was then shown with `tly_sabotage scene thief`, the same Plan + ThiefScene path:
```
17:24:30 the thief is staged on a chest at (60,18) on Farm, walking 8 tile(s) in from (55,14), lid driven
17:24:35 the thief opens the lid, frame 257 of 257 to 261, up to frame 261   (the Junimo Chest's lid)
```
Three runs landed there. A third Junimo Chest placed afterwards showed the shared stock at
13 Parsnip, 6 Copper Ore: the units are gone from all of them.

**Double Wood rewrite** (vanilla Default board via `tly_bundlesource Vanilla Default` + `tly_reset`,
Winter 8, 50 Wood held, `arm tamper`):
```
17:29:12 Darkness: the CloudScene scene takes tonight's overnight slot for Tampering (not skippable).
17:29:21 Darkness: Construction slot(s) 0,1 now ask for 8 Holly each instead of Wood (Winter 8).
status: tampered: Construction slot 0: Wood -> 8 Holly; tampered: Construction slot 1: Wood -> 8 Holly;
        pending morning reports: 1; tly_sabotage fair 388 -> "the board does not ask for it"
```

**Tamper scene on arrivals** (`tly_sabotage edge` = the current map's own warp onto the Farm, the same
`Farmer.warpFarmer` vanilla calls on the edge tile; lines stepped with `tly_eventstep`):
```
Bus Stop: edge BusStop (9,22) -> Farm (79,17)
  starting the board-changed scene at the porch (64,15); the farmer arrived at (78,17) facing 2 ...
  (event: farmer tile (64,15), Junimos at (64,17) and (62,17))
  after the board-changed scene the farmer is at (78,17) facing 2 on Farm.
Farmhouse door: edge FarmHouse (3,12) -> Farm (64,15)
  arrived at (64,15) ... after the scene the farmer is at (64,15) facing 2.
Forest: edge Forest (67,-1) -> Farm (41,64)
  arrived at (40,64) ... "It has tainted all the Wood." ... after the scene the farmer is at (40,64) facing 2.
```
(The game moves Bus Stop and Forest arrivals by the farm's `BusStopEntry` / `ForestEntry` map
properties; the scene returns him to where he actually arrived.) Not checked live: a busy arrival
keeping the report (unit-tested), a skip of the tamper scene, the first-night-of-a-save net, a
postponed guaranteed Winter tamper.

## Handoff farms

Not loaded or touched. Read-only check of the three save files: no Junimo Chests, no Mini-Shipping
Bins, and no bundle on any of their boards asks an item twice, so the target lists, the thief draw
and the Winter 1 tamper pick are unchanged. All three remain valid. `standard_451080615`'s note in
TODO now says the Junimos meet you at the porch on any arrival, the door included. If any of their
nights collides with a wedding, birth, repair or Wildcard night event, the strike is now postponed
rather than landed bare (none is expected on the nights listed).

## Concerns

- Jeff should hear the new rule's cost: a strike postponed on a collision night only returns via the
  roll or the guarantee, so a kind past its guarantee can simply not come that week.
- Junimo Chest group warded if ANY of them is on a circle: my call, one line to flip.
- A thief scene that took the slot and then cannot stage still lands without a scene (unchanged).
- The Junimo lines say "them" ("The darkness has touched them") after "all the Wood" and asks like
  "8 Hollies" / "7 Pikes" read oddly; dialogue and the ask table were not mine to change.

## Fix round 1

Commits `79c4eb0` (M2 split, no behaviour change) and `a04020b` (C1, I1, M1, M5), pushed to
`origin/story`. Not touched, per the coordinator: ChestBlight off the scene maps, Junimo-network warding.

**C1, a strike commits only when its scene stages.** The commit (chance drop, cap or spacing,
StruckEvents) moved out of the `pickFarmEvent` postfix into `StrikeSceneBase.setUp`, after staging
and the timeline build succeed (`src/TheLongestYear/Scenes/StrikeSceneBase.cs:190`). `PendingStrike.Apply`
(`src/TheLongestYear/Loop/PendingStrike.cs:90`) now runs the effect only for a committed strike, so a
scene that could not stage or threw in setUp ends without landing anything; it stays uncommitted and
the save/morning net (`SabotageService.Pending.cs:68` `SettlePendingIfAny`) postpones it (nothing
recorded, guarantee still owed; the displaced random farm event loses that night). The same holds
when another mod replaces the event after our postfix: setUp never runs, the net postpones. The
no-scene-by-design path uses `PendingStrike.LandNow` (`:97`). `StrikeScenePatch.SceneTookSlot` and
`SabotageService.CommitPendingScene` are gone. The pure life is `StrikeLifecycle` and
`StrikeNetAction` in `src/TheLongestYear.Core/Sabotage/StrikeSlot.cs:85,67`; `StrikeSlot.LandsAtNet` and
its identity test were removed.

**I1.** `GuaranteedTamper` (`StrikeSlot.cs:132`): done at commit, carried past week 1 when
postponed, the carry cleared only when the tamper lands; a failed apply makes it owed again with the
carry kept. Wired in `SabotageService.Pending.cs` (OnPostponed, OnCommitted, OnApplied).

**M1.** `StrikeScenes.WildcardTwistHasTheSlot` doc says postponed.

**M2.** `SabotageRules.cs` 490 -> 210 lines, with `TamperRule.cs` (241: TamperCandidate,
TamperTarget, TamperRule) and `ChestDraw.cs` (54: ChestSeat, ChestHost, the chest draw half of the
now-partial `BlightRule`). `SabotageService.Night.cs` 403 -> 293, the pending strike in
`SabotageService.Pending.cs` (131).

**M5.** New tests in `StrikeSlotTests` drive the life through its paths: staged scene commits once
and lands once; a scene that cannot stage never lands and the net postpones it, after which it can
neither commit nor apply; another mod's replacement leaves it to the net, which postpones; a staged
scene that ended before its beat lands at the net and can no longer be postponed; no-scene-by-design
commits and lands at once. Two guaranteed-tamper sequences: postponed, committed, failed (still owed
on Winter 9) and postponed, committed, landed (done, carry cleared).

Tests:
```
dotnet build TheLongestYear.sln            -> 0 Error(s)
dotnet test tests/TheLongestYear.Tests --no-build
Passed!  - Failed: 0, Passed: 4037, Skipped: 0, Total: 4037
```

Live (mine, minimized, throwaway farm deleted after, config and Saves as before, no ERROR lines, game
closed): the bus-repair collision still logs `tonight's CropBlight is postponed (WorldChangeEvent has
the overnight slot)`; the next armed night logs `the crows are staged ...`, then `tonight's
CropBlight is committed and recorded.` (the commit now follows staging), then `2 crop(s) struck
down`, status `Struck this loop: CropBlight`, live crops 30 -> 28. A real staging failure (a chest
with no ground beside it) was not forced live: no headless way to wall a chest in; the unit tests
cover the path.

Handoff farms: still valid (unchanged by this round).
