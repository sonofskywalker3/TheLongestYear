# Season Pity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After five fails at the same season gate, each further fail eases that gate a little: a kept board gets a lower quota for that season, a reshuffled board leaves out the hardest eligible items.

**Architecture:** Pure rules live in `TheLongestYear.Core` (`SeasonPity`, `ItemHardness`, a `SeasonEase` adjustment applied in `GeneratedBundleSet.BuildRequirements`, a `PityTrim` applied in `BundleSlotFiller.Fill`). The mod project only records fails/passes, stamps the reshuffle trim into `MetaState` so a reload regenerates the identical board, and threads the two adjustments through `BundleEngine` at the existing call sites. Counting is always on; `PityEnabled=false` zeroes the effect.

**Tech Stack:** C# / .NET 6, SMAPI 4, xUnit (`tests/TheLongestYear.Tests`), `dotnet test TheLongestYear.sln`.

**Spec:** `docs/superpowers/specs/2026-08-25-season-pity-design.md`

## Global Constraints

- No em dashes anywhere (code comments, strings, docs, commit messages). Use a comma, colon or hyphen.
- Every task ends with a commit on `master` that also bumps `Version` in `src/TheLongestYear/manifest.json` by one patch (0.12.18 is current; Task 1 commits 0.12.19, Task 2 0.12.20, and so on). Commit locally only; never push or release without Jeff's explicit "yes, push".
- Player-facing strings live only in `src/TheLongestYear/i18n/default.json` and are read via `Strings.Get("key", ...)`; `I18nGuardTests` fails on any key referenced in source but missing from the file, or present in the file but unreachable from source.
- Core never references `Game1`/SMAPI types. Anything that needs the game goes in `src/TheLongestYear`.
- Season index: `Season.Spring = 0 .. Season.Winter = 3`; `Calendar.MonthsPerYear = 4`.
- Defaults from the spec, exact: `PityEnabled=true`, `PityThreshold=5`, `PityQuotaStep=0.10`, `PityQuotaFloor=0.50`, `PityTrimPerStep=2`.
- Applies to Engine (TLY Custom) boards only. Vanilla boards never see easing or trims (same guard as the keep-bundles hold: `BundleHold.IsOfferable`).
- Run tests with: `dotnet test TheLongestYear.sln -v quiet` from the project root (`C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`). 750 tests pass before this plan starts.

---

## File map

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/GameplayConfig.cs` (modify) | Five `Pity*` config keys. |
| `src/TheLongestYear.Core/MetaState.cs` (modify) | `SeasonFailCounts`, `LastFailSeason`, `BoardTrimSeason`, `BoardTrimSteps`. |
| `src/TheLongestYear.Core/SeasonPity.cs` (create) | Counting rules, ease steps, quota factor, trim count, reshuffle stamping, display steps. |
| `src/TheLongestYear.Core/SeasonEase.cs` (create) | `SeasonEase(Season, int Steps, double Factor)` record + `Apply` to a `BundleRequirement`. |
| `src/TheLongestYear.Core/GeneratedBundleSet.cs` (modify) | `BuildRequirements` gains an optional `SeasonEase?`. |
| `src/TheLongestYear.Core/ItemHardness.cs` (create) | Hardness score and deterministic pool trim. |
| `src/TheLongestYear.Core/PityTrim.cs` (create) | `PityTrim(Season, int Units)` record. |
| `src/TheLongestYear.Core/BundleSlotFiller.cs` (modify) | `Fill` gains optional `PityTrim?` + `RarityThresholds?`; quality-off and item trim. |
| `src/TheLongestYear.Core/BundleHold.cs` (modify) | `ConsumeChoiceAtReset` clears the trim stamp when no choice was made. |
| `src/TheLongestYear/Loop/BundleEngine.cs` (modify) | Threads `PityTrim` into `Fill` and `SeasonEase` into `BuildRequirements`. |
| `src/TheLongestYear/Loop/WorldResetService.cs` (modify) | Reset-time generation passes the stamped trim and the ease. |
| `src/TheLongestYear/ModEntry.cs` (modify) | Load-time regeneration passes the same; `tly_pity`; GMCM. |
| `src/TheLongestYear/Loop/RunController.cs` (modify) | Records fail/pass, stamps reshuffle trim, eased prompt text. |
| `src/TheLongestYear/UI/SeasonGoalsMenu.cs`, `UI/MenuLauncher.cs` (modify) | "eased Nx" title. |
| `src/TheLongestYear/Integration/IGenericModConfigMenuApi.cs` (modify) | `AddNumberOption` signatures. |
| `src/TheLongestYear/i18n/default.json` (modify) | New strings. |
| `tests/TheLongestYear.Tests/SeasonPityTests.cs`, `SeasonEaseTests.cs`, `ItemHardnessTests.cs` (create); `BundleSlotFillerTests.cs`, `GeneratedBundleSetTests.cs`, `BundleHoldTests.cs`, `MetaStateTests.cs` (modify) | Tests. |
| `CHANGELOG.md` (modify) | `## Unreleased` entry. |

---

### Task 1: Config keys, MetaState fields, SeasonPity rules

