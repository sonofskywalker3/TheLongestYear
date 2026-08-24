# Keep-Bundles Hold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On every Fail night the player chooses, before the shrine, whether to keep the current bundle board for the next loop (first hold free, then 50/100/200/300 JP, counter resets on reshuffle), and the day-1 Junimo speech tells them up front that impossible-looking asks are expected and can be held.

**Architecture:** Two pure Core rules (`BundleHoldPricing`, `BundleHold`) mutate two new `MetaState` fields (`BundleSeedLoop`, `ConsecutiveHolds`). The bundle seed's loop input switches from `CompletedResets` to `MetaState.EffectiveBundleSeedLoop` at both call sites (reset generation and load-time manifest check). `RunController` inserts a vanilla question dialogue between the Fail cutscene and the shrine; the existing shrine watchdog is generalized to cover it.

**Tech Stack:** C# / .NET, SMAPI 4.x, Harmony (untouched), xUnit tests in `tests/TheLongestYear.Tests` (Core only, no game install needed).

**Spec:** `docs/superpowers/specs/2026-08-24-keep-bundles-hold-design.md`

## Global Constraints

- No em dashes in any new text (code comments, strings, docs, commit messages).
- Every commit bumps `manifest.json` `Version` by one patch (0.12.16 -> 0.12.17 -> ...). Check the current value before each bump; never reuse a version.
- Commit locally only. No push, no release, no Nexus edit.
- `git add` specific files only; never `git add .`.
- Run tests with: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`
- Build the mod with: `dotnet build TheLongestYear.sln -c Release`
- The i18n guard test (`I18nGuardTests`) fails the build if a `Strings.Get` key is missing from `src/TheLongestYear/i18n/default.json`. Add keys in the same task that uses them.
- `Strings.Get(key, tokens)` uses `{{name}}` placeholders in `default.json`.
- Never touch the save `None_443632257`; smoke tests use a throwaway clone.

## File map

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/MetaState.cs` | Add `BundleSeedLoop`, `ConsecutiveHolds`, `EffectiveBundleSeedLoop` |
| `src/TheLongestYear.Core/GameplayConfig.cs` | Add `BundleHoldCosts` |
| `src/TheLongestYear.Core/BundleHoldPricing.cs` (new) | Price for the next hold |
| `src/TheLongestYear.Core/BundleHold.cs` (new) | Apply keep/reshuffle to MetaState |
| `src/TheLongestYear/Loop/WorldResetService.cs` | Seed uses effective loop |
| `src/TheLongestYear/ModEntry.cs` | Load-time seed uses effective loop; `tly_hold` command; `tly_genbundles` default |
| `src/TheLongestYear/Loop/RunController.cs` | Hold prompt on Fail nights; generalized watchdog |
| `src/TheLongestYear/Integration/IntroEventInjector.cs` | junimo-9b speak line |
| `src/TheLongestYear/UI/SeasonGoalsMenu.cs` | "Held (loop N)" title suffix |
| `src/TheLongestYear/i18n/default.json` | New keys; em dash purge |
| `docs/CHANGELOG.md`, `README.md`, `docs/nexus-description.bbcode` | Release notes (What's New) |
| `tests/TheLongestYear.Tests/*` | Tests per task |

---

### Task 1: MetaState fields

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs` (after `BundlesGeneratedForReset`, line ~57)
- Test: `tests/TheLongestYear.Tests/MetaStateTests.cs`

**Interfaces:**
- Produces: `int MetaState.BundleSeedLoop` (default -1), `int MetaState.ConsecutiveHolds` (default 0), `bool MetaState.HoldChoiceMadeForReset` (default false), `int MetaState.EffectiveBundleSeedLoop` (computed, read-only).

- [ ] **Step 1: Write the failing tests**

Append to `tests/TheLongestYear.Tests/MetaStateTests.cs` inside the class:

```csharp
    [Fact]
    public void BundleSeedLoop_defaults_to_minus_one_and_round_trips()
    {
        Assert.Equal(-1, new MetaState().BundleSeedLoop);
        Assert.Equal(0, new MetaState().ConsecutiveHolds);
        Assert.False(new MetaState().HoldChoiceMadeForReset);

        var original = new MetaState { BundleSeedLoop = 3, ConsecutiveHolds = 2, HoldChoiceMadeForReset = true };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.Equal(3, restored.BundleSeedLoop);
        Assert.Equal(2, restored.ConsecutiveHolds);
        Assert.True(restored.HoldChoiceMadeForReset);
    }

    [Fact]
    public void EffectiveBundleSeedLoop_falls_back_to_CompletedResets_when_unset()
    {
        var legacy = new MetaState { CompletedResets = 5 };
        Assert.Equal(5, legacy.EffectiveBundleSeedLoop);

        var held = new MetaState { CompletedResets = 5, BundleSeedLoop = 2 };
        Assert.Equal(2, held.EffectiveBundleSeedLoop);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~MetaStateTests"`
Expected: build error, `BundleSeedLoop` not defined.

- [ ] **Step 3: Add the fields**

In `src/TheLongestYear.Core/MetaState.cs`, directly after the `BundlesGeneratedForReset` property:

```csharp
    /// <summary>The loop number the CURRENT bundle board's seed derives from
    /// (<see cref="BundleEngineSeed.For"/>'s second argument). Normally equals
    /// <see cref="CompletedResets"/>; stays behind it while the player holds the board across
    /// resets (keep-bundles hold, spec 2026-08-24). -1 = never set (legacy saves), which
    /// <see cref="EffectiveBundleSeedLoop"/> resolves to CompletedResets.</summary>
    public int BundleSeedLoop { get; set; } = -1;

    /// <summary>How many times in a row the player has held the board at a Fail night. Drives
    /// the hold price (first hold free). Reset to 0 whenever they let the board reshuffle.</summary>
    public int ConsecutiveHolds { get; set; }

    /// <summary>True between the Fail-night hold choice and the reset that consumes it, so
    /// PerformReset can tell "player chose" from "reset arrived some other way" (console
    /// tly_reset, post-win new loop), which must reshuffle. Cleared by PerformReset.</summary>
    public bool HoldChoiceMadeForReset { get; set; }

    /// <summary>The loop number to seed bundle generation with: <see cref="BundleSeedLoop"/>
    /// when set, else <see cref="CompletedResets"/>. Both the reset-time generation and the
    /// load-time manifest re-check MUST use this, or a held board is flagged stale on reload.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveBundleSeedLoop => BundleSeedLoop >= 0 ? BundleSeedLoop : CompletedResets;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~MetaStateTests"`
Expected: all pass.

- [ ] **Step 5: Bump version and commit**

Edit `manifest.json` `"Version"` to the next patch. Then:

```bash
git add src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/MetaStateTests.cs manifest.json
git commit -m "v0.12.17: MetaState.BundleSeedLoop + ConsecutiveHolds for the keep-bundles hold"
```

---

### Task 2: Hold price curve

**Files:**
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs` (after `PoolTuning`, line ~45)
- Create: `src/TheLongestYear.Core/BundleHoldPricing.cs`
- Test: `tests/TheLongestYear.Tests/BundleHoldPricingTests.cs`

**Interfaces:**
- Produces: `List<long> GameplayConfig.BundleHoldCosts` (default `[0, 50, 100, 200, 300]`); `static long BundleHoldPricing.CostFor(int consecutiveHolds, IReadOnlyList<long> curve)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/BundleHoldPricingTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleHoldPricingTests
{
    private static readonly long[] Default = { 0, 50, 100, 200, 300 };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(2, 100)]
    [InlineData(3, 200)]
    [InlineData(4, 300)]
    [InlineData(9, 300)]
    public void Default_curve_first_free_then_escalates_and_caps(int holds, long expected)
    {
        Assert.Equal(expected, BundleHoldPricing.CostFor(holds, Default));
    }

    [Fact]
    public void Config_default_matches_spec_curve()
    {
        Assert.Equal(Default, new GameplayConfig().BundleHoldCosts);
    }

    [Fact]
    public void Custom_curve_is_honoured_and_last_value_repeats()
    {
        var curve = new long[] { 10, 20 };
        Assert.Equal(10, BundleHoldPricing.CostFor(0, curve));
        Assert.Equal(20, BundleHoldPricing.CostFor(1, curve));
        Assert.Equal(20, BundleHoldPricing.CostFor(5, curve));
    }

    [Fact]
    public void Empty_curve_is_free()
    {
        Assert.Equal(0, BundleHoldPricing.CostFor(3, System.Array.Empty<long>()));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~BundleHoldPricingTests"`
Expected: build error, `BundleHoldPricing` / `BundleHoldCosts` not defined.

- [ ] **Step 3: Implement**

In `src/TheLongestYear.Core/GameplayConfig.cs`, after the `PoolTuning` property:

```csharp
    /// <summary>JP price of holding the bundle board across a Fail-night reset, indexed by how
    /// many holds the player has taken in a row (index 0 = first hold). The last value repeats.
    /// Reshuffling resets the counter. Spec 2026-08-24 keep-bundles hold.</summary>
    public List<long> BundleHoldCosts { get; set; } = new() { 0, 50, 100, 200, 300 };
```

Create `src/TheLongestYear.Core/BundleHoldPricing.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Price of the NEXT keep-bundles hold, from the config curve. Pure.</summary>
public static class BundleHoldPricing
{
    public static long CostFor(int consecutiveHolds, IReadOnlyList<long> curve)
    {
        if (curve.Count == 0) return 0;
        int index = Math.Clamp(consecutiveHolds, 0, curve.Count - 1);
        return Math.Max(0, curve[index]);
    }
}
```

(If `GameplayConfig.cs` lacks `using System.Collections.Generic;`, the project's implicit usings cover it; check the existing `Dictionary<string, string>` properties compile the same way.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~BundleHoldPricingTests"`
Expected: 9 pass.

- [ ] **Step 5: Bump version and commit**

```bash
git add src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear.Core/BundleHoldPricing.cs tests/TheLongestYear.Tests/BundleHoldPricingTests.cs manifest.json
git commit -m "v0.12.18: BundleHoldPricing curve (first free, 50/100/200/300 cap) + config knob"
```

---

### Task 3: BundleHold rule

**Files:**
- Create: `src/TheLongestYear.Core/BundleHold.cs`
- Test: `tests/TheLongestYear.Tests/BundleHoldTests.cs`

**Interfaces:**
- Consumes: `MetaState.BundleSeedLoop/ConsecutiveHolds/EffectiveBundleSeedLoop` (Task 1), `BundleHoldPricing.CostFor` (Task 2).
- Produces: `enum BundleHold.HoldResult { Kept, Reshuffled, NotEnoughJp }`; `static HoldResult BundleHold.Apply(MetaState state, bool keep, IReadOnlyList<long> curve)`; `static long BundleHold.NextCost(MetaState state, IReadOnlyList<long> curve)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/BundleHoldTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleHoldTests
{
    private static readonly long[] Curve = { 0, 50, 100, 200, 300 };

    [Fact]
    public void First_hold_is_free_and_pins_seed_loop_to_current()
    {
        var s = new MetaState { CompletedResets = 2, JunimoPoints = 10 };

        var result = BundleHold.Apply(s, keep: true, Curve);

        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(10, s.JunimoPoints);
        Assert.Equal(1, s.ConsecutiveHolds);
        Assert.Equal(2, s.BundleSeedLoop);
        Assert.Equal(2, s.EffectiveBundleSeedLoop);
    }

    [Fact]
    public void Second_hold_costs_fifty_and_keeps_seed_loop()
    {
        var s = new MetaState { CompletedResets = 3, JunimoPoints = 80, BundleSeedLoop = 2, ConsecutiveHolds = 1 };

        var result = BundleHold.Apply(s, keep: true, Curve);

        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(30, s.JunimoPoints);
        Assert.Equal(2, s.ConsecutiveHolds);
        Assert.Equal(2, s.BundleSeedLoop);
    }

    [Fact]
    public void NotEnoughJp_leaves_state_untouched()
    {
        var s = new MetaState { CompletedResets = 3, JunimoPoints = 20, BundleSeedLoop = 2, ConsecutiveHolds = 1 };

        var result = BundleHold.Apply(s, keep: true, Curve);

        Assert.Equal(BundleHold.HoldResult.NotEnoughJp, result);
        Assert.Equal(20, s.JunimoPoints);
        Assert.Equal(1, s.ConsecutiveHolds);
        Assert.Equal(2, s.BundleSeedLoop);
    }

    [Fact]
    public void Reshuffle_resets_counter_and_advances_seed_loop_to_upcoming()
    {
        var s = new MetaState { CompletedResets = 3, JunimoPoints = 500, BundleSeedLoop = 1, ConsecutiveHolds = 2 };

        var result = BundleHold.Apply(s, keep: false, Curve);

        Assert.Equal(BundleHold.HoldResult.Reshuffled, result);
        Assert.Equal(500, s.JunimoPoints);
        Assert.Equal(0, s.ConsecutiveHolds);
        Assert.Equal(4, s.BundleSeedLoop);   // CompletedResets + 1 = the loop PerformReset is about to create
    }

    [Fact]
    public void Reshuffle_on_legacy_save_advances_from_CompletedResets()
    {
        var s = new MetaState { CompletedResets = 3 };   // BundleSeedLoop = -1
        BundleHold.Apply(s, keep: false, Curve);
        Assert.Equal(4, s.BundleSeedLoop);
    }

    [Fact]
    public void NextCost_reads_the_curve_at_the_current_counter()
    {
        Assert.Equal(0, BundleHold.NextCost(new MetaState(), Curve));
        Assert.Equal(200, BundleHold.NextCost(new MetaState { ConsecutiveHolds = 3 }, Curve));
    }

    [Fact]
    public void Hold_after_reshuffle_is_free_again()
    {
        var s = new MetaState { CompletedResets = 5, JunimoPoints = 10, BundleSeedLoop = 1, ConsecutiveHolds = 3 };
        BundleHold.Apply(s, keep: false, Curve);
        s.CompletedResets = 6;   // PerformReset bumped it
        var result = BundleHold.Apply(s, keep: true, Curve);
        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(10, s.JunimoPoints);
        Assert.Equal(6, s.BundleSeedLoop);
    }

    [Fact]
    public void Both_answers_stamp_the_choice_flag_but_NotEnoughJp_does_not()
    {
        var kept = new MetaState { JunimoPoints = 0 };
        BundleHold.Apply(kept, keep: true, Curve);
        Assert.True(kept.HoldChoiceMadeForReset);

        var shuffled = new MetaState();
        BundleHold.Apply(shuffled, keep: false, Curve);
        Assert.True(shuffled.HoldChoiceMadeForReset);

        var broke = new MetaState { ConsecutiveHolds = 1, JunimoPoints = 0 };
        BundleHold.Apply(broke, keep: true, Curve);
        Assert.False(broke.HoldChoiceMadeForReset);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~BundleHoldTests"`
Expected: build error, `BundleHold` not defined.

- [ ] **Step 3: Implement**

Create `src/TheLongestYear.Core/BundleHold.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>The pure rule for the Fail-night "hold the town's wishes" choice (spec 2026-08-24).
/// Called BEFORE WorldResetService.PerformReset bumps CompletedResets. Mutates MetaState only
/// on Kept / Reshuffled; persistence is FinalizeReset's existing _store.Save().</summary>
public static class BundleHold
{
    public enum HoldResult
    {
        /// <summary>Board kept: JP deducted, ConsecutiveHolds incremented, BundleSeedLoop pinned.</summary>
        Kept,
        /// <summary>Board will reshuffle: counter reset, BundleSeedLoop advanced to the upcoming loop.</summary>
        Reshuffled,
        /// <summary>Player cannot afford this hold; nothing changed.</summary>
        NotEnoughJp
    }

    /// <summary>Price the player would pay to hold right now.</summary>
    public static long NextCost(MetaState state, IReadOnlyList<long> curve)
        => BundleHoldPricing.CostFor(state.ConsecutiveHolds, curve);

    public static HoldResult Apply(MetaState state, bool keep, IReadOnlyList<long> curve)
    {
        if (!keep)
        {
            state.ConsecutiveHolds = 0;
            state.BundleSeedLoop = state.CompletedResets + 1;
            state.HoldChoiceMadeForReset = true;
            return HoldResult.Reshuffled;
        }

        long cost = NextCost(state, curve);
        if (state.JunimoPoints < cost)
            return HoldResult.NotEnoughJp;

        state.JunimoPoints -= cost;
        state.BundleSeedLoop = state.EffectiveBundleSeedLoop;   // materialize -1 to the current loop
        state.ConsecutiveHolds += 1;
        state.HoldChoiceMadeForReset = true;
        return HoldResult.Kept;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~BundleHoldTests"`
Expected: 8 pass.

- [ ] **Step 5: Bump version and commit**

```bash
git add src/TheLongestYear.Core/BundleHold.cs tests/TheLongestYear.Tests/BundleHoldTests.cs manifest.json
git commit -m "v0.12.19: BundleHold.Apply pure rule (keep deducts/pins, reshuffle resets/advances)"
```

---

### Task 4: Seed call sites use the effective loop

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs:473`
- Modify: `src/TheLongestYear/ModEntry.cs:1928` (ResolveRequirements), `:1447-1459` (CmdGenBundles default)
- Test: `tests/TheLongestYear.Tests/BundleHoldSeedTests.cs`

**Interfaces:**
- Consumes: `MetaState.EffectiveBundleSeedLoop` (Task 1), `BundleHold.Apply` (Task 3), `BundleEngineSeed.For`.

- [ ] **Step 1: Write the failing test (Core-level proof the two sites agree)**

Create `tests/TheLongestYear.Tests/BundleHoldSeedTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Models the reset-then-reload sequence: the seed PerformReset generates with must equal
/// the seed ResolveRequirements re-derives with on the next load, whether or not the board was held.</summary>
public class BundleHoldSeedTests
{
    private const ulong Basis = 0x1234_5678_9ABC_DEF0UL;
    private static readonly long[] Curve = { 0, 50 };

    private static int SeedAtReset(MetaState s)
    {
        s.CompletedResets += 1;                                  // PerformReset step 11
        s.BundlesGeneratedForReset = s.CompletedResets;          // step 11a marker
        return BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);
    }

    private static int SeedAtLoad(MetaState s)
    {
        Assert.Equal(RequirementsSource.EngineManifest,
            EngineModeDecider.Decide(s.BundlesGeneratedForReset, s.CompletedResets, ccTouched: false));
        return BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);
    }

    [Fact]
    public void Held_board_regenerates_from_the_same_seed_as_the_previous_loop()
    {
        var s = new MetaState { CompletedResets = 1, BundlesGeneratedForReset = 1, JunimoPoints = 0 };
        int loop1Seed = BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);

        BundleHold.Apply(s, keep: true, Curve);
        int resetSeed = SeedAtReset(s);
        int loadSeed = SeedAtLoad(s);

        Assert.Equal(loop1Seed, resetSeed);
        Assert.Equal(resetSeed, loadSeed);
        Assert.Equal(2, s.CompletedResets);
    }

    [Fact]
    public void Reshuffled_board_uses_the_new_loop_seed_and_reloads_identically()
    {
        var s = new MetaState { CompletedResets = 1, BundlesGeneratedForReset = 1 };
        int loop1Seed = BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);

        BundleHold.Apply(s, keep: false, Curve);
        int resetSeed = SeedAtReset(s);
        int loadSeed = SeedAtLoad(s);

        Assert.NotEqual(loop1Seed, resetSeed);
        Assert.Equal(BundleEngineSeed.For(Basis, 2), resetSeed);
        Assert.Equal(resetSeed, loadSeed);
    }

    [Fact]
    public void Legacy_save_without_hold_fields_behaves_exactly_as_before()
    {
        var s = new MetaState { CompletedResets = 4, BundlesGeneratedForReset = 4 };
        Assert.Equal(BundleEngineSeed.For(Basis, 4), SeedAtLoad(s));
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~BundleHoldSeedTests"`
Expected: all three PASS already (Tasks 1-3 supply the rule). This test exists to pin the contract that the two mod-side call sites in Steps 3-4 must follow; the mod project has no unit tests, so the seed expression is verified by inspection plus the Task 10 reload smoke.

- [ ] **Step 3: Switch the reset-time seed**

In `src/TheLongestYear/Loop/WorldResetService.cs` line 473, change:

```csharp
                int seed = BundleEngineSeed.For(unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.CompletedResets);
```
to
```csharp
                // Keep-bundles hold (spec 2026-08-24): the seed loop is EffectiveBundleSeedLoop, which
                // RunController's Fail-night choice already pinned (hold) or advanced to this loop
                // (reshuffle) before we got here. Legacy saves resolve to CompletedResets.
                int seed = BundleEngineSeed.For(unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
```

Also add a log line right after `engine.WriteToWorld(generatedSet, _monitor);`:

```csharp
                _monitor.Log(
                    $"Reset: bundle seed loop {_meta.EffectiveBundleSeedLoop} (CompletedResets {_meta.CompletedResets}, consecutive holds {_meta.ConsecutiveHolds}).",
                    LogLevel.Info);
```

Guard against a Fail path that never ran the choice (console `tly_reset`, the post-win "new loop" path): immediately before the `if (vanillaBoard)` at line 462 add:

```csharp
            // A reset that skipped the Fail-night hold choice (console tly_reset, post-win new loop)
            // must behave like a reshuffle. RunController stamps HoldChoiceMadeForReset = true on
            // either answer (via BundleHold.Apply); anything else lands here with it false.
            // Note: BundleSeedLoop itself can't be used for this check; two consecutive holds leave
            // it two loops behind CompletedResets, which is legitimate.
            if (!_meta.HoldChoiceMadeForReset)
            {
                _meta.BundleSeedLoop = _meta.CompletedResets;
                _meta.ConsecutiveHolds = 0;
            }
            _meta.HoldChoiceMadeForReset = false;
```

- [ ] **Step 4: Switch the load-time seed and the diagnostics default**

In `src/TheLongestYear/ModEntry.cs` `ResolveRequirements` (line 1928):

```csharp
                var seed = BundleEngineSeed.For(seedBasis, state.EffectiveBundleSeedLoop);
```

and in the success log at line 1947 change `(loop {state.CompletedResets}, ` to `(loop {state.CompletedResets}, seed loop {state.EffectiveBundleSeedLoop}, `.

In `CmdGenBundles` (line 1451-1453) change the default from `_meta.State.CompletedResets` to `_meta.State.EffectiveBundleSeedLoop` and update the usage string in the `ConsoleCommands.Add("tly_genbundles", ...)` registration (line 237) to say `Usage: tly_genbundles [seedLoop] (default: the current board's seed loop)`.

- [ ] **Step 5: Build the mod and run all tests**

Run: `dotnet build TheLongestYear.sln -c Release` then `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`
Expected: build clean, all tests pass (count = previous + new ones).

- [ ] **Step 6: Bump version and commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/BundleHoldSeedTests.cs manifest.json
git commit -m "v0.12.20: bundle seed uses EffectiveBundleSeedLoop at reset and load; HoldChoiceMadeForReset guard"
```

---

### Task 5: Fail-night hold prompt in RunController

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs` (`OnCutsceneEnded` Fail case line 354-360; watchdog 322-340; add `ShowHoldChoice`)
- Modify: `src/TheLongestYear/i18n/default.json` (new `hold.*` keys; amend `cutscene.day28.fail`)

**Interfaces:**
- Consumes: `BundleHold.Apply/NextCost` (Task 3), `_config.BundleHoldCosts` (Task 2), `_store.State`, `TryOpenShrineThenContinue`, `ContinueAfterResetSpend`, `Strings.Get`.
- Produces: nothing new for later tasks.

- [ ] **Step 1: Add the strings**

In `src/TheLongestYear/i18n/default.json`, next to the `dialog.win.*` keys (line ~23), add:

```json
    "dialog.hold.prompt": "Should we hold the town's wishes steady for your next spring?",
    "dialog.hold.keep-free": "Keep these bundles (free)",
    "dialog.hold.keep": "Keep these bundles ({{cost}} JP)",
    "dialog.hold.reshuffle": "Let time reshuffle them",
    "dialog.hold.not-enough-jp": "The Junimos need {{cost}} JP to hold the town's wishes. You have {{have}}.",
```

Replace `cutscene.day28.fail` (line 396) with:

```json
    "cutscene.day28.fail": "At this pace we won't be able to restore the Community Center in time, @.#$b#So we will use our magic to rewind the year. Don't worry, we have enough power left over to give you a head-start this time.#$b#Before we do, tell us: should we hold the town's wishes steady, or let time reshuffle them?$h",
```

- [ ] **Step 2: Add ShowHoldChoice and route the Fail branch through it**

In `src/TheLongestYear/Loop/RunController.cs`, replace the Fail case body (lines 355-359) with:

```csharp
                case Day28Branch.Fail:
                    // Hide the day/time HUD across the choice -> shop -> reset so the stale
                    // (pre-rewind) calendar date isn't shown while the player decides and shops.
                    // ContinueAfterResetSpend restores it once the world is back on Spring 1.
                    Game1.displayHUD = false;
                    ShowHoldChoice();
                    break;
```

Add this method directly after `ApplyKeepPlaying` (line ~300):

```csharp
        /// <summary>Fail-night "hold the town's wishes" choice (spec 2026-08-24). Asked BEFORE the
        /// shrine so the player can't accidentally spend the JP they meant for the hold. Either
        /// answer runs BundleHold.Apply then continues into the shrine -> reset chain. If the
        /// dialogue is clobbered, the watchdog treats it as reshuffle (today's behaviour).</summary>
        private void ShowHoldChoice()
        {
            MetaState meta = _store.State;
            long cost = BundleHold.NextCost(meta, _config.BundleHoldCosts);
            string keepLabel = cost == 0
                ? Strings.Get("dialog.hold.keep-free")
                : Strings.Get("dialog.hold.keep", new Dictionary<string, string> { ["cost"] = cost.ToString() });
            var responses = new[]
            {
                new StardewValley.Response("keep",      keepLabel),
                new StardewValley.Response("reshuffle", Strings.Get("dialog.hold.reshuffle"))
            };

            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                _monitor.Log("Hold choice: no currentLocation available, defaulting to reshuffle.", LogLevel.Warn);
                ApplyHoldChoice(keep: false);
                return;
            }

            loc.createQuestionDialogue(Strings.Get("dialog.hold.prompt"), responses, (Farmer who, string key) =>
            {
                _menuWatch = null;
                if (key == "keep")
                {
                    BundleHold.HoldResult result = BundleHold.Apply(meta, keep: true, _config.BundleHoldCosts);
                    if (result == BundleHold.HoldResult.NotEnoughJp)
                    {
                        Game1.playSound("cancel");
                        Game1.addHUDMessage(new HUDMessage(Strings.Get("dialog.hold.not-enough-jp",
                            new Dictionary<string, string> { ["cost"] = cost.ToString(), ["have"] = meta.JunimoPoints.ToString() }), HUDMessage.error_type));
                        ShowHoldChoice();   // re-ask; the player can pick reshuffle
                        return;
                    }
                    _monitor.Log($"Hold choice: KEEP (cost {cost} JP, consecutive holds now {meta.ConsecutiveHolds}, seed loop {meta.BundleSeedLoop}).", LogLevel.Info);
                    Game1.playSound("junimoMeep1");
                    TryOpenShrineThenContinue(ContinueAfterResetSpend);
                    return;
                }
                ApplyHoldChoice(keep: false);
            });

            // Watchdog: if something replaces the question box before an answer, default to reshuffle.
            if (Game1.activeClickableMenu is StardewValley.Menus.DialogueBox box)
                _menuWatch = (box, () => ApplyHoldChoice(keep: false));
        }

        private void ApplyHoldChoice(bool keep)
        {
            BundleHold.HoldResult result = BundleHold.Apply(_store.State, keep, _config.BundleHoldCosts);
            _monitor.Log($"Hold choice: {result} (seed loop {_store.State.BundleSeedLoop}).", LogLevel.Info);
            TryOpenShrineThenContinue(ContinueAfterResetSpend);
        }
```

- [ ] **Step 3: Generalize the watchdog**

Rename `_shrineWatch` to `_menuWatch` everywhere in `RunController.cs` (declaration line 324, uses in `TryOpenShrineThenContinue` lines 310-313, and `TickShrineWatchdog` lines 332-339). Change the log text in `TickShrineWatchdog` to:

```csharp
            _monitor.Log(
                "A day-28 menu (hold choice or Junimo Shrine) was replaced before it closed normally; running its continuation now " +
                "(banked JP is untouched; spend it next time the shrine opens).", LogLevel.Warn);
```

Keep the method name `TickShrineWatchdog` (the driver calls it every tick; no need to touch `Day28CutsceneDriver`).

Add `using TheLongestYear.Core;` at the top of `RunController.cs` if `BundleHold` doesn't resolve (the file already uses `MetaState`, `VaultRules` etc., so it is probably present).

- [ ] **Step 4: Build and run tests**

Run: `dotnet build TheLongestYear.sln -c Release` then `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`
Expected: build clean; the i18n guard passes with the new `dialog.hold.*` keys; all tests pass.

- [ ] **Step 5: Bump version and commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/i18n/default.json manifest.json
git commit -m "v0.12.21: Fail-night keep/reshuffle hold prompt before the shrine, watchdog covers it"
```

---

### Task 6: Day-1 intro line and Season Goals "Held" suffix

**Files:**
- Modify: `src/TheLongestYear/Integration/IntroEventInjector.cs:143-145`
- Modify: `src/TheLongestYear/UI/SeasonGoalsMenu.cs:400-404`
- Modify: `src/TheLongestYear/i18n/default.json` (`event.intro.junimo-9b`, `menu.goals.title-held`)

**Interfaces:**
- Consumes: `MetaState.ConsecutiveHolds` (Task 1), `Strings.Get`.

- [ ] **Step 1: Add the strings**

In `default.json` after `event.intro.junimo-9` (line 391) insert:

```json
    "event.intro.junimo-9b": "Some of what a season asks may look beyond your reach. That is no mistake. It is the shape of the work.#$b#Gather what you can, and keep for later what you cannot yet use. When the year unwinds, we can even hold the town's wishes steady, so the next spring asks the same of you.$h",
```

After `menu.goals.title` (line 300) insert:

```json
    "menu.goals.title-held": "Season Goals: {{season}} (day {{day}}) held {{holds}}x",
```

- [ ] **Step 2: Inject the speak line**

In `src/TheLongestYear/Integration/IntroEventInjector.cs`, between the junimo-9 entry and the `"pause 300"` that precedes junimo-10 (lines 143-145), insert:

```csharp
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-9b")}\"",
```

so the sequence reads: `speak junimo-9`, `pause 300`, `speak junimo-9b`, `pause 300`, `speak junimo-10`.

- [ ] **Step 3: Title suffix**

In `src/TheLongestYear/UI/SeasonGoalsMenu.cs` replace lines 400-404 with:

```csharp
            var titleTokens = new Dictionary<string, string>
            {
                ["season"] = SeasonName(_season),
                ["day"] = _run.DayOfMonth.ToString(),
            };
            string title;
            if (_meta != null && _meta.ConsecutiveHolds > 0)
            {
                titleTokens["holds"] = _meta.ConsecutiveHolds.ToString();
                title = Strings.Get("menu.goals.title-held", titleTokens);
            }
            else
            {
                title = Strings.Get("menu.goals.title", titleTokens);
            }
```

- [ ] **Step 4: Build and run tests**

Run: `dotnet build TheLongestYear.sln -c Release` then `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`
Expected: clean, all pass.

- [ ] **Step 5: Bump version and commit**

```bash
git add src/TheLongestYear/Integration/IntroEventInjector.cs src/TheLongestYear/UI/SeasonGoalsMenu.cs src/TheLongestYear/i18n/default.json manifest.json
git commit -m "v0.12.22: Junimo intro line about held wishes; Season Goals title shows hold count"
```

---

### Task 7: `tly_hold` console command

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (registration block line ~237-249; dispatch switch line ~1353; new `CmdHold` near `CmdBuyUpgrade`)

**Interfaces:**
- Consumes: `BundleHold.Apply`, `_meta.State`, `_config.BundleHoldCosts`.

- [ ] **Step 1: Register**

In the `ConsoleCommands.Add` block add:

```csharp
            helper.ConsoleCommands.Add("tly_hold", "Debug: apply the Fail-night hold choice in memory without a fail night. Usage: tly_hold keep|reshuffle|status. keep deducts JP per the config curve; the next reset (tly_reset) then honours it.", this.CmdHold);
```

In the dispatch switch (near `case "tly_buyupgrade"`) add:

```csharp
                case "tly_hold": this.CmdHold(command, args); break;
```

- [ ] **Step 2: Implement**

Next to `CmdBuyUpgrade` add:

```csharp
        private void CmdHold(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "keep":
                case "reshuffle":
                    var result = BundleHold.Apply(s, keep: mode == "keep", _config.BundleHoldCosts);
                    this.Monitor.Log($"tly_hold {mode}: {result}. JP {s.JunimoPoints}, consecutive holds {s.ConsecutiveHolds}, seed loop {s.BundleSeedLoop}, choice stamped {s.HoldChoiceMadeForReset}.", LogLevel.Info);
                    break;
                default:
                    this.Monitor.Log($"tly_hold status: CompletedResets {s.CompletedResets}, seed loop {s.EffectiveBundleSeedLoop} (stored {s.BundleSeedLoop}), consecutive holds {s.ConsecutiveHolds}, next hold costs {BundleHold.NextCost(s, _config.BundleHoldCosts)} JP, choice stamped {s.HoldChoiceMadeForReset}.", LogLevel.Info);
                    break;
            }
        }
```

- [ ] **Step 3: Build**

Run: `dotnet build TheLongestYear.sln -c Release`
Expected: clean.

- [ ] **Step 4: Bump version and commit**

```bash
git add src/TheLongestYear/ModEntry.cs manifest.json
git commit -m "v0.12.23: tly_hold debug command (keep|reshuffle|status)"
```

---

### Task 8: Em dash purge of player-facing strings

**Files:**
- Modify: `src/TheLongestYear/i18n/default.json` (every line containing `—`)

- [ ] **Step 1: List them**

Run: `grep -n "—" src/TheLongestYear/i18n/default.json`
Expected: 29 lines (plus any added by mistake in Tasks 5-6; there should be none).

- [ ] **Step 2: Rewrite each line, meaning preserved**

Rules: replace ` — ` with `. ` when it joins two clauses, `, ` when it introduces an aside, `: ` when it introduces an explanation or list. Keep every `@`, `#$b#`, `$h`, `$s`, `$a`, `{{token}}`, `^` and `\n` exactly. Examples of the intended edits:

- `"event.intro.lewis-1": "... Welcome to the valley.#$b#I'm Lewis, mayor of Pelican Town. I came up to greet you and your new farm.$h"`
- `"event.intro.junimo-1": "Hello, @. We are the spirits of this land, the Junimos.#$b#Few folk can see us anymore.$h"`
- `"event.intro.junimo-3": "But the world grew busy. They chased coin and comfort, and forgot one another.#$b#..."`
- `"event.intro.junimo-4": "...miss what they traded away: the valley whole, and alive.#$b#That longing is what woke us... that, and you.$h"`
- `"event.intro.junimo-5": "We can make the land strong again, but not alone, and neither can you.#$b#..."`
- `"event.intro.junimo-6": "...Fall short, and the year unwinds to spring, and you begin again.$s"`
- `"event.intro.junimo-8": "When the year unwinds, all of it returns: the fields, the buildings, even the folk you have come to know.#$b#..."`
- `"menu.goals.title": "Season Goals: {{season}} (day {{day}})"`
- `"gmcm.cart-limit.tooltip"`: `Off = full vanilla cart; the Cart Stall upgrades do nothing.` (already dash-free; only edit the part with the dash).

Do the remaining lines the same way. Do not change keys.

- [ ] **Step 3: Verify none remain and tests pass**

Run: `grep -c "—" src/TheLongestYear/i18n/default.json` -> expected `0`.
Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release` -> all pass (the i18n guard only checks key presence; any test asserting exact string text will show up here; fix the expected text in that test, not the string).

- [ ] **Step 4: Bump version and commit**

```bash
git add src/TheLongestYear/i18n/default.json manifest.json
git commit -m "v0.12.24: remove em dashes from all player-facing strings"
```

---

### Task 9: Docs and release notes

**Files:**
- Modify: `docs/CHANGELOG.md` (new top entry), `README.md` (What's New), `docs/nexus-description.bbcode` (What's New, identical content)
- Modify: `TODO.md` (move the parked DerivePins note under a "0.13.x brainstorm" heading with the pity-counter idea; add the wording note that weekly goals point at season slots)

- [ ] **Step 1: CHANGELOG entry** (version = the manifest version after Task 8; the "0.13.0" label is the user's call at release time)

```markdown
## 0.12.24 (unreleased)

- New: on a Fail night the Junimos ask whether to hold the town's wishes (keep the same bundle board for the next loop) or let time reshuffle them. The first hold is free; holding again in a row costs 50, 100, 200, then 300 JP (config `BundleHoldCosts`). Reshuffling resets the price.
- New: the day-1 Junimo speech says up front that impossible-looking asks are expected and can be held across a rewind.
- Season Goals title shows how many times the board has been held.
- Text: removed all em dashes from in-game strings.
- Debug: `tly_hold keep|reshuffle|status`.
```

- [ ] **Step 2: README + Nexus What's New** with the same three player-facing bullets (README Markdown, Nexus BBCode), keeping the two files content-identical per the workspace rule.

- [ ] **Step 3: TODO.md** edits as listed in Files.

- [ ] **Step 4: Commit (no version bump for docs-only)**

```bash
git add docs/CHANGELOG.md README.md docs/nexus-description.bbcode TODO.md
git commit -m "docs: keep-bundles hold release notes; park DerivePins brainstorm with pity-counter idea"
```

---

### Task 10: Live smoke on a throwaway clone

**Files:** none (evidence in `test-output/pt-hold-*.png` and the SMAPI log)

- [ ] **Step 1: Deploy** with `tools/deploy.ps1` (then `git checkout -- test-output/log-archive/`, deploy prunes tracked logs). Clone `None_443632257` to a new `<Name>_<newId>` folder; never load the original.
- [ ] **Step 2: Fresh-run baseline:** load the clone, run `tly_hold status` -> seed loop = CompletedResets, next hold 0 JP.
- [ ] **Step 3: Hold (free):** `tly_hold keep` then `tly_reset`. Log must show `Reset: bundle seed loop N` with N unchanged from before and `BundleEngine: wrote 31 bundles`. Open the Season Goals board: same bundles as before the reset; title ends in `held 1x`. JP unchanged. Screenshot.
- [ ] **Step 4: Reload from title.** Log must show `Requirements source: engine manifest (loop N+1, seed loop N, ...)` and no `manifest mismatch` WARN.
- [ ] **Step 5: Hold (50 JP):** give JP if needed (`tly_testdonate` or an existing JP debug), `tly_hold keep` -> JP drops by 50, `tly_reset` -> same board again, title `held 2x`.
- [ ] **Step 6: Reshuffle:** `tly_hold reshuffle`, `tly_reset` -> different board, title has no `held`, `tly_hold status` shows next hold 0 JP.
- [ ] **Step 7: Real Fail night:** on the clone, sleep on Spring 28 with an unmet gate. Verify the order: cutscene -> hold question -> shrine -> reset, and that picking Keep with too little JP shows the HUD error and re-asks.
- [ ] **Step 8: Intro line:** start a brand-new farm on the clone profile (or `debug ebi` the intro event id) and confirm junimo-9b plays between 9 and 10.
- [ ] **Step 9:** delete the clone save folder; confirm `None_443632257` is untouched. Record results in `TODO.md` (table like the 2026-08-21 loop-reset smoke) and commit.
