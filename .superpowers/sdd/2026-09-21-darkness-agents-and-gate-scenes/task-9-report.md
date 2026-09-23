# Task 9 report: the hall

Branch `story`. Manifest version untouched (`0.18.15`). Two commits, both pushed.

## What I implemented

`HallScene` is the real scene now: seven seconds, no text. The Community Center seen from OUTSIDE,
in Town, at night. Its two front windows glow like firelight and black shapes slide across the glass.
Shane comes up the path from the Saloon side, stops dead below the door, gives a jump of shock, backs
away two tiles still facing the hall, then turns and runs out the way he came. The reversion lands on
the jump. The interior is never opened, entered or drawn, and nothing in the frame says which room or
slot was hit.

Three new files, two of them shared, plus one small addition to `SceneActor` and one to `SceneCamera`.

### `SceneWindow` (`src/TheLongestYear.Core/Sabotage/SceneWindow.cs`, pure, tested)

The arithmetic behind a lit window, in Core because it is the only part of one that can be checked
without a graphics device.

```csharp
public static class SceneWindow
{
    public static int ShapeCount { get; }                       // how many silhouettes there are

    public static float Flicker(int elapsedMs, int windowIndex); // 0.55 plus or minus 0.15
    public static int SlideX(int elapsedMs, int shapeIndex, int spanLeft, int spanWidth, int shapeWidth);

    public readonly struct ClippedDraw
    {
        public int DestX, DestY, DestWidth, DestHeight { get; }
        public int SourceX, SourceY, SourceWidth, SourceHeight { get; }
    }

    public static bool Clip(
        int shapeX, int shapeY, int shapeWidth, int shapeHeight,
        int windowX, int windowY, int windowWidth, int windowHeight,
        int sourceWidth, int sourceHeight,
        out ClippedDraw draw);
}
```

**`Clip` is a clipped SOURCE rectangle and not a scissor rectangle, on purpose.** A scene paints
inside a SpriteBatch the game opened (Game1.cs:13698). Scissor testing needs a RasterizerState with
`ScissorTestEnable` and a device write, and neither can be changed without ending that batch and
opening one with different state. Cutting the destination rectangle down to the pane and taking the
matching slice of the texture gives the same picture and touches no device state at all. 16 tests in
`tests/TheLongestYear.Tests/SceneWindowTests.cs`, including a sweep of 500 shape positions asserting
that neither the destination nor the source slice ever leaves its bounds.

### `SceneWindowGlow` (`src/TheLongestYear/Scenes/SceneWindowGlow.cs`), shared, for Task 10

```csharp
internal sealed class SceneWindowGlow
{
    public SceneWindowGlow(Vector2 originTile, IReadOnlyList<Rectangle> tileRelativePanes);
    public int Count { get; }
    public void AddLights();                       // one warm LightSource per pane
    public void RemoveLights();                    // takes back exactly those
    public void Paint(SpriteBatch b, int elapsedMs);
    public string Describe();                      // for the log
}
```

`tileRelativePanes` are in PIXELS relative to the top left corner of `originTile`, so a building's
windows are measured once and stay right wherever the camera is.

Two parts, and they are different things. The PAINTED glow is what reads as a lit room: a flat warm
fill on the glass, flickering, drawn after the lightmap has been composited, so the night never dims
it. The LIGHT SOURCES are what reads as the light getting out: a warm pool per pane in the game's own
lightmap. Neither alone looks like a lit building.

**The light colour is inverted on purpose.** The lightmap is subtracted from the frame
(`Game1.lightingBlend`), so a light is given the complement of the colour it casts. Vanilla's torch
is `new Color(0, 80, 160)` for exactly that reason (Object.cs:2756) and it burns orange. Firelight
(255,140,40) therefore goes in as (0,115,215).

**`LightContext.None`, not `WindowLight`.** `LightSource.Draw` fades a `WindowLight` out by itself
whenever it rains or the hour says the lights should be off, and this one has to burn through
whatever tonight's weather is. The proving run was a rainy night.

