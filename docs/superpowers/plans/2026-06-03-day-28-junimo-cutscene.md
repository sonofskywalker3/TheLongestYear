# Day-28 Junimo Bedtime Cutscene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On the morning after sleeping the 28th, play a forced black-screen Junimo cutscene that branches on the season's gate: FAIL → rewind dialogue → JP shop → reset to Spring 1 → forced full save; CONTINUE → congratulations → roll into the next season.

**Architecture:** A pure `Day28CutsceneDecider` (Core, unit-tested) drives a `Day28CutsceneDriver` (glue) that mirrors the shipped `IntroSequenceDriver`: it polls `UpdateTicked`, starts a forced vanilla `Event` (black via the `fade` command, Junimo speaks), and on event-end calls back into `RunController`. `RunController.OnDayEnding` routes the existing `RunAction` to a `PendingCutscene` branch; `OnCutsceneEnded` opens the existing JP shop + reset (FAIL) or `DoDayStartSeasonAndHub` (CONTINUE). The FAIL path appends a forced `SaveGame.Save` to close the save-folder churn.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, Harmony, Stardew Valley 1.6.15 (PC), xUnit tests. Two projects: `TheLongestYear.Core` (pure, testable) and `TheLongestYear` (SMAPI glue).

**Spec:** `docs/superpowers/specs/2026-06-03-day-28-junimo-cutscene-design.md`

**Build note (SMAPI may be running):** compile-only with `dotnet build -p:EnableModDeploy=false -p:EnableModZip=false`. Tests: `dotnet test`. Keep 0 warnings and the suite green (currently 421 passing).

---

### Task 1: Core decider — branch/action types + `Day28CutsceneDecider`

**Files:**
- Create: `src/TheLongestYear.Core/Day28/Day28CutsceneDecider.cs`
- Test: `tests/TheLongestYear.Tests/Day28CutsceneDeciderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/Day28CutsceneDeciderTests.cs`:

```csharp
using TheLongestYear.Core.Day28;
using Xunit;

namespace TheLongestYear.Tests;

public class Day28CutsceneDeciderTests
{
    private static Day28Action Next(Day28Branch branch, bool started, bool eventActive)
        => Day28CutsceneDecider.Next(new Day28Snapshot(branch, started, eventActive));

    [Fact]
    public void No_pending_branch_is_a_no_op()
    {
        Assert.Equal(Day28Action.None, Next(Day28Branch.None, started: false, eventActive: false));
        Assert.Equal(Day28Action.None, Next(Day28Branch.None, started: true, eventActive: true));
    }

    [Theory]
    [InlineData(Day28Branch.Fail)]
    [InlineData(Day28Branch.Continue)]
    public void Pending_and_not_started_starts_the_cutscene(Day28Branch branch)
    {
        Assert.Equal(Day28Action.StartCutscene, Next(branch, started: false, eventActive: false));
    }

    [Theory]
    [InlineData(Day28Branch.Fail)]
    [InlineData(Day28Branch.Continue)]
    public void Started_while_event_is_active_waits(Day28Branch branch)
    {
        Assert.Equal(Day28Action.Waiting, Next(branch, started: true, eventActive: true));
    }

    [Theory]
    [InlineData(Day28Branch.Fail)]
    [InlineData(Day28Branch.Continue)]
    public void Started_and_event_ended_runs_the_continuation(Day28Branch branch)
    {
        Assert.Equal(Day28Action.RunContinuation, Next(branch, started: true, eventActive: false));
    }

    [Fact]
    public void An_active_event_before_start_still_waits_rather_than_double_starting()
    {
        // Defensive: some other event is up the instant we became pending.
        Assert.Equal(Day28Action.Waiting, Next(Day28Branch.Fail, started: false, eventActive: true));
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

Run: `dotnet test --filter Day28CutsceneDeciderTests`
Expected: FAIL — `Day28CutsceneDecider` / `Day28Branch` / `Day28Action` / `Day28Snapshot` do not exist (compile error).

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/Day28/Day28CutsceneDecider.cs`:

