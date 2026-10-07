# Task 10 report: the cloud

Branch `story`. Manifest version untouched (`0.18.15`). Code commit `81ee071` ("scenes: the cloud over
the valley"), pushed to `origin/story`. This report is committed after it.

Status: DONE_WITH_CONCERNS (see "Open" at the end).

## What I built

### The cloud scene (`CloudScene`, replacing the Task 6 stub)

The tamper strike's overnight scene, about 12 seconds, no text, no witness. The world map in its
Winter art fills the screen. A dark cloud of 40 soft blobs drifts in slowly from the mountain and
mines side (a spawn line along the map's top right) and settles everywhere, a little thicker on the
farm, which ends up the darkest place on the map. The map dims under it. When the farm settles, the
low `shadowDie` sounds and the board is rewritten. Then a hold and a fade.

- **Map art.** It uses the map tab's own data, read the same way the map tab reads it:
  `WorldMapManager.GetPositionData(farm)` gives the region, then the base texture and every area
  overlay whose condition holds (the custom-farm overlay included). The Winter art is always used:
  the game loads `name_winter` when that asset exists (`MapRegion.GetTexture`), so the scene asks
  for `LooseSprites/map_winter` whatever today's season is. The real menu is never opened. On the
  standard farm the scene logs `base LooseSprites/map_winter plus 0 overlay(s)`. That is correct:
  the standard farm has no overlay in `Data/WorldMap`.
- **Fit (designer change, see below).** The map scales to fill the screen (`SceneCloud.Fit`). It
  uses the largest scale at which the whole map fits, keeps the aspect ratio, and centres it on
  whole pixels, with black round it. When a whole number of screen pixels per art pixel still fills
  at least 90% of the fractional fit, it uses that whole number. At 1280x720 that is 4 (1200x720 at
  (40,0)). At 3840x2130 it is 11 (3300x1980 at (270,75)). The scene paints in the world layer
  (`Paint`), so the base's fade lands over it. It fits to `Game1.viewport`, which is the screen size
  divided by the zoom.
- **The farm rect.** This is the Farm area's `PixelArea` from `Data/WorldMap`, which is (68,62
  50x34) in art pixels and (272,248 200x136) in map pixels. It scales with the map.
- **The cloud.** `SceneCloud.Plan` (Core, pure) with a fixed seed, so every player sees the same
  cloud:
  - Thirty scattered blobs (75%) sit one to a cell on a 6x5 jittered grid over the whole map rect.
    The jitter is a quarter of a cell, so the cloud covers forest, mountains, water, the desert edge
    and the margins evenly and never clusters on the town.
  - Ten farm blobs (25%) sit on a 5x2 jittered grid over the farm rect, inset by 20%.
  - Blob sizes are 22 to 32% of the map width for scattered blobs, and 0.7 to 1.0 of the farm's
    short side for farm blobs.
  - Each blob travels a gently bowed path with a cubic ease-out, and drifts a few pixels once it
    has settled.
  - Alpha ramps from 0 to 0.7 over the first 60% of the blob's travel.
  - The tint is `new Color(20, 0, 30)`.
  - The full-map dim is `Black * 0.35`, smoothstepped in from 1500 to 9000 ms.
  - The blob is a 64px radial texture built in `Stage` and disposed in `Cleanup`. Its alpha profile
    is (1 - r^2)^2 with a fixed 10% grain.
- **Reuse.** The base's fade and world-pump handling are used as they are. `SceneLocation` is null,
  so nothing is pumped. One small addition to `StrikeSceneBase`: `protected static Rectangle
  WholeScreen()`, which the base's own fade now uses as well, so the backdrop and the fade cover the
  same rectangle and there is no duplicated block.
- **Calling the scene off.** If the world map has no farm position or the region has no base
  texture, the scene calls itself off, and the tamper lands with no scene.

### Debug

`tly_sabotage scene cloud` plays the scene now. It makes a fresh fair tamper plan with
`SabotageService.PrepareTamper`, which is parked and not written, so the board really changes at
the scene's beat. If there is no plan, it plays against a no-op effect. `Tamper` and `PrepareTamper`
share one private `PlanFairTamper`. The usage text lists `scene cloud`.

## Timeline as implemented

| At (ms) | What |
| --- | --- |
| 0 | the map fades in over 800 |
| 1500 | the first blobs start to drift in; scattered starts run 1500 to 5500, farm starts 4500 to 6000; each blob travels 3000 to 4500 ms, eased out |
| 9000 | every farm blob is at rest, the dim is full, the low `shadowDie` plays (pitch -900, the same as the hall) and `ApplyStrike()` runs |
| 11000 | fade out over 1200 |
| 12200 | end |

This replaces the brief's 9.7-second table (pour at 1500 over 3000, settled at 5500, strike at 6500,
fade at 8500), as the controller ruled after Jeff's first look.

