# The Rewind Cutscene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the static day-28 Fail card with a staged sequence: a bedroom scene where the Junimos spend their power, a camera pan across Town where the year visibly runs backward, and a Spring 1 morning that is painted before the player answers anything.

**Architecture:** The bedroom stays drawn by us (a menu), because the dark wake frame is exactly where past vanilla-`Event` attempts died. The pan is a real staged sequence in Town reusing the ending's event commands. All schedule arithmetic (which seasons unwind, where they swap, what the clock reads) lives in `TheLongestYear.Core` as pure functions with unit tests; the mod projects only apply those values to the engine.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xUnit. Stardew Valley 1.6 PC decompile at `Stardee Valoo/decompiled-pc/Stardew Valley`.

**Spec:** `docs/superpowers/specs/2026-09-11-rewind-cutscene-design.md`

## Global Constraints

- **Branch is `story`. Do NOT bump `manifest.json`'s `Version`.** Only the release line owns version bumps; a bump here conflicts on every merge.
- Commit after every task. Small, one-change commits.
- **Every new i18n key must be used, and every used key must exist.** `I18nGuardTests.NoOrphanKeys_InDefaultJson` fails the build on an unused key. Delete retired keys in the same commit that stops using them.
- Jeff's lines are used **verbatim**. Do not reword them.
- **No em dashes** in any player-facing string, comment or doc.
- Pure logic goes in `src/TheLongestYear.Core/` and gets xUnit tests. Engine glue goes in `src/TheLongestYear/` and is verified live.
- Call `GameLocation.updateSeasonalTileSheets()` only. **Never `seasonUpdate()`**, which mutates terrain and crops rather than repainting.
- The mod must stay dormant on non-TLY saves: every new driver path guards on `RunActivation.IsActive`.
- Run tests with `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`. Deploy with `pwsh -NoProfile -File tools/deploy.ps1 -Minimized` (it closes the running game first; a plain `dotnet build` fails with a locked DLL while the game is up).

---