```csharp
namespace TheLongestYear.Core.Day28
{
    /// <summary>Which day-28 bedtime branch is queued (set by RunController.OnDayEnding from the
    /// gate's RunAction). None = no cutscene this morning.</summary>
    public enum Day28Branch
    {
        None,
        Fail,     // gate closed → rewind dialogue → JP shop → reset to Spring 1
        Continue  // gate open → congratulations → roll into the next season
    }

    /// <summary>What the driver should do this tick.</summary>
    public enum Day28Action
    {
        None,            // nothing pending — let the normal flow run
        Waiting,         // an event is playing (or just ended) — do nothing this tick
        StartCutscene,   // pending + not started → start the forced Junimo event
        RunContinuation  // event finished → run the branch continuation (shop+reset / continue)
    }

    /// <summary>Immutable snapshot of the inputs the decider needs (no game refs).
    /// <paramref name="EventActive"/> folds in the post-end window the way the intro does.</summary>
    public readonly record struct Day28Snapshot(
        Day28Branch Branch,
        bool Started,
        bool EventActive);

    /// <summary>Pure step machine mirroring <c>IntroSequenceDecider</c>. The driver owns the
    /// settled-frame / menu / cooldown guards; this only sequences start → wait → continue.</summary>
    public static class Day28CutsceneDecider
    {
        public static Day28Action Next(Day28Snapshot s)
        {
            if (s.Branch == Day28Branch.None)
                return Day28Action.None;
            if (s.EventActive)
                return Day28Action.Waiting;     // covers "before start, something else is up" and "just ended"
            if (!s.Started)
                return Day28Action.StartCutscene;
            return Day28Action.RunContinuation; // started + no event active → the scene has ended
        }
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter Day28CutsceneDeciderTests`
Expected: PASS (8 cases).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Day28/Day28CutsceneDecider.cs tests/TheLongestYear.Tests/Day28CutsceneDeciderTests.cs
git commit -m "feat(day28): pure cutscene decider (branch/action state machine) + tests"
```

---

### Task 2: Core content — event id + branch dialogue

**Files:**
- Create: `src/TheLongestYear.Core/Day28/Day28CutsceneContent.cs`

No new test file — the strings are content; they're exercised via the injector (Task 3) and in-game. (The decider tests already cover the branching logic.)

- [ ] **Step 1: Write the implementation**

Create `src/TheLongestYear.Core/Day28/Day28CutsceneContent.cs`:

```csharp
namespace TheLongestYear.Core.Day28
{
    /// <summary>Content + identifiers for the day-28 bedtime Junimo cutscene. Kept in Core so the
    /// dialogue is one source of truth and the glue injector just stages it. The cutscene is
    /// started manually by <c>Day28CutsceneDriver</c> via <c>location.startEvent</c>, so this id is
    /// NOT a Data/Events key and is NOT matched by EventSuppressionPatch (which only hooks
    /// checkEventPrecondition). It fires on every qualifying 28th — there is intentionally no
    /// cross-loop suppression flag (unlike the intro's HasSeenIntro).</summary>
    public static class Day28CutsceneContent
    {
        /// <summary>Event id passed to <c>new Event(script, null, EventId)</c> (lands in eventsSeen,
        /// harmless). Distinct from any vanilla id and from the intro id.</summary>
        public const string EventId = "tly_day28";

        /// <summary>Gate CLOSED: the year will rewind; the Junimo offers a head-start (JP shop
        /// follows). <c>@</c> = player name; <c>#$b#</c> = dialogue-box page break; <c>$h</c> =
        /// happy portrait pose (harmless on the portrait-less Junimo).</summary>
        public const string FailDialogue =
            "At this pace we won't be able to restore the Community Center in time, @.#$b#" +
            "So we will use our magic to rewind the year — but don't worry. " +
            "We have enough power left over to give you a head-start this time.$h";