## Designer changes applied this round (all in `81ee071`)

1. **Balance.** The split moved from 60/40 to 75/25 (scattered/farm), and the non-farm coverage
   went up. On the settled frame at 1280x720, the whole map averages 52 luminance against 155
   clear, and the farm averages 34 against 210. By cell, the three most darkened cells are all on
   the farm (8 to 15% of their clear luminance), and the darkest cell off the farm is at 17%. The
   farm is still the darkest place, by a smaller margin than the first cut.
2. **Pace.** The blobs now drift in slowly (timeline above).
3. **Everywhere, not on settlements.** The scattered rests are a jittered grid over the full map
   rect. In the first cut they were uniform random, which left patches bare. I checked the settled
   frames for bare areas and found none.
4. **Fill the screen.** The map-tab fit is dropped (see Fit above). At native resolution the first
   cut was a small 1200x720 card in the middle of a 4K black screen.
5. **Morning text.** `event.darkness.tamper-1` in `default.json` is now, verbatim, "@, last night
   the darkness struck! It has tainted all the {{old}}." Other language files are untouched.
6. **The Junimo "tainted" scene moved off waking.**
   - **Old trigger.** `RunController.DoDayStartSeasonAndHub` called `SabotageService.ShowMorning`,
     which started `SeasonTurnDriver.StartTamperWhenSettled`. That built an event which, under
     black, ran `tlyChangeLocation Farm` and then `warp farmer` to the doorstep: the blink and warp
     Jeff saw.
   - **New trigger.** `ShowMorning` no longer starts anything when a tamper report is waiting. It
     logs "the Junimos wait for the farmer to step out onto the farm" and the morning goes on (the
     hub and the rest). A new `Player.Warped` handler (`ModEntry.OnWarpedForTamperScene`) asks the
     pure rule `TamperPorchRule.ShouldStart(tamperPending, enteredLocationName, isLocalPlayer,
     busy)`. When the local player enters `Farm` with a tamper report waiting and no event, farm
     event, menu or season scene up, it calls `SabotageService.TryStartTamperScene`, which starts
     `SeasonTurnDriver.StartTamperHere` at the farmer's own tile and facing.
   - **The new event.** `SeasonTurnEventInjector.BuildTamper(farmerX, farmerY, facing, ...)` holds
     black (`tlyBlack`), pins the farmer to his own tile (`warp farmer` to the tile he is on), sets
     the viewport, places the two Junimos on the porch marks relative to him, then fades in. There
     is no location change.
   - **Why the pin.** On the first live run without it, the event's farmer line left him a tile up
     in the doorway, and `exitEvent` then put him back on the step: a one-tile jump as the black
     lifted. With the pin he stays on (64,16) throughout. The log confirms it, and so do the frames.
   - **Once only.** The report is consumed only once the scene has really started, so the scene
     plays once. A day the farmer never leaves the house leaves the report saved, and the scene
     plays on his next Farm entry.
   - **What follows.** After the scene, `ShowMorningReports()` runs exactly as before (the popup).
   - **Which strikes are affected.** Only Tampering. Blight (crops and chests) and Reversion
     mornings go through the same `ShowMorning`, and when no tamper is waiting it still shows their
     HUD lines on waking, unchanged. When a tamper IS waiting, the night's other reports wait and
     show after the Junimo scene. That matches the old behaviour, where they also showed after the
     wake-up scene.
   - The old pending/timeout machinery (`StartTamperWhenSettled`, `_pendingStart`) is deleted.
   - The debug `tly_sabotage scene [old] [new]` now plays the scene where the farmer stands on the
     Farm, and warns if he is not on the Farm.