## File Structure

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/Rewind/RewindSchedule.cs` | Pure: which seasons unwind, where they swap along the route, what the clock reads at a given progress. |
| `src/TheLongestYear.Core/Rewind/SpringPaint.cs` | Pure: the cosmetic Spring 1 values (season, time, weather). |
| `src/TheLongestYear/Integration/TownRouteProbe.cs` | Debug command `tly_townroute`: logs the measured Blacksmith door and Farm warp tiles. |
| `src/TheLongestYear/UI/RewindBedroomScene.cs` | Beats 1 to 9. Drawn. Lights, Junimos, darkness dials, the white. |
| `src/TheLongestYear/Integration/RewindPanScene.cs` | Beat 10. Route, the four dials, the villager. |
| `src/TheLongestYear/Integration/RewindSpringPaint.cs` | Applies and holds `SpringPaint` values across beats 11 to 14. |
| `src/TheLongestYear/Integration/Day28CutsceneDriver.cs` | Modified: opens the rewind sequence for Fail instead of the card. |
| `src/TheLongestYear/Loop/RunController.cs` | Modified: retire the `displayHUD = false` hack; the hold question moves into the bedroom. |
| `src/TheLongestYear/i18n/default.json` | Modified: `cutscene.rewind.*` added, `cutscene.day28.fail` retired. |
| `tests/TheLongestYear.Tests/RewindScheduleTests.cs` | Tests for Task 1. |
| `tests/TheLongestYear.Tests/SpringPaintTests.cs` | Tests for Task 2. |

---

### Task 1: RewindSchedule (pure)

The arithmetic behind the pan: a Fall failure unwinds Fall, Summer, Spring and swaps twice; a Spring failure unwinds nothing and swaps never.

**Files:**
- Create: `src/TheLongestYear.Core/Rewind/RewindSchedule.cs`
- Test: `tests/TheLongestYear.Tests/RewindScheduleTests.cs`

**Interfaces:**
- Consumes: `TheLongestYear.Core.Season` (Spring=0, Summer=1, Fall=2, Winter=3).
- Produces:
  - `IReadOnlyList<Season> RewindSchedule.SeasonsToUnwind(Season failed)` — the failed season first, descending to Spring inclusive.
  - `IReadOnlyList<double> RewindSchedule.SwapFractions(Season failed)` — the progress points (0..1 exclusive) where the tilesheets swap. Empty for Spring.
  - `int RewindSchedule.ClockAt(double progress, int startTime, int endTime)` — the backward clock value, in Stardew's HHmm form, rounded down to the nearest 10 minutes.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class RewindScheduleTests
{
    [Fact]
    public void SeasonsToUnwind_runs_from_the_failed_season_down_to_spring()
    {
        Assert.Equal(new List<Season> { Season.Fall, Season.Summer, Season.Spring },
            RewindSchedule.SeasonsToUnwind(Season.Fall));
        Assert.Equal(new List<Season> { Season.Winter, Season.Fall, Season.Summer, Season.Spring },
            RewindSchedule.SeasonsToUnwind(Season.Winter));
    }

    [Fact]
    public void SeasonsToUnwind_for_spring_is_spring_alone()
    {
        Assert.Equal(new List<Season> { Season.Spring }, RewindSchedule.SeasonsToUnwind(Season.Spring));
    }

    [Fact]
    public void SwapFractions_divide_the_route_evenly_between_the_seasons()
    {
        Assert.Equal(new List<double> { 1.0 / 3.0, 2.0 / 3.0 }, RewindSchedule.SwapFractions(Season.Fall));
        Assert.Equal(new List<double> { 0.25, 0.5, 0.75 }, RewindSchedule.SwapFractions(Season.Winter));
        Assert.Equal(new List<double> { 0.5 }, RewindSchedule.SwapFractions(Season.Summer));
    }

    [Fact]
    public void SwapFractions_for_spring_is_empty_because_nothing_unwinds()
    {
        Assert.Empty(RewindSchedule.SwapFractions(Season.Spring));
    }

    [Theory]
    [InlineData(0.0, 2400)]
    [InlineData(1.0, 600)]
    [InlineData(0.5, 1500)]
    public void ClockAt_runs_backward_from_start_to_end(double progress, int expected)
    {
        Assert.Equal(expected, RewindSchedule.ClockAt(progress, 2400, 600));
    }

    [Fact]
    public void ClockAt_rounds_down_to_ten_minute_steps()
    {
        int t = RewindSchedule.ClockAt(0.37, 2400, 600);
        Assert.Equal(0, t % 10);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter RewindScheduleTests`
Expected: FAIL, the type `RewindSchedule` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Rewind;

/// <summary>The arithmetic behind the rewind pan: which seasons unwind, where along the camera
/// route each tilesheet swap lands, and what the backward clock reads at a given progress. Pure so
/// the odd cases (a Spring failure unwinds nothing) are settled in tests, not in a playtest.</summary>
public static class RewindSchedule
{
    /// <summary>The failed season first, descending to Spring inclusive. A Spring failure yields
    /// Spring alone: there is nothing earlier in the year to wash back to.</summary>
    public static IReadOnlyList<Season> SeasonsToUnwind(Season failed)
    {
        var list = new List<Season>();
        for (int s = (int)failed; s >= 0; s--)
            list.Add((Season)s);
        return list;
    }

    /// <summary>Progress points (exclusive of both ends) where the map repaints. One fewer than the
    /// number of seasons, spaced evenly, so the camera spends equal distance in each.</summary>
    public static IReadOnlyList<double> SwapFractions(Season failed)
    {
        int count = SeasonsToUnwind(failed).Count;
        var fractions = new List<double>();
        for (int i = 1; i < count; i++)
            fractions.Add((double)i / count);
        return fractions;
    }