**`AddLights` must be called AFTER `SceneCamera.CutTo`**, because `CutTo` runs
`resetForPlayerEntry`, which clears and rebuilds every light on the map.

### `SceneWalk` (`src/TheLongestYear/Scenes/SceneWalk.cs`), shared, moved out of `ThiefScene`

```csharp
internal sealed class SceneWalk
{
    public SceneWalk(IReadOnlyList<(int X, int Y)> tiles);
    public int Steps { get; }
    public (int X, int Y) Start { get; }
    public (int X, int Y) End { get; }
    public Vector2 At(float tilesIn);                   // world pixels, extrapolates past the start
    public int FacingAt(float tilesIn, bool backwards); // a SceneActor facing constant
}
```

`ThiefScene.AlongWalk` and `ThiefScene.StepFacing` are deleted and it uses this instead, which is the
whole of the change to that scene. Behaviour there is unchanged: the thief clamps `tilesIn` to
`[0, Steps]` before calling, so the new extrapolation never runs for him.

`At` is deliberately NOT clamped at the near end. A figure running out of the shot carries on past
the first tile along the walk's OVERALL direction, end to start, which is the way he came. Following
the first LEG instead sent Shane sideways along the bottom of the frame for the rest of the scene,
because the last step of his walk in happened to be horizontal. Caught on the first overnight frames.

### `SceneActor.Lift` (one new field)

```csharp
/// Lifted off the ground in screen pixels, for a jump. The shadow stays where the feet were.
public float Lift;
```

`Draw` subtracts it from the sprite's position and leaves the shadow alone, which is what makes the
lift read as a jump rather than as a figure sliding up the screen. Nothing else in `SceneActor`
changed.

### `SceneCamera.TileSize` is public now

It was `private const int TileSize = 64`. It is `public const` with a one line comment. No other
member changed.

### `HallScene` itself

**Staging.** Location `Town`, from `Game1.getLocationFromName`. The camera sits on
`(52, HallTiles.Bottom - 3)`, which at 1920x1080 puts the viewport at (2400,644) and shows tile rows
10 to 27: the whole facade (rows 11 to 20) with the path, the steps and the flowerbeds below it. A
Town with no map loaded is the only thing that calls the scene off.

**The hall's own tiles are vanilla's**, not a guess: `Town.refurbishCommunityCenter` (Town.cs:377)
walks `new Rectangle(47, 11, 11, 9)` with `x <= Right` and `y <= Bottom`, so the building covers
twelve tiles by ten, (47,11) to (58,20). The front door is (52,20), which the brief also said.

**The windows, measured, and there are TWO of them.** The task asked for four to six. The abandoned
Community Center has exactly two windows on its front, one either side of the door, each a boarded
pane with its shutters open. **Town's own map declares no `WindowLight` property on the building at
all**, so there was nothing in the map data to take the positions from, and the scene logs that fact
every time it stages so a later reader can check whether it ever changes. They were measured off a
frame of this very scene at zoom 1: the flicker makes the painted panes the only thing that changes
between two consecutive frames, so differencing two frames gives their screen rectangles exactly, the
viewport falls out of that, and the real glass was then read off the same frame pixel by pixel.

```csharp
private static readonly Rectangle[] FrontWindows =
{
    new Rectangle(118, 414, 60, 92),
    new Rectangle(590, 414, 60, 92),
};
```

Pixels relative to (47,11). They are deliberately a little inside the glass rather than flush with
the frame, because a pane that overshoots by a pixel reads as a glowing wall.

**Shane.** A `SceneActor` from `Characters\Shane`, 16x32, never the real NPC. The walk comes from
`ScenePath.WalkTo` over `SceneGround.PassableGrid`, targeting the DOOR tile itself: the door is a
building tile and so is never walked on, which makes the search stop on the clear tile in front of
it, (52,21), which is exactly where he should be standing when he looks up. The ways in are the
bottom row of the FRAME, at least four tiles WEST of the door, which is the direction the Saloon
lies in. Town's own map edge is fifty tiles away and would have put him half a minute's walk out of
shot. Trimmed to five tiles, which the fixed 1500 ms walk window makes a brisk walk home.