7. **Plurals.**
   - `ItemPlurals` (Core) wraps the game's own `Lexicon.makePlural`, which is passed in as a lambda.
   - What makePlural does, read off the PC decompile: it has a fixed list it leaves alone (Tea
     Leaves, Clay, Hops, Driftwood and others), a few phrases ("lumps of Coal", "bushels of
     Wheat"), and otherwise the -ies, -es and -s rules. In any language but English it returns the
     word unchanged.
   - Its gaps, now corrected: mass nouns go through the s rule (Beer becomes "Beers", Wool "Wools",
     Hay "Haies", Honey "Honies", Milk "Milks", Mayonnaise "Mayonnaises"). There is an override
     list of mass nouns, plus a rule that a vowel before a final y takes a plain s.
   - `{{old}}` is the plural name. The ask is the bare name for a count of 1 and "3 Parsnips" for
     more.
   - A plural ask uses the new key `event.darkness.tamper-3-plural`, "Bring us {{new}} instead. They
     remain pure."
   - Salad stays with the game ("Salads"), since it is countable.
8. **The dark fade under the middle line.** This was the event command `glow 60 0 90 true`, a
   purple full-screen glow added just before tamper-2 and lifted by `stopGlowing` before tamper-3.
   Both commands are removed. The `playSound shadowDie` under the middle line stays. The three
   season-turn scenes keep their own glow, which this change does not touch.

## Files

- `src/TheLongestYear/Scenes/CloudScene.cs`: the scene (was the stub).
- `src/TheLongestYear.Core/Sabotage/SceneCloud.cs` (new): plan, timing, alpha, dim, fit and map-to-paint.
- `src/TheLongestYear/Scenes/StrikeSceneBase.cs`: adds `WholeScreen()`.
- `src/TheLongestYear/Loop/SabotageService.cs`: adds `PrepareTamper` and `PlanFairTamper`; changes `ShowMorning`; adds `TamperSceneOwed`, `TryStartTamperScene` and plurals; the `StartTamperScene` signature changed.
- `src/TheLongestYear/ModEntry.cs`: wires `scene cloud` and the Warped handler, the delegate and the debug replay.
- `src/TheLongestYear/Integration/SeasonTurnDriver.cs`: `StartTamperHere` replaces `StartTamperWhenSettled`.
- `src/TheLongestYear/Integration/SeasonTurnEventInjector.cs`: `BuildTamper` now plays where the farmer stands, with the plural key and no glow.
- `src/TheLongestYear/i18n/default.json`: tamper-1 replaced; tamper-3-plural added.
- `src/TheLongestYear.Core/Sabotage/TamperPorchRule.cs` (new) and `ItemPlurals.cs` (new).
- `tests/TheLongestYear.Tests/SceneCloudTests.cs` (new, 29), `TamperPorchRuleTests.cs` (new, 10) and `ItemPluralsTests.cs` (new, 31).
- `docs/superpowers/specs/2026-09-21-darkness-agents-and-gate-scenes-design.md` and `docs/superpowers/plans/2026-09-21-darkness-agents-and-gate-scenes.md`: updated for every change above.

## Tests

`dotnet build TheLongestYear.sln` (run with the game closed): Build succeeded, 0 errors.
`dotnet test tests/TheLongestYear.Tests --no-build`: Passed 3834, Failed 0. That is 3764 before
this task plus 70 new tests.

The new tests cover:

- **Split:** the 75/25 split.
- **Rests:** every farm rest lies inside the farm, and every cell of the 6x5 grid holds exactly one
  scattered rest.
- **Density:** blob area per pixel is higher on the farm than on the map.
- **Spawn:** blobs spawn top right.
- **Determinism:** the same seed gives the same cloud.
- **Timing:** starts run from 1500 to 6000 (scattered up to 5500, farm 4500 to 6000), every farm
  blob arrives at 9000 and nothing moves after it, and every travel takes 3000 to 4500 ms.
- **Motion and alpha:** progress is monotonic, the settled drift is bounded, alpha runs from 0 to
  0.7 and holds, and the dim is 0 at 1500 and 0.35 at 9000.
- **Fit:** art-to-map times four; the fit at 1280x720 (4, at (40,0)) and at 3840x2130 (11, at
  (270,75), farm at (1018,757 550x374)); the fractional fallback; and the whole map fits on six
  common screens.
- **The trigger rule.**
- **Plurals:** against a copy of vanilla makePlural, covering countables, names the game leaves
  alone, mass nouns, the vowel-y rule, other languages, and the ask and plural-key choice.

## Live check

**Resolutions.**

- **Native resolution:** 3840x2160 borderless at zoom 1 and UI scale 1, from the game's own
  `startup_preferences` (`preferredResolutionX/Y` 3840/2160, `windowedBorderlessFullscreen` true)
  and Jeff's recent saves (zoom 1, UI scale 1).
- **How it ran:** as a window through the mod's `WindowWidth`/`WindowHeight` dial. The client area
  came out at 3840x2130, because the window's title bar takes the rest. The 1280x720 runs used the
  same dial.
- **Afterwards:** the dial was restored to its previous 1920x1080 from a backup copy.

**Commands (all my launches, minimized, no mouse or keyboard).**

- `tools/deploy.ps1 -Minimized` (and `-NoBuild` for relaunches), and `tools/bridge.ps1` (`tly_newgame
  standard skipintro`, `tly_select Farming`, `tly_loadsave`, `tly_totitle`, `tly_sabotage scene
  cloud`, `tly_sabotage arm tamper`, `tly_eventstep`).
- `tools/send-smapi-command.ps1` (`debug season winter`, `debug sleep`, `debug warp Farm 64 16`, and
  `patch export Data/WorldMap` to read the map data).
- A PrintWindow burst script in my scratchpad that restores the window without activating it
  (`SW_SHOWNOACTIVATE`) and re-minimises it (`SW_SHOWMINNOACTIVE`).
- `debug warp Farm 64 16` stands in for walking out of the door: it is the same warp onto the same
  tile, and it raises the same `Warped` event.

**Saves and cleanup.**

- Two throwaway farms: `standard_451060190` (the 1280x720 runs and the first native runs) and
  `standard_451061955` (the final native run, from a fresh farm through Winter 2, `arm tamper` and
  a real night). Both are deleted. The saves folder matches its listing from before I started.
- The game is closed.
- `git checkout -- test-output/` restored the log archives that deploy pruned.
- No ERROR line from the mod in any run.

**What was verified live on the final build.**

- The overnight path: the guaranteed Winter tamper struck, and "the CloudScene scene takes tonight's
  overnight slot for Tampering (not skippable)". The board rewrote at the settle beat ("Summer
  Crops slot 2 now asks for 3 Winter Root instead of Summer Squash").
- Waking showed no Junimo scene, and the log said the Junimos wait.
- Stepping onto the Farm started the scene at (64,16), facing 2: "Summer Squashes -> 3 Winter
  Roots". All three lines read correctly.
- After the scene the farmer is on the same tile.
- On the 1280x720 run, where a blight report was also waiting, the HUD popup "Some of your things
  have gone missing overnight. (1)" followed the scene as before. On the native run only the tamper
  report was waiting, so no HUD line followed. That is the unchanged `ShowMorningReports`
  behaviour, which shows nothing for a lone tamper.

### Frames

All frames are in `test-output/scenes/cloud/`, untracked, and I looked at every one. The capture
clock is offset from the scene clock: about +450 ms at 1280x720 and about +2300 ms on the native
overnight run (fixed off the fade edges). The frames below are the nearest to each beat.

| File | What it shows |
| --- | --- |
| `cloud-720-01-map-in-1000.png` | The Winter map, full height at 1280x720, black bars left and right, no cloud yet. |
| `cloud-720-02-drifting-4000.png` | The first soft violet blobs drifting over the mountains and the west forest; most of the map is still clear. |
| `cloud-720-03-farm-arriving-7000.png` | A veil over most of the valley, with the farm's dark mass forming over the farmhouse area. |
| `cloud-720-04-settled-10000.png` | Settled. An even veil over the whole map, margins and water included, and a dark violet knot over the farm, the darkest place on the map. |
| `cloud-720-05-fade-11600.png` | The same scene partway into the fade to black. |
| `cloud-4k-01-map-in-1000.png` | Native 3840x2130: the map fills the height at 11x, with black bars at the sides. |
| `cloud-4k-02-drifting-4000.png` | Native: blobs drifting in over the north and west. |
| `cloud-4k-03-farm-arriving-7000.png` | Native: the veil is nearly complete and the farm darkening. |
| `cloud-4k-04-settled-10000.png` | Native, settled: the same picture as 720p, scaled; the farm is clearly darkest. |
| `cloud-4k-05-fade-11600.png` | Native, fading. |
| `cloud-4k-06-waking-no-scene.png` | The next morning: the farmer in the farmhouse with the HUD up, and no Junimo scene on waking. |
| `porch-720-01-farmhouse-before.png` | Inside the farmhouse, just before stepping out. |
| `porch-720-02-black-on-entry.png` | Black on Farm entry while the Junimos are placed. |
| `porch-720-03-fade-in-junimos-placed.png` | Fading in on the farmer at his door, with both Junimos already on their marks (no snap). |
| `porch-720-04-scene-at-door.png` | The scene at full view, the farmer on the step he walked out to. |
| `porch-720-05-popup-after.png` | After the scene: the HUD popup "Some of your things have gone missing overnight. (1)". |
| `porch-720-06-same-tile-after.png` | Control back, the farmer on the same tile as during the scene. |
| `porch-4k-01-line1-summer-squashes.png` | "Rodger, last night the darkness struck! It has tainted all the Summer Squashes." |
| `porch-4k-02-line2-no-glow.png` | "The darkness has touched them. We can no longer use them for our restoration." on the normal view: no purple dimming. |
| `porch-4k-03-line3-they-remain-pure.png` | "Bring us 3 Winter Roots instead. They remain pure." |
| `porch-4k-04-after.png` | After the scene, normal view and HUD. |
| `newline-4k-tea-leaves-preplural.png` | The new tamper-1 line with the name filled in ("all the Shrimp"), from before the plural change. |
| `sheet-720.png`, `sheet-4k.png`, `porch2-sheet.png`, `lines4k-sheet.png` | Contact sheets of the above. |

The `raw*`, `final*`, `porch1`, `porch2`, `morning*` and `lines4k` subfolders hold the full bursts.
`porch1-*` is the run that showed the one-tile doorway jump fixed by the pin.

## Open

- **Earlier frames in this folder.** The early native frames (`preview-n-*`, `raw4k-overnight`)
  are from the superseded map-tab fit, which shows a small card. They are kept only as the record
  of why the fit changed.
- **Wake-up failures before the move.** Two runs of the OLD wake-up trigger ended with "the
  board-changed scene could not start; continuing the morning" (the 20 s settle timeout). I did not
  chase it, because the trigger was then replaced. Note that the old trigger was flaky on this
  build.
- **Not run live:** a day where the farmer never leaves the house, so the scene plays on the next
  day's entry. The rule is unit-tested, and the report persists in the save, but this was not
  watched.
- **Not run live:** a custom farm type with its own world-map overlay. The overlay path mirrors
  `MapArea.GetTextures` but was not seen on screen.
- **Not run live:** zoom other than 1. `Fit` uses `Game1.viewport` (the world layer) and is
  unit-tested for scale, but every live run used zoom 1.
- **Two tamper reports at once.** When two tamper reports wait together (only possible through
  debug commands), one scene plays, for the first, and both reports are consumed.
- **Edge case in the plural ask.** For a mass noun with a count above 1, it reads "Bring us 3 Beer
  instead. They remain pure."