    /// <summary>The clock at a point along the route, running from <paramref name="startTime"/> down
    /// to <paramref name="endTime"/>. Stardew stores time as HHmm and ticks in ten-minute steps, so
    /// the result is floored to a multiple of 10 to avoid values the game never produces.</summary>
    public static int ClockAt(double progress, int startTime, int endTime)
    {
        double clamped = Math.Clamp(progress, 0.0, 1.0);
        double minutesStart = ToMinutes(startTime);
        double minutesEnd = ToMinutes(endTime);
        double minutes = minutesStart + (minutesEnd - minutesStart) * clamped;
        int stepped = (int)(Math.Floor(minutes / 10.0) * 10.0);
        return ToClock(stepped);
    }

    private static double ToMinutes(int clock) => clock / 100 * 60 + clock % 100;

    private static int ToClock(int minutes) => minutes / 60 * 100 + minutes % 60;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter RewindScheduleTests`
Expected: PASS, 6 tests.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS, no regressions.

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/Rewind/RewindSchedule.cs tests/TheLongestYear.Tests/RewindScheduleTests.cs
git commit -m "rewind: season swap points and the backward clock, as pure arithmetic"
```

---

### Task 2: SpringPaint (pure)

The cosmetic Spring 1 values that beat 11 applies and holds through the menus, kept separate from the real reset so the two can be compared.

**Files:**
- Create: `src/TheLongestYear.Core/Rewind/SpringPaint.cs`
- Test: `tests/TheLongestYear.Tests/SpringPaintTests.cs`

**Interfaces:**
- Produces: `readonly record struct SpringPaintValues(Season Season, int DayOfMonth, int TimeOfDay, bool Raining, bool Snowing, bool DebrisWeather)` and `SpringPaintValues SpringPaint.Values(int wakeTime = 600)`.

- [ ] **Step 1: Write the failing test**

```csharp
using TheLongestYear.Core;
using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class SpringPaintTests
{
    [Fact]
    public void Values_paint_a_clear_spring_one_morning()
    {
        SpringPaintValues v = SpringPaint.Values();
        Assert.Equal(Season.Spring, v.Season);
        Assert.Equal(1, v.DayOfMonth);
        Assert.Equal(600, v.TimeOfDay);
    }

    [Fact]
    public void Values_clear_every_weather_flag_the_pan_may_have_set()
    {
        SpringPaintValues v = SpringPaint.Values();
        Assert.False(v.Raining);
        Assert.False(v.Snowing);
        Assert.False(v.DebrisWeather);
    }

    [Fact]
    public void Values_accept_a_different_wake_time()
    {
        Assert.Equal(620, SpringPaint.Values(620).TimeOfDay);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SpringPaintTests`
Expected: FAIL, the type `SpringPaint` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace TheLongestYear.Core.Rewind;

/// <summary>What Spring 1 has to look like from the end of the pan until the player takes control.
/// Jeff, 2026-09-11: the reset itself can happen afterwards, but the paint must be on the last
/// screen and must hold through the questions and the purchasing.</summary>
public readonly record struct SpringPaintValues(
    Season Season,
    int DayOfMonth,
    int TimeOfDay,
    bool Raining,
    bool Snowing,
    bool DebrisWeather);

public static class SpringPaint
{
    /// <summary>The default wake time, matching vanilla's 6am.</summary>
    public const int DefaultWakeTime = 600;