Nothing about him can stop the scene. An empty list of ways in is still handed to the search, whose
own fallback then stages him five tiles from the door with a short walk, which is the spec's "stage
him in view and skip the walk in". A sheet that will not load logs a warning and the scene plays with
nobody on the path. The reversion lands in every case.

**The timeline**, exactly the brief's:

| At | What |
|---|---|
| 0 | camera on the facade, fade in from black over 700, glow and shapes already running |
| 1500 | he steps into frame at the bottom and walks up the path |
| 3000 | he stops on the tile below the door, facing the hall |
| 3300 | the jump, `dwop` at pitch -400, and `ApplyStrike()` |
| 3800 | he backs away two tiles over 800 ms, still facing the hall |
| 4600 | he turns and runs out, 150 ms a tile, on past the start of his walk and off the shot |
| 5400 | the hold on the windows, `shadowDie` at pitch -900 |
| 6200 | fade over 800 |
| 7000 | end, `Cleanup` takes the lights back and restores the camera |

**The jump is 28 screen pixels, not the brief's 16, and that is a deliberate deviation.** Sixteen is
four pixels of his sheet at the game's 4x draw scale, and on the first overnight frames it was not
readable at all against a figure sixteen sheet pixels wide. Twenty eight is seven sheet pixels, which
reads as a start without reading as a leap. The arc itself is the brief's: nothing, up, nothing,
over 300 ms.

Shane takes `SceneCamera.NightTint`, like Linus and the Brute. The window fill and the light sources
deliberately do not.

### `tly_sabotage scene hall`

Through `ScenePreview`, like the crows and the thief. `SabotageService` gained one method:

```csharp
/// A fair pick at the current level, PARKED rather than opened, so the scene lands it at its
/// own beat exactly as the overnight path would. Null when nothing may fairly be taken tonight.
public PendingStrike PrepareRevert(Random rng);
```

With a candidate the pick is real and that slot really comes undone at the jump. With none the scene
still plays, against an effect that does nothing, and the log says which of the two it was: unlike
the thief, a hall with nothing to lose is still a scene worth watching. The command is in the
`tly_sabotage` switch, in both usage strings, and reachable through the file bridge.

## Tests

```
> dotnet build -c Debug --nologo -v q
    0 Error(s)
> dotnet test --nologo -v q
Passed!  - Failed: 0, Passed: 2752, Skipped: 0, Total: 2752, Duration: 1 s - TheLongestYear.Tests.dll (net6.0)
```

Baseline was 2736. The 16 new ones are `SceneWindowTests`: the flicker's bounds over eight seconds
and six windows, two windows not pulsing in step, a full period coming back round, every shape
staying inside its own journey over twenty seconds, the three shapes not starting on top of each
other, a shape moving right with time, a negative clock treated as the start, the two range guards,
a shape wholly inside a pane, cut at the left, cut at the right, cut top and bottom, missing the pane
three ways, touching the edge only, the 500 position sweep that neither rectangle ever leaves its
bounds, and the six zero-size guards. Output is pristine apart from the project's pre-existing
warnings.

Nothing else in this task is pure: it is `Game1` draw, `LightSource` and Town geometry, which the
test project cannot reach.

## Live checks (automated runs, my launches)

`tools/deploy.ps1 -Minimized`, `tools/bridge.ps1` and `tools/send-smapi-command.ps1` only. No mouse,
no keyboard, no existing save loaded, throwaway farms from `tly_newgame standard skipintro` only.
Screenshots are `PrintWindow` bursts with `ShowWindow(SW_SHOWNOACTIVATE)` before and
`SW_SHOWMINNOACTIVE` after, exactly as the Task 7 and 8 reports describe: the window is restored
WITHOUT taking the foreground, the keyboard or the mouse. No input was ever synthesised.

