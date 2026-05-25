# The Longest Year — v1 Plan 02: Contract System (pure logic)

> **For agentic workers:** Implement task-by-task with TDD. Steps use checkbox (`- [ ]`) syntax. All work here is pure logic in `TheLongestYear.Core`, verified by `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj` (no game required).

**Goal:** Model the Community Center as themed, season-scheduled contracts and generate, every run, a *solvable* year-plan whose contracts collectively complete the entire CC — the guarantee from spec §10.

**Architecture:** Add pure domain types (`Season`, `Theme`, `CcItem`, `Contract`, `YearPlan`) and a seeded `ContractGenerator` to `TheLongestYear.Core`. The generator takes the CC "ground truth" (a list of `CcItem`s, each tagged with its theme, rarity, and the seasons it's obtainable) plus a seed, and partitions every item into a `(season, theme)` contract such that the union covers the whole CC and each item lands in a season it can actually be obtained. Reading the real `Data/Bundles` into `CcItem`s, and applying the bonus/liability gameplay effects, are game-integration concerns deferred to later plans — this plan keeps the algorithm pure and tested against synthetic CC data.

**Tech Stack:** C# / .NET 6, xUnit. No StardewValley/SMAPI references (Core stays pure).

**Repo conventions:** Work on branch `feat/v1-plan-02-contracts`. Local commits only (never push). End commit bodies with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.

**Scope note (deferred refinements):** spec §10 also wants "early weeks use early-obtainable items" and "hard items appear only rarely." Those are *difficulty-smoothing* refinements layered on top of a correct partition; this plan delivers the correct, solvable, season-valid partition first. Week-level ordering and difficulty weighting are a follow-up (Plan 02.1 or folded into Plan 03 when week sequencing exists).

**Files created in this plan:**

```
src/TheLongestYear.Core/
  Season.cs            — Spring..Winter enum + monthIndex bridge
  Theme.cs             — the 5 CC themes
  CcItem.cs            — one CC-required item: id, theme, rarity, obtainable seasons
  ThemeModifiers.cs    — per-theme bonus/liability identifiers (effects applied later)
  Contract.cs          — one (season, theme) contract: required item ids + bonus/liability + satisfaction check
  YearPlan.cs          — the 20 contracts for a run, with lookup helpers
  ContractGenerator.cs — seeded, solvable partition of the CC into a YearPlan
tests/TheLongestYear.Tests/
  ThemeModifiersTests.cs
  ContractTests.cs
  YearPlanTests.cs
  ContractGeneratorTests.cs
```

---

## Task 0: Branch

- [ ] **Step 1: Create and switch to the feature branch**

Run from repo root:
```bash
git checkout -b feat/v1-plan-02-contracts
git branch --show-current
```
Expected: `feat/v1-plan-02-contracts`.

---

## Task 1: `Season`, `Theme`, and `CcItem`

**Files:**
- Create: `src/TheLongestYear.Core/Season.cs`
- Create: `src/TheLongestYear.Core/Theme.cs`
- Create: `src/TheLongestYear.Core/CcItem.cs`
- Test: `tests/TheLongestYear.Tests/ContractTests.cs` (CcItem portion; contract tests added in Task 3)

- [ ] **Step 1: Write the failing CcItem test**

Create `tests/TheLongestYear.Tests/ContractTests.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcItemTests
{
    [Fact]
    public void CcItem_exposes_its_properties()
    {
        var item = new CcItem("RedCabbage", Theme.Farming, Rarity.VeryRare, new HashSet<Season> { Season.Summer });

        Assert.Equal("RedCabbage", item.Id);
        Assert.Equal(Theme.Farming, item.Theme);
        Assert.Equal(Rarity.VeryRare, item.Rarity);
        Assert.True(item.IsObtainableIn(Season.Summer));
        Assert.False(item.IsObtainableIn(Season.Winter));
    }

    [Fact]
    public void Season_maps_to_month_index()
    {
        Assert.Equal(0, (int)Season.Spring);
        Assert.Equal(3, (int)Season.Winter);
        Assert.Equal(Season.Fall, SeasonExtensions.FromMonthIndex(2));
    }
}
```

- [ ] **Step 2: Run tests; expect FAIL** — `Season`/`Theme`/`CcItem` don't exist.

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`

- [ ] **Step 3: Create `Season.cs`**

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>The four seasons, ordered to match Stardew's month index (Spring=0..Winter=3).</summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3
}

public static class SeasonExtensions
{
    public static Season FromMonthIndex(int monthIndex)
    {
        if (monthIndex is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(monthIndex), monthIndex, "Month index must be 0-3.");
        return (Season)monthIndex;
    }

    public static int ToMonthIndex(this Season season) => (int)season;
}
```

- [ ] **Step 4: Create `Theme.cs`**

```csharp
namespace TheLongestYear.Core;

/// <summary>The five contract themes, each mapped to a Community Center item room.</summary>
public enum Theme
{
    Foraging, // Crafts Room
    Farming,  // Pantry
    Fishing,  // Fish Tank
    Mining,   // Boiler Room
    Mixed     // Bulletin Board
}
```

- [ ] **Step 5: Create `CcItem.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// One Community Center required item — the "ground truth" unit the contracts schedule.
/// Tagged with the theme it belongs to, its rarity (JP value), and the seasons it can be obtained.
/// </summary>
public sealed class CcItem
{
    public string Id { get; }
    public Theme Theme { get; }
    public Rarity Rarity { get; }
    public IReadOnlySet<Season> ObtainableSeasons { get; }

    public CcItem(string id, Theme theme, Rarity rarity, IReadOnlySet<Season> obtainableSeasons)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id must be non-empty.", nameof(id));
        if (obtainableSeasons is null || obtainableSeasons.Count == 0)
            throw new ArgumentException("An item must be obtainable in at least one season.", nameof(obtainableSeasons));

        Id = id;
        Theme = theme;
        Rarity = rarity;
        ObtainableSeasons = obtainableSeasons;
    }

    public bool IsObtainableIn(Season season) => ObtainableSeasons.Contains(season);
}
```

- [ ] **Step 6: Run tests; expect PASS.**

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear.Core/Season.cs src/TheLongestYear.Core/Theme.cs src/TheLongestYear.Core/CcItem.cs tests/TheLongestYear.Tests/ContractTests.cs
git commit -m "feat(core): add Season, Theme, and CcItem domain types"
```

---

## Task 2: `ThemeModifiers`

**Files:**
- Create: `src/TheLongestYear.Core/ThemeModifiers.cs`
- Test: `tests/TheLongestYear.Tests/ThemeModifiersTests.cs`

Each theme carries a bonus and a liability (spec §3). This task stores the *identifiers*; the gameplay effects are applied in a later plan. Identifiers are stable strings so the effect layer can switch on them.

- [ ] **Step 1: Write the failing test**

Create `tests/TheLongestYear.Tests/ThemeModifiersTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ThemeModifiersTests
{
    [Theory]
    [InlineData(Theme.Foraging)]
    [InlineData(Theme.Farming)]
    [InlineData(Theme.Fishing)]
    [InlineData(Theme.Mining)]
    [InlineData(Theme.Mixed)]
    public void Every_theme_has_a_distinct_nonempty_bonus_and_liability(Theme theme)
    {
        var (bonus, liability) = ThemeModifiers.For(theme);
        Assert.False(string.IsNullOrWhiteSpace(bonus));
        Assert.False(string.IsNullOrWhiteSpace(liability));
        Assert.NotEqual(bonus, liability);
    }
}
```

- [ ] **Step 2: Run tests; expect FAIL.**

- [ ] **Step 3: Create `ThemeModifiers.cs`**

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>
/// Maps each theme to the identifiers of its bonus (helps that playstyle) and liability
/// (throttles a different income stream). The gameplay effects keyed by these ids are
/// implemented in the obtainability/effects plan; here they are just stable identifiers.
/// </summary>
public static class ThemeModifiers
{
    public static (string BonusId, string LiabilityId) For(Theme theme) => theme switch
    {
        Theme.Foraging => ("forage_yield_up", "mine_drops_off"),
        Theme.Farming  => ("crop_growth_up", "forage_drops_off"),
        Theme.Fishing  => ("fish_bite_up", "crop_growth_down"),
        Theme.Mining   => ("mine_drops_up", "forage_drops_off"),
        Theme.Mixed    => ("shop_discount", "stamina_drain_up"),
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
    };
}
```

- [ ] **Step 4: Run tests; expect PASS.**

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/ThemeModifiers.cs tests/TheLongestYear.Tests/ThemeModifiersTests.cs
git commit -m "feat(core): add per-theme bonus/liability identifiers"
```

---

## Task 3: `Contract` and `YearPlan`

**Files:**
- Create: `src/TheLongestYear.Core/Contract.cs`
- Create: `src/TheLongestYear.Core/YearPlan.cs`
- Test: `tests/TheLongestYear.Tests/YearPlanTests.cs` (and extend `ContractTests.cs`)

- [ ] **Step 1: Write the failing Contract test (append to `ContractTests.cs`)**

Add this class to `tests/TheLongestYear.Tests/ContractTests.cs`:

```csharp
public class ContractSatisfactionTests
{
    private static Contract Make(params string[] required)
        => new Contract(Season.Spring, Theme.Farming, required, "crop_growth_up", "forage_drops_off");

    [Fact]
    public void Contract_is_satisfied_when_all_required_ids_donated()
    {
        var c = Make("Parsnip", "Potato");
        var donated = new HashSet<string> { "Parsnip", "Potato", "Daffodil" };
        Assert.True(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Contract_is_not_satisfied_when_an_item_is_missing()
    {
        var c = Make("Parsnip", "Potato");
        var donated = new HashSet<string> { "Parsnip" };
        Assert.False(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Empty_contract_is_always_satisfied()
    {
        var c = Make();
        Assert.True(c.IsSatisfiedBy(new HashSet<string>()));
    }
}
```

- [ ] **Step 2: Write the failing YearPlan test**

Create `tests/TheLongestYear.Tests/YearPlanTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class YearPlanTests
{
    private static YearPlan TwentyEmptyContracts()
    {
        var contracts = new List<Contract>();
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
            foreach (Theme t in System.Enum.GetValues(typeof(Theme)))
                contracts.Add(new Contract(s, t, new string[0], "b", "l"));
        return new YearPlan(contracts);
    }

    [Fact]
    public void Get_returns_the_contract_for_a_season_and_theme()
    {
        var plan = TwentyEmptyContracts();
        var c = plan.Get(Season.Fall, Theme.Mining);
        Assert.Equal(Season.Fall, c.Season);
        Assert.Equal(Theme.Mining, c.Theme);
    }

    [Fact]
    public void ForSeason_returns_the_five_theme_contracts()
    {
        var plan = TwentyEmptyContracts();
        var fall = plan.ForSeason(Season.Fall).ToList();
        Assert.Equal(5, fall.Count);
        Assert.Equal(new[] { Theme.Foraging, Theme.Farming, Theme.Fishing, Theme.Mining, Theme.Mixed },
            fall.Select(c => c.Theme).OrderBy(t => (int)t));
    }

    [Fact]
    public void Constructor_rejects_a_plan_missing_a_season_theme_slot()
    {
        var contracts = new List<Contract> { new Contract(Season.Spring, Theme.Mining, new string[0], "b", "l") };
        Assert.Throws<System.ArgumentException>(() => new YearPlan(contracts));
    }
}
```

- [ ] **Step 3: Run tests; expect FAIL.**

- [ ] **Step 4: Create `Contract.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// One weekly-championable contract: the items of a single theme that must be donated in a
/// single season, plus the bonus/liability ids active when this theme is championed.
/// Satisfaction is cumulative — any of the run's donated item ids count, by design (spec §6).
/// </summary>
public sealed class Contract
{
    public Season Season { get; }
    public Theme Theme { get; }
    public IReadOnlyList<string> RequiredItemIds { get; }
    public string BonusId { get; }
    public string LiabilityId { get; }

    public Contract(Season season, Theme theme, IEnumerable<string> requiredItemIds, string bonusId, string liabilityId)
    {
        Season = season;
        Theme = theme;
        RequiredItemIds = requiredItemIds?.ToList() ?? throw new ArgumentNullException(nameof(requiredItemIds));
        BonusId = bonusId ?? throw new ArgumentNullException(nameof(bonusId));
        LiabilityId = liabilityId ?? throw new ArgumentNullException(nameof(liabilityId));
    }

    /// <summary>True when every required item id is present in the run's cumulative donations.</summary>
    public bool IsSatisfiedBy(ISet<string> donatedItemIds)
        => RequiredItemIds.All(donatedItemIds.Contains);
}
```

- [ ] **Step 5: Create `YearPlan.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// The full set of contracts for one run: exactly one per (season, theme) — 20 in total.
/// </summary>
public sealed class YearPlan
{
    private readonly Dictionary<(Season, Theme), Contract> _bySlot;

    public IReadOnlyList<Contract> Contracts { get; }

    public YearPlan(IReadOnlyList<Contract> contracts)
    {
        if (contracts is null)
            throw new ArgumentNullException(nameof(contracts));

        _bySlot = new Dictionary<(Season, Theme), Contract>();
        foreach (Contract c in contracts)
        {
            var key = (c.Season, c.Theme);
            if (_bySlot.ContainsKey(key))
                throw new ArgumentException($"Duplicate contract for {c.Season}/{c.Theme}.", nameof(contracts));
            _bySlot[key] = c;
        }

        int expected = Calendar.MonthsPerYear * 5; // 5 themes per season
        if (_bySlot.Count != expected)
            throw new ArgumentException($"A year plan needs exactly {expected} contracts (one per season/theme); got {_bySlot.Count}.", nameof(contracts));

        Contracts = contracts.ToList();
    }

    public Contract Get(Season season, Theme theme) => _bySlot[(season, theme)];

    public IEnumerable<Contract> ForSeason(Season season)
        => Contracts.Where(c => c.Season == season);
}
```

- [ ] **Step 6: Run tests; expect PASS.**

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear.Core/Contract.cs src/TheLongestYear.Core/YearPlan.cs tests/TheLongestYear.Tests/ContractTests.cs tests/TheLongestYear.Tests/YearPlanTests.cs
git commit -m "feat(core): add Contract and YearPlan models"
```

---

## Task 4: `ContractGenerator` (the solvable partition)

**Files:**
- Create: `src/TheLongestYear.Core/ContractGenerator.cs`
- Test: `tests/TheLongestYear.Tests/ContractGeneratorTests.cs`

The generator assigns each `CcItem` to one season it is obtainable in (seeded-random among its candidates), groups items by `(chosenSeason, item.Theme)`, and emits all 20 contracts (empty where a theme has no items that season). This guarantees: full coverage (every item assigned exactly once), season-validity (each item in an obtainable season), and theme-correctness — i.e., the union of contracts always completes the whole CC (spec §10).

- [ ] **Step 1: Write the failing generator tests**

Create `tests/TheLongestYear.Tests/ContractGeneratorTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ContractGeneratorTests
{
    private static List<CcItem> SampleCc() => new()
    {
        new CcItem("Parsnip", Theme.Farming, Rarity.Common, new HashSet<Season> { Season.Spring }),
        new CcItem("RedCabbage", Theme.Farming, Rarity.VeryRare, new HashSet<Season> { Season.Summer }),
        new CcItem("Salmon", Theme.Fishing, Rarity.Uncommon, new HashSet<Season> { Season.Fall }),
        new CcItem("CopperBar", Theme.Mining, Rarity.Common, new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }),
        new CcItem("Daffodil", Theme.Foraging, Rarity.Common, new HashSet<Season> { Season.Spring }),
    };

    private static Dictionary<string, Contract> ByItem(YearPlan plan)
    {
        var map = new Dictionary<string, Contract>();
        foreach (var c in plan.Contracts)
            foreach (var id in c.RequiredItemIds)
                map[id] = c;
        return map;
    }

    [Fact]
    public void Plan_has_twenty_contracts()
    {
        var plan = new ContractGenerator().Generate(SampleCc(), seed: 1);
        Assert.Equal(20, plan.Contracts.Count);
    }

    [Fact]
    public void Every_item_is_assigned_exactly_once()
    {
        var cc = SampleCc();
        var plan = new ContractGenerator().Generate(cc, seed: 1);
        var assigned = plan.Contracts.SelectMany(c => c.RequiredItemIds).ToList();

        Assert.Equal(cc.Count, assigned.Count);                       // no duplicates, none dropped
        Assert.Equal(cc.Select(i => i.Id).OrderBy(x => x),
                     assigned.OrderBy(x => x));                       // exactly the CC set
    }

    [Fact]
    public void Each_item_lands_in_an_obtainable_season_and_its_theme()
    {
        var cc = SampleCc();
        var plan = new ContractGenerator().Generate(cc, seed: 7);
        var byItem = ByItem(plan);

        foreach (var item in cc)
        {
            var contract = byItem[item.Id];
            Assert.Equal(item.Theme, contract.Theme);
            Assert.True(item.IsObtainableIn(contract.Season),
                $"{item.Id} placed in {contract.Season} but is only obtainable in [{string.Join(",", item.ObtainableSeasons)}]");
        }
    }

    [Fact]
    public void Same_seed_produces_an_identical_plan()
    {
        var a = new ContractGenerator().Generate(SampleCc(), seed: 42);
        var b = new ContractGenerator().Generate(SampleCc(), seed: 42);

        Assert.Equal(Serialize(a), Serialize(b));
    }

    [Fact]
    public void Contracts_carry_their_theme_modifiers()
    {
        var plan = new ContractGenerator().Generate(SampleCc(), seed: 1);
        var mining = plan.Get(Season.Spring, Theme.Mining);
        var (bonus, liability) = ThemeModifiers.For(Theme.Mining);
        Assert.Equal(bonus, mining.BonusId);
        Assert.Equal(liability, mining.LiabilityId);
    }

    private static string Serialize(YearPlan plan)
        => string.Join("|", plan.Contracts
            .OrderBy(c => (int)c.Season).ThenBy(c => (int)c.Theme)
            .Select(c => $"{c.Season}.{c.Theme}:{string.Join(",", c.RequiredItemIds.OrderBy(x => x))}"));
}
```

- [ ] **Step 2: Run tests; expect FAIL** — `ContractGenerator` doesn't exist.

- [ ] **Step 3: Create `ContractGenerator.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds a run's <see cref="YearPlan"/> by partitioning the CC ground truth into
/// (season, theme) contracts. Each item is placed in one season it is obtainable in
/// (seeded-random among candidates), so the union of all contracts always completes the
/// whole CC and nothing is scheduled out of season — the solvability guarantee (spec §10).
/// </summary>
public sealed class ContractGenerator
{
    public YearPlan Generate(IReadOnlyList<CcItem> ccItems, int seed)
    {
        if (ccItems is null)
            throw new ArgumentNullException(nameof(ccItems));

        var rng = new Random(seed);

        // Group required item ids by the (season, theme) slot we assign them to.
        var grouped = new Dictionary<(Season, Theme), List<string>>();
        foreach (Season s in Enum.GetValues(typeof(Season)))
            foreach (Theme t in Enum.GetValues(typeof(Theme)))
                grouped[(s, t)] = new List<string>();

        // Deterministic iteration order so a given seed always yields the same plan.
        foreach (CcItem item in ccItems.OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            Season chosen = ChooseSeason(item, rng);
            grouped[(chosen, item.Theme)].Add(item.Id);
        }

        var contracts = new List<Contract>(grouped.Count);
        foreach (var kvp in grouped)
        {
            var (season, theme) = kvp.Key;
            var (bonus, liability) = ThemeModifiers.For(theme);
            contracts.Add(new Contract(season, theme, kvp.Value, bonus, liability));
        }

        return new YearPlan(contracts);
    }

    private static Season ChooseSeason(CcItem item, Random rng)
    {
        // Stable candidate order, then a seeded pick — keeps generation deterministic.
        var candidates = item.ObtainableSeasons.OrderBy(s => (int)s).ToList();
        return candidates[rng.Next(candidates.Count)];
    }
}
```

- [ ] **Step 4: Run tests; expect PASS.**

- [ ] **Step 5: Run the FULL suite to confirm no regressions**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: all green (Plan 01's 32 + the new contract tests).

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/ContractGenerator.cs tests/TheLongestYear.Tests/ContractGeneratorTests.cs
git commit -m "feat(core): add solvable ContractGenerator (season-valid CC partition)"
```

---

## Done criteria for Plan 02

- `dotnet test` green: generator guarantees full CC coverage, season-validity, theme-correctness, and seed-determinism.
- Core still references no game assemblies.
- The contract domain model (`CcItem` → `Contract` → `YearPlan`) is ready for: the real `Data/Bundles` adapter (later), week sequencing + championing + gate wiring (Plan 03), and the bonus/liability effect layer (Plan 06).

## Self-review notes (spec coverage)

- **5 themes mapped to CC rooms** (spec §3) → `Theme` (Task 1).
- **Each contract = required items + bonus + liability** (spec §3) → `Contract` + `ThemeModifiers` (Tasks 2–3).
- **Solvability guarantee: union completes the CC, in-season only** (spec §10) → `ContractGenerator` coverage + season-validity tests (Task 4).
- **Per-run re-randomization** (spec §10) → seeded generation; a fresh seed per run yields a different valid partition.
- **Cumulative donation satisfaction** (spec §6) → `Contract.IsSatisfiedBy` over the run's accumulated donations (Task 3).
- Deferred: difficulty smoothing / early-week easing (spec §10 refinement), real bundle-data adapter, championing + gates (Plan 03), effect application (Plan 06).