    public static SpringPaintValues Values(int wakeTime = DefaultWakeTime)
        => new(Season.Spring, 1, wakeTime, Raining: false, Snowing: false, DebrisWeather: false);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SpringPaintTests`
Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Rewind/SpringPaint.cs tests/TheLongestYear.Tests/SpringPaintTests.cs
git commit -m "rewind: the cosmetic Spring 1 paint values"
```

---

### Task 3: Measure the Town route

The spec refuses to guess the pan's endpoints. This task measures them from the live map and writes the numbers down.

**Files:**
- Create: `src/TheLongestYear/Integration/TownRouteProbe.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (register the command alongside the other `tly_` registrations)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: the measured constants, recorded in this plan and used by Task 5. `TownRouteProbe.Register(IModHelper helper, IMonitor monitor)`.

- [ ] **Step 1: Write the probe**

`GameLocation.warps` is a `NetObjectList<Warp>` where each `Warp` exposes `X`, `Y` and `TargetName`; `GameLocation.doors` is a `NetPointDictionary<string, NetString>` mapping a door tile to the building it opens. Both are readable without entering the location.

```csharp
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Debug only: prints the two tiles the rewind pan travels between, read off the live
    /// Town map rather than estimated. Run once, write the numbers into RewindPanScene.</summary>
    internal static class TownRouteProbe
    {
        public static void Register(IModHelper helper, IMonitor monitor)
        {
            helper.ConsoleCommands.Add(
                "tly_townroute",
                "Print Town's Blacksmith door tile and its Farm warp tile (debug: the rewind pan's endpoints).",
                (_, _) =>
                {
                    GameLocation town = Game1.getLocationFromName("Town");
                    if (town == null) { monitor.Log("tly_townroute: Town is not loaded.", LogLevel.Warn); return; }

                    foreach (var door in town.doors.Pairs)
                        monitor.Log($"tly_townroute: door at ({door.Key.X},{door.Key.Y}) -> {door.Value}", LogLevel.Info);

                    foreach (Warp w in town.warps.Where(w => w.TargetName == "Farm"))
                        monitor.Log($"tly_townroute: warp to Farm at ({w.X},{w.Y})", LogLevel.Info);
                });
        }
    }
}
```

- [ ] **Step 2: Register it**

In `ModEntry.Entry`, beside the other `helper.ConsoleCommands.Add("tly_...")` calls:

```csharp
Integration.TownRouteProbe.Register(helper, this.Monitor);
```

- [ ] **Step 3: Deploy and measure**

```bash
pwsh -NoProfile -File tools/deploy.ps1 -Minimized
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "pause when window is inactive" -FromLine 0 -TimeoutSec 180
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_loadsave <SaveFolder>"
pwsh -NoProfile -File tools/send-smapi-command.ps1 "tly_townroute"
```

Read `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. The Blacksmith line is the door whose value is `Blacksmith`; the Farm line is the warp whose target is `Farm`.

- [ ] **Step 4: Record the numbers**

Write both tiles into this plan under Task 5's `PanStart` and `PanEnd` constants, replacing the placeholders there, and commit the plan edit with the code.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Integration/TownRouteProbe.cs src/TheLongestYear/ModEntry.cs docs/superpowers/plans/2026-09-11-rewind-cutscene.md
git commit -m "rewind: tly_townroute probe, and the measured pan endpoints"
```

---

### Task 4: The bedroom scene, beats 1 to 9

**Files:**
- Create: `src/TheLongestYear/UI/RewindBedroomScene.cs`
- Modify: `src/TheLongestYear/i18n/default.json`

**Interfaces:**
- Consumes: `EndingSpeechBox(Texture2D portrait, List<string> pages)` from `TheLongestYear.Integration`; `JunimoPalette.Get(int index)`.
- Produces: `RewindBedroomScene(Action onComplete)` as an `IClickableMenu`, and `bool RewindBedroomScene.HoldChosen` readable by the driver after completion.

**Why a menu and not an Event:** the June attempts died on this exact frame. `fade` revealed the room, `globalFade` blinked back, and a `RenderedWorld` overlay painted over the event's own dialogue box. A menu draws the world state and the text in one ordered pass. See `Day28CutsceneMenu`'s class comment.

- [ ] **Step 1: Add the lines**

In `src/TheLongestYear/i18n/default.json`, beside the other cutscene keys. Jeff's text, verbatim:

```json
"cutscene.rewind.junimo-1": "@, you have worked hard, but we have not gained enough power to proceed.",
"cutscene.rewind.junimo-2": "Your efforts have attracted the attention of our adversary. It is coming for you now, and we cannot hold it back.",
"cutscene.rewind.junimo-3": "But the work must continue, we cannot fail here. So we will use what power we have to give you another chance.",
"cutscene.rewind.junimo-4": "The year will begin again, and the darkness will sleep once more, until the light begins to grow.",
"cutscene.rewind.morning": "We have bought you more time. Use it and what remains of our power wisely, and remember that every step you take is a small victory.",
```

Do **not** delete `cutscene.day28.fail` yet: it is still referenced by `Day28CutsceneMenu` until Task 7 stops using it, and the i18n guard fails on a key referenced but missing.

- [ ] **Step 2: Write the scene**

The three dials, in one class. `Junimo` is `StardewValley.Characters.Junimo`; construct as the ending does (`new Junimo(pos, -1, temporary: true)`), set `stayPut.Value = true` so they sway in place rather than wander, and set the colour through the same reflection helper the ending uses. Each gets a `LightSource(textureIndex, position, radius, color)` added to `Game1.currentLightSources`.

```csharp
// Phase order. Each advances on a timer except Say, which waits for its box to close.
private enum Phase { LightsOut, JunimosIn, Say1, Say2, DarknessIn, Say3, Say4Ask, White, Done }
```

- Beat 2, lights out: remove the farmhouse's own entries from `Game1.currentLightSources` and keep them out. Jeff, 2026-09-11: the room is still daylit on Spring 1, only the glowing auras are gone.
- Beat 6, darkness in: ease ambient toward black while shrinking every Junimo light radius toward a floor. A cloud-shaped edge is deliberately NOT built here; judge the circles first (spec, Section 1).
- Beat 8, the ask: the hold-or-reshuffle question, asked here because it decides how the reset builds the board and so cannot wait until after it. Store the answer in `HoldChosen`.
- Beat 9, white: run the radii back up past the screen size, warming the colour to white. The flash is the Junimos spending everything, not an effect on top.

- [ ] **Step 3: Verify it compiles**

Run: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`
Expected: `0 Error(s)`.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/UI/RewindBedroomScene.cs src/TheLongestYear/i18n/default.json
git commit -m "rewind: the bedroom scene, beats 1 to 9"
```

---

### Task 5: The pan, beat 10

**Files:**
- Create: `src/TheLongestYear/Integration/RewindPanScene.cs`

**Interfaces:**
- Consumes: `RewindSchedule.SeasonsToUnwind`, `RewindSchedule.SwapFractions`, `RewindSchedule.ClockAt` (Task 1); `EndingSpeaker.Pick(MetaState, int, Func<string,bool>)` for the villager.
- Produces: `RewindPanScene.Start(Season failed, Action onComplete)`.

**Measured endpoints** (fill from Task 3, do not guess):

Measured live on 2026-09-11 with `tly_townroute`:

```csharp
// Clint's shop door, Town (94,81).
private static readonly Point PanStart = new Point(94, 81);
// The west-edge road out to the Bus Stop, Town (-1,53..55): the way to the farm, and the
// "top left" of Jeff's brief relative to Clint's. x=0 rather than -1, which is off-map.
private static readonly Point PanEnd   = new Point(0, 54);
```

**Town has no warp targeting `Farm`.** The probe printed 16 doors and Town's full warp list: BusStop
at (-1,53..55), Mountain along y=-1, Forest at (-1,89..93), Beach along y=110. The farm is reached
through the Bus Stop, not from Town directly, so the pan's far end is that west-edge road.

- [ ] **Step 1: Write the scene**

Camera only: the farmer stays in bed, so nothing has to explain why they are outdoors at night. The camera uses the ending's `tlyPanTo` (`EndingEventCommands.PanToName`), which already does eased viewport moves over a duration.

Four dials, ticked against one normalised `progress` from 0 to 1:

```csharp
// Seasons: repaint at each fraction. updateSeasonalTileSheets only, NEVER seasonUpdate,
// which mutates terrain and crops instead of repainting them.
foreach (double f in _swaps)
    if (Crossed(f, progress))
    {
        Game1.season = _seasons[++_seasonIndex];
        town.updateSeasonalTileSheets();
        Gust();                       // hides the cut: the swap is a dispose-and-reload, never a dissolve
    }

// Clock: Game1.UpdateGameClock recomputes outdoorLight from timeOfDay every tick, so driving
// the clock backward lights the valley backward with no tinting of our own.
Game1.timeOfDay = RewindSchedule.ClockAt(progress, ClockStart, ClockEnd);

// Wind: blows the whole way, gusting on each swap.
Game1.isDebrisWeather = true;

// The villager: walks toward the farm, stops, "?" emote, turns back.
```

The villager comes from the ending's own selector, so the one who almost remembers you when you win is the one who forgets you when you fail:

```csharp
string villager = EndingSpeaker.Pick(_meta.State, _config.DejaVuThreshold, Eligible);
```

A Spring failure has no swaps (Task 1 returns an empty list), so the pan runs its length on wind, clock and the villager alone.

- [ ] **Step 2: Deploy and check it compiles**

Run: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`
Expected: `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Integration/RewindPanScene.cs
git commit -m "rewind: the Town pan, beat 10"
```

---

### Task 6: The Spring 1 paint, beats 11 to 14

**Files:**
- Create: `src/TheLongestYear/Integration/RewindSpringPaint.cs`
- Modify: `src/TheLongestYear/Loop/RunController.cs` (retire `Game1.displayHUD = false`)

**Interfaces:**
- Consumes: `SpringPaint.Values(int)` (Task 2).
- Produces: `RewindSpringPaint.Apply()` and `RewindSpringPaint.Release()`.

- [ ] **Step 1: Write the applier**

```csharp
SpringPaintValues v = SpringPaint.Values();
Game1.season = (StardewValley.Season)(int)v.Season;
Game1.dayOfMonth = v.DayOfMonth;
Game1.timeOfDay = v.TimeOfDay;
Game1.isRaining = v.Raining;
Game1.isSnowing = v.Snowing;
Game1.isDebrisWeather = v.DebrisWeather;
```

Hold it across beats 12 to 14; the real reset lands underneath and reconciles for real. `Release()` stops holding once the player takes control.

- [ ] **Step 2: Retire the stale-date hack**

In `RunController.OnCutsceneEnded`, the `Day28Branch.Fail` arm sets `Game1.displayHUD = false` so the pre-rewind date is not shown through the choice and the shrine. With the paint applied the date is now correct, so delete that line and the matching restore in `ContinueAfterResetSpend`, and replace the comment with why the date is now safe to show.

- [ ] **Step 3: Audit what reads the season during the window**

This is the spec's flagged risk: `Game1.season` and the clock are global and are briefly set to something the run state does not yet agree with.

Run: `grep -rn "Game1.season\|Game1.dayOfMonth\|Game1.timeOfDay" src/TheLongestYear/ --include=*.cs | grep -v bin/`

For each hit, decide whether it can run during the paint window (the cutscene, the pity dialogs, the shrine). Record the findings as a comment block in `RewindSpringPaint.cs`. Anything that can misbehave gets guarded.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Integration/RewindSpringPaint.cs src/TheLongestYear/Loop/RunController.cs
git commit -m "rewind: paint Spring 1 from the end of the pan, and retire the hidden-HUD hack"
```

---

### Task 7: Wire it into the driver

**Files:**
- Modify: `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`
- Modify: `src/TheLongestYear/i18n/default.json` (retire `cutscene.day28.fail`)

**Interfaces:**
- Consumes: `RewindBedroomScene` (Task 4), `RewindPanScene.Start` (Task 5), `RewindSpringPaint.Apply` (Task 6).

- [ ] **Step 1: Keep the open window exactly as it is**

Do not touch the conditions that decide when to open. The driver waits out `Game1.newDay`, any overnight `FarmEvent`, a pending `locationRequest` and any open menu, and opens while the wake fade is still dark. That window was chosen in the 2026-06-03 playtest and is defended by a watchdog that re-arms if `showEndOfNightStuff` steals the frame. Only what gets opened changes.

- [ ] **Step 2: Open the sequence for Fail**

`Day28Branch.Fail` opens `RewindBedroomScene`, whose completion starts `RewindPanScene`, whose completion calls `RewindSpringPaint.Apply()` and then the existing `RunController.OnCutsceneEnded()`. `Day28Branch.Continue` still opens `Day28CutsceneMenu` unchanged.

- [ ] **Step 3: Retire the old Fail text**

`cutscene.day28.fail` now has no reader. Delete the key. Its closing question already lives in the bedroom's beat 8.

- [ ] **Step 4: Run the suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS, including `I18nGuardTests.NoOrphanKeys_InDefaultJson` — which is the check that catches the retired key if step 3 was missed.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Integration/Day28CutsceneDriver.cs src/TheLongestYear/i18n/default.json
git commit -m "rewind: the driver opens the new sequence for Fail; the card is retired"
```

---

### Task 8: Live verification

Nothing above proves it looks right. `tly_failreset` queues the Fail branch from anywhere, so the whole sequence is reachable without playing to a real day 28.

**Files:** none. This task produces a report.

- [ ] **Step 1: Deploy and load**

```bash
pwsh -NoProfile -File tools/deploy.ps1 -Minimized
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "pause when window is inactive" -FromLine 0 -TimeoutSec 180
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_loadsave <SaveFolder>"
```

Throwaway save only: the Rodger `None_` lineage. Never `PuffPuff_*`, never `Cheatside_*`.

- [ ] **Step 2: Run the Fail sequence**

```bash
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_failreset"
```

- [ ] **Step 3: Check each beat against the spec**

- The house lights are out and stay out, and Spring 1 is still daylit.
- The Junimos animate and hold station at the bed rather than wandering.
- The darkness closes in; judge whether the light circles read as too tidy, which is the decision on whether the cloud mask gets built.
- The white comes from the Junimo light growing, not a cut to white.
- The hold question is asked in the bedroom, before the pan.
- Each season swap is hidden by its gust.
- The villager turns back.
- **Nothing between the end of the pan and taking control ever shows a date that is not Spring 1.**

- [ ] **Step 4: Repeat for a Spring failure**

Force a Spring 28 fail and confirm the pan still runs on wind, clock and the villager with no season swap, and that it does not look broken or empty.

- [ ] **Step 5: Report to Jeff and commit any tuning**

Durations, radii and the gust timing are all tuning values. Expect to change them after seeing it. Commit each change separately.

---

## Self-Review

**Spec coverage:** Beats 1 to 9 are Task 4; beat 10 is Task 5; beats 11 to 14 are Task 6; the driver wiring is Task 7; the route measurement that the spec left open is Task 3; the season and clock arithmetic including the Spring case is Task 1; the paint-versus-state rule is Tasks 2 and 6; the `Game1.season` audit the spec flagged is Task 6 step 3; the cloud-mask decision is deferred to observation in Task 8 step 3, as the spec asks. The JP cost and the day counter are non-goals and appear in no task.

**Placeholders:** `PanStart` and `PanEnd` in Task 5 are intentionally unfilled, because Task 3 measures them and writes them in. That is a sequenced dependency, not a hole.

**Type consistency:** `RewindSchedule.SeasonsToUnwind`, `SwapFractions` and `ClockAt` are used in Task 5 with the signatures defined in Task 1. `SpringPaint.Values` returns `SpringPaintValues` and is consumed in Task 6 as defined in Task 2. `EndingSpeaker.Pick(MetaState, int, Func<string,bool>)` matches the existing signature in `src/TheLongestYear.Core/Ending/EndingSpeaker.cs`.