Six throwaway farms were made and **all six deleted** (`standard_449716805`, `_449717158`,
`_449717541`, `_449717858`, `_449718076`, `_449718274`). The saves folder is back to what it was.
`git checkout -- test-output/log-archive` run and clean. The game is closed.

### The real overnight path, three times

Set up each time with: `tly_select Farming`, `debug season fall`, `tly_setday 12`, `debug sleep` (so
`Run.Season` syncs to Fall, which is when reversion opens), `tly_playseason quarter 1` (19 to 21 real
CC slots donated), `tly_sabotage arm revert`, `debug sleep`.

All three armed, played and reverted:

- `Void Essence came undone from Adventurer's (slot 22/0) on Fall 13`
- `Bug Meat came undone from Spirit's Eve (slot 34/0) on Fall 13`
- `Anchovy came undone from Ocean Fish (slot 8/1) on Fall 13`

The staging line from the last of them:

```
Darkness: the hall is staged on the Community Center front at (47,11) to (58,20) in Town,
2 window(s) in world pixels (3126,1118 60x92) (3598,1118 60x92), 2 light(s), Shane walks 5 tile(s)
in from (51,25) and stops at (52,21), drawn from a sheet of 64x416, 16x32 frames, 4 column(s).
Town's map declares no WindowLight on the hall.
```

No ERROR line and no Harmony `failed to apply` line in any run.

