# New-game Intro Sequence Rework — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On a fresh game, play the Lewis→Junimo cutscenes before the player controls the farmer, then wake on Spring 1 at 6am and open the theme picker — and stop the loop reset from re-dropping the starter parsnip seeds.

**Architecture:** Pure decision logic goes in `TheLongestYear.Core` (xUnit-tested); Game1/SMAPI/Harmony glue lives in the main `TheLongestYear` project and is runtime-verified via the SMAPI log (the test project references only Core, matching the codebase's existing split). A small `IntroSequenceDriver` orchestrates warps and lets the registered Data/Events entries auto-fire; a pure `IntroSequenceDecider` decides each step.

**Tech Stack:** C# / .NET 6, SMAPI, HarmonyX, xUnit. Spec: `docs/superpowers/specs/2026-05-30-intro-sequence-rework-design.md`.

**Commands:**
- Tests: `dotnet test TheLongestYear.sln -c Release`
- Build (SMAPI closed): `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
- Build (SMAPI running): add `-p:EnableModDeploy=false -p:EnableModZip=false`
- Every commit ends with: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

## File structure

- **Create** `src/TheLongestYear.Core/Intro/IntroEventKeys.cs` — single source of truth for the intro event ids, mail-flag names, and the precondition-keyed event keys. Pure strings, no game refs.
- **Create** `src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs` — `IntroGate.IsFreshIntroMorning(...)` + `IntroSequenceDecider.Next(IntroSnapshot)` returning an `IntroAction`. Pure.
- **Create** `tests/TheLongestYear.Tests/IntroEventKeysTests.cs` and `tests/TheLongestYear.Tests/IntroSequenceDeciderTests.cs`.
- **Modify** `src/TheLongestYear/Integration/IntroEventInjector.cs` — use `IntroEventKeys` for ids/flags/keys (delete the local copies).
- **Create** `src/TheLongestYear/Integration/IntroSequenceDriver.cs` — UpdateTicked-driven orchestrator; calls the Core decider, performs warps, opens the picker at the end.
- **Modify** `src/TheLongestYear/ModEntry.cs` — construct + wire the driver.
- **Modify** `src/TheLongestYear/Loop/RunController.cs` — suppress the day-1 auto-open of the hub on the fresh-intro morning.
- **Modify** `src/TheLongestYear/Loop/WorldResetService.cs` — remove the starter gift box after `loadForNewGame`.
- **Modify** `src/TheLongestYear/Loop/StandardFarmEnforcer.cs` — also force-skip + hide the skip-intro toggle (broaden to "new-game CharacterCustomization enforcer").

---

## Task 1: Centralise intro keys in Core + fix the broken preconditions

**Files:**
- Create: `src/TheLongestYear.Core/Intro/IntroEventKeys.cs`
- Test: `tests/TheLongestYear.Tests/IntroEventKeysTests.cs`
- Modify: `src/TheLongestYear/Integration/IntroEventInjector.cs`

Background: the live keys use `D 1` (=`Dating`), `s spring` (=`Shipped`), `m <flag>`/`!m <flag>` (=`EarnedMoney`) — all wrong, so neither event can fire. Correct keys: `u 1` (DayOfMonth), `Season spring`, `n <flag>` (has-mail), `!n <flag>` (not-mail).

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/IntroEventKeysTests.cs`:

```csharp
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Tests;

public class IntroEventKeysTests
{
    [Fact]
    public void PorchKey_uses_valid_preconditions()
    {
        // u=DayOfMonth, Season=spring, n=has-mail, !n=not-mail. Must NOT use D/s/m (Dating/Shipped/EarnedMoney).
        Assert.Equal(
            "tly_intro_porch/u 1/Season spring/!n tly_intro_porch_seen/!n tly_intro_done",
            IntroEventKeys.PorchKey);
    }

    [Fact]
    public void CcKey_gates_on_porch_seen_via_mail()
    {
        Assert.Equal(
            "tly_intro_cc/n tly_intro_porch_seen/!n tly_intro_cc_seen/!n tly_intro_done",
            IntroEventKeys.CcKey);
    }

    [Fact]
    public void Keys_contain_no_legacy_letter_preconditions()
    {
        foreach (var key in new[] { IntroEventKeys.PorchKey, IntroEventKeys.CcKey })
        {
            Assert.DoesNotContain("/D ", key);
            Assert.DoesNotContain("/s ", key);
            Assert.DoesNotContain("m " + IntroEventKeys.PorchSeenMail, key);
        }
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: FAIL — `IntroEventKeys` does not exist (compile error).

- [ ] **Step 3: Create the Core type**

Create `src/TheLongestYear.Core/Intro/IntroEventKeys.cs`:

```csharp
namespace TheLongestYear.Core.Intro
{
    /// <summary>
    /// Single source of truth for the day-1 narrative intro: event ids, the per-run /
    /// cross-run mail-flag names, and the fully-formed Data/Events keys (id + preconditions).
    ///
    /// Precondition letters matter: <c>u</c>=DayOfMonth, <c>Season</c>=season, <c>n</c>=has-mail,
    /// <c>!n</c>=not-mail. The original keys used <c>D</c>/<c>s</c>/<c>m</c>, which the game maps to
    /// Dating/Shipped/EarnedMoney — so both events silently failed every precondition check and
    /// never fired. Do NOT reintroduce single-letter D/s/m here.
    /// </summary>
    public static class IntroEventKeys
    {
        public const string PorchEventId = "tly_intro_porch";
        public const string CcEventId    = "tly_intro_cc";

        public const string PorchSeenMail = "tly_intro_porch_seen";
        public const string CcSeenMail     = "tly_intro_cc_seen";
        public const string IntroDoneMail  = "tly_intro_done";

        // Porch: Spring 1, porch not seen this run, intro not done cross-run.
        public static string PorchKey =>
            $"{PorchEventId}/u 1/Season spring/!n {PorchSeenMail}/!n {IntroDoneMail}";

        // CC: gated on the porch event having fired this run (forward-only chain), cc not seen, intro not done.
        public static string CcKey =>
            $"{CcEventId}/n {PorchSeenMail}/!n {CcSeenMail}/!n {IntroDoneMail}";
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS (3 new tests; total = 362).

- [ ] **Step 5: Point IntroEventInjector at the Core keys**

In `src/TheLongestYear/Integration/IntroEventInjector.cs`:
1. Add `using TheLongestYear.Core.Intro;` at the top.
2. Delete the local `private const string PorchEventId/CcEventId/PorchSeenMail/CcSeenMail/IntroDoneMail` declarations.
3. Replace every use of those locals with `IntroEventKeys.PorchEventId`, `IntroEventKeys.PorchSeenMail`, etc. (the `ApplyMailFlagsForRun`, `MarkIntroSeenIfApplicable`, `ClearIntroState`, and `OnAssetRequested` bodies all reference them).
4. Replace the two key-builder methods so they return the Core keys:

```csharp
private static string PorchEventKey() => IntroEventKeys.PorchKey;
private static string CcEventKey()    => IntroEventKeys.CcKey;
```

(Leave the `PorchEventScript()` / `CcEventScript()` bodies unchanged.)

- [ ] **Step 6: Build the mod, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear.Core/Intro/IntroEventKeys.cs tests/TheLongestYear.Tests/IntroEventKeysTests.cs src/TheLongestYear/Integration/IntroEventInjector.cs
git commit -m "fix(intro): correct event preconditions (u/Season/n), centralise keys in Core

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Pure intro-sequence decider in Core

**Files:**
- Create: `src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs`
- Test: `tests/TheLongestYear.Tests/IntroSequenceDeciderTests.cs`

Defines the fresh-intro gate (shared by the driver and RunController) and the step decider used by the driver glue. Locations are passed as plain strings so Core stays game-free.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/IntroSequenceDeciderTests.cs`:

```csharp
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Tests;

public class IntroSequenceDeciderTests
{
    private static IntroSnapshot Fresh(bool porch = false, bool cc = false, bool eventActive = false) =>
        new IntroSnapshot(
            HasSeenIntro: false, Season: Season.Spring, DayOfMonth: 1,
            PorchSeen: porch, CcSeen: cc, EventActive: eventActive);

    [Fact]
    public void IsFreshIntroMorning_true_only_on_spring1_unseen()
    {
        Assert.True(IntroGate.IsFreshIntroMorning(hasSeenIntro: false, Season.Spring, 1));
        Assert.False(IntroGate.IsFreshIntroMorning(hasSeenIntro: true, Season.Spring, 1));
        Assert.False(IntroGate.IsFreshIntroMorning(hasSeenIntro: false, Season.Spring, 2));
        Assert.False(IntroGate.IsFreshIntroMorning(hasSeenIntro: false, Season.Summer, 1));
    }

    [Fact]
    public void AlreadySeen_yields_None()
    {
        var s = Fresh() with { HasSeenIntro = true };
        Assert.Equal(IntroAction.None, IntroSequenceDecider.Next(s));
    }

    [Fact]
    public void Fresh_no_flags_starts_porch()
        => Assert.Equal(IntroAction.StartPorch, IntroSequenceDecider.Next(Fresh()));

    [Fact]
    public void Event_active_waits()
        => Assert.Equal(IntroAction.Waiting, IntroSequenceDecider.Next(Fresh(eventActive: true)));

    [Fact]
    public void Porch_seen_warps_to_cc()
        => Assert.Equal(IntroAction.WarpToCc, IntroSequenceDecider.Next(Fresh(porch: true)));

    [Fact]
    public void Both_seen_opens_picker()
        => Assert.Equal(IntroAction.OpenPicker, IntroSequenceDecider.Next(Fresh(porch: true, cc: true)));
}
```

- [ ] **Step 2: Run tests, verify they fail**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: FAIL — `IntroSnapshot` / `IntroAction` / `IntroGate` / `IntroSequenceDecider` undefined.

- [ ] **Step 3: Create the Core decider**

Create `src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs`:

```csharp
namespace TheLongestYear.Core.Intro
{
    /// <summary>What the driver should do this tick.</summary>
    public enum IntroAction
    {
        None,        // not a fresh-intro context — let the normal flow run
        Waiting,     // a cutscene is playing — do nothing
        StartPorch,  // warp to the Farm so the porch (Lewis) event fires
        WarpToCc,    // porch done — warp into the Community Center so the Junimo event fires
        OpenPicker   // both cutscenes done — warp home and open the theme picker
    }

    /// <summary>Immutable snapshot of the inputs the decider needs (no game refs).</summary>
    public readonly record struct IntroSnapshot(
        bool HasSeenIntro,
        Season Season,
        int DayOfMonth,
        bool PorchSeen,
        bool CcSeen,
        bool EventActive);

    /// <summary>Shared gate: is this the one morning the intro chain should own?</summary>
    public static class IntroGate
    {
        public static bool IsFreshIntroMorning(bool hasSeenIntro, Season season, int dayOfMonth)
            => !hasSeenIntro && season == Season.Spring && dayOfMonth == 1;
    }

    /// <summary>Pure step machine. Mail flags (PorchSeen/CcSeen) are the progression state.</summary>
    public static class IntroSequenceDecider
    {
        public static IntroAction Next(IntroSnapshot s)
        {
            if (!IntroGate.IsFreshIntroMorning(s.HasSeenIntro, s.Season, s.DayOfMonth))
                return IntroAction.None;
            if (s.EventActive)
                return IntroAction.Waiting;
            if (!s.PorchSeen)
                return IntroAction.StartPorch;
            if (!s.CcSeen)
                return IntroAction.WarpToCc;
            return IntroAction.OpenPicker;
        }
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS (6 new tests; total = 368).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs tests/TheLongestYear.Tests/IntroSequenceDeciderTests.cs
git commit -m "feat(intro): pure intro-sequence decider + fresh-morning gate in Core

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: IntroSequenceDriver (glue) + wiring

**Files:**
- Create: `src/TheLongestYear/Integration/IntroSequenceDriver.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`

Not unit-tested (touches `Game1`/SMAPI — test project can't load those). Verified at runtime via the SMAPI log. Relies on Task 1's valid preconditions so the registered Data/Events entries auto-fire when the farmer is warped into the Farm / Community Center.

- [ ] **Step 1: Create the driver**

Create `src/TheLongestYear/Integration/IntroSequenceDriver.cs`:

```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Orchestrates the day-1 intro on a fresh run: warps the farmer to the Farm so the porch
    /// (Lewis) event auto-fires, then into the Community Center so the Junimo event fires, then
    /// back to the farmhouse where it opens the theme picker. The events position their own
    /// actors and warp targets; this driver only sequences them and detects completion via the
    /// per-run mail flags + <see cref="Game1.eventUp"/>. Decision logic lives in the pure
    /// <see cref="IntroSequenceDecider"/>; this class is the Game1 glue.
    ///
    /// Activates only when <see cref="IntroGate.IsFreshIntroMorning"/> holds and the world is
    /// interactive (no fade/minigame/menu), so it never runs on the black save-creation screen.
    /// </summary>
    internal sealed class IntroSequenceDriver
    {
        private const int FarmTileX = 64, FarmTileY = 15;   // just outside the farmhouse door; porch event repositions the farmer
        private const int CcTileX = 32,  CcTileY = 22;      // CC south door; CC event repositions the farmer
        private const int HomeTileX = 9, HomeTileY = 9;     // farmhouse bed area

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly GameplayConfig _config;
        private System.Func<MenuLauncher> _launcher;

        private bool _finished;
        private int _cooldownUntilTick;   // debounce warps so we don't re-fire mid-fade

        public IntroSequenceDriver(IMonitor monitor, MetaStore meta, GameplayConfig config)
        {
            _monitor = monitor;
            _meta = meta;
            _config = config;
        }

        public void Attach(IModHelper helper, System.Func<MenuLauncher> launcher)
        {
            _launcher = launcher;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Re-arm for a (possibly replayed) fresh morning.
            if (IntroGate.IsFreshIntroMorning(_meta.State.HasSeenIntro, _meta.Run.Season, _meta.Run.DayOfMonth))
                _finished = false;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_config.Enabled || _finished) return;
            if (!Context.IsWorldReady) return;
            // Wait for an interactive frame so we never act on the black load screen.
            if (Game1.fadeToBlackAlpha > 0f || Game1.currentMinigame != null) return;
            if (Game1.ticks < _cooldownUntilTick) return;

            var p = Game1.player;
            if (p == null) return;

            var snap = new IntroSnapshot(
                HasSeenIntro: _meta.State.HasSeenIntro,
                Season: _meta.Run.Season,
                DayOfMonth: _meta.Run.DayOfMonth,
                PorchSeen: p.mailReceived.Contains(IntroEventKeys.PorchSeenMail),
                CcSeen:    p.mailReceived.Contains(IntroEventKeys.CcSeenMail),
                EventActive: Game1.eventUp || Game1.currentLocation?.currentEvent != null);

            switch (IntroSequenceDecider.Next(snap))
            {
                case IntroAction.StartPorch:
                    if (Game1.currentLocation?.Name != "Farm")
                    {
                        _monitor.Log("Intro: warping to Farm for the porch (Lewis) event.", LogLevel.Info);
                        Game1.warpFarmer("Farm", FarmTileX, FarmTileY, 2, false);
                        Bump();
                    }
                    break;

                case IntroAction.WarpToCc:
                    if (Game1.currentLocation?.Name != "CommunityCenter")
                    {
                        _monitor.Log("Intro: porch done — warping to the Community Center for the Junimo event.", LogLevel.Info);
                        Game1.warpFarmer("CommunityCenter", CcTileX, CcTileY, 0, false);
                        Bump();
                    }
                    break;

                case IntroAction.OpenPicker:
                    _monitor.Log("Intro: both cutscenes done — returning home and opening the theme picker.", LogLevel.Info);
                    if (Game1.currentLocation?.Name != "FarmHouse")
                    {
                        Game1.warpFarmer("FarmHouse", HomeTileX, HomeTileY, 2, false);
                        Bump();
                        return; // open the picker next tick, after the warp settles
                    }
                    _launcher?.Invoke()?.OpenWeeklyHub();
                    _finished = true;
                    break;

                case IntroAction.Waiting:
                case IntroAction.None:
                default:
                    break;
            }
        }

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30; // ~0.5s at 60fps
    }
}
```

Notes for the implementer:
- `Game1.fadeToBlackAlpha` and `Game1.currentMinigame` are public statics (verify against the PC runtime; if a name differs, the build error will say so — adjust, do not guess silently).
- The tile constants are first guesses; the events reposition the farmer anyway. If a warp lands the farmer somewhere the event can't start, adjust during the playtest.

- [ ] **Step 2: Wire the driver in ModEntry**

In `src/TheLongestYear/ModEntry.cs`:
1. Add a field near the other service fields (~line 36): `private IntroSequenceDriver _introDriver;`
2. In `Entry`, right after `_introInjector = new IntroEventInjector(...)` (~line 84), add:

```csharp
_introDriver = new IntroSequenceDriver(this.Monitor, _meta, _config);
_introDriver.Attach(helper, () => _launcher);
```

(The `() => _launcher` lazy accessor matches how `SeasonGoalsBoard.ConnectTo` already defers to `_launcher`, which isn't constructed until `OnSaveLoaded`.)

- [ ] **Step 3: Build, verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
Expected: Build succeeded, 0 warnings. (If a `Game1` member name is wrong for PC, fix it here against the decompile/build error.)

- [ ] **Step 4: Run the unit suite (no regressions)**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS (368).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Integration/IntroSequenceDriver.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(intro): IntroSequenceDriver orchestrates Lewis->Junimo->picker pre-control

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Defer the day-1 picker in RunController

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs` (`DoDayStartSeasonAndHub`, ~lines 307-324)

The driver opens the picker at the end of the chain, so the existing week-start auto-open must NOT also fire on the fresh-intro morning. Uses the same Core gate.

- [ ] **Step 1: Add the suppression guard**

In `DoDayStartSeasonAndHub`, change the week-start block so it skips when it's the fresh-intro morning. Add `using TheLongestYear.Core.Intro;` if not present. Replace:

```csharp
            if (Calendar.IsWeekStart(Run.DayOfMonth)
                && Run.OfferPresentedWeek != Run.WeekOfYear)
            {
                PresentOffer(targetWeekOfYear: Run.WeekOfYear);
            }
```

with:

```csharp
            // On the fresh-run intro morning the IntroSequenceDriver opens the picker at the end
            // of the Lewis->Junimo chain. Suppress the normal auto-open here so it doesn't pop on
            // the black save-creation screen (and so it isn't opened twice).
            bool introWillOpenPicker = IntroGate.IsFreshIntroMorning(
                _store.State.HasSeenIntro, Run.Season, Run.DayOfMonth);

            if (!introWillOpenPicker
                && Calendar.IsWeekStart(Run.DayOfMonth)
                && Run.OfferPresentedWeek != Run.WeekOfYear)
            {
                PresentOffer(targetWeekOfYear: Run.WeekOfYear);
            }
```

(Confirm the field name for the MetaStore on `RunController` — it is `_store` per existing uses like `_store.State.VictoryAcknowledged`. `Run` is the existing `RunState` accessor.)

- [ ] **Step 2: Build + test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; tests PASS (368).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs
git commit -m "fix(intro): suppress day-1 hub auto-open on the fresh-intro morning

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Strip the starter parsnip gift box on reset

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (`PerformReset`, after `loadForNewGame` at ~line 136)

`PerformReset` only runs on loop resets / `tly_reset`, never on the genuine first new game, so removing the box here unconditionally = first-loop-only.

- [ ] **Step 1: Add gift-box removal helper + call it**

In `PerformReset`, immediately after `Game1.game1.loadForNewGame(loadedGame: false);` (line 136), insert:

```csharp
            // First-loop-only starting seeds: loadForNewGame rebuilds the FarmHouse, whose
            // constructor (AddStarterGiftBox) drops a starter gift box of 15 parsnip seeds.
            // FarmerReset wipes the inventory but not this placed box, so it reappears every
            // loop. PerformReset only runs on resets (never the first new game), so removing it
            // here means run 1 keeps the vanilla nudge and every loop after gets none.
            RemoveStarterGiftBox();
```

Then add this private method to the class (place it near the other helpers):

```csharp
        /// <summary>Remove the vanilla starter gift box (15 parsnip seeds) that the rebuilt
        /// FarmHouse drops on every loadForNewGame. Identified by Chest.giftbox; the starter box
        /// is the giftbox sitting in the FarmHouse (the only one placed at construction).</summary>
        private void RemoveStarterGiftBox()
        {
            var farmHouse = Game1.getLocationFromName("FarmHouse");
            if (farmHouse == null) return;

            var toRemove = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();
            foreach (var kv in farmHouse.objects.Pairs)
            {
                if (kv.Value is StardewValley.Objects.Chest c && c.giftbox.Value)
                    toRemove.Add(kv.Key);
            }
            foreach (var tile in toRemove)
                farmHouse.objects.Remove(tile);

            if (toRemove.Count > 0)
                _monitor.Log($"In-place reset: removed {toRemove.Count} starter gift box(es) from the FarmHouse (first-loop-only seeds).", LogLevel.Info);
            else
                _monitor.Log("In-place reset: no starter gift box found in the FarmHouse to remove.", LogLevel.Trace);
        }
```

Notes: confirm `Chest.giftbox` is the right member on the PC runtime (the Android decompile constructs the starter box with `giftbox: true`). If the property name differs, the build error will surface it — adjust against the decompile. `farmHouse.objects` is an `OverlaidDictionary` exposing `.Pairs` and `.Remove(Vector2)` (used elsewhere in this file).

- [ ] **Step 2: Build + test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; tests PASS (368).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs
git commit -m "fix(reset): strip the starter parsnip gift box on loop reset (first loop only)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Force-skip vanilla intro + hide the toggle

**Files:**
- Modify: `src/TheLongestYear/Loop/StandardFarmEnforcer.cs`

Mirror the existing farm-type scrub: while no save is loaded and a `CharacterCustomization` is open, reflect the skip-intro state on + hide the button. Reuses the class's existing `FindCharacterCustomization` + per-instance tracking, so this just extends `ScrubFarmTypeOptions`. Runtime-verified (reflection; no unit test, same as the farm-type scrub).

- [ ] **Step 1: Update the class summary**

Change the `<summary>` opener so it reads "Forces every new TLY game onto the Standard farm **and skips the intro cutscene**, by scrubbing the relevant options out of `CharacterCustomization`." (One-line doc edit — keep the rest.)

- [ ] **Step 2: Add the skip-intro scrub**

At the end of `ScrubFarmTypeOptions(IClickableMenu cc)` (just before the final `Game1.whichFarm = 0;`), add a call `ForceSkipIntro(type, cc);` and add this method:

```csharp
        /// <summary>Force the skip-intro toggle on and hide its button so the vanilla bus-drive
        /// intro never plays — TLY's own Lewis->Junimo chain is the intro. PC field names differ
        /// from the Android decompile (MobileCustomizer.skipIntro / skipIntroButton), so reflect
        /// and log loudly if a field is absent.</summary>
        private void ForceSkipIntro(System.Type type, IClickableMenu cc)
        {
            FieldInfo skipFlag = type.GetField("skipIntro", FieldFlags);
            if (skipFlag != null && skipFlag.FieldType == typeof(bool))
                skipFlag.SetValue(cc, true);

            FieldInfo skipButton = type.GetField("skipIntroButton", FieldFlags);
            object button = skipButton?.GetValue(cc);
            if (button != null)
            {
                // ClickableComponent.visible is a public field; setting false hides + unsnaps it.
                FieldInfo visible = button.GetType().GetField("visible", FieldFlags);
                visible?.SetValue(button, false);
            }

            if (skipFlag == null && skipButton == null)
            {
                _monitor.Log(
                    "StandardFarmEnforcer: skipIntro/skipIntroButton not found on CharacterCustomization — " +
                    "field names may have changed; vanilla intro will play. Farm-type scrub is unaffected.",
                    LogLevel.Warn);
            }
            else
            {
                _monitor.Log("StandardFarmEnforcer: forced skip-intro on and hid the skip-intro button.", LogLevel.Info);
            }
        }
```

- [ ] **Step 3: Build + test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; tests PASS (368).

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/StandardFarmEnforcer.cs
git commit -m "feat(newgame): force-skip the vanilla intro and hide the skip-intro toggle

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Full build, deploy, and integration verification

**Files:** none (verification only).

- [ ] **Step 1: Full release build + full test suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build succeeded, 0 warnings; all tests PASS (368). Confirm the built DLL deployed to the Steam `Mods\TheLongestYear\` folder (or build with deploy on if SMAPI is closed).

- [ ] **Step 2: Launch + read the boot log**

After the user starts SMAPI, pull/read `TheLongestYear/SMAPI-latest.txt`. Confirm:
- `Harmony: N patch class(es) applied, 0 failed.`
- No `Unknown precondition` / `invalid event precondition` lines for `tly_intro_*`.
- `StandardFarmEnforcer: forced skip-intro on...` appears at character creation.

- [ ] **Step 3: New-game playtest (meaningful — user-run)**

Hand to the user. Expected, in order: hit Start → **no farm-type or skip-intro options shown** → **Lewis porch cutscene** → **Junimo CC cutscene** → wake on Spring 1, 6am, in the farmhouse → **theme picker** → control. No picker on a black screen; no free farmer movement before the cutscenes. Driver log shows `warping to Farm` → `warping to the Community Center` → `opening the theme picker` in order. Note any wrong tile placement (Lewis/Junimo) for a follow-up nudge.

- [ ] **Step 4: Reset playtest — parsnip box gone**

Run `tly_reset` (or play to a Winter 28 loop). Confirm the FarmHouse has **no** starter gift box / parsnip seeds, and the log shows `removed N starter gift box(es)`. Confirm run 1 (pre-reset) still had them.

- [ ] **Step 5: Final commit if any tile/field fixes were made during verification**

```bash
git add -A
git commit -m "fix(intro): playtest adjustments (tile/field tweaks)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-review notes

- **Spec coverage:** Component 1 (preconditions)→Task 1; Component 2 (driver)→Tasks 2-3; Component 3 (picker defer)→Task 4; Component 4 (gift box)→Task 5; Component 5 (skip-intro)→Task 6; testing section→Task 7. All covered.
- **Type consistency:** `IntroEventKeys.{PorchKey,CcKey,PorchSeenMail,CcSeenMail,IntroDoneMail,PorchEventId,CcEventId}`, `IntroSnapshot`, `IntroAction`, `IntroGate.IsFreshIntroMorning`, `IntroSequenceDecider.Next` are defined in Tasks 1-2 and used consistently in Tasks 3-4.
- **Runtime-only items flagged:** driver glue, gift-box removal, and skip-intro scrub explicitly note PC-vs-decompile field risk and the build-error/log feedback loop instead of guessing.