**Files:**
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs` (after `BundleHoldCosts`, line ~50)
- Modify: `src/TheLongestYear.Core/MetaState.cs` (after `HoldChoiceMadeForReset`, line ~73)
- Create: `src/TheLongestYear.Core/SeasonPity.cs`
- Test: `tests/TheLongestYear.Tests/SeasonPityTests.cs`, `tests/TheLongestYear.Tests/MetaStateTests.cs`

**Interfaces:**
- Produces: `GameplayConfig.PityEnabled/PityThreshold/PityQuotaStep/PityQuotaFloor/PityTrimPerStep`; `MetaState.SeasonFailCounts : List<int>`, `MetaState.LastFailSeason : int` (-1 none), `MetaState.BoardTrimSeason : int` (-1 none), `MetaState.BoardTrimSteps : int`; `SeasonPity.RecordFail(MetaState, Season)`, `SeasonPity.RecordPass(MetaState, Season, GameplayConfig)`, `SeasonPity.EaseSteps(MetaState, Season, GameplayConfig) : int`, `SeasonPity.QuotaFactor(int steps, GameplayConfig) : double`, `SeasonPity.TrimUnits(int steps, GameplayConfig) : int`, `SeasonPity.StampReshuffleTrim(MetaState, GameplayConfig)`, `SeasonPity.CurrentEase(MetaState, GameplayConfig) : SeasonEase?` (defined in Task 2; in this task return a tuple, see Step 3), `SeasonPity.DisplaySteps(MetaState, GameplayConfig) : int`.

- [ ] **Step 1: Write the failing tests**

`tests/TheLongestYear.Tests/SeasonPityTests.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SeasonPityTests
{
    private static GameplayConfig Cfg(bool enabled = true, int threshold = 5, double step = 0.10, double floor = 0.50, int trim = 2)
        => new() { PityEnabled = enabled, PityThreshold = threshold, PityQuotaStep = step, PityQuotaFloor = floor, PityTrimPerStep = trim };

    [Fact]
    public void RecordFail_increments_only_that_season_and_remembers_it()
    {
        var s = new MetaState();
        SeasonPity.RecordFail(s, Season.Summer);
        SeasonPity.RecordFail(s, Season.Summer);
        Assert.Equal(new List<int> { 0, 2, 0, 0 }, s.SeasonFailCounts);
        Assert.Equal((int)Season.Summer, s.LastFailSeason);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 1)]
    [InlineData(9, 4)]
    public void EaseSteps_is_fails_beyond_threshold(int fails, int expected)
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { fails, 0, 0, 0 } };
        Assert.Equal(expected, SeasonPity.EaseSteps(s, Season.Spring, Cfg()));
    }

    [Fact]
    public void EaseSteps_is_zero_when_disabled_but_counting_continues()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 8, 0, 0, 0 } };
        Assert.Equal(0, SeasonPity.EaseSteps(s, Season.Spring, Cfg(enabled: false)));
        SeasonPity.RecordFail(s, Season.Spring);
        Assert.Equal(9, s.SeasonFailCounts[0]);
    }

    [Fact]
    public void RecordPass_drops_to_threshold_never_raises()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 9, 2, 0, 0 } };
        SeasonPity.RecordPass(s, Season.Spring, Cfg());
        SeasonPity.RecordPass(s, Season.Summer, Cfg());
        Assert.Equal(5, s.SeasonFailCounts[0]);
        Assert.Equal(2, s.SeasonFailCounts[1]);
    }

    [Fact]
    public void QuotaFactor_steps_down_and_floors()
    {
        Assert.Equal(1.0, SeasonPity.QuotaFactor(0, Cfg()), 6);
        Assert.Equal(0.8, SeasonPity.QuotaFactor(2, Cfg()), 6);
        Assert.Equal(0.5, SeasonPity.QuotaFactor(9, Cfg()), 6);
    }

    [Fact]
    public void TrimUnits_scales_per_step()
    {
        Assert.Equal(0, SeasonPity.TrimUnits(0, Cfg()));
        Assert.Equal(6, SeasonPity.TrimUnits(3, Cfg()));
    }

    [Fact]
    public void StampReshuffleTrim_records_season_and_units_or_clears()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 0, 7, 0, 0 }, LastFailSeason = 1 };
        SeasonPity.StampReshuffleTrim(s, Cfg());
        Assert.Equal(1, s.BoardTrimSeason);
        Assert.Equal(4, s.BoardTrimSteps);   // (7-5) steps * 2 per step

        var none = new MetaState { SeasonFailCounts = new List<int> { 3, 0, 0, 0 }, LastFailSeason = 0 };
        SeasonPity.StampReshuffleTrim(none, Cfg());
        Assert.Equal(-1, none.BoardTrimSeason);
        Assert.Equal(0, none.BoardTrimSteps);
    }

    [Fact]
    public void Counts_pad_to_four_when_short_or_missing()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 2 } };
        Assert.Equal(0, SeasonPity.EaseSteps(s, Season.Winter, Cfg()));
        SeasonPity.RecordFail(s, Season.Winter);
        Assert.Equal(new List<int> { 2, 0, 0, 1 }, s.SeasonFailCounts);
    }

    [Fact]
    public void DisplaySteps_uses_quota_ease_when_held_else_board_trim()
    {
        var held = new MetaState { SeasonFailCounts = new List<int> { 7, 0, 0, 0 }, LastFailSeason = 0, ConsecutiveHolds = 1, BoardTrimSeason = -1 };
        Assert.Equal(2, SeasonPity.DisplaySteps(held, Cfg()));
        var shuffled = new MetaState { SeasonFailCounts = new List<int> { 7, 0, 0, 0 }, LastFailSeason = 0, ConsecutiveHolds = 0, BoardTrimSeason = 0, BoardTrimSteps = 4 };
        Assert.Equal(2, SeasonPity.DisplaySteps(shuffled, Cfg()));   // 4 units / 2 per step
        Assert.Equal(0, SeasonPity.DisplaySteps(new MetaState(), Cfg()));
    }
}
```

Append to `tests/TheLongestYear.Tests/MetaStateTests.cs` (inside the class):

```csharp
    [Fact]
    public void Pity_fields_round_trip_and_default()
    {
        var fresh = new MetaState();
        Assert.Equal(new[] { 0, 0, 0, 0 }, fresh.SeasonFailCounts);
        Assert.Equal(-1, fresh.LastFailSeason);
        Assert.Equal(-1, fresh.BoardTrimSeason);
        Assert.Equal(0, fresh.BoardTrimSteps);

        var original = new MetaState { SeasonFailCounts = new System.Collections.Generic.List<int> { 1, 6, 0, 0 }, LastFailSeason = 1, BoardTrimSeason = 1, BoardTrimSteps = 2 };
        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.Equal(new[] { 1, 6, 0, 0 }, restored.SeasonFailCounts);
        Assert.Equal(1, restored.LastFailSeason);
        Assert.Equal(1, restored.BoardTrimSeason);
        Assert.Equal(2, restored.BoardTrimSteps);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: build errors (`SeasonPity`, `PityEnabled`, `SeasonFailCounts` undefined).

- [ ] **Step 3: Implement**

`GameplayConfig.cs`, right after `BundleHoldCosts`:

```csharp
    /// <summary>Season pity (spec 2026-08-25): after <see cref="PityThreshold"/> fails at the SAME
    /// season, each further fail eases that season's gate. Counting always runs; this switch only
    /// zeroes the effect so it can be turned on later without losing history.</summary>
    public bool PityEnabled { get; set; } = true;

    /// <summary>Fails at one season before easing starts (the first N are standard difficulty).</summary>
    public int PityThreshold { get; set; } = 5;

    /// <summary>Quota reduction per ease step when the player KEEPS the board (0.10 = -10%).</summary>
    public double PityQuotaStep { get; set; } = 0.10;

    /// <summary>Lowest quota factor the keep-path easing can reach.</summary>
    public double PityQuotaFloor { get; set; } = 0.50;

    /// <summary>Hardest items removed from that season's slot pools per ease step when the player RESHUFFLES.</summary>
    public int PityTrimPerStep { get; set; } = 2;
```

`MetaState.cs`, right after `HoldChoiceMadeForReset`:

```csharp
    /// <summary>Fails recorded at each season gate, index = (int)Season. Drives season pity
    /// (spec 2026-08-25). Padded to four entries by <see cref="SeasonPity"/> on read.</summary>
    public List<int> SeasonFailCounts { get; set; } = new() { 0, 0, 0, 0 };

    /// <summary>The season index of the most recent Fail night, -1 before the first fail. The
    /// keep-path quota easing applies to this season only.</summary>
    public int LastFailSeason { get; set; } = -1;

    /// <summary>Season index whose slot pools were trimmed when the CURRENT board was rolled
    /// (reshuffle-path pity), -1 = no trim. Stamped by SeasonPity.StampReshuffleTrim before the
    /// reset generates; a reload must regenerate with the same values or the manifest check fails.</summary>
    public int BoardTrimSeason { get; set; } = -1;

    /// <summary>Trim units applied when the current board was rolled (see <see cref="BoardTrimSeason"/>).</summary>
    public int BoardTrimSteps { get; set; }
```

Create `src/TheLongestYear.Core/SeasonPity.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Pure rules for season pity (spec 2026-08-25): per-season fail counting, the ease
/// steps beyond the threshold, the keep-path quota factor and the reshuffle-path trim units.
/// Mutates MetaState only in RecordFail / RecordPass / StampReshuffleTrim.</summary>
public static class SeasonPity
{
    private const int NoSeason = -1;

    /// <summary>Ensures the list has exactly MonthsPerYear entries (old saves may be short or null).</summary>
    public static List<int> Counts(MetaState state)
    {
        state.SeasonFailCounts ??= new List<int>();
        while (state.SeasonFailCounts.Count < Calendar.MonthsPerYear)
            state.SeasonFailCounts.Add(0);
        return state.SeasonFailCounts;
    }

    public static void RecordFail(MetaState state, Season season)
    {
        Counts(state)[(int)season] += 1;
        state.LastFailSeason = (int)season;
    }

    public static void RecordPass(MetaState state, Season season, GameplayConfig config)
    {
        List<int> counts = Counts(state);
        int threshold = Math.Max(0, config.PityThreshold);
        counts[(int)season] = Math.Min(counts[(int)season], threshold);
    }

    public static int EaseSteps(MetaState state, Season season, GameplayConfig config)
    {
        if (!config.PityEnabled) return 0;
        return Math.Max(0, Counts(state)[(int)season] - Math.Max(0, config.PityThreshold));
    }

    public static double QuotaFactor(int steps, GameplayConfig config)
    {
        double step = Math.Clamp(config.PityQuotaStep, 0.0, 1.0);
        double floor = Math.Clamp(config.PityQuotaFloor, 0.0, 1.0);
        return Math.Max(floor, 1.0 - step * Math.Max(0, steps));
    }

    public static int TrimUnits(int steps, GameplayConfig config)
        => Math.Max(0, steps) * Math.Max(0, config.PityTrimPerStep);

    /// <summary>Called when the player lets time reshuffle on a Fail night, BEFORE the reset
    /// generates the new board. Records which season's pools get trimmed and by how much, or
    /// clears the stamp when no easing is due.</summary>
    public static void StampReshuffleTrim(MetaState state, GameplayConfig config)
    {
        int season = state.LastFailSeason;
        int units = season >= 0 && season < Calendar.MonthsPerYear
            ? TrimUnits(EaseSteps(state, (Season)season, config), config)
            : 0;
        state.BoardTrimSeason = units > 0 ? season : NoSeason;
        state.BoardTrimSteps = units;
    }

    /// <summary>Clears the reshuffle trim stamp (a reset that skipped the Fail-night choice).</summary>
    public static void ClearBoardTrim(MetaState state)
    {
        state.BoardTrimSeason = NoSeason;
        state.BoardTrimSteps = 0;
    }

    /// <summary>The keep-path quota easing in force for the current board: the last failed
    /// season and its ease steps, only while the board is held (ConsecutiveHolds > 0).
    /// (Season, Steps, Factor); Steps == 0 means no easing.</summary>
    public static (Season Season, int Steps, double Factor) CurrentQuotaEase(MetaState state, GameplayConfig config)
    {
        if (state.ConsecutiveHolds <= 0 || state.LastFailSeason < 0 || state.LastFailSeason >= Calendar.MonthsPerYear)
            return (Season.Spring, 0, 1.0);
        var season = (Season)state.LastFailSeason;
        int steps = EaseSteps(state, season, config);
        return (season, steps, QuotaFactor(steps, config));
    }

    /// <summary>Steps to show in the Season Goals title: the quota ease while held, else the
    /// trim stamped on the current board expressed in steps.</summary>
    public static int DisplaySteps(MetaState state, GameplayConfig config)
    {
        var ease = CurrentQuotaEase(state, config);
        if (ease.Steps > 0) return ease.Steps;
        if (state.BoardTrimSeason >= 0 && config.PityTrimPerStep > 0)
            return state.BoardTrimSteps / config.PityTrimPerStep;
        return 0;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass (750 + 10 new).

- [ ] **Step 5: Commit**

Bump `manifest.json` `Version` to `0.12.19`, then:

```bash
git add src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear.Core/MetaState.cs src/TheLongestYear.Core/SeasonPity.cs tests/TheLongestYear.Tests/SeasonPityTests.cs tests/TheLongestYear.Tests/MetaStateTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.12.19: season pity core rules, config keys, MetaState fields"
```

---

### Task 2: Keep-path quota easing in BuildRequirements

**Files:**
- Create: `src/TheLongestYear.Core/SeasonEase.cs`
- Modify: `src/TheLongestYear.Core/GeneratedBundleSet.cs:23-55`
- Modify: `src/TheLongestYear.Core/SeasonPity.cs` (`CurrentQuotaEase` returns `SeasonEase?`)
- Test: `tests/TheLongestYear.Tests/SeasonEaseTests.cs`, `tests/TheLongestYear.Tests/SeasonPityTests.cs`

**Interfaces:**
- Consumes: `SeasonPity.CurrentQuotaEase` tuple from Task 1 (replaced here).
- Produces: `public sealed record SeasonEase(Season Season, int Steps, double Factor)` with `static BundleRequirement Apply(BundleRequirement req, SeasonEase ease)`; `GeneratedBundleSet.BuildRequirements(pins, quotas, SeasonEase? ease = null)`; `SeasonPity.CurrentQuotaEase(MetaState, GameplayConfig) : SeasonEase?` (null when no easing).

- [ ] **Step 1: Write the failing tests**

`tests/TheLongestYear.Tests/SeasonEaseTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SeasonEaseTests
{
    private static BundleRequirement Percentage(int[] ramp) => BundleRequirement.CreatePercentage(
        "Artisan", Theme.Farming, new[] { "(O)1", "(O)2", "(O)3", "(O)4", "(O)5", "(O)6", "(O)7" }, 6, ramp);

    [Fact]
    public void Percentage_only_the_eased_season_drops_and_ramp_stays_monotonic()
    {
        var req = Percentage(new[] { 3, 4, 5, 6 });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Summer, 2, 0.8));
        // ceil(4 * 0.8) = 4 -> no change at 4; use 5 to see it: ceil(5*0.8)=4
        Assert.Equal(new[] { 3, 4, 5, 6 }, eased.CumulativeRequiredBySeason);

        var req2 = Percentage(new[] { 5, 5, 5, 6 });
        var eased2 = SeasonEase.Apply(req2, new SeasonEase(Season.Spring, 3, 0.7));
        Assert.Equal(new[] { 4, 5, 5, 6 }, eased2.CumulativeRequiredBySeason);   // ceil(5*0.7)=4, later seasons untouched
    }

    [Fact]
    public void Percentage_winter_still_demands_completion_and_floor_applies()
    {
        var req = Percentage(new[] { 6, 6, 6, 6 });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Winter, 9, 0.5));
        Assert.Equal(new[] { 6, 6, 6, 6 }, eased.CumulativeRequiredBySeason);   // Winter never eases
    }

    [Fact]
    public void Percentage_zero_stays_zero_and_nonzero_stays_at_least_one()
    {
        var req = Percentage(new[] { 0, 1, 3, 6 });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Summer, 5, 0.5));
        Assert.Equal(new[] { 0, 1, 3, 6 }, eased.CumulativeRequiredBySeason);
    }

    [Fact]
    public void PerItem_pins_due_in_eased_season_slide_one_per_step_capped_at_winter()
    {
        var req = BundleRequirement.CreatePerItem("Blacksmith", Theme.Mining, new Dictionary<string, Season>
        {
            ["(O)334"] = Season.Spring, ["(O)335"] = Season.Summer, ["(O)336"] = Season.Fall,
        });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Spring, 2, 0.8));
        Assert.Equal(Season.Fall, eased.ItemSeasonPins!["(O)334"]);
        Assert.Equal(Season.Summer, eased.ItemSeasonPins["(O)335"]);
        var capped = SeasonEase.Apply(req, new SeasonEase(Season.Fall, 5, 0.5));
        Assert.Equal(Season.Winter, capped.ItemSeasonPins!["(O)336"]);
        Assert.Equal(Season.Spring, capped.ItemSeasonPins["(O)334"]);
    }

    [Fact]
    public void Seasonal_due_season_slides_like_per_item()
    {
        var req = BundleRequirement.CreateSeasonal("Spring Crops", Theme.Farming, new[] { "(O)24" }, Season.Spring);
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Spring, 1, 0.9));
        Assert.Equal(Season.Summer, eased.SeasonalSeason);
        Assert.Same(req, SeasonEase.Apply(req, new SeasonEase(Season.Summer, 1, 0.9)));   // other seasons untouched
    }

    [Fact]
    public void Zero_steps_returns_same_instance()
    {
        var req = Percentage(new[] { 3, 4, 5, 6 });
        Assert.Same(req, SeasonEase.Apply(req, new SeasonEase(Season.Spring, 0, 1.0)));
    }

    [Fact]
    public void BuildRequirements_applies_ease_after_clamp()
    {
        var set = new GeneratedBundleSet(new[]
        {
            new BundleSpec("Pantry", 1, "Totally Unknown Bundle", "Totally Unknown Bundle", "O 495 30", 0, 2,
                new[] { "(O)24", "(O)188", "(O)190" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList()),
        });
        var plain = set.BuildRequirements(new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);
        var eased = set.BuildRequirements(new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas,
            new SeasonEase(Season.Spring, 5, 0.5));
        int plainSpring = plain[0].CumulativeRequiredBySeason![0];
        int easedSpring = eased[0].CumulativeRequiredBySeason![0];
        Assert.True(easedSpring <= plainSpring);
        Assert.Equal(plain[0].CumulativeRequiredBySeason![3], eased[0].CumulativeRequiredBySeason![3]);
    }
}
```

Replace `DisplaySteps_uses_quota_ease_when_held_else_board_trim` in `SeasonPityTests.cs` with the same test plus:

```csharp
    [Fact]
    public void CurrentQuotaEase_is_null_unless_held_with_steps()
    {
        var cfg = Cfg();
        Assert.Null(SeasonPity.CurrentQuotaEase(new MetaState(), cfg));
        var notHeld = new MetaState { SeasonFailCounts = new List<int> { 7, 0, 0, 0 }, LastFailSeason = 0 };
        Assert.Null(SeasonPity.CurrentQuotaEase(notHeld, cfg));
        var held = new MetaState { SeasonFailCounts = new List<int> { 7, 0, 0, 0 }, LastFailSeason = 0, ConsecutiveHolds = 1 };
        var ease = SeasonPity.CurrentQuotaEase(held, cfg);
        Assert.NotNull(ease);
        Assert.Equal(Season.Spring, ease!.Season);
        Assert.Equal(2, ease.Steps);
        Assert.Equal(0.8, ease.Factor, 6);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: build errors (`SeasonEase` undefined, `BuildRequirements` has no third parameter).

- [ ] **Step 3: Implement**

Create `src/TheLongestYear.Core/SeasonEase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Keep-path season pity (spec 2026-08-25 section 2): the season the player keeps
/// failing, its ease steps, and the quota factor those steps give. Applied to a requirement
/// manifest AFTER the obtainability clamp. Only <see cref="Season"/> is eased; Winter never is.</summary>
public sealed record SeasonEase(Season Season, int Steps, double Factor)
{
    public static BundleRequirement Apply(BundleRequirement req, SeasonEase ease)
    {
        if (ease.Steps <= 0 || ease.Season == Season.Winter)
            return req;

        switch (req.Kind)
        {
            case BundleKind.Percentage:
            {
                int s = (int)ease.Season;
                int[] ramp = req.CumulativeRequiredBySeason!.ToArray();
                if (ramp[s] > 0)
                    ramp[s] = Math.Max(1, (int)Math.Ceiling(ramp[s] * ease.Factor));
                for (int i = 1; i < ramp.Length; i++)
                    ramp[i] = Math.Max(ramp[i], ramp[i - 1]);
                if (ramp.SequenceEqual(req.CumulativeRequiredBySeason!))
                    return req;
                return BundleRequirement.CreatePercentage(
                    req.Name, req.Theme, req.Ingredients, req.NumberOfSlots, ramp,
                    req.IngredientStacks, req.IngredientQualities);
            }

            case BundleKind.PerItem:
            {
                bool changed = false;
                var pins = new Dictionary<string, Season>(req.ItemSeasonPins!, StringComparer.Ordinal);
                foreach (KeyValuePair<string, Season> kv in req.ItemSeasonPins!)
                {
                    if (kv.Value != ease.Season) continue;
                    pins[kv.Key] = Slide(kv.Value, ease.Steps);
                    changed = true;
                }
                if (!changed) return req;
                return BundleRequirement.CreatePerItem(
                    req.Name, req.Theme, req.Ingredients, pins, req.IngredientStacks, req.IngredientQualities);
            }

            case BundleKind.Seasonal:
                if (req.SeasonalSeason != ease.Season) return req;
                return BundleRequirement.CreateSeasonal(
                    req.Name, req.Theme, req.Ingredients, Slide(req.SeasonalSeason!.Value, ease.Steps),
                    req.IngredientStacks, req.IngredientQualities);

            default:
                return req;
        }
    }

    private static Season Slide(Season from, int steps)
        => (Season)Math.Min((int)from + steps, (int)Season.Winter);
}
```

In `GeneratedBundleSet.BuildRequirements`, change the signature and the end of the loop:

```csharp
    public IReadOnlyList<BundleRequirement> BuildRequirements(
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        SeasonEase? ease = null)
    {
        ...
            if (req.CumulativeRequiredBySeason != null)
            {
                ... existing clamp ...
            }
            if (ease != null)
                req = SeasonEase.Apply(req, ease);   // season pity, keep path (spec 2026-08-25)
            result.Add(req);
```

In `SeasonPity.cs`, replace `CurrentQuotaEase` with:

```csharp
    /// <summary>The keep-path quota easing in force for the current board, or null: the last
    /// failed season and its ease steps, only while the board is held (ConsecutiveHolds > 0).</summary>
    public static SeasonEase? CurrentQuotaEase(MetaState state, GameplayConfig config)
    {
        if (state.ConsecutiveHolds <= 0 || state.LastFailSeason < 0 || state.LastFailSeason >= Calendar.MonthsPerYear)
            return null;
        var season = (Season)state.LastFailSeason;
        int steps = EaseSteps(state, season, config);
        return steps > 0 ? new SeasonEase(season, steps, QuotaFactor(steps, config)) : null;
    }
```

and in `DisplaySteps` use `var ease = CurrentQuotaEase(state, config); if (ease != null) return ease.Steps;`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.20`.

```bash
git add src/TheLongestYear.Core/SeasonEase.cs src/TheLongestYear.Core/GeneratedBundleSet.cs src/TheLongestYear.Core/SeasonPity.cs tests/TheLongestYear.Tests/SeasonEaseTests.cs tests/TheLongestYear.Tests/SeasonPityTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.12.20: season pity keep path, quota easing applied after the clamp"
```

---

### Task 3: ItemHardness score and deterministic trim

**Files:**
- Create: `src/TheLongestYear.Core/ItemHardness.cs`
- Create: `src/TheLongestYear.Core/PityTrim.cs`
- Test: `tests/TheLongestYear.Tests/ItemHardnessTests.cs`

**Interfaces:**
- Consumes: `PoolItem(ItemId, Price, Weight, Seasons, Locations)`, `PoolDomain`, `RarityTiers.FromPrice(price, RarityThresholds)`, `Rarity` enum (Common, Uncommon, Rare, VeryRare).
- Produces: `public sealed record PityTrim(Season Season, int Units)`; `ItemHardness.Score(PoolItem item, PoolDomain domain, RarityThresholds thresholds) : int`; `ItemHardness.Trim(IReadOnlyList<PoolItem> pool, int count, int minKeep, PoolDomain domain, RarityThresholds thresholds) : IReadOnlyList<PoolItem>`; `ItemHardness.NeedsStation(PoolDomain) : bool`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemHardnessTests
{
    private static readonly RarityThresholds T = new();   // 50 / 200 / 600

    private static PoolItem Item(string id, int price, Season[]? seasons = null)
        => new(id, price, 3, seasons ?? Array.Empty<Season>(), Array.Empty<string>());

    [Fact]
    public void Score_rarity_tier_is_the_base()
    {
        Assert.Equal(1, ItemHardness.Score(Item("(O)1", 10), PoolDomain.Fish, T));
        Assert.Equal(2, ItemHardness.Score(Item("(O)1", 60), PoolDomain.Fish, T));
        Assert.Equal(3, ItemHardness.Score(Item("(O)1", 250), PoolDomain.Fish, T));
        Assert.Equal(4, ItemHardness.Score(Item("(O)1", 700), PoolDomain.Fish, T));
    }

    [Fact]
    public void Score_adds_two_for_station_domains_and_one_for_late_spawn()
    {
        Assert.Equal(3, ItemHardness.Score(Item("(O)1", 10), PoolDomain.ArtisanGoods, T));
        Assert.Equal(2, ItemHardness.Score(Item("(O)1", 10, new[] { Season.Fall }), PoolDomain.Fish, T));
        Assert.Equal(2, ItemHardness.Score(Item("(O)1", 10, new[] { Season.Winter, Season.Fall }), PoolDomain.Fish, T));
        Assert.Equal(1, ItemHardness.Score(Item("(O)1", 10, new[] { Season.Summer, Season.Fall }), PoolDomain.Fish, T));
    }

    [Fact]
    public void Trim_removes_hardest_first_ties_by_ordinal_id_and_keeps_order()
    {
        var pool = new[]
        {
            Item("(O)10", 700), Item("(O)20", 10), Item("(O)30", 250), Item("(O)05", 700), Item("(O)40", 60),
        };
        var trimmed = ItemHardness.Trim(pool, count: 2, minKeep: 1, PoolDomain.Fish, T);
        // Two VeryRare (score 4): "(O)05" and "(O)10" removed (highest score, then higher ordinal id first).
        Assert.Equal(new[] { "(O)20", "(O)30", "(O)40" }, trimmed.Select(p => p.ItemId));
    }

    [Fact]
    public void Trim_never_drops_below_minKeep()
    {
        var pool = new[] { Item("(O)1", 700), Item("(O)2", 700), Item("(O)3", 10) };
        var trimmed = ItemHardness.Trim(pool, count: 5, minKeep: 2, PoolDomain.Fish, T);
        Assert.Equal(2, trimmed.Count);
        Assert.Contains(trimmed, p => p.ItemId == "(O)3");
    }

    [Fact]
    public void Trim_zero_returns_same_instance()
    {
        var pool = new[] { Item("(O)1", 700) };
        Assert.Same(pool, ItemHardness.Trim(pool, 0, 1, PoolDomain.Fish, T));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: build errors (`ItemHardness` undefined).

- [ ] **Step 3: Implement**

Create `src/TheLongestYear.Core/PityTrim.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Reshuffle-path season pity (spec 2026-08-25 section 3): trim <see cref="Units"/>
/// hardness units from the slot pools of bundles that feed <see cref="Season"/>'s gate.</summary>
public sealed record PityTrim(Season Season, int Units);
```

Create `src/TheLongestYear.Core/ItemHardness.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Hardness ranking for the reshuffle-path pity trim (spec 2026-08-25 section 3).
/// Score = rarity tier (Common 1 .. VeryRare 4) + 2 if the domain needs a station or recipe
/// + 1 if the item's earliest spawn season is Fall or Winter. Higher = harder.</summary>
public static class ItemHardness
{
    private const int StationBonus = 2;
    private const int LateSpawnBonus = 1;

    public static bool NeedsStation(PoolDomain domain) => domain == PoolDomain.ArtisanGoods;

    public static int Score(PoolItem item, PoolDomain domain, RarityThresholds thresholds)
    {
        int score = RarityTiers.FromPrice(item.Price, thresholds) switch
        {
            Rarity.VeryRare => 4,
            Rarity.Rare => 3,
            Rarity.Uncommon => 2,
            _ => 1,
        };
        if (NeedsStation(domain)) score += StationBonus;
        if (item.Seasons.Count > 0 && item.Seasons.Min() >= Season.Fall) score += LateSpawnBonus;
        return score;
    }

    /// <summary>Removes up to <paramref name="count"/> items, hardest first (ties: higher ordinal
    /// id first, so the result is deterministic), never leaving fewer than
    /// <paramref name="minKeep"/>. Preserves the input order of the survivors.</summary>
    public static IReadOnlyList<PoolItem> Trim(
        IReadOnlyList<PoolItem> pool, int count, int minKeep, PoolDomain domain, RarityThresholds thresholds)
    {
        int removable = Math.Min(Math.Max(0, count), Math.Max(0, pool.Count - Math.Max(0, minKeep)));
        if (removable == 0)
            return pool;

        var drop = new HashSet<string>(
            pool.OrderByDescending(p => Score(p, domain, thresholds))
                .ThenByDescending(p => p.ItemId, StringComparer.Ordinal)
                .Take(removable)
                .Select(p => p.ItemId),
            StringComparer.Ordinal);
        return pool.Where(p => !drop.Contains(p.ItemId)).ToList();
    }
}
```

Note: `Cooking`, `GeodeMinerals` and `TapperGoods` pools exist in `ItemPools` but no `PoolDomain` samples from them today (see `BundleSlotFiller.Candidates`), so `NeedsStation` only names `ArtisanGoods`. If a later plan adds those domains, extend `NeedsStation` there.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.21`.

```bash
git add src/TheLongestYear.Core/ItemHardness.cs src/TheLongestYear.Core/PityTrim.cs tests/TheLongestYear.Tests/ItemHardnessTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.12.21: item hardness score and deterministic pool trim"
```

---

### Task 4: Trim inside BundleSlotFiller.Fill

**Files:**
- Modify: `src/TheLongestYear.Core/BundleSlotFiller.cs:29-62`
- Test: `tests/TheLongestYear.Tests/BundleSlotFillerTests.cs`

**Interfaces:**
- Consumes: `PityTrim`, `ItemHardness.Trim`, `SeasonPity` not needed here.
- Produces: `BundleSlotFiller.Fill(spec, match, pools, tuning, rng, PityTrim? trim = null, RarityThresholds? thresholds = null)`; `BundleSlotFiller.TrimApplies(DomainMatch match, PityTrim? trim) : bool`; `BundleSlotFiller.DomainRollsQuality(PoolDomain) : bool`.

Rule (refines spec section 3's half-trim wording): when a trim applies to a bundle, and its domain can roll quality asks (`QualityCrops`, `SeasonalCrops`, `SeasonalForage`, `Fish`), every slot's quality is forced to base and that costs ONE trim unit for the whole bundle; the remaining units remove whole items from the candidate pool before sampling. Domains without quality rolls spend all units on items. A trim applies to a bundle when `match.Season == null` (season-agnostic pools such as Metals/ArtisanGoods/Fish feed every season's gate) or `match.Season == trim.Season`.

- [ ] **Step 1: Write the failing tests**

Append to `BundleSlotFillerTests`:

```csharp
    private static readonly RarityThresholds Thresholds = new();

    [Fact]
    public void Trim_removes_hardest_items_from_candidates_for_matching_season()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 8).Select(i => Item($"(O){100 + i}", price: 10 + i * 100,
                seasons: new[] { Season.Spring })).ToList(),
        };
        var spec = Spec("Spring Crops", 4, numberOfSlots: 4);
        var match = new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring);
        // 3 units: 1 spent on quality-off (crops roll quality), 2 remove the two priciest items.
        var filled = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(5),
            new PityTrim(Season.Spring, 3), Thresholds);
        Assert.DoesNotContain(filled.Slots, s => s.ItemId is "(O)107" or "(O)106");
        Assert.All(filled.Slots, s => Assert.Equal(0, s.Quality));
    }

    [Fact]
    public void Trim_ignores_bundles_for_other_seasons_and_applies_to_season_agnostic_pools()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 8).Select(i => Item($"(O){100 + i}", price: 10 + i * 100,
                seasons: new[] { Season.Summer })).ToList(),
            Metals = Enumerable.Range(0, 6).Select(i => Item($"(O){200 + i}", price: 10 + i * 150)).ToList(),
        };
        var summer = BundleSlotFiller.Fill(Spec("Summer Crops", 4, 4), new DomainMatch(PoolDomain.SeasonalCrops, Season.Summer),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 4), Thresholds);
        var plain = BundleSlotFiller.Fill(Spec("Summer Crops", 4, 4), new DomainMatch(PoolDomain.SeasonalCrops, Season.Summer),
            pools, Tuning, new Random(5));
        Assert.Equal(plain.Slots, summer.Slots);

        var metals = BundleSlotFiller.Fill(Spec("Blacksmith's", 3, 3), new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 2), Thresholds);
        Assert.DoesNotContain(metals.Slots, s => s.ItemId is "(O)205" or "(O)204");
    }

    [Fact]
    public void Trim_never_starves_the_bundle_below_its_slot_count()
    {
        var pools = new ItemPools
        {
            Metals = Enumerable.Range(0, 4).Select(i => Item($"(O){200 + i}", price: 10 + i * 150)).ToList(),
        };
        var spec = Spec("Blacksmith's", 3, 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 10), Thresholds);
        Assert.NotSame(spec, filled);              // still filled (guard stopped at 3 candidates)
        Assert.Equal(3, filled.Slots.Count);
        Assert.DoesNotContain(filled.Slots, s => s.ItemId == "(O)203");
    }

    [Fact]
    public void DomainRollsQuality_matches_RollQuality_domains()
    {
        Assert.True(BundleSlotFiller.DomainRollsQuality(PoolDomain.QualityCrops));
        Assert.True(BundleSlotFiller.DomainRollsQuality(PoolDomain.Fish));
        Assert.False(BundleSlotFiller.DomainRollsQuality(PoolDomain.Metals));
        Assert.False(BundleSlotFiller.DomainRollsQuality(PoolDomain.ArtisanGoods));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: build errors (no 6/7-argument `Fill`, no `DomainRollsQuality`).

- [ ] **Step 3: Implement**

Replace `Fill` in `BundleSlotFiller.cs`:

```csharp
    public static BundleSpec Fill(
        BundleSpec spec, DomainMatch match, ItemPools pools,
        BundleGenerationTuning tuning, Random rng,
        PityTrim? trim = null, RarityThresholds? thresholds = null)
    {
        if (match.Domain == PoolDomain.None)
            return spec;

        IReadOnlyList<PoolItem> candidates = Candidates(spec, match, pools);
        int targetCount = spec.PickCount > 0
            ? Math.Min(spec.PickCount, spec.Slots.Count)
            : spec.Slots.Count;

        // Season pity, reshuffle path (spec 2026-08-25): quality-off costs one unit for the whole
        // bundle when the domain rolls quality; the rest remove the hardest candidates, never
        // below what this bundle needs to fill.
        bool qualityOff = false;
        if (TrimApplies(match, trim))
        {
            int units = trim!.Units;
            if (DomainRollsQuality(match.Domain) && units > 0)
            {
                qualityOff = true;
                units -= 1;
            }
            candidates = ItemHardness.Trim(candidates, units, targetCount, match.Domain, thresholds ?? new RarityThresholds());
        }

        if (candidates.Count < targetCount)
            return spec;

        List<PoolItem> chosen = WeightedSampler.Sample(candidates, targetCount, rng);
        var slots = chosen.Select(item => new BundleSlotSpec(
            item.ItemId,
            RollStack(match.Domain, item, tuning, rng),
            qualityOff ? 0 : RollQuality(match.Domain, item, tuning, rng))).ToList();
        ... (rest unchanged: LargeQuantityForage block, return spec with ...)
    }

    /// <summary>A trim applies to bundles feeding the trimmed season's gate: season-agnostic
    /// pools (Metals, ArtisanGoods, Fish, CrabPot, MonsterDrops, generic crops) feed every
    /// season, so they count; season-named bundles count only for their own season.</summary>
    public static bool TrimApplies(DomainMatch match, PityTrim? trim)
        => trim != null && trim.Units > 0 && match.Domain != PoolDomain.None
           && (match.Season == null || match.Season == trim.Season);

    /// <summary>Mirrors the domains <see cref="RollQuality"/> can give a silver/gold ask.</summary>
    public static bool DomainRollsQuality(PoolDomain domain)
        => domain is PoolDomain.QualityCrops or PoolDomain.SeasonalCrops or PoolDomain.SeasonalForage or PoolDomain.Fish;
```

Keep `RollQuality` as is; the `qualityOff` flag must still call `RollQuality`'s rng? No: when `qualityOff` is true the rng is NOT advanced for quality. That is fine because the trim stamp is part of the regeneration inputs (MetaState), so reload reproduces the same draws.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass (existing filler tests unchanged since the new parameters default to null).

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.22`.

```bash
git add src/TheLongestYear.Core/BundleSlotFiller.cs tests/TheLongestYear.Tests/BundleSlotFillerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.12.22: reshuffle-path pity trim inside BundleSlotFiller"
```

---

### Task 5: Engine plumbing (BundleEngine, WorldResetService, ModEntry regeneration) and the reset stamp

**Files:**
- Modify: `src/TheLongestYear.Core/BundleHold.cs:58-68`
- Modify: `src/TheLongestYear/Loop/BundleEngine.cs:107-216`
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs:462-488`
- Modify: `src/TheLongestYear/ModEntry.cs:1931-1990` (`ResolveRequirements`) and `1460-1475` (`tly_genbundles`)
- Test: `tests/TheLongestYear.Tests/BundleHoldTests.cs`

**Interfaces:**
- Consumes: `PityTrim`, `SeasonEase`, `SeasonPity.CurrentQuotaEase`, `SeasonPity.ClearBoardTrim`, `MetaState.BoardTrimSeason/BoardTrimSteps`.
- Produces: `BundleEngine(IMonitor, BundleGenerationTuning, bool nonObjectDonationsEnabled, RarityThresholds thresholds)`; `BundleEngine.Generate(int seed, PityTrim? trim = null)`; `BundleEngine.BuildRequirements(set, basePins, quotas, SeasonEase? ease = null)`; `static PityTrim? BundleEngine.TrimFor(MetaState)`.

- [ ] **Step 1: Write the failing test (Core part)**

Append to `BundleHoldTests`:

```csharp
    [Fact]
    public void ConsumeChoiceAtReset_without_a_choice_clears_the_board_trim_stamp()
    {
        var s = new MetaState { CompletedResets = 4, BoardTrimSeason = 0, BoardTrimSteps = 4 };
        bool made = BundleHold.ConsumeChoiceAtReset(s);
        Assert.False(made);
        Assert.Equal(-1, s.BoardTrimSeason);
        Assert.Equal(0, s.BoardTrimSteps);
    }

    [Fact]
    public void ConsumeChoiceAtReset_with_a_choice_keeps_the_board_trim_stamp()
    {
        var s = new MetaState { CompletedResets = 4, BoardTrimSeason = 0, BoardTrimSteps = 4, HoldChoiceMadeForReset = true };
        Assert.True(BundleHold.ConsumeChoiceAtReset(s));
        Assert.Equal(0, s.BoardTrimSeason);
        Assert.Equal(4, s.BoardTrimSteps);
    }
```

- [ ] **Step 2: Run the tests to verify the first fails**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: `ConsumeChoiceAtReset_without_a_choice_clears_the_board_trim_stamp` fails (stamp untouched).

- [ ] **Step 3: Implement**

`BundleHold.ConsumeChoiceAtReset`, inside `if (!choiceMade)` add `SeasonPity.ClearBoardTrim(state);`.

`BundleEngine.cs`:

```csharp
        private readonly RarityThresholds _thresholds;

        public BundleEngine(IMonitor monitor, BundleGenerationTuning tuning, bool nonObjectDonationsEnabled, RarityThresholds thresholds = null)
        {
            ...
            _thresholds = thresholds ?? new RarityThresholds();
        }

        /// <summary>The reshuffle-path pity trim stamped on the CURRENT board, or null. Every
        /// Generate call for a live board must pass this so a reload reproduces the same set.</summary>
        public static PityTrim TrimFor(MetaState meta)
            => meta.BoardTrimSeason >= 0 && meta.BoardTrimSeason < Calendar.MonthsPerYear && meta.BoardTrimSteps > 0
                ? new PityTrim((Core.Season)meta.BoardTrimSeason, meta.BoardTrimSteps)
                : null;

        public GeneratedBundleSet Generate(int seed, PityTrim trim = null)
        {
            ... unchanged until the Fill call ...
                        composed = BundleSlotFiller.Fill(pick, match, itemPools, _tuning, slotRng, trim, _thresholds);
            ...
        }

        public IReadOnlyList<BundleRequirement> BuildRequirements(
            GeneratedBundleSet set,
            IReadOnlyDictionary<string, Core.Season> basePins,
            IReadOnlyDictionary<string, int[]> bundleQuotas,
            SeasonEase ease = null)
        {
            ...
            return set.BuildRequirements(merged, bundleQuotas, ease);
        }
```

`WorldResetService.PerformReset` engine branch:

```csharp
                var engine = new BundleEngine(_monitor, _config.PoolTuning, _config.EnableNonObjectDonations, _config.RarityThresholds);
                int seed = BundleEngineSeed.For(unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
                PityTrim trim = BundleEngine.TrimFor(_meta);
                GeneratedBundleSet generatedSet = engine.Generate(seed, trim);
                engine.WriteToWorld(generatedSet, _monitor);
                SeasonEase ease = SeasonPity.CurrentQuotaEase(_meta, _config);
                _monitor.Log(
                    $"Reset: bundle seed loop {_meta.EffectiveBundleSeedLoop} (CompletedResets {_meta.CompletedResets}, consecutive holds {_meta.ConsecutiveHolds}, " +
                    $"pity trim {(trim == null ? "none" : $"{trim.Season} x{trim.Units}")}, pity ease {(ease == null ? "none" : $"{ease.Season} {ease.Steps} steps")}).",
                    LogLevel.Info);
                _meta.BundlesGeneratedForReset = _meta.CompletedResets;
                LastGeneratedRequirements = engine.BuildRequirements(generatedSet, _itemSeasonPins, _bundleQuotas, ease);
```

`ModEntry.ResolveRequirements`, EngineManifest branch: construct the engine with `_config.RarityThresholds`, call `engine.Generate(seed, TheLongestYear.Loop.BundleEngine.TrimFor(state))`, and `engine.BuildRequirements(set, itemSeasonPins, bundleQuotas, SeasonPity.CurrentQuotaEase(state, _config))`. GenerateFreshRun branch: engine with thresholds, `Generate(seed)` (no trim on a fresh run), `BuildRequirements(..., ease: null)`.

`tly_genbundles` (ModEntry ~1466-1473): pass `_config.RarityThresholds` to both engine constructors and `BundleEngine.TrimFor(_meta.State)` to both `Generate` calls so the diagnostic matches the live board.

- [ ] **Step 4: Build and run the tests**

Run: `dotnet build TheLongestYear.sln -v quiet` then `dotnet test TheLongestYear.sln -v quiet`
Expected: 0 errors; all tests pass.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.23`.

```bash
git add src/TheLongestYear.Core/BundleHold.cs src/TheLongestYear/Loop/BundleEngine.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/BundleHoldTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.12.23: thread pity trim and quota ease through BundleEngine, reset and load paths"
```

---

### Task 6: RunController records fails and passes, stamps the reshuffle trim, eased prompt text

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs:310-363` (ShowHoldChoice / ApplyHoldChoice) and `:704-760` (OnDayEnding)
- Modify: `src/TheLongestYear/i18n/default.json` (after `dialog.hold.prompt`)
- Test: `I18nGuardTests` (existing, guards the new key); manual build.

**Interfaces:**
- Consumes: `SeasonPity.RecordFail/RecordPass/EaseSteps/StampReshuffleTrim`, `BundleHold.Apply`.
- Produces: i18n key `dialog.hold.prompt-eased` with `{{season}}`.

- [ ] **Step 1: Add the string**

In `default.json` after line 26 (`dialog.hold.prompt`):

```json
    "dialog.hold.prompt-eased": "Should we hold the town's wishes steady for your next spring? Keep them and we will ask a little less of {{season}}. Let time reshuffle them and we will leave out the hardest of {{season}}'s asks.",
```

- [ ] **Step 2: Run I18nGuardTests to see the unreachable-key failure**

Run: `dotnet test TheLongestYear.sln -v quiet --filter I18nGuardTests`
Expected: FAIL: `dialog.hold.prompt-eased` present in default.json but unreachable from source.

- [ ] **Step 3: Implement**

`OnDayEnding`:

```csharp
                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    SeasonPity.RecordPass(_store.State, Run.Season, _config);   // season pity: passed gates fall back to the threshold
                    ...

                case RunAction.FailReset:
                    SeasonPity.RecordFail(_store.State, Run.Season);   // season pity: counted before the Fail-night choice reads it
                    _monitor.Log($"Season pity: {Run.Season} fails now {SeasonPity.Counts(_store.State)[(int)Run.Season]}, ease steps next loop {SeasonPity.EaseSteps(_store.State, Run.Season, _config)}.", LogLevel.Info);
                    SuppressResetDoomedRoomScenes();
                    _pendingCutscene = Day28Branch.Fail;
                    break;

                case RunAction.Win:
                    SeasonPity.RecordPass(_store.State, Season.Winter, _config);
                    ...
```

`ShowHoldChoice`: choose the prompt key.

```csharp
            int easeSteps = meta.LastFailSeason >= 0
                ? SeasonPity.EaseSteps(meta, (Season)meta.LastFailSeason, _config)
                : 0;
            string prompt = easeSteps > 0
                ? Strings.Get("dialog.hold.prompt-eased", new Dictionary<string, string>
                    { ["season"] = TheLongestYear.UI.SeasonGoalsMenu.SeasonName((Season)meta.LastFailSeason) })
                : Strings.Get("dialog.hold.prompt");
            ...
            loc.createQuestionDialogue(prompt, responses, (Farmer who, string key) =>
```

If `SeasonGoalsMenu.SeasonName` is private, make it `internal static` (it already maps `Season` to the i18n season name).

Stamp the trim on every reshuffle path. In the question callback after `ApplyHoldChoice(keep: false)` is reached, and in `ApplyHoldChoice` itself:

```csharp
        private void ApplyHoldChoice(bool keep)
        {
            BundleHold.HoldResult result = BundleHold.Apply(_store.State, keep, _config.BundleHoldCosts);
            if (!keep)
                SeasonPity.StampReshuffleTrim(_store.State, _config);   // reshuffle-path pity: the reset reads this stamp
            _monitor.Log($"Hold choice: {result} (seed loop {_store.State.BundleSeedLoop}, board trim {_store.State.BoardTrimSeason}/{_store.State.BoardTrimSteps}).", LogLevel.Info);
            TryOpenShrineThenContinue(ContinueAfterResetSpend);
        }
```

The keep branch inside the callback leaves the stamp alone (a kept board is the same board, trimmed or not).

- [ ] **Step 4: Build and run the tests**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass, including `I18nGuardTests`.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.24`.

```bash
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/UI/SeasonGoalsMenu.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json
git commit -m "v0.12.24: record season fails and passes, stamp reshuffle trim, eased Fail-night prompt"
```

---

### Task 7: Season Goals title shows "eased Nx"

**Files:**
- Modify: `src/TheLongestYear/UI/SeasonGoalsMenu.cs:68-83` (constructor) and `:399-417` (title)
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs:97`
- Modify: `src/TheLongestYear/i18n/default.json` (after `menu.goals.title-held`)

**Interfaces:**
- Consumes: `SeasonPity.DisplaySteps(MetaState, GameplayConfig)`.
- Produces: `SeasonGoalsMenu(IMonitor, RunState, MetaState, IReadOnlyList<BundleRequirement>, int easeSteps)`; keys `menu.goals.title-eased`, `menu.goals.title-held-eased`.

- [ ] **Step 1: Add the strings**

```json
    "menu.goals.title-eased": "Season Goals: {{season}} (day {{day}}) eased {{steps}}x",
    "menu.goals.title-held-eased": "Season Goals: {{season}} (day {{day}}) held {{holds}}x eased {{steps}}x",
```

- [ ] **Step 2: Run I18nGuardTests to see the unreachable-key failure**

Run: `dotnet test TheLongestYear.sln -v quiet --filter I18nGuardTests`
Expected: FAIL on the two new keys.

- [ ] **Step 3: Implement**

Constructor gains `int easeSteps = 0` stored in `private readonly int _easeSteps;`. `MenuLauncher` line 97 passes `SeasonPity.DisplaySteps(_store.State, _config)` (MenuLauncher already holds `_config`; if not, add a `GameplayConfig` constructor parameter and pass `_config` from `ModEntry` where `MenuLauncher` is constructed).

Title block:

```csharp
            bool held = _meta != null && _meta.ConsecutiveHolds > 0 && _meta.BundlesGeneratedForReset >= 0;
            bool eased = _easeSteps > 0 && _meta != null && _meta.BundlesGeneratedForReset >= 0;
            var tokens = new Dictionary<string, string>
            {
                ["season"] = SeasonName(_season),
                ["day"] = _run.DayOfMonth.ToString(),
                ["holds"] = held ? _meta.ConsecutiveHolds.ToString() : "0",
                ["steps"] = _easeSteps.ToString(),
            };
            string title = (held, eased) switch
            {
                (true, true) => Strings.Get("menu.goals.title-held-eased", tokens),
                (true, false) => Strings.Get("menu.goals.title-held", tokens),
                (false, true) => Strings.Get("menu.goals.title-eased", tokens),
                _ => Strings.Get("menu.goals.title", tokens),
            };
```

- [ ] **Step 4: Build and run the tests**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.25`.

```bash
git add src/TheLongestYear/UI/SeasonGoalsMenu.cs src/TheLongestYear/UI/MenuLauncher.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json
git commit -m "v0.12.25: Season Goals title shows eased Nx"
```

---

### Task 8: `tly_pity` console command and GMCM section

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (command registration near line 249, dispatch near line 1364, handler after `CmdHold` line ~1864, GMCM after line 1215)
- Modify: `src/TheLongestYear/Integration/IGenericModConfigMenuApi.cs`
- Modify: `src/TheLongestYear/i18n/default.json` (after `gmcm.auto-detect.tooltip`)

**Interfaces:**
- Consumes: `SeasonPity.*`.
- Produces: console `tly_pity status|set <season> <n>`; GMCM options for the five keys.

- [ ] **Step 1: Add the strings**

```json
    "gmcm.pity.section": "Season pity",
    "gmcm.pity.enabled.name": "Ease a gate after repeated fails",
    "gmcm.pity.enabled.tooltip": "After the threshold number of fails at the same season, each further fail eases that season: a kept board asks for a lower quota, a reshuffled board leaves out the hardest items. Fails are always counted; this only turns the easing on or off.",
    "gmcm.pity.threshold.name": "Fails before easing",
    "gmcm.pity.threshold.tooltip": "How many fails at one season are played at standard difficulty before easing starts. Passing a season drops its count back to this number.",
    "gmcm.pity.quota-step.name": "Quota reduction per step",
    "gmcm.pity.quota-step.tooltip": "On a kept board, the failed season's quota is reduced by this fraction per fail beyond the threshold.",
    "gmcm.pity.quota-floor.name": "Lowest quota factor",
    "gmcm.pity.quota-floor.tooltip": "The kept-board quota never drops below this fraction of the standard quota.",
    "gmcm.pity.trim.name": "Items trimmed per step",
    "gmcm.pity.trim.tooltip": "On a reshuffle, this many of the hardest items eligible for the failed season are left out of the roll per fail beyond the threshold.",
```

- [ ] **Step 2: Run I18nGuardTests to see the failure**

Expected: FAIL on the eleven new keys.

- [ ] **Step 3: Implement**

`IGenericModConfigMenuApi.cs`, add inside the interface:

```csharp
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name,
            Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null,
            Func<int, string> formatValue = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name,
            Func<string> tooltip = null, float? min = null, float? max = null, float? interval = null,
            Func<float, string> formatValue = null, string fieldId = null);
```

GMCM registration (before the "Registered GMCM options." log):

```csharp
            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.pity.section"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.PityEnabled,
                setValue: v => _config.PityEnabled = v,
                name: () => Strings.Get("gmcm.pity.enabled.name"),
                tooltip: () => Strings.Get("gmcm.pity.enabled.tooltip"));
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => _config.PityThreshold,
                setValue: v => _config.PityThreshold = v,
                name: () => Strings.Get("gmcm.pity.threshold.name"),
                tooltip: () => Strings.Get("gmcm.pity.threshold.tooltip"),
                min: 0, max: 20, interval: 1);
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => (float)_config.PityQuotaStep,
                setValue: v => _config.PityQuotaStep = v,
                name: () => Strings.Get("gmcm.pity.quota-step.name"),
                tooltip: () => Strings.Get("gmcm.pity.quota-step.tooltip"),
                min: 0f, max: 0.5f, interval: 0.05f);
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => (float)_config.PityQuotaFloor,
                setValue: v => _config.PityQuotaFloor = v,
                name: () => Strings.Get("gmcm.pity.quota-floor.name"),
                tooltip: () => Strings.Get("gmcm.pity.quota-floor.tooltip"),
                min: 0.1f, max: 1f, interval: 0.05f);
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => _config.PityTrimPerStep,
                setValue: v => _config.PityTrimPerStep = v,
                name: () => Strings.Get("gmcm.pity.trim.name"),
                tooltip: () => Strings.Get("gmcm.pity.trim.tooltip"),
                min: 0, max: 10, interval: 1);
```

Console command, registered next to `tly_hold`:

```csharp
            helper.ConsoleCommands.Add("tly_pity", "Debug: season pity counters. Usage: tly_pity status | tly_pity set <spring|summer|fall|winter> <fails>.", this.CmdPity);
```

dispatch case `case "tly_pity": this.CmdPity(command, args); break;` next to `tly_hold`, and the handler after `CmdHold`:

```csharp
        private void CmdPity(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            if (mode == "set")
            {
                if (args.Length < 3 || !Enum.TryParse(args[1], ignoreCase: true, out TheLongestYear.Core.Season season) || !int.TryParse(args[2], out int fails))
                {
                    this.Monitor.Log("Usage: tly_pity set <spring|summer|fall|winter> <fails>", LogLevel.Warn);
                    return;
                }
                SeasonPity.Counts(s)[(int)season] = Math.Max(0, fails);
                s.LastFailSeason = (int)season;
                _meta.Save();
                this.Monitor.Log($"tly_pity: {season} fails set to {fails} (LastFailSeason = {season}). Saved.", LogLevel.Info);
            }
            var counts = SeasonPity.Counts(s);
            var ease = SeasonPity.CurrentQuotaEase(s, _config);
            this.Monitor.Log(
                $"tly_pity status: fails Spring {counts[0]} / Summer {counts[1]} / Fall {counts[2]} / Winter {counts[3]}; threshold {_config.PityThreshold}; " +
                $"steps Spring {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Spring, _config)} / Summer {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Summer, _config)} / Fall {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Fall, _config)} / Winter {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Winter, _config)}; " +
                $"last fail season {s.LastFailSeason}; held {s.ConsecutiveHolds}; quota ease {(ease == null ? "none" : $"{ease.Season} {ease.Steps} steps factor {ease.Factor:0.00}")}; " +
                $"board trim season {s.BoardTrimSeason} units {s.BoardTrimSteps}; enabled {_config.PityEnabled}.",
                LogLevel.Info);
        }
```

(`_meta.Save()` is the existing MetaStore persist call used elsewhere in ModEntry; match its actual name.)

- [ ] **Step 4: Build and run the tests**

Run: `dotnet test TheLongestYear.sln -v quiet`
Expected: all pass.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.12.26`.

```bash
git add src/TheLongestYear/ModEntry.cs src/TheLongestYear/Integration/IGenericModConfigMenuApi.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json
git commit -m "v0.12.26: tly_pity command and GMCM season pity section"
```

---

### Task 9: Produce invariant regression test

**Files:**
- Test: `tests/TheLongestYear.Tests/GeneratedBundleSetTests.cs` (append)

**Interfaces:**
- Consumes: `ItemPoolBuilder.Build(...)` (signature as used in `GatedItemVettingTests.Vetting_SurvivesConfigOverride_EmptiedTuningLists`), `BundleSlotFiller.Fill`, `GeneratedBundleSet.BuildRequirements`, `ItemPools.DerivedSeasonPins`.

- [ ] **Step 1: Write the test**

```csharp
    /// <summary>Spec 2026-08-25 section 4: real produce whose earliest spawn season is Fall or
    /// Winter is never REQUIRED by the Spring gate. Rolls a Spring-named crop bundle and a
    /// season-agnostic percentage bundle from pools that contain Fall-only crops, builds the
    /// manifest with the derived pins, and checks Spring can be satisfied without them.</summary>
    [Fact]
    public void SpringGate_NeverRequires_FallOrWinterOnlyProduce()
    {
        var pools = ItemPoolBuilder.Build(
            new[]
            {
                new RawCropEntry("24", new[] { Season.Spring }),
                new RawCropEntry("188", new[] { Season.Spring }),
                new RawCropEntry("190", new[] { Season.Spring }),
                new RawCropEntry("192", new[] { Season.Spring }),
                new RawCropEntry("276", new[] { Season.Fall }),   // Pumpkin
                new RawCropEntry("278", new[] { Season.Fall }),   // Bok Choy
                new RawCropEntry("280", new[] { Season.Fall }),   // Yam
            },
            GatedItemVettingTests.Objects(("24", GatedItemVettingTests.Obj()), ("188", GatedItemVettingTests.Obj()),
                ("190", GatedItemVettingTests.Obj()), ("192", GatedItemVettingTests.Obj()),
                ("276", GatedItemVettingTests.Obj()), ("278", GatedItemVettingTests.Obj()), ("280", GatedItemVettingTests.Obj())),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(), new BundleGenerationTuning());

        var springSpec = new BundleSpec("Pantry", 0, "Spring Crops", "Spring Crops", "O 495 30", 0, 4,
            new[] { "(O)24", "(O)188", "(O)190", "(O)192" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());
        var filledSpring = BundleSlotFiller.Fill(springSpec, new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring),
            pools, new BundleGenerationTuning(), new Random(11));
        Assert.All(filledSpring.Slots, s => Assert.DoesNotContain(s.ItemId, pools.DerivedSeasonPins.Keys));

        var anySpec = new BundleSpec("Pantry", 1, "Totally Unknown Bundle", "Totally Unknown Bundle", "O 495 30", 0, 3,
            new[] { "(O)24", "(O)276", "(O)278", "(O)280" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());
        var set = new GeneratedBundleSet(new[] { filledSpring, anySpec });
        var reqs = set.BuildRequirements(pools.DerivedSeasonPins, GameplayConfig.DefaultBundleQuotas);

        // Donating only the Spring-obtainable items must satisfy every bundle's Spring gate.
        var springOnly = new HashSet<string>(
            filledSpring.Slots.Select(s => s.ItemId).Concat(new[] { "(O)24" }), StringComparer.Ordinal);
        Assert.All(reqs, r => Assert.True(r.IsSatisfiedAtSeasonEnd(Season.Spring, springOnly),
            $"{r.Name} demands Fall/Winter-only produce in Spring"));
    }
```

If `GatedItemVettingTests.Objects` / `Obj()` are private, make them `internal static` in that file (they are small fixture helpers).

- [ ] **Step 2: Run the test**

Run: `dotnet test TheLongestYear.sln -v quiet --filter SpringGate_NeverRequires_FallOrWinterOnlyProduce`
Expected: PASS on the first run (this pins existing behaviour). If it fails, the failure is a real regression in the clamp or filler and must be fixed before continuing; do not weaken the assertion.

- [ ] **Step 3: Commit**

Bump `manifest.json` to `0.12.27`.

```bash
git add tests/TheLongestYear.Tests/GeneratedBundleSetTests.cs tests/TheLongestYear.Tests/GatedItemVettingTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.12.27: regression test, Spring gate never requires Fall/Winter-only produce"
```

---

### Task 10: CHANGELOG, spec note, and live smoke

**Files:**
- Modify: `CHANGELOG.md` (add `## Unreleased` above `## 0.12.18`)
- Modify: `docs/superpowers/specs/2026-08-25-season-pity-design.md` (section 3, quality rule refinement)

- [ ] **Step 1: CHANGELOG**

```markdown
## Unreleased

### Added
- **Season pity** (spec `docs/superpowers/specs/2026-08-25-season-pity-design.md`). Fails are counted per
  season gate. The first 5 fails at a season are standard difficulty; from the 6th, keeping the board
  lowers that season's quota by 10% per extra fail (floor 50%), and reshuffling leaves the hardest
  eligible items out of the roll (2 per extra fail; quality asks go first). Passing a season drops its
  count back to 5. Season Goals title shows "eased Nx". Config `PityEnabled`, `PityThreshold`,
  `PityQuotaStep`, `PityQuotaFloor`, `PityTrimPerStep` (GMCM section "Season pity"). Debug `tly_pity`.
  TLY Custom boards only.
```

- [ ] **Step 2: Spec refinement**

In the spec's section 3, replace the "Quality asks" paragraph with:

> Quality asks: when a trim applies to a bundle whose domain can roll quality (Quality Crops, seasonal crops, seasonal forage, fish), every slot in that bundle is forced to base quality and that costs one trim unit for the bundle; the remaining units remove whole items. Domains without quality rolls spend every unit on items.

- [ ] **Step 3: Commit**

Bump `manifest.json` to `0.12.28`.

```bash
git add CHANGELOG.md docs/superpowers/specs/2026-08-25-season-pity-design.md src/TheLongestYear/manifest.json
git commit -m "v0.12.28: changelog and spec note for season pity"
```

- [ ] **Step 4: Live smoke (ask Jeff before touching the game window; see user memory ask-before-driving-desktop.md)**

Deploy with `tools/deploy.ps1`, then `git checkout -- test-output/log-archive/` (deploy prunes tracked logs). Load the Rodger throwaway save with `tly_loadsave <folder>` (never the Load menu). Then:

1. `tly_pity set spring 7` then `tly_pity status`: expect Spring steps 2.
2. `tly_failreset`: after the Junimo scene the prompt must read the eased text naming Spring. Choose **Keep**. After the reset: `tly_pity status` shows quota ease Spring 2 steps factor 0.80; open the Bundle Log: title ends "held 1x eased 2x"; a percentage bundle's Spring number is lower than on the previous board (compare `tly_bundlesource` / the log's requirements dump).
3. Reload from title with `tly_loadsave`: no manifest mismatch WARN in the log; title unchanged.
4. `tly_failreset` again, choose **Reshuffle**: log line `Hold choice: Reshuffled ... board trim 0/6` (7 fails +1 = 8, steps 3, units 6); after reset the log's `Reset: ... pity trim Spring x6`; the new board's artisan/metal bundles omit their priciest items (compare against `tly_genbundles` output); title reads "eased 3x".
5. Reload from title: no mismatch WARN.
6. `tly_day28continue` from a Spring 28 state: `tly_pity status` shows Spring fails back at 5, steps 0.
7. Close the game with `close-smapi.ps1`; record the results table in TODO.md.

---

## Self-review

**Spec coverage.** Section 1 state and rules: Task 1 (+ `ClearBoardTrim` in Task 5). Section 2 keep path: Task 2 (Percentage, PerItem, Seasonal, monotonic, Winter untouched, recomputed on every build via `CurrentQuotaEase`). Section 3 reshuffle path: Tasks 3, 4, 5, 6 (score, trim, guard, stamp, season-applies rule). Section 4 invariant: Task 9. Section 5 prompt/title/debug/config: Tasks 6, 7, 8. Section 6 error handling: padding (`SeasonPity.Counts`), config clamping (`QuotaFactor`, `TrimUnits`, `EaseSteps` clamp negatives), trim guard (Task 4). Section 7 tests: each task; live smoke Task 10. Out of scope items untouched; Vanilla boards never reach `BundleEngine`, so no easing (matches the hold).

**Placeholder scan.** None. Every step has code or an exact command.

**Type consistency.** `SeasonPity.CurrentQuotaEase` returns a tuple in Task 1 and is replaced by `SeasonEase?` in Task 2 (the Task 1 `DisplaySteps` test is rewritten in Task 2 accordingly). `BundleEngine.Generate(int, PityTrim)` / `BuildRequirements(..., SeasonEase)` signatures match their call sites in Task 5 and the diagnostics in Task 8. `BundleSlotFiller.Fill` optional parameters default to null so Task 4's earlier tests and Task 9 compile unchanged. `SeasonGoalsMenu.SeasonName` visibility is widened in Task 6 and reused in Task 7.