`test-output` is gitignored, so these are **untracked**, under
`C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\test-output\scenes\hall\`:

| File | What it actually shows |
|---|---|
| `hall-1-fade-in-glow-up.png` | About a second in. Town at night in the rain, HUD gone, the whole Community Center front in frame with the path, steps and flowerbeds below it. Both windows are lit warm orange inside their frames. Nobody on the path yet, which is right before his 1500 ms cue. |
| `hall-2-shane-stopped-facing-the-hall.png` | Shane standing on the stone tile below the front door, back to the camera, facing up at the building. Both windows lit. Rain streaks across the frame. |
| `hall-3-the-jump.png` | The same spot at the jump beat. CORRECTED in fix round 1: the lift is not plainly visible in this full frame, and his feet sit about where they stood in hall-2, so this frame does not by itself show the jump. A silhouette is crossing the LEFT window as a dark block on its left half. This is the frame the slot came undone on. The jump is shown by `fix1-3-the-jump.png` and `fix1-4-jump-crop-2x.png`. |
| `hall-4-running-out.png` | He has turned to face the camera and is running away down the path, several tiles below where he stopped, near the bottom of the frame. The windows still burn behind him. |
| `hall-5-hold-on-the-windows.png` | Nobody in the shot. Just the dark facade, the two lit windows, and a silhouette sitting across the left one. This is the beat the scene ends on. |
| `hall-6-next-morning.png` | Sun. 14, 6:00 am. The whole HUD is back (toolbar, clock, date, gold, JP, energy), daylight, the farmer waking in his own bed, and the popup "You awaken with a feeling that something is wrong at the Community Center." |
| `hall-7-glow-inside-the-glass.png` | The money shot, a 4x crop of both windows from the hold frame. The fill stops exactly at the glass: the shutters, the sill, the mullion frame and the vines around them are all untouched night colours. The left pane shows the boarded window's own flame-shaped highlights through the translucent fill. The right pane has a silhouette across its left half, and it too stops dead at the frame. |
| `hall-8-preview-command-fade-in.png` | `tly_sabotage scene hall` played mid-day on a clear (not rainy) save, for comparison: the same framing and the same two lit windows against a dry night. Proves the debug command path. |
| `hall-9-preview-glow-inside-the-glass.png` | The same 4x crop from that dry run. Same result, and easier to read without the rain: the glow is inside the glass on both windows and nowhere else. |

### What was NOT checked live, plainly

- **The 75 percent zoom check did not happen, and I could not make it happen.** I tried twice. I
  backed up and edited `zoomLevel` in both `%APPDATA%\StardewValley\startup_preferences` and
  `%APPDATA%\StardewValley\default_options` to 0.75, relaunched, and measured the painted panes off
  the frames by differencing consecutive frames: they came back 60x92 screen pixels both times, which
  is their world size, so the game was still rendering at zoom 1 and the check was not actually being
  made. There is no vanilla `debug` command for zoom and no bridge command for it, and driving the
  in-game Options menu needs the mouse, which this task may not use. **Both files are restored to
  `<zoomLevel>1</zoomLevel>` and I verified that after the last run.**

  What I can say instead, and it is a structural argument rather than a picture: the panes are placed
  with `Game1.GlobalToLocal(Game1.viewport, worldPixels)`, which is the same transform the map
  layers and every sprite in the frame use, and the zoom is applied afterwards as a uniform scale of
  the finished world render target. The pane and the window art therefore cannot move relative to one
  another at any zoom. `hall-7` is the evidence that they line up at zoom 1. **A reviewer who can
  drive the Options menu should confirm it at 75 percent before this is called done.**

- **Winter.** Reversion opens in Fall and Winter and every live run was Fall 13. Winter changes the
  facade's snow overlay on the Town tilesheet, so the window art could conceivably shift. Untested.

- **A no-candidate `tly_sabotage scene hall`** was exercised (the first measuring run logged
  `no slot may fairly come undone tonight, so the scene plays and nothing is taken` and the scene
  played), but the with-candidate branch of the DEBUG command was not: all three real-pick runs went
  through the overnight path, not the command. The command's real-pick branch shares
  `SabotageService.PrepareRevert` with nothing else, and it is two lines.

## Files changed

- `src/TheLongestYear.Core/Sabotage/SceneWindow.cs` (new)
- `tests/TheLongestYear.Tests/SceneWindowTests.cs` (new)
- `src/TheLongestYear/Scenes/SceneWindowGlow.cs` (new)
- `src/TheLongestYear/Scenes/SceneWalk.cs` (new)
- `src/TheLongestYear/Scenes/HallScene.cs` (the stub replaced)
- `src/TheLongestYear/Scenes/SceneActor.cs` (the `Lift` field)
- `src/TheLongestYear/Scenes/SceneCamera.cs` (`TileSize` made public)
- `src/TheLongestYear/Scenes/ThiefScene.cs` (its walk playback replaced by `SceneWalk`)
- `src/TheLongestYear/Loop/SabotageService.cs` (`PrepareRevert`)
- `src/TheLongestYear/ModEntry.cs` (`tly_sabotage scene hall`, usage strings)

## Commits

- `465a4c9` scenes: shadows in the hall
- `1f62479` scenes: the hall, as the screenshots wanted it

Both pushed to `origin/story`.

## What the screenshots changed

Four things only a picture could have told me. The second commit is exactly these.

1. **The window rectangles were in the wrong place and there were twice too many of them.** The first
   pass put four 64x64 panes where the brief's shape suggested, and the frame showed four orange
   blocks sitting on the plaster above the windows and across the "PELICAN TOWN" sign. The building
   has two windows and they are lower and taller than a tile. Measured and rewritten.
2. **Shane walked in straight off the bottom of the shot.** The search's nearest way into the frame
   was the tile directly below the door, so he came up a vertical line, which is nobody's way home
   from the Saloon. The ways in are now at least four tiles west of the door.
3. **He vanished mid frame on the way out.** He ran back along the walk and then stopped being drawn
   at a computed cut-off while still plainly in the shot. He now runs on past the start of his walk
   indefinitely and there is no cut-off at all.
4. **The jump was invisible.** See the deviation above.

## Self-review findings, fixed before reporting

- `SceneWindowGlow` originally added its lights with the wrong `LightSource` constructor and put them
  in a `HashSet`. The live API is `new LightSource(string id, int textureIndex, Vector2 position,
  float radius, Color colour)` and `Game1.currentLightSources` is a `Dictionary<string, LightSource>`
  (the PC decompile in this workspace is an older 1.6 and disagrees). The mod's own
  `RewindBedroomScene` was the reference.
- `SabotageService.PrepareRevert` was written with a leftover tautological ternary choosing the
  event. Removed.
- `DescribeMapWindowLights` had a garbled loop bound that would have walked off the end of a property
  with a trailing value. Fixed to `i + 1 < lights.Length`.
- Swept the whole new surface for em dashes, semicolons in prose and strings, and ellipses: none.
  `HallScene.cs` is 355 lines, under the project's 400 line split point.

## Concerns

- **The 75 percent zoom check is not done.** Written up above with what I tried and what I can argue
  instead. This is the one acceptance item I could not close.
- **Two windows, not four to six.** The building only has two. It reads well (see `hall-7`) but it is
  fewer lit panes than the brief pictured, and the shapes crossing them are correspondingly sparser.
- **The silhouettes are soft, not figures.** They are `Game1.shadowTexture` stretched to 88 pixels
  wide and clipped, which reads as something passing in front of the fire rather than as a person
  walking. That is what the brief asked for ("flat black silhouettes, a stretched vanilla shadow
  texture") and at normal zoom it works, but nobody would say "that is a person".
- **The window light pools are weak in rain.** `Game1.drawLighting` multiplies every light by 0.33
  and lifts the ambient toward white when it is raining (Game1.cs:13880), and every overnight run
  happened to be a rainy night. The painted glow carries the shot on its own, which is why it still
  reads, but the "light spilling on the wall and the path" half of the effect is nearly absent in
  rain. `hall-8` is the dry comparison and the pools are clearly there.
- **`SceneCamera.TileSize` is public now.** A one word widening, but it is a shared class Task 10
  will also touch.
- **Shane spends less than four seconds of a seven second scene in the shot.** He enters at 1500 and
  is off the bottom of the frame by about 5200, which leaves a full second of windows before the fade.
  That is the intended shape (the hold on the windows is the last beat) but it is worth saying out
  loud.
- **Winter is untested**, see above.


## Fix round 1

Finished by a fresh implementer after the round 1 implementer died mid-round. Its partial edits
were committed as `a6ec9f1` and I verified them rather than trusting them. On top of the review's
findings, **Jeff changed three requirements on 2026-09-23** after seeing the scene (relayed by the
coordinator), and they are folded into this round: smaller shadows that read as men, dimmer light,
and Shane walking PAST the hall on the town's real paths instead of up to it.

### Review findings

| Finding | Status | Where |
|---|---|---|
| IMPORTANT 1: the 5-tile Trim cancelled the Saloon-side walk in, so Shane came straight up the door column | ADDRESSED, then superseded by Jeff's route change. `a6ec9f1` did remove the Trim, but on screen he still popped into view mid-frame at (48,24), three rows above the bottom edge. I first moved his way in onto the row the bottom edge cuts (he came in over the bottom left from (48,26), 9 tiles, seen live), then Jeff's change replaced the whole walk. He now enters from the dirt road at the bottom right, crosses the front of the hall and leaves down the dirt path at the bottom left, so the door column is never his line. | `src/TheLongestYear/Scenes/HallWalker.cs:163` (`PlanWayIn`), `:185` (`PlanWayOut`) |
| IMPORTANT 2: `PrepareRevert` duplicated `Revert`'s fairness setup | ADDRESSED. Both now call one private `PickFairReversion(rng)`, which owns the day of year, the deadline, the save snapshot and the obtainability model. | `src/TheLongestYear/Loop/SabotageService.cs:477`, `:487`, `:498` |
| MINOR: FrontWindows measured off the abandoned art, no guard for a restored CC | ADDRESSED. `a6ec9f1` added `HallFacade.IsRestored` but only LOGGED it, and the panes were lit regardless. It is now read before anything is lit, and a restored or Joja front gets no panes and no lights (the scene plays on the dark front). Not exercised live: every test farm has an abandoned CC. | `src/TheLongestYear/Scenes/HallScene.cs:85` |
| MINOR: 1-texel sliver in the window paint | ADDRESSED. The panes were 2 pixels off the art's 4 pixel texel grid on every side (118,414 60x92), so half a texel of the dark frame was lit round each pane. Measured off the old `hall-8` frame pixel by pixel: the dark border column at screen x 724 to 727 was lit from 726. Now (120,416 56x88), the whole texels of glass. The silhouettes are snapped to the texel grid as well, so their cut edge never lands mid-texel either. Verified on the new frames: the border texels (screen x 724 to 727, y 472 to 475, y 564 to 567) are unlit on all four pixels. | `src/TheLongestYear/Scenes/HallFacade.cs:52`, `src/TheLongestYear/Scenes/SceneWindowGlow.cs:170` |
| MINOR: HallScene near 400 lines | ADDRESSED. `a6ec9f1` split `HallFacade` out. Shane now lives in his own `HallWalker` too. HallScene 154 lines, HallWalker 300, SceneWindowGlow 237. | `src/TheLongestYear/Scenes/HallWalker.cs` |
| MINOR: the hall-3 description overstated the jump | ADDRESSED. The row in the table above is corrected in place. | this file |

### Jeff's changes, 2026-09-23

1. **Shadows too big, should read as a man.** The stretched 88 pixel `shadowTexture` blob is gone.
   Each shape is now a 7 by 18 texel mask (`SceneWindow.Silhouette`: head, neck, shoulders, body)
   drawn at the game's 4 pixels a texel, so 28 by 72 world pixels against a 56 by 88 pane, the head
   5 texels below the top of the glass and the legs hidden by the sill. It bobs one texel a step
   (`SceneWindow.StrideBob`), the three out of step. Built once as a texture on the first painted
   frame and disposed in `Cleanup`. A device that will not build it logs a warning and the glass
   burns with nobody crossing it. `src/TheLongestYear.Core/Sabotage/SceneWindow.cs:52`,
   `src/TheLongestYear/Scenes/SceneWindowGlow.cs` (`Silhouette()`).
2. **Light too bright.** Glow alpha 0.55 plus or minus 0.15 is now 0.35 plus or minus 0.10
   (`SceneWindow.cs:23`), and each `LightSource` colour is scaled to 60 percent
   (`SceneWindowGlow.cs:37`). On the same pixel of the left pane the fill went from (177,75,20) to
   (150,72,13).
3. **Shane walks past, on real paths.** `HallWalker` routes both halves with
   `PathFindController.findPathForNPCSchedules` over Town (the schedule pathing, which prefers
   stone, wood and dirt), and `SceneRoute` (new, in Core, tested) cuts each route to the part in
   shot plus three tiles outside it. The ends were read off Town's own Back layer `Type` property
   (exported with `patch export Maps/Town`): he comes along the dirt road from (66,29), up the dirt
   connector onto the cobbles, stops at (52,23) three tiles below the door, jumps, backs away to
   (52,25) still facing the hall, then goes west to join the dirt path at (40,23) and down it toward
   (40,32), the way to the square and the road to Marnie's, and out of the bottom left of the frame.
   The camera did not need to move.
   **Tried first and dropped:** real routes from the Saloon door (45,70) to the Forest exit
   (-1,89). Neither goes near the hall, so he came up and went down the same column under the door,
   which reads as visiting, not passing.
   **Pace:** a long way in starts earlier (under the fade) rather than being hurried, at 250 ms a
   tile. Tonight's way in is 7 tiles from 1250 ms. The way out is 19 tiles at 75 ms a tile, so he
   clears the frame at about 5900 ms, not the brief's 5400, and the hold on the empty windows is
   about 300 ms before the fade. The brief's "runs out the way he came" is now "hurries on toward
   home". Plan Task 9 and spec Scene 3 are updated to say all of this.

### Tests

TDD for the pure parts: the new tests in `SceneWindowTests` (dimmer flicker bounds, the mask's
shape, texel snapping, the stride bob) and `SceneRouteTests` (6 tests) were written first. RED was
a compile failure (`error CS0103: The name 'SceneRoute' does not exist in the current context`),
GREEN after the code.

```
> dotnet build -c Debug --nologo -v q
    0 Error(s)