        /// <summary>Gate OPEN: on track; roll into the next season (no shop).</summary>
        public const string ContinueDialogue =
            "Great job, @ — you're doing well!#$b#" +
            "Keep this up and we'll save the valley together. " +
            "We'll gain even more power from the work you do this season.$h";
    }
}
```

- [ ] **Step 2: Build Core, verify it compiles**

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear.Core/Day28/Day28CutsceneContent.cs
git commit -m "feat(day28): cutscene event id + FAIL/CONTINUE Junimo dialogue (Core content)"
```

---

### Task 3: Glue — event-script injector

**Files:**
- Create: `src/TheLongestYear/Integration/Day28CutsceneInjector.cs`

Mirrors `IntroEventInjector.BuildIntroEvent`. The black screen comes from the vanilla `fade` command (`Event.Fade`): it sets `fadeToBlack`+`fadeIn` and, once the screen is fully black, advances the event while *holding* black — so the following `speak`/`pause` commands render over black. `speak Junimo` requires the actor to exist, so we still `addTemporaryActor Junimo` (off-screen behind the black). `skippable` is omitted on purpose (a skip would bypass `end` and strand the driver — the 2026-06-01 intro lesson).

- [ ] **Step 1: Write the implementation**

Create `src/TheLongestYear/Integration/Day28CutsceneInjector.cs`:

```csharp
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Integration
{
    /// <summary>Builds the day-28 bedtime cutscene as a vanilla Event script: fade to black, a
    /// Junimo meep, one page of branch dialogue, then <c>end</c>. No <c>changeLocation</c> — the
    /// scene plays wherever the player wakes (the FarmHouse) and the <c>fade</c> command hides it.
    /// Not skippable (no <c>skippable</c> token): the driver detects "scene ended" by the event
    /// going inactive, and a skip would race that.</summary>
    internal static class Day28CutsceneInjector
    {
        public static string BuildEvent(Day28Branch branch)
        {
            string line = branch == Day28Branch.Fail
                ? Day28CutsceneContent.FailDialogue
                : Day28CutsceneContent.ContinueDialogue;

            return string.Join("/", new[]
            {
                "none",                         // music
                "3 9",                          // initial viewport (FarmHouse interior; irrelevant once black)
                "farmer 3 9 2",                 // place the farmer; restored to pre-event tile on end
                // (no "skippable" — forced scene)
                "addTemporaryActor Junimo 16 16 5 9 2 false character Junimo",
                "fade",                         // fade to black and HOLD (Event.Fade)
                "pause 700",
                "playSound junimoMeep1",
                "pause 400",
                $"speak Junimo \"{line}\"",
                "pause 500",
                "playSound junimoMeep1",
                "pause 900",
                "end"
            });
        }
    }
}
```

- [ ] **Step 2: Build the mod project, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Integration/Day28CutsceneInjector.cs
git commit -m "feat(day28): black-screen Junimo event-script injector (fade + speak)"
```

---

### Task 4: RunController — route the gate to `PendingCutscene`, add `OnCutsceneEnded`

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`

This replaces the old `_pendingReset` bool with a `Day28Branch` flag, defers the morning to the driver, and adds the continuation the driver calls. The FAIL continuation reuses the existing `TryOpenShrineThenContinue(ContinueAfterResetSpend)`; CONTINUE just runs `DoDayStartSeasonAndHub`.

- [ ] **Step 1: Add the using for Core.Day28**

In `RunController.cs`, after the existing `using CoreSeason = TheLongestYear.Core.Season;` (line 8), add:

```csharp
using TheLongestYear.Core.Day28;
```

- [ ] **Step 2: Replace the `_pendingReset` field with a `PendingCutscene` flag**

Replace (lines ~31):

```csharp
        private bool _pendingReset;
```

with:

```csharp
        /// <summary>Which day-28 bedtime cutscene is queued for the next morning (set in
        /// OnDayEnding from the gate's RunAction). The <see cref="Day28CutsceneDriver"/> reads this,
        /// plays the in-bed Junimo scene, and calls <see cref="OnCutsceneEnded"/> when it ends.
        /// None = no cutscene (normal day). Replaces the old _pendingReset bool: the reset now runs
        /// AFTER the FAIL cutscene's JP shop instead of straight out of OnDayStarted.</summary>
        private Day28Branch _pendingCutscene = Day28Branch.None;

        /// <summary>Exposed for the driver's per-tick decision.</summary>
        public Day28Branch PendingCutscene => _pendingCutscene;
```

- [ ] **Step 3: Update `OnDayStarted` to defer to the driver**

Replace the `_pendingReset` block in `OnDayStarted` (lines ~166-177):

```csharp
            if (_pendingReset)
            {
                _pendingReset = false;
                // 2026-05-29 spec: JP-spend popup pops on every natural loop reset so the
                // player can dump banked JP on whatever upgrades they want active for the
                // next loop. Menu's exitFunction continues with PerformReset + the rest of
                // OnDayStarted. If the menu can't open (active event / existing menu), fall
                // through to immediate reset — UX miss but never a lost reset.
                // Manual tly_reset intentionally NOT routed through here (debug stays raw).
                TryOpenShrineThenContinue(ContinueAfterResetSpend);
                return;
            }
```

with:

```csharp
            if (_pendingCutscene != Day28Branch.None)
            {
                // A day-28 outcome is queued. The Day28CutsceneDriver plays the in-bed Junimo
                // scene this morning (UpdateTicked), then calls OnCutsceneEnded() to open the
                // JP shop + reset (Fail) or roll into the next season (Continue). Suppress the
                // normal season-sync/hub flow until the scene resolves — same shape as the old
                // _pendingReset early-return.
                return;
            }
```

- [ ] **Step 4: Update `OnDayEnding` to set the branch**

In `OnDayEnding`, replace the `FailReset` case (lines ~366-369):

```csharp
                case RunAction.FailReset:
                    AwardInterimJp("run failed");
                    _pendingReset = true;
                    break;
```

with:

```csharp
                case RunAction.FailReset:
                    AwardInterimJp("run failed");
                    _pendingCutscene = Day28Branch.Fail;
                    break;
```

And replace the `AdvanceMonth` case (lines ~362-364):

```csharp
                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    break; // game advances the date; OnDayStarted clears the month's selections
```

with:

```csharp
                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    // Queue the "great job, next season" Junimo cutscene for the morning. The
                    // game still advances the date; OnCutsceneEnded → DoDayStartSeasonAndHub
                    // clears the month's selections and opens the planning hub after the scene.
                    _pendingCutscene = Day28Branch.Continue;
                    break;
```

- [ ] **Step 5: Add `OnCutsceneEnded` (called by the driver)**

Add this method immediately after `TryOpenShrineThenContinue` (after line ~256, before `DebugForceFailReset`):

```csharp
        /// <summary>Called by <see cref="Day28CutsceneDriver"/> when the day-28 bedtime cutscene
        /// has finished. Clears the pending branch and runs its continuation:
        /// FAIL → JP shop, then on close PerformReset + forced full save (ContinueAfterResetSpend);
        /// CONTINUE → roll straight into the next season's day-start flow (no shop, no reset).</summary>
        public void OnCutsceneEnded()
        {
            Day28Branch branch = _pendingCutscene;
            _pendingCutscene = Day28Branch.None;

            switch (branch)
            {
                case Day28Branch.Fail:
                    TryOpenShrineThenContinue(ContinueAfterResetSpend);
                    break;
                case Day28Branch.Continue:
                    DoDayStartSeasonAndHub();
                    break;
                case Day28Branch.None:
                default:
                    // Defensive: driver fired with nothing queued. Fall back to the normal flow
                    // so the morning is never stranded.
                    DoDayStartSeasonAndHub();
                    break;
            }
        }
```

- [ ] **Step 6: Route the debug command through the cutscene**

Replace `DebugForceFailReset` (lines ~264-268):

```csharp
        public void DebugForceFailReset()
        {
            _monitor.Log("tly_failreset: simulating a day-28 gate-miss reset (shrine then reset).", LogLevel.Info);
            TryOpenShrineThenContinue(ContinueAfterResetSpend);
        }
```

with:

```csharp
        public void DebugForceFailReset()
        {
            _monitor.Log("tly_failreset: queuing the day-28 FAIL cutscene (Junimo → shrine → reset).", LogLevel.Info);
            // Set the pending branch so the Day28CutsceneDriver plays the real bedtime scene this
            // tick (the driver polls UpdateTicked, not just DayStarted), exercising the full
            // cutscene → shop → reset → forced-save path from anywhere. tly_reset stays raw.
            _pendingCutscene = Day28Branch.Fail;
        }
```

- [ ] **Step 7: Build, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings, no remaining reference to `_pendingReset`.

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs
git commit -m "feat(day28): route gate RunAction to PendingCutscene + OnCutsceneEnded continuation"
```

---

### Task 5: RunController — forced full save after reset (closes the save-folder churn)

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`

`ContinueAfterResetSpend` already does `PerformReset → BeginNewRun → _store.Save()`. Append a forced full `SaveGame.Save` so the on-disk save (folder + inner files, freshly renamed to the new uniqueID inside `PerformReset`) holds the consistent Spring 1 world immediately — instead of waiting for the next natural sleep (batch-B notes §4). `SaveGame.Save()` is an enumerator that spins a background task and yields until done, so we drain it.

- [ ] **Step 1: Insert the forced-save call**

In `ContinueAfterResetSpend`, the tail currently reads (lines ~284-286):

```csharp
            _store.Save();
            _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
            DoDayStartSeasonAndHub();
```

Replace with:

```csharp
            _store.Save();
            ForceFullSave();
            _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
            DoDayStartSeasonAndHub();
```

- [ ] **Step 2: Add the `ForceFullSave` method**

Add immediately after `ContinueAfterResetSpend` (before `DoDayStartSeasonAndHub`):

```csharp
        /// <summary>Write a full game save right after the in-place reset so the on-disk save —
        /// whose folder + inner files PerformReset just renamed to the new uniqueID — holds the
        /// consistent Spring 1 world NOW, closing the rename window the reset would otherwise leave
        /// open until the next natural sleep (batch-B notes §4). <c>SaveGame.Save()</c> is an
        /// enumerator that runs the write on a background task and yields until done, so we drain
        /// it to completion. Guarded: SaveGame.Save no-ops while an event/minigame is up, and any
        /// failure degrades to the existing inner-file-rename mitigation (the save stays loadable;
        /// the next sleep rewrites it) rather than aborting the reset.</summary>
        private void ForceFullSave()
        {
            try
            {
                if (Game1.eventUp || Game1.currentMinigame != null)
                {
                    _monitor.Log(
                        "Day-28 save: skipped (event/minigame active). Save stays loadable via the " +
                        "inner-file rename; the next sleep will rewrite it.",
                        LogLevel.Warn);
                    return;
                }

                var save = StardewValley.SaveGame.Save();
                while (save.MoveNext()) { }

                _monitor.Log(
                    "Day-28 save: full save written post-reset — save folder is now consistent at Spring 1.",
                    LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                _monitor.Log(
                    $"Day-28 save: forced save failed ({ex.Message}). Save remains loadable via the " +
                    "inner-file rename; the next natural sleep will rewrite it.",
                    LogLevel.Warn);
            }
        }
```