> dotnet test --nologo -v q
Passed!  - Failed: 0, Passed: 2769, Skipped: 0, Total: 2769, Duration: 1 s - TheLongestYear.Tests.dll (net6.0)
```

2753 before this round's new tests, so 16 new.

### Live check (automated run, mine)

`tools/deploy.ps1 -Minimized`, `tools/bridge.ps1`, `tools/send-smapi-command.ps1` only, with the
PrintWindow burst restoring the window WITHOUT focus as in the earlier rounds. No mouse, no
keyboard. Six launches of mine, each on a fresh `tly_newgame standard skipintro` farm
(`standard_449850260`, `_449850478`, `_449850660`, `_449851243`, `_449851441`, `_449851573`), all
six deleted afterwards, game closed, `git checkout -- test-output/log-archive` run.
`tly_sabotage scene hall` each time, as the mid-day preview. A day 1 farm has no donated slot, so
the scene played against the no-op effect. No ERROR and no Harmony failure in any log.

Final staging line:

```
Darkness: the hall is staged on the Community Center front at (47,11) to (58,20) in Town, facade
abandoned, 2 window(s) in world pixels (3128,1120 56x88) (3600,1120 56x88), 2 light(s), Shane walks
in 7 tile(s) from (54,28) at 250 ms a tile from 1250 ms, stops on the path at (52,23), backs away
to (52,25), hurries off toward home 19 tile(s) to (40,28) at 75 ms a tile, drawn from a sheet of
64x416, 16x32 frames, 4 column(s). Town's map declares no WindowLight on the hall.
```

Screenshots, untracked (gitignored), in
`C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\test-output\scenes\hall\`. The
capture clock runs about 860 ms behind the scene clock (fixed off the jump frame). I looked at
every one.

| File | What it shows |
|---|---|
| `fix1-1-in-from-the-road-bottom-right.png` | Shane coming up into the frame at its bottom edge, right of the door, on the dirt connector from the road. Both windows lit, a silhouette in the left one. |
| `fix1-2-up-the-path-onto-the-cobbles.png` | Him below the cobbles, walking up to his stop. |
| `fix1-3-the-jump.png` | The jump frame, on the path three tiles below the door, back to the camera. |
| `fix1-4-jump-crop-2x.png` | Six consecutive frames at his stop, 2x. The second is visibly lifted against its neighbours (about 28 screen pixels), then he stands, then backs down the path still facing up. |
| `fix1-5-carrying-on-west-past-the-hall.png` | After the fright he is walking west across the lawn in front of the hall toward the dirt path at the left. |
| `fix1-6-down-the-dirt-path-toward-home.png` | Him on the left dirt path heading down and out of the bottom left of the frame. |
| `fix1-7-hold-on-the-windows.png` | Nobody in the shot, the two lit windows. |
| `fix1-8-silhouettes-4x.png` | The left window at 4x in three frames: a black man's silhouette (head, neck, shoulders, body) against the glow, cut exactly at the glass. |
| `fix1-9-dimmer-glow-on-the-texel-grid-4x.png` | Both panes at 4x: the dimmer fill stops on whole texels, the dark frame round it unlit on every side. |

### Still open, plainly

- **A restored Community Center was not seen live.** The guard is a read of mail flags before
  anything is lit, not a picture.
- **Zoom 75 percent** was closed by the review from the decompile, not by a screenshot. Nothing in
  this round changes how the panes are placed.
- **The with-candidate branch of `tly_sabotage scene hall`** was not exercised (day 1 farms have no
  donated slots). It shares `PickFairReversion` with `tly_sabotage revert` now.
- **The paces are brisk.** 250 ms a tile walking in is about twice vanilla's stroll, and 75 ms a
  tile leaving is a hard run. That is what seven seconds allows for walking past rather than up to
  the hall. Jeff may want the scene longer.
- **The six `standard_4497*` farms the original report said were deleted are back** in the saves
  folder, all recreated at 06:47 on 2026-09-22, which looks like Steam Cloud restoring them. I did
  not touch them. Mine from this round may come back the same way.