- [ ] **Step 3: Build, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs
git commit -m "feat(day28): force a full save after reset to close the save-folder churn"
```

---

### Task 6: Glue — `Day28CutsceneDriver`

**Files:**
- Create: `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`

Mirrors `IntroSequenceDriver`: subscribe `UpdateTicked`, use the pure decider, start the forced event on a settled frame, run the continuation when it ends. Re-arm is implicit — when `RunController.PendingCutscene` returns to `None` (after `OnCutsceneEnded`), `_started` resets, so the driver also works for the mid-day `tly_failreset` debug path (no reliance on DayStarted). A `Bump()` cooldown after start covers the few ticks before `Game1.eventUp` flips, so the decider never mis-reads the just-started event as "ended."

- [ ] **Step 1: Write the implementation**

Create `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`:

```csharp
using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core.Day28;
using TheLongestYear.Loop;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Plays the day-28 bedtime Junimo cutscene the vanilla way, mirroring
    /// <see cref="IntroSequenceDriver"/>: on a settled morning frame, if
    /// <see cref="RunController.PendingCutscene"/> is set, it starts ONE forced event
    /// (<see cref="Day28CutsceneInjector.BuildEvent"/>); when that event ends it calls
    /// <see cref="RunController.OnCutsceneEnded"/> (FAIL → shop+reset, CONTINUE → next season).
    ///
    /// Re-arm is implicit: OnCutsceneEnded clears PendingCutscene to None, which resets
    /// <c>_started</c> on the next tick — so this also drives the mid-day <c>tly_failreset</c>
    /// debug path, not just real morning resets.
    /// </summary>
    internal sealed class Day28CutsceneDriver
    {
        private readonly IMonitor _monitor;
        private Func<RunController> _runController;

        private bool _started;
        private int _cooldownUntilTick;

        public Day28CutsceneDriver(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Subscribe once (from ModEntry.Entry). The RunController is built later on save
        /// load, so it's resolved through a thunk — same pattern as the intro driver's launcher.</summary>
        public void Attach(IModHelper helper, Func<RunController> runController)
        {
            _runController = runController;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            RunController rc = _runController?.Invoke();
            if (rc == null) return;

            Day28Branch branch = rc.PendingCutscene;
            if (branch == Day28Branch.None)
            {
                _started = false; // idle / re-arm for the next pending episode
                return;
            }

            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Before we start, wait for the morning wake-fade to settle (starting during a fade
            // fights the engine's player placement — the intro lesson). After we start, the
            // event's own `fade` is expected, so don't bail on it.
            if (!_started && Game1.fadeToBlackAlpha > 0f) return;
            if (Game1.ticks < _cooldownUntilTick) return;

            bool eventActive = Game1.eventUp || Game1.eventOver
                || Game1.currentLocation?.currentEvent != null;

            switch (Day28CutsceneDecider.Next(new Day28Snapshot(branch, _started, eventActive)))
            {
                case Day28Action.StartCutscene:
                    GameLocation loc = Game1.currentLocation;
                    if (loc != null && Game1.activeClickableMenu == null)
                    {
                        _monitor.Log($"Day-28 cutscene: starting the {branch} Junimo scene.", LogLevel.Info);
                        loc.startEvent(new Event(
                            Day28CutsceneInjector.BuildEvent(branch),
                            null,
                            Day28CutsceneContent.EventId));
                        _started = true;
                        Bump();
                    }
                    break;

                case Day28Action.RunContinuation:
                    _monitor.Log("Day-28 cutscene: scene ended — running the continuation.", LogLevel.Info);
                    rc.OnCutsceneEnded(); // clears PendingCutscene → _started resets next tick
                    break;

                case Day28Action.Waiting:
                case Day28Action.None:
                default:
                    break;
            }
        }

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30; // ~0.5s at 60fps
    }
}
```

- [ ] **Step 2: Build, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Integration/Day28CutsceneDriver.cs
git commit -m "feat(day28): cutscene driver (poll → start forced event → run continuation)"
```

---

### Task 7: ModEntry — wire the driver

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

Construct + attach the driver ONCE in `Entry` (mirroring the intro driver at lines 80-81), resolving the RunController through a thunk because it's built later on save load. Subscribing once in `Entry` avoids the double-subscription that would happen if it were attached in `OnSaveLoaded` (which reruns per save load).

- [ ] **Step 1: Add the field**

Next to the existing `private IntroSequenceDriver _introDriver;` (line ~37), add:

```csharp
        private Day28CutsceneDriver _day28Driver;
```

- [ ] **Step 2: Construct + attach in `Entry`**

Immediately after the existing intro-driver wiring (lines ~80-81):

```csharp
            _introDriver = new IntroSequenceDriver(this.Monitor, _meta, _config);
            _introDriver.Attach(helper, () => _launcher);
```

add:

```csharp
            _day28Driver = new Day28CutsceneDriver(this.Monitor);
            _day28Driver.Attach(helper, () => _runController);
```

(`Day28CutsceneDriver` is in the `TheLongestYear.Integration` namespace, same as `IntroSequenceDriver` — confirm ModEntry already has `using TheLongestYear.Integration;`; if not, add it.)

- [ ] **Step 3: Build, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "feat(day28): wire Day28CutsceneDriver in ModEntry (attach once in Entry)"
```

---

### Task 8: Full build + test gate

**Files:** none (verification only)

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test`
Expected: PASS — all prior tests plus the 8 new `Day28CutsceneDeciderTests` (≥429 total), 0 failures.

- [ ] **Step 2: Full compile-only build, confirm 0 warnings**

Run: `dotnet build -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 Warning(s).

- [ ] **Step 3: Grep for leftover `_pendingReset`**

Run (PowerShell): confirm no stale references remain.
Expected: only the `PendingCutscene`/`_pendingCutscene` names appear; no `_pendingReset`.

- [ ] **Step 4: Commit any cleanup (if needed)**

```bash
git add -A
git commit -m "chore(day28): build + test gate green"
```

---

## Deploy + playtest (post-implementation — separate from the coding tasks)

Not a coding task; handled after the plan executes. Kill SMAPI, `dotnet build` (deploy flags on), relaunch `StardewModdingAPI.exe`, confirm the SMAPI log shows the mod loaded clean (Harmony patch-class count unchanged from 42, 0 failed). Then the **reserved meaningful playtest**:
- `tly_failreset` → watch the FAIL cutscene → JP shop → reset → confirm a **single consistent save** on disk (new id, Spring 1, no orphan/dupe folder) via the `Day-28 save:` log line.
- Sleep a real Spring/Summer/Fall 28 with the gate passed → CONTINUE cutscene → next season, no shop, no extra save.
- On that same real reset, verify last session's reset-dependent fixes: recipe wipe→baseline+banked, FarmHouse furniture (bed + fireplace), event-gating (seen scenes don't replay; Demetrius ~Spring 5; furnace only when unbanked), Keep Horse carry-over.

---

## Self-review

**Spec coverage:**
- FAIL branch (rewind → shop → reset → save): Tasks 2 (dialogue), 3 (script), 4 (routing + OnCutsceneEnded), 5 (forced save). ✓
- CONTINUE branch (congrats → next season, no shop): Tasks 2, 3, 4 (`AdvanceMonth` → `Continue`, `OnCutsceneEnded` → `DoDayStartSeasonAndHub`). ✓
- WIN untouched: Task 4 leaves the `Win`/`_pendingWinChoice` case unchanged. ✓
- Black screen + Junimo dialogue, no farmer sprite: Task 3 (`fade` + `speak Junimo`, no bed sprite). ✓
- Morning-after timing, settle before start, unskippable: Tasks 3 (no `skippable`) + 6 (fade-settle guard, cooldown). ✓
- Driver mirrors intro / pure decider in Core: Tasks 1, 6. ✓
- No cross-loop suppression (fires every 28th): Task 2 doc + Task 6 implicit re-arm. ✓
- Not caught by EventSuppressionPatch: Task 2 doc (manual `startEvent` bypasses `checkEventPrecondition`). ✓
- `tly_failreset` exercises the cutscene: Task 4 Step 6. ✓
- Tests + 0 warnings: Tasks 1, 8. ✓

**Placeholder scan:** none — every code step has complete code; dialogue is final (wording flagged tweakable only for line breaks, which are already written).

**Type consistency:** `Day28Branch` {None,Fail,Continue}, `Day28Action` {None,Waiting,StartCutscene,RunContinuation}, `Day28Snapshot(Branch,Started,EventActive)`, `Day28CutsceneDecider.Next`, `Day28CutsceneContent.{EventId,FailDialogue,ContinueDialogue}`, `Day28CutsceneInjector.BuildEvent(Day28Branch)`, `RunController.{PendingCutscene,OnCutsceneEnded,ForceFullSave}`, `Day28CutsceneDriver.Attach(IModHelper, Func<RunController>)` — names match across Tasks 1-7. ✓
