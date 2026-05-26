# The Longest Year — v1 Plan 03: Run Lifecycle & In-Place World Reset

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the tested pure logic from Plans 01–02 into the *first playable, resettable loop*: wire the weekly + monthly gates to the day-end events, add the weekly championing flow, and perform a safe **in-place world rewind to Spring 1** (guarded by a one-time save backup and proven by a two-loop leak test).

**Architecture:** Keep the split. New **pure** decision logic goes in `TheLongestYear.Core` and is TDD'd with `dotnet test` (no game): a hand-authored CC ground-truth catalog, a serializable `RunState`, the seeded weekly-champion offer, a `RunManager` that maps a day-end into a `RunAction` (via the existing `GateEvaluator`), and a `WorldFingerprint` whose `Diff` powers the leak test. The **game-integration** layer in `TheLongestYear` (the SMAPI mod) executes those decisions: a `WorldResetService` that leans on the game's own `Game1.loadForNewGame` plus targeted manual resets, a `WorldStateProbe` that captures fingerprints, a one-time `SaveBackup`, and a `RunController` that subscribes to `DayEnding`/`DayStarted` and drives the loop. Reset/loop code is verified **in-game** (I deploy, the user plays, I read the SMAPI log).

**Reset approach (researched against the 1.6 Android decompile, which matches our PC 1.6 target):** `Game1.loadForNewGame(loadedGame: false)` is the game's own new-game initializer — it clears `Game1.locations`, rebuilds the `Farm` and every location, **regenerates the CC bundles (donation progress wiped for free)**, re-adds NPCs, and resets the date toward Spring/year-1. We reuse it for the heavy lifting, then apply targeted resets for what it leaves on the persistent `Game1.player` object (money, stamina, skill XP + levels, professions, inventory, friendships, mail, events, quests) and for mine progress. This is the "fix the data, don't fight the system" path from workspace memory — no fragile per-collection hacks.

**CC ground-truth decision:** Plan 03 uses a **hand-authored minimal `CcItemCatalog`** in Core (a curated `CcItem` set spread across the 5 themes and 4 seasons, enough to generate a solvable `YearPlan` and drive the loop). A real `Data/Bundles` adapter is **deferred to Plan 04** — the vanilla bundle strings encode *which item / which room* but not rarity or obtainable-season, the two fields the solvability guarantee needs, so a catalog with explicit metadata is the correct unit now and the bundle adapter becomes a data source feeding the same `CcItem` shape later.

**Tech Stack:** C# / .NET 6, SMAPI 4.x (`Pathoschild.Stardew.ModBuildConfig` 4.*), Harmony (available, unused here), xUnit. Stardew 1.6 on PC.

**Repo conventions:** Work on branch `feat/v1-plan-03-lifecycle-reset`. Local commits only — **never push or publish without explicit approval**. End every commit body with:
`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
Files < 400 lines; all tuning values live in `GameplayConfig`; Core references no game assemblies.

**Persistence rule (do NOT regress):** meta-state *and* run-state are per-save data committed **only** in the game's `Saving` event — never eagerly — so Junimo Points can't be save-scummed out of a doomed run. Plan 03 extends the same `MetaStore` to also persist `RunState`.

---

## File layout after Plan 03

```
src/TheLongestYear.Core/                 (pure, unit-tested)
  CcItemCatalog.cs        — NEW: hand-authored CC ground-truth + rarity lookup
  RunState.cs             — NEW: serializable per-run progress (date, ledger, championing, seed)
  ChampionService.cs      — NEW: seeded weekly 1-of-2 theme offer + "5th never championed"
  RunManager.cs           — NEW: day-end -> RunAction, wrapping GateEvaluator
  WorldFingerprint.cs     — NEW: baseline snapshot + Diff (leak-test comparison)
  MetaState.cs            — MODIFY: add BackupDone flag
src/TheLongestYear/                       (SMAPI mod, verified in-game)
  MetaStore.cs            — MODIFY: also load/save RunState as per-save data
  ModEntry.cs             — MODIFY: build YearPlan, own RunController, register commands
  Loop/
    WorldResetService.cs  — NEW: in-place rewind (loadForNewGame + targeted resets)
    FarmerReset.cs        — NEW: Farmer baseline reset (split out to stay < 400 lines)
    WorldStateProbe.cs    — NEW: capture a WorldFingerprint from the live game
    SaveBackup.cs         — NEW: one-time save-folder backup before the first reset
    RunController.cs       — NEW: SMAPI wiring of the loop (events, offer, fail/advance/win)
tests/TheLongestYear.Tests/
  CcItemCatalogTests.cs   — NEW
  RunStateTests.cs        — NEW
  ChampionServiceTests.cs — NEW
  RunManagerTests.cs      — NEW
  WorldFingerprintTests.cs— NEW
```

---

## Task 0: Branch

**Files:** none (git only)

- [ ] **Step 1: Create and switch to the feature branch**

Run from repo root `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`:

```bash
git checkout -b feat/v1-plan-03-lifecycle-reset
git branch --show-current
```

Expected: `feat/v1-plan-03-lifecycle-reset`.

- [ ] **Step 2: Confirm the baseline is green before changing anything**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (the 50 existing tests from Plans 01–02).

---

## Task 1: Core — `CcItemCatalog` (hand-authored CC ground truth)

**Files:**
- Create: `src/TheLongestYear.Core/CcItemCatalog.cs`
- Test: `tests/TheLongestYear.Tests/CcItemCatalogTests.cs`

The catalog is a curated `CcItem` set covering all 5 themes across the 4 seasons. It must produce a *solvable* `YearPlan` through the existing `ContractGenerator` (reusing Plan 02's invariants), and expose a rarity lookup for the interim JP award (Task 10).

- [ ] **Step 1: Write the failing catalog tests**

Create `tests/TheLongestYear.Tests/CcItemCatalogTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcItemCatalogTests
{
    [Fact]
    public void Catalog_is_non_empty_and_ids_are_unique()
    {
        var items = CcItemCatalog.Items;
        Assert.NotEmpty(items);
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(Theme.Foraging)]
    [InlineData(Theme.Farming)]
    [InlineData(Theme.Fishing)]
    [InlineData(Theme.Mining)]
    [InlineData(Theme.Mixed)]
    public void Every_theme_is_represented(Theme theme)
        => Assert.Contains(CcItemCatalog.Items, i => i.Theme == theme);

    [Fact]
    public void Every_season_has_at_least_one_obtainable_item()
    {
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
            Assert.Contains(CcItemCatalog.Items, i => i.IsObtainableIn(s));
    }

    [Fact]
    public void Catalog_generates_a_solvable_year_plan()
    {
        var plan = new ContractGenerator().Generate(CcItemCatalog.Items, seed: 1);
        var assigned = plan.Contracts.SelectMany(c => c.RequiredItemIds).ToList();

        // Full coverage: every catalog item assigned exactly once.
        Assert.Equal(CcItemCatalog.Items.Count, assigned.Count);
        Assert.Equal(CcItemCatalog.Items.Select(i => i.Id).OrderBy(x => x),
                     assigned.OrderBy(x => x));

        // Season-validity + theme-correctness for every placed item.
        var byId = CcItemCatalog.Items.ToDictionary(i => i.Id);
        foreach (var c in plan.Contracts)
            foreach (var id in c.RequiredItemIds)
            {
                Assert.Equal(byId[id].Theme, c.Theme);
                Assert.True(byId[id].IsObtainableIn(c.Season));
            }
    }

    [Fact]
    public void RarityOf_returns_the_items_rarity_and_a_default_for_unknown()
    {
        var sample = CcItemCatalog.Items.First();
        Assert.Equal(sample.Rarity, CcItemCatalog.RarityOf(sample.Id));
        Assert.Equal(Rarity.Common, CcItemCatalog.RarityOf("not-a-real-id"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `CcItemCatalog` does not exist.

- [ ] **Step 3: Implement `CcItemCatalog`**

Create `src/TheLongestYear.Core/CcItemCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Hand-authored minimal Community Center ground truth for v1. Each <see cref="CcItem"/> is tagged
/// with the theme/room it belongs to, its rarity (JP value), and the seasons it can be obtained.
/// This is intentionally curated, not exhaustive: it is enough to generate a solvable year-plan and
/// drive the loop. A real Data/Bundles adapter (which supplies item+room but not rarity/season) is
/// deferred to Plan 04 and will feed this same CcItem shape.
/// </summary>
public static class CcItemCatalog
{
    private static readonly IReadOnlyList<CcItem> _items = Build();
    private static readonly IReadOnlyDictionary<string, Rarity> _rarityById =
        _items.ToDictionary(i => i.Id, i => i.Rarity);

    public static IReadOnlyList<CcItem> Items => _items;

    /// <summary>Rarity for a catalog item id; <see cref="Rarity.Common"/> for anything unknown.</summary>
    public static Rarity RarityOf(string itemId)
        => _rarityById.TryGetValue(itemId, out Rarity r) ? r : Rarity.Common;

    private static CcItem Item(string id, Theme theme, Rarity rarity, params Season[] seasons)
        => new CcItem(id, theme, rarity, new HashSet<Season>(seasons));

    private static IReadOnlyList<CcItem> Build() => new List<CcItem>
    {
        // Foraging (Crafts Room) — one seasonal forage each, plus a rare.
        Item("WildHorseradish", Theme.Foraging, Rarity.Common,   Season.Spring),
        Item("Spice Berry",     Theme.Foraging, Rarity.Common,   Season.Summer),
        Item("CommonMushroom",  Theme.Foraging, Rarity.Common,   Season.Fall),
        Item("Crocus",          Theme.Foraging, Rarity.Uncommon, Season.Winter),
        Item("Morel",           Theme.Foraging, Rarity.Rare,     Season.Spring, Season.Fall),

        // Farming (Pantry) — staple crops + a banked rarity (Red Cabbage, Summer).
        Item("Parsnip",         Theme.Farming,  Rarity.Common,   Season.Spring),
        Item("Melon",           Theme.Farming,  Rarity.Uncommon, Season.Summer),
        Item("Pumpkin",         Theme.Farming,  Rarity.Uncommon, Season.Fall),
        Item("RedCabbage",      Theme.Farming,  Rarity.VeryRare, Season.Summer),

        // Fishing (Fish Tank) — seasonal fish + a rare.
        Item("Sardine",         Theme.Fishing,  Rarity.Common,   Season.Spring, Season.Fall, Season.Winter),
        Item("Sunfish",         Theme.Fishing,  Rarity.Common,   Season.Spring, Season.Summer),
        Item("Salmon",          Theme.Fishing,  Rarity.Uncommon, Season.Fall),
        Item("Catfish",         Theme.Fishing,  Rarity.Rare,     Season.Spring, Season.Fall),

        // Mining (Boiler Room) — bars/minerals obtainable year-round, plus a gem.
        Item("CopperBar",       Theme.Mining,   Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("IronBar",         Theme.Mining,   Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("Quartz",          Theme.Mining,   Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("FrozenTear",      Theme.Mining,   Rarity.Uncommon, Season.Winter),
        Item("Diamond",         Theme.Mining,   Rarity.Rare,     Season.Spring, Season.Summer, Season.Fall, Season.Winter),

        // Mixed (Bulletin Board) — cross-cutting items, spread across seasons.
        Item("Egg",             Theme.Mixed,    Rarity.Common,   Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("Wool",            Theme.Mixed,    Rarity.Uncommon, Season.Spring, Season.Summer, Season.Fall, Season.Winter),
        Item("Honey",           Theme.Mixed,    Rarity.Uncommon, Season.Spring, Season.Summer, Season.Fall),
        Item("Truffle",         Theme.Mixed,    Rarity.Rare,     Season.Fall, Season.Winter),
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (catalog generates a solvable plan; rarity lookup works).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/CcItemCatalog.cs tests/TheLongestYear.Tests/CcItemCatalogTests.cs
git commit -m "feat(core): add hand-authored CcItemCatalog (CC ground truth) for v1 loop"
```

---

## Task 2: Core — `RunState` (serializable per-run progress)

**Files:**
- Create: `src/TheLongestYear.Core/RunState.cs`
- Test: `tests/TheLongestYear.Tests/RunStateTests.cs`

`RunState` is the loop's mutable per-run progress, persisted as per-save data (Task 6). It holds the date mirror, the cumulative donation ledger (item ids — quantities/JP wiring are Plan 04), the championing record, and the contract-generation seed (so a reload reproduces the same `YearPlan`).

- [ ] **Step 1: Write the failing RunState tests**

Create `tests/TheLongestYear.Tests/RunStateTests.cs`:

```csharp
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunStateTests
{
    [Fact]
    public void New_run_state_starts_at_spring_one_week_one()
    {
        var run = new RunState();
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Equal(1, run.WeekOfYear);
        Assert.Equal(1, run.RunNumber);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.ChampionedThemesThisMonth);
        Assert.Null(run.CurrentChampion);
    }

    [Fact]
    public void WeekOfYear_combines_season_and_day()
    {
        var run = new RunState { Season = Season.Summer, DayOfMonth = 8 };
        Assert.Equal(6, run.WeekOfYear); // month index 1 -> 4 weeks + week 2 = 6
    }

    [Fact]
    public void RecordDonation_is_idempotent_per_item_id()
    {
        var run = new RunState();
        run.RecordDonation("Parsnip");
        run.RecordDonation("Parsnip");
        Assert.Single(run.DonatedItemIds);
        Assert.Contains("Parsnip", run.DonatedSet());
    }

    [Fact]
    public void Champion_records_current_and_adds_to_month_set()
    {
        var run = new RunState();
        run.Champion(Theme.Mining);
        Assert.Equal(Theme.Mining, run.CurrentChampion);
        Assert.True(run.IsChampioned(Theme.Mining));
        run.Champion(Theme.Mining); // re-championing same theme does not duplicate
        Assert.Single(run.ChampionedThemesThisMonth);
    }

    [Fact]
    public void BeginNewMonth_advances_season_and_clears_championing_only()
    {
        var run = new RunState();
        run.RecordDonation("Parsnip");
        run.Champion(Theme.Mining);
        run.BeginNewMonth(Season.Summer);

        Assert.Equal(Season.Summer, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.ChampionedThemesThisMonth);
        Assert.Null(run.CurrentChampion);
        Assert.Contains("Parsnip", run.DonatedSet()); // donations are cumulative across months
    }

    [Fact]
    public void BeginNewRun_resets_everything_and_bumps_run_number()
    {
        var run = new RunState { RunNumber = 3 };
        run.RecordDonation("Parsnip");
        run.Champion(Theme.Mining);
        run.Season = Season.Winter;
        run.DayOfMonth = 28;

        run.BeginNewRun(seed: 99);

        Assert.Equal(4, run.RunNumber);
        Assert.Equal(99, run.Seed);
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.ChampionedThemesThisMonth);
        Assert.Null(run.CurrentChampion);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var run = new RunState
        {
            Seed = 42, RunNumber = 2, Season = Season.Fall, DayOfMonth = 15,
            DonatedItemIds = { "Parsnip", "CopperBar" },
            ChampionedThemesThisMonth = { Theme.Mining },
            CurrentChampion = Theme.Mining
        };

        string json = JsonSerializer.Serialize(run);
        RunState restored = JsonSerializer.Deserialize<RunState>(json)!;

        Assert.Equal(42, restored.Seed);
        Assert.Equal(2, restored.RunNumber);
        Assert.Equal(Season.Fall, restored.Season);
        Assert.Equal(15, restored.DayOfMonth);
        Assert.Equal(new[] { "Parsnip", "CopperBar" }, restored.DonatedItemIds);
        Assert.Equal(new[] { Theme.Mining }, restored.ChampionedThemesThisMonth);
        Assert.Equal(Theme.Mining, restored.CurrentChampion);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `RunState` does not exist.

- [ ] **Step 3: Implement `RunState`**

Create `src/TheLongestYear.Core/RunState.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Mutable per-run progress for one loop attempt. Persisted as per-save data and committed only with
/// the game's own save (see MetaStore) so it cannot be save-scummed. Lists (not sets) keep JSON
/// round-tripping simple; membership helpers enforce uniqueness.
/// </summary>
public sealed class RunState
{
    /// <summary>Seed used to generate this run's YearPlan; stored so a reload reproduces the plan.</summary>
    public int Seed { get; set; }

    /// <summary>1-based attempt counter (loop number), for logging and the narrative layer.</summary>
    public int RunNumber { get; set; } = 1;

    public Season Season { get; set; } = Season.Spring;

    public int DayOfMonth { get; set; } = 1;

    /// <summary>Cumulative donated item ids this run (consumed-on-donate; quantities/JP land in Plan 04).</summary>
    public List<string> DonatedItemIds { get; set; } = new();

    /// <summary>Themes championed in the current month (cleared each month). The 5th is never championed.</summary>
    public List<Theme> ChampionedThemesThisMonth { get; set; } = new();

    /// <summary>The theme championed this week, whose bonus/liability are active. Null between weeks.</summary>
    public Theme? CurrentChampion { get; set; }

    public int WeekOfYear => Calendar.WeekOfYear((int)Season, DayOfMonth);

    public int WeekInMonth => Calendar.WeekInMonth(DayOfMonth);

    public bool IsChampioned(Theme theme) => ChampionedThemesThisMonth.Contains(theme);

    /// <summary>Add a donated item id; idempotent so re-donating the same id is a no-op in the ledger.</summary>
    public void RecordDonation(string itemId)
    {
        if (!DonatedItemIds.Contains(itemId))
            DonatedItemIds.Add(itemId);
    }

    /// <summary>The donation ledger as a set, for Contract.IsSatisfiedBy.</summary>
    public ISet<string> DonatedSet() => new HashSet<string>(DonatedItemIds);

    /// <summary>Champion a theme for this week: set current and add to the month's championed set.</summary>
    public void Champion(Theme theme)
    {
        CurrentChampion = theme;
        if (!ChampionedThemesThisMonth.Contains(theme))
            ChampionedThemesThisMonth.Add(theme);
    }

    /// <summary>Advance to a new month: change season, reset to day 1, clear championing. Donations persist.</summary>
    public void BeginNewMonth(Season season)
    {
        Season = season;
        DayOfMonth = 1;
        ChampionedThemesThisMonth.Clear();
        CurrentChampion = null;
    }

    /// <summary>Start a fresh loop attempt: reset to Spring 1, wipe ledger + championing, set the new seed.</summary>
    public void BeginNewRun(int seed)
    {
        RunNumber += 1;
        Seed = seed;
        Season = Season.Spring;
        DayOfMonth = 1;
        DonatedItemIds.Clear();
        ChampionedThemesThisMonth.Clear();
        CurrentChampion = null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/RunState.cs tests/TheLongestYear.Tests/RunStateTests.cs
git commit -m "feat(core): add serializable RunState (date, ledger, championing, seed)"
```

---

## Task 3: Core — `ChampionService` (seeded weekly 1-of-2 offer)

**Files:**
- Create: `src/TheLongestYear.Core/ChampionService.cs`
- Test: `tests/TheLongestYear.Tests/ChampionServiceTests.cs`

Each week the player is offered **2** themes from those not yet championed this month and picks 1 (spec §4). The offer is a pure function of `(Seed, WeekOfYear, ChampionedThemesThisMonth)` so it is stable when re-queried within a week and deterministic across reloads. With 5 themes and 4 championing weeks, there are always ≥ 2 candidates, so the offer is always exactly 2; the un-championed 5th falls out naturally.

> Plan 03 simplification: the offer is drawn from not-yet-championed themes only. The spec's "completable that week" filter depends on obtainability + bonus/liability effects (Plan 06) and is deferred; noted in the code.

- [ ] **Step 1: Write the failing ChampionService tests**

Create `tests/TheLongestYear.Tests/ChampionServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ChampionServiceTests
{
    [Fact]
    public void Offer_has_two_distinct_themes()
    {
        var run = new RunState { Seed = 1 };
        var offer = ChampionService.OfferForWeek(run);
        Assert.Equal(2, offer.Count);
        Assert.Equal(2, offer.Distinct().Count());
    }

    [Fact]
    public void Offer_is_deterministic_for_the_same_seed_and_week()
    {
        var a = ChampionService.OfferForWeek(new RunState { Seed = 7 });
        var b = ChampionService.OfferForWeek(new RunState { Seed = 7 });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Offer_excludes_already_championed_themes()
    {
        var run = new RunState { Seed = 3 };
        run.Champion(Theme.Mining);
        var offer = ChampionService.OfferForWeek(run);
        Assert.DoesNotContain(Theme.Mining, offer);
    }

    [Fact]
    public void Over_a_month_exactly_four_distinct_themes_can_be_championed()
    {
        var run = new RunState { Seed = 11 };
        var championed = new List<Theme>();

        for (int week = 1; week <= 4; week++)
        {
            run.DayOfMonth = (week - 1) * 7 + 1; // 1, 8, 15, 22
            var offer = ChampionService.OfferForWeek(run);
            Assert.Equal(2, offer.Count);
            Assert.All(offer, t => Assert.DoesNotContain(t, championed));
            run.Champion(offer[0]);             // always pick the first offered
            championed.Add(offer[0]);
        }

        Assert.Equal(4, championed.Distinct().Count());
        Assert.Equal(5, run.ChampionedThemesThisMonth.Count == 4 ? 5 : 5); // sanity: 5 themes total exist
        Assert.Single(System.Enum.GetValues(typeof(Theme)).Cast<Theme>().Except(championed)); // exactly 1 never championed
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `ChampionService` does not exist.

- [ ] **Step 3: Implement `ChampionService`**

Create `src/TheLongestYear.Core/ChampionService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Produces the weekly 1-of-2 champion offer (spec §4). The offer is a pure, deterministic function of
/// the run's seed, the current week-of-year, and which themes are already championed this month — so it
/// is stable across re-queries within a week and across reloads.
/// </summary>
public static class ChampionService
{
    private const int WeekSaltPrime = 7919;

    /// <summary>The number of themes offered each week.</summary>
    public const int OfferSize = 2;

    /// <summary>
    /// Up to <see cref="OfferSize"/> distinct themes not yet championed this month, seeded-deterministic.
    /// (Plan 06 will further restrict to themes completable that week.)
    /// </summary>
    public static IReadOnlyList<Theme> OfferForWeek(RunState run)
    {
        if (run is null)
            throw new ArgumentNullException(nameof(run));

        // Stable candidate order, then a seeded shuffle keyed by (seed, week).
        List<Theme> candidates = Enum.GetValues(typeof(Theme))
            .Cast<Theme>()
            .Where(t => !run.IsChampioned(t))
            .OrderBy(t => (int)t)
            .ToList();

        var rng = new Random(run.Seed ^ (run.WeekOfYear * WeekSaltPrime));
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        return candidates.Take(OfferSize).ToList();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/ChampionService.cs tests/TheLongestYear.Tests/ChampionServiceTests.cs
git commit -m "feat(core): add seeded weekly champion offer (1-of-2, deterministic)"
```

---

## Task 4: Core — `RunManager` (day-end → `RunAction`)

**Files:**
- Create: `src/TheLongestYear.Core/RunManager.cs`
- Test: `tests/TheLongestYear.Tests/RunManagerTests.cs`

`RunManager` is the pure brain the mod calls at day end. It computes the two gate inputs from `RunState` + `YearPlan` (championed contract satisfied? all five satisfied?), runs the existing `GateEvaluator`, and translates the result into a richer `RunAction` the mod can execute.

- [ ] **Step 1: Write the failing RunManager tests**

Create `tests/TheLongestYear.Tests/RunManagerTests.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunManagerTests
{
    // A YearPlan where, per season, Mining requires "ore" and the other four themes require nothing.
    private static YearPlan PlanRequiringMiningOre()
    {
        var contracts = new List<Contract>();
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
            foreach (Theme t in System.Enum.GetValues(typeof(Theme)))
            {
                var required = t == Theme.Mining ? new[] { "ore" } : System.Array.Empty<string>();
                var (b, l) = ThemeModifiers.For(t);
                contracts.Add(new Contract(s, t, required, b, l));
            }
        return new YearPlan(contracts);
    }

    private static RunManager Mgr() => new RunManager(new GateEvaluator());

    [Fact]
    public void Midweek_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 3 };
        run.Champion(Theme.Mining);
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Week_end_with_satisfied_champion_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        run.Champion(Theme.Mining);
        run.RecordDonation("ore");
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Week_end_with_unsatisfied_champion_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        run.Champion(Theme.Mining); // "ore" not donated
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Week_end_with_no_champion_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 }; // never championed
        run.RecordDonation("ore");
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Month_end_all_five_done_not_winter_advances_month()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        run.Champion(Theme.Mining);
        run.RecordDonation("ore"); // only Mining requires anything; all five now satisfied
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Month_end_missing_required_item_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        run.Champion(Theme.Foraging); // champion an empty contract -> championed-complete is true
        // "ore" NOT donated, so the (unchampioned) Mining contract is incomplete -> monthly fail.
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Winter_end_all_done_wins()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.Champion(Theme.Mining);
        run.RecordDonation("ore");
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `RunManager` / `RunAction` do not exist.

- [ ] **Step 3: Implement `RunManager` and `RunAction`**

Create `src/TheLongestYear.Core/RunManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What the mod should do after a day ends.</summary>
public enum RunAction
{
    /// <summary>Keep playing (mid-week, or a passed weekly checkpoint that isn't month-end).</summary>
    Continue,
    /// <summary>A gate failed — perform the in-place reset and start a fresh run.</summary>
    FailReset,
    /// <summary>Month cleared (not Winter) — let the game advance; clear this month's championing.</summary>
    AdvanceMonth,
    /// <summary>Winter cleared with the whole CC done — the loop breaks.</summary>
    Win
}

/// <summary>
/// The day-end decision maker. Derives the gate inputs from the run's ledger against its YearPlan,
/// runs <see cref="GateEvaluator"/>, and maps the result to a <see cref="RunAction"/>.
/// </summary>
public sealed class RunManager
{
    private readonly GateEvaluator _gate;

    public RunManager(GateEvaluator gate) => _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public RunAction EvaluateDayEnd(RunState run, YearPlan plan)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (plan is null) throw new ArgumentNullException(nameof(plan));

        ISet<string> donated = run.DonatedSet();

        // No champion at week's end is a weekly failure (you must champion one each week).
        bool championedComplete =
            run.CurrentChampion.HasValue &&
            plan.Get(run.Season, run.CurrentChampion.Value).IsSatisfiedBy(donated);

        bool allFiveComplete = plan.ForSeason(run.Season).All(c => c.IsSatisfiedBy(donated));

        GateResult result = _gate.EvaluateDayEnd(
            run.DayOfMonth, (int)run.Season, championedComplete, allFiveComplete);

        return result switch
        {
            GateResult.WeeklyFail => RunAction.FailReset,
            GateResult.MonthlyFail => RunAction.FailReset,
            GateResult.Win => RunAction.Win,
            GateResult.Continue when Calendar.IsMonthEnd(run.DayOfMonth) && allFiveComplete
                => RunAction.AdvanceMonth,
            _ => RunAction.Continue
        };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/RunManager.cs tests/TheLongestYear.Tests/RunManagerTests.cs
git commit -m "feat(core): add RunManager mapping day-end to RunAction via GateEvaluator"
```

---

## Task 5: Core — `WorldFingerprint` (leak-test comparison)

**Files:**
- Create: `src/TheLongestYear.Core/WorldFingerprint.cs`
- Test: `tests/TheLongestYear.Tests/WorldFingerprintTests.cs`

The two-loop leak test (spec §12) catches state that leaks between runs. The *gathering* needs the game (Task 8), but the *comparison* is pure: a flat snapshot of baseline-relevant scalars with a `Diff` that names every field that differs.

- [ ] **Step 1: Write the failing WorldFingerprint tests**

Create `tests/TheLongestYear.Tests/WorldFingerprintTests.cs`:

```csharp
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WorldFingerprintTests
{
    private static WorldFingerprint Baseline() => new WorldFingerprint
    {
        Year = 1, Season = Season.Spring, DayOfMonth = 1,
        Money = 500, Stamina = 270, InventoryItemCount = 0, TotalSkillXp = 0,
        CropCount = 0, PlacedObjectCount = 0, BuildingCount = 1,
        CompletedBundleCount = 0, FriendshipCount = 0, MailReceivedCount = 0,
        EventsSeenCount = 1, LowestMineLevel = -1
    };

    [Fact]
    public void Identical_fingerprints_have_no_diff_and_match()
    {
        var a = Baseline();
        var b = Baseline();
        Assert.Empty(a.Diff(b));
        Assert.True(a.Matches(b));
    }

    [Fact]
    public void A_single_difference_is_named_and_breaks_match()
    {
        var a = Baseline();
        var b = Baseline();
        b.CropCount = 3;

        var diff = a.Diff(b);
        Assert.Single(diff);
        Assert.Contains("CropCount", diff[0]);
        Assert.False(a.Matches(b));
    }

    [Fact]
    public void Multiple_differences_are_all_named()
    {
        var a = Baseline();
        var b = Baseline();
        b.Money = 999;
        b.LowestMineLevel = 40;

        var diff = a.Diff(b);
        Assert.Equal(2, diff.Count);
        Assert.Contains(diff, d => d.Contains("Money"));
        Assert.Contains(diff, d => d.Contains("LowestMineLevel"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `WorldFingerprint` does not exist.

- [ ] **Step 3: Implement `WorldFingerprint`**

Create `src/TheLongestYear.Core/WorldFingerprint.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// A flat snapshot of baseline-relevant world/player scalars, captured by the mod after a reset.
/// Comparing two post-reset fingerprints (the two-loop leak test) reveals any state that leaks
/// between runs — the classic in-place-reset failure mode (spec §12).
/// </summary>
public sealed class WorldFingerprint
{
    public int Year { get; set; }
    public Season Season { get; set; }
    public int DayOfMonth { get; set; }
    public int Money { get; set; }
    public int Stamina { get; set; }
    public int InventoryItemCount { get; set; }
    public int TotalSkillXp { get; set; }
    public int CropCount { get; set; }
    public int PlacedObjectCount { get; set; }
    public int BuildingCount { get; set; }
    public int CompletedBundleCount { get; set; }
    public int FriendshipCount { get; set; }
    public int MailReceivedCount { get; set; }
    public int EventsSeenCount { get; set; }
    public int LowestMineLevel { get; set; }

    /// <summary>Field-by-field differences vs <paramref name="other"/>; empty means a clean match.</summary>
    public IReadOnlyList<string> Diff(WorldFingerprint other)
    {
        var diffs = new List<string>();
        void Cmp(string name, object a, object b)
        {
            if (!Equals(a, b)) diffs.Add($"{name}: {a} -> {b}");
        }

        Cmp(nameof(Year), Year, other.Year);
        Cmp(nameof(Season), Season, other.Season);
        Cmp(nameof(DayOfMonth), DayOfMonth, other.DayOfMonth);
        Cmp(nameof(Money), Money, other.Money);
        Cmp(nameof(Stamina), Stamina, other.Stamina);
        Cmp(nameof(InventoryItemCount), InventoryItemCount, other.InventoryItemCount);
        Cmp(nameof(TotalSkillXp), TotalSkillXp, other.TotalSkillXp);
        Cmp(nameof(CropCount), CropCount, other.CropCount);
        Cmp(nameof(PlacedObjectCount), PlacedObjectCount, other.PlacedObjectCount);
        Cmp(nameof(BuildingCount), BuildingCount, other.BuildingCount);
        Cmp(nameof(CompletedBundleCount), CompletedBundleCount, other.CompletedBundleCount);
        Cmp(nameof(FriendshipCount), FriendshipCount, other.FriendshipCount);
        Cmp(nameof(MailReceivedCount), MailReceivedCount, other.MailReceivedCount);
        Cmp(nameof(EventsSeenCount), EventsSeenCount, other.EventsSeenCount);
        Cmp(nameof(LowestMineLevel), LowestMineLevel, other.LowestMineLevel);
        return diffs;
    }

    public bool Matches(WorldFingerprint other) => Diff(other).Count == 0;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/WorldFingerprint.cs tests/TheLongestYear.Tests/WorldFingerprintTests.cs
git commit -m "feat(core): add WorldFingerprint + Diff for the two-loop leak test"
```

---

## Task 6: Mod — persist `RunState` and add `BackupDone`

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Modify: `src/TheLongestYear/MetaStore.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`
- Test: `tests/TheLongestYear.Tests/MetaStateTests.cs` (extend)

`RunState` must persist alongside `MetaState` under the same anti-save-scum rule — committed only on `Saving`. We extend `MetaStore` to hold and round-trip both, and add a `BackupDone` flag to `MetaState` (banked-forever) so the save backup runs exactly once.

- [ ] **Step 1: Add a failing test for `BackupDone` round-tripping**

Append to `tests/TheLongestYear.Tests/MetaStateTests.cs` (inside the existing `MetaStateTests` class):

```csharp
    [Fact]
    public void BackupDone_round_trips_and_defaults_false()
    {
        Assert.False(new MetaState().BackupDone);

        var original = new MetaState { BackupDone = true };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.True(restored.BackupDone);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `MetaState.BackupDone` does not exist.

- [ ] **Step 3: Add `BackupDone` to `MetaState`**

In `src/TheLongestYear.Core/MetaState.cs`, add this property after `StashCapacityTier`:

```csharp
    /// <summary>True once the one-time pre-first-reset save backup has been taken (banked forever).</summary>
    public bool BackupDone { get; set; }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Extend `MetaStore` to also persist `RunState`**

Overwrite `src/TheLongestYear/MetaStore.cs`:

```csharp
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    /// <summary>
    /// Loads and persists both banked meta-state and the active run-state as per-save data, scoped to
    /// one playthrough and committed as part of the game's own save (never eagerly) — so neither can be
    /// save-scummed.
    /// </summary>
    internal sealed class MetaStore
    {
        private const string MetaDataKey = "meta-state";
        private const string RunDataKey = "run-state";
        private readonly IDataHelper _data;

        public MetaState State { get; private set; } = new MetaState();
        public RunState Run { get; private set; } = new RunState();

        public MetaStore(IDataHelper data) => _data = data;

        /// <summary>Load this playthrough's banked progress and active run. Call when a save is loaded.</summary>
        public void Load()
        {
            State = _data.ReadSaveData<MetaState>(MetaDataKey) ?? new MetaState();
            Run = _data.ReadSaveData<RunState>(RunDataKey) ?? new RunState();
        }

        /// <summary>Commit banked progress and run-state into the save. Call from the game's Saving event.</summary>
        public void Save()
        {
            _data.WriteSaveData(MetaDataKey, State);
            _data.WriteSaveData(RunDataKey, Run);
        }
    }
}
```

- [ ] **Step 6: Update `ModEntry` log lines to mention the run**

In `src/TheLongestYear/ModEntry.cs`, replace the body of `OnSaveLoaded` with:

```csharp
            _meta.Load();
            this.Monitor.Log(
                $"Run {_meta.Run.RunNumber} loaded ({_meta.Run.Season} {_meta.Run.DayOfMonth}). JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);
```

> Wiring the `RunController` into `ModEntry` happens in Task 10; this task only ensures `RunState` persists.

- [ ] **Step 7: Build the mod and confirm it deploys**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds; ModBuildConfig copies the mod + `TheLongestYear.Core.dll` to the Stardew `Mods/TheLongestYear` folder.

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/MetaState.cs src/TheLongestYear/MetaStore.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "feat(mod): persist RunState as per-save data; add one-time BackupDone flag"
```

---

## Task 7: Mod — `WorldResetService` + `FarmerReset` (the in-place rewind)

**Files:**
- Create: `src/TheLongestYear/Loop/WorldResetService.cs`
- Create: `src/TheLongestYear/Loop/FarmerReset.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (add the `tly_reset` debug command)
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs` (add `StartingMoney`)

This is the riskiest part of the mod and is verified **in-game**. The reset reuses `Game1.loadForNewGame(loadedGame: false)` (rebuilds farm + locations, regenerates CC bundles, resets the date) and then applies targeted resets for the persistent `Game1.player` and mine progress. We trigger it via a standalone `tly_reset` command **first**, so it can be iterated in isolation before being wired to the fail path (Task 10).

> **No unit tests here** — this code touches `Game1`. It is exercised by the in-game steps and the Task 8 leak test. Per workspace memory, PC-first uses direct field access; the Android port will swap platform-differing members to `AccessTools` reflection.

- [ ] **Step 1: Add `StartingMoney` to `GameplayConfig`**

In `src/TheLongestYear.Core/GameplayConfig.cs`, add inside the class:

```csharp
    /// <summary>Gold the farmer starts each run with after a reset.</summary>
    public int StartingMoney { get; set; } = 500;
```

- [ ] **Step 2: Implement `FarmerReset`**

Create `src/TheLongestYear/Loop/FarmerReset.cs`:

```csharp
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Resets the persistent <see cref="Farmer"/> to a run baseline. Game1.loadForNewGame rebuilds the
    /// world but leaves the existing player's money/skills/inventory/relationships intact, so we clear
    /// them here. (Plan 07 will carve the Junimo Stash out of the inventory wipe.)
    /// </summary>
    internal static class FarmerReset
    {
        public static void ToBaseline(Farmer p, int startingMoney)
        {
            p.Money = startingMoney;

            // Inventory — wipe entirely for now (Stash preservation is Plan 07).
            p.Items.Clear();

            // Skills: XP array + the six derived levels.
            for (int i = 0; i < p.experiencePoints.Count; i++)
                p.experiencePoints[i] = 0;
            p.farmingLevel.Value = 0;
            p.miningLevel.Value = 0;
            p.fishingLevel.Value = 0;
            p.foragingLevel.Value = 0;
            p.combatLevel.Value = 0;
            p.luckLevel.Value = 0;
            p.professions.Clear();

            // Relationships, mail, events, quests.
            p.friendshipData.Clear();
            p.mailReceived.Clear();
            p.eventsSeen.Clear();
            p.questLog.Clear();

            // Suppress the vanilla intro cutscene from replaying every loop (matches TitleMenu's new-game path).
            p.eventsSeen.Add("60367");

            // Vitals to full.
            p.stamina = p.maxStamina.Value;
            p.health = p.maxHealth;
        }
    }
}
```

- [ ] **Step 3: Implement `WorldResetService`**

Create `src/TheLongestYear/Loop/WorldResetService.cs`:

```csharp
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Performs the in-place rewind to Spring 1. Reuses the game's own new-game initializer
    /// (Game1.loadForNewGame) for the heavy lifting — it clears Game1.locations, rebuilds the Farm and
    /// every location, regenerates the CC bundles (wiping donation progress), and re-adds NPCs — then
    /// applies targeted resets for the persistent Farmer and mine progress. "Fix the data, don't fight
    /// the system": no per-collection hacks.
    /// </summary>
    internal sealed class WorldResetService
    {
        private readonly IMonitor _monitor;

        public WorldResetService(IMonitor monitor) => _monitor = monitor;

        public void PerformReset(int startingMoney)
        {
            _monitor.Log("In-place reset: starting.", LogLevel.Info);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            Game1.game1.loadForNewGame(loadedGame: false);

            // 2. Calendar -> Spring 1, year 1, morning. (loadForNewGame leaves dayOfMonth = 0 as a flag.)
            Game1.year = 1;
            Game1.season = Season.Spring;
            Game1.dayOfMonth = 1;
            Game1.timeOfDay = 600;
            Game1.netWorldState.Value.Date.Year = 1;
            Game1.netWorldState.Value.Date.Season = Season.Spring;
            Game1.netWorldState.Value.Date.DayOfMonth = 1;
            Game1.stats.DaysPlayed = 1;

            // 3. Farmer baseline (loadForNewGame leaves the existing player's stats intact).
            FarmerReset.ToBaseline(Game1.player, startingMoney);

            // 4. Mine progress.
            Game1.netWorldState.Value.LowestMineLevelForOrder = -1;
            MineShaft.clearActiveMines();

            // 5. Place the player home, awake, in the rebuilt FarmHouse.
            GameLocation home = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.currentLocation = home;
            Game1.currentLocation = home;
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            home.resetForPlayerEntry();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}.",
                LogLevel.Info);
        }
    }
}
```

- [ ] **Step 4: Register a `tly_reset` debug command in `ModEntry`**

In `src/TheLongestYear/ModEntry.cs`, add the field, the `using`, the command registration, and the handler.

Add near the top (with the other usings):

```csharp
using TheLongestYear.Loop;
```

Add a field alongside `_meta`:

```csharp
        private WorldResetService _reset;
```

In `Entry`, after `_meta = new MetaStore(helper.Data);`, add:

```csharp
            _reset = new WorldResetService(this.Monitor);
```

In `Entry`, with the other `ConsoleCommands.Add` calls, add:

```csharp
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
```

Add the handler method to the class:

```csharp
        private void ForceReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset(_config.StartingMoney);
        }
```

- [ ] **Step 5: Build and deploy**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds and deploys to the Mods folder.

- [ ] **Step 6: In-game verification (I deploy; the user plays; I read the log)**

Launch via `StardewModdingAPI.exe` (not `steam://rungameid`, which skips SMAPI). On a loaded save (ideally one with crops planted, some money, and a skill level or two):

1. Note current money/season/skills.
2. Run `tly_reset` in the SMAPI console.
3. Expect the log to show `In-place reset: complete. Spring 1, money 500.` and the game to be at Spring 1 with: empty farm (no crops/objects), money = 500, skills 0, empty inventory, CC bundles all empty.

I then read `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` to confirm no exceptions and the expected post-reset state. **Expect to iterate here** — the player-warp / first-day setup is the fragile part; fix forward against the log (e.g. a black screen, a wrong location, or a `loadForNewGame` side effect) before moving on.

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/Loop/FarmerReset.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(mod): in-place reset via loadForNewGame + targeted Farmer/mine resets (tly_reset)"
```

---

## Task 8: Mod — `WorldStateProbe` + `tly_leaktest` (two-loop leak test)

**Files:**
- Create: `src/TheLongestYear/Loop/WorldStateProbe.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (add the `tly_leaktest` command)

The probe captures a `WorldFingerprint` from the live game; `tly_leaktest` resets twice and asserts the two post-reset fingerprints match (the leak test from spec §12). Any leaked state shows up as a named field diff in the log.

- [ ] **Step 1: Implement `WorldStateProbe`**

Create `src/TheLongestYear/Loop/WorldStateProbe.cs`:

```csharp
using System.Linq;
using StardewValley;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Captures a <see cref="WorldFingerprint"/> from the live game for the leak test.</summary>
    internal static class WorldStateProbe
    {
        public static WorldFingerprint Capture()
        {
            Farmer p = Game1.player;

            int crops = 0, placedObjects = 0, buildings = 0;
            foreach (GameLocation loc in Game1.locations)
            {
                placedObjects += loc.objects.Count();
                buildings += loc.buildings.Count;
                foreach (TerrainFeature tf in loc.terrainFeatures.Values)
                    if (tf is HoeDirt hd && hd.crop != null)
                        crops++;
            }

            int totalXp = 0;
            for (int i = 0; i < p.experiencePoints.Count; i++)
                totalXp += p.experiencePoints[i];

            int completedBundles = Game1.netWorldState.Value.Bundles.Pairs
                .Count(kvp => kvp.Value.All(done => done));

            return new WorldFingerprint
            {
                Year = Game1.year,
                Season = (CoreSeason)(int)Game1.season,
                DayOfMonth = Game1.dayOfMonth,
                Money = p.Money,
                Stamina = (int)p.stamina,
                InventoryItemCount = p.Items.Count(it => it != null),
                TotalSkillXp = totalXp,
                CropCount = crops,
                PlacedObjectCount = placedObjects,
                BuildingCount = buildings,
                CompletedBundleCount = completedBundles,
                FriendshipCount = p.friendshipData.Count(),
                MailReceivedCount = p.mailReceived.Count(),
                EventsSeenCount = p.eventsSeen.Count(),
                LowestMineLevel = Game1.netWorldState.Value.LowestMineLevelForOrder
            };
        }
    }
}
```

- [ ] **Step 2: Register a `tly_leaktest` command in `ModEntry`**

In `src/TheLongestYear/ModEntry.cs`, add the command registration in `Entry`:

```csharp
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
```

Add the handler:

```csharp
        private void LeakTest(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset(_config.StartingMoney);
            var first = WorldStateProbe.Capture();

            _reset.PerformReset(_config.StartingMoney);
            var second = WorldStateProbe.Capture();

            var diff = first.Diff(second);
            if (diff.Count == 0)
            {
                this.Monitor.Log("Leak test PASSED: two consecutive resets produced an identical baseline.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Leak test FAILED: {diff.Count} field(s) leaked between runs:", LogLevel.Error);
                foreach (string d in diff)
                    this.Monitor.Log($"  - {d}", LogLevel.Error);
            }
        }
```

- [ ] **Step 3: Build and deploy**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds and deploys.

- [ ] **Step 4: In-game verification**

On a loaded save, run `tly_leaktest`. I read the SMAPI log:
- **Pass:** `Leak test PASSED: two consecutive resets produced an identical baseline.`
- **Fail:** a list of named field diffs (e.g. `LowestMineLevel: -1 -> 40`). Fix the corresponding reset step in Task 7's `WorldResetService`/`FarmerReset` and re-run until it passes. This loop is the whole point of the leak test — iterate to green.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Loop/WorldStateProbe.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(mod): add WorldStateProbe + tly_leaktest two-loop leak check"
```

---

## Task 9: Mod — `SaveBackup` (one-time backup before the first reset)

**Files:**
- Create: `src/TheLongestYear/Loop/SaveBackup.cs`
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (back up before the first reset)
- Modify: `src/TheLongestYear/ModEntry.cs` (pass the store so the flag persists)

The reset is destructive; before the first-ever reset we copy the entire save folder so a reset bug can't nuke a save (spec §12). It runs once, gated by `MetaState.BackupDone`, which persists on the next `Saving`.

- [ ] **Step 1: Implement `SaveBackup`**

Create `src/TheLongestYear/Loop/SaveBackup.cs`:

```csharp
using System;
using System.IO;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// One-time backup of the current save folder, taken before the first destructive reset. Gated by
    /// <see cref="MetaState.BackupDone"/> (persisted on the next Saving) so it runs exactly once.
    /// PC path via SMAPI's Constants.CurrentSavePath; the Android port is deferred.
    /// </summary>
    internal static class SaveBackup
    {
        public static void BackupOnce(MetaState meta, IMonitor monitor)
        {
            if (meta.BackupDone)
                return;

            string savePath = Constants.CurrentSavePath;
            if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath))
            {
                monitor.Log("Save backup skipped: no current save folder found.", LogLevel.Warn);
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string dest = $"{savePath}_TLY_BACKUP_{stamp}";

            try
            {
                CopyDirectory(savePath, dest);
                meta.BackupDone = true;
                monitor.Log($"One-time save backup written to: {dest}", LogLevel.Info);
            }
            catch (IOException ex)
            {
                monitor.Log($"Save backup FAILED ({ex.Message}); reset aborted to protect the save.", LogLevel.Error);
                throw;
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}
```

- [ ] **Step 2: Call the backup at the start of `PerformReset`**

`WorldResetService` needs the `MetaState` to gate the backup. Change its constructor and `PerformReset` in `src/TheLongestYear/Loop/WorldResetService.cs`.

Replace the field + constructor:

```csharp
        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.MetaState _meta;

        public WorldResetService(IMonitor monitor, TheLongestYear.Core.MetaState meta)
        {
            _monitor = monitor;
            _meta = meta;
        }
```

Insert at the very top of `PerformReset`, before step 1:

```csharp
            // One-time safety backup before the first destructive reset (throws if it fails -> reset aborts).
            SaveBackup.BackupOnce(_meta, _monitor);
```

- [ ] **Step 3: Update `ModEntry` to construct the service with the store's state**

In `src/TheLongestYear/ModEntry.cs`, the `_reset` is constructed in `Entry` before a save is loaded, but `MetaStore.State` is replaced on load. Construct `_reset` in `OnSaveLoaded` instead so it references the loaded `MetaState`.

Remove the line added in Task 7:

```csharp
            _reset = new WorldResetService(this.Monitor);
```

In `OnSaveLoaded`, after `_meta.Load();`, add:

```csharp
            _reset = new WorldResetService(this.Monitor, _meta.State);
```

Guard the two debug commands against a null `_reset` (in case they're run before a save loads): in both `ForceReset` and `LeakTest`, the existing `Context.IsWorldReady` check already prevents this, since `_reset` is assigned in `OnSaveLoaded` which fires before the world is ready for commands. No further change needed.

- [ ] **Step 4: Build and deploy**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds and deploys.

- [ ] **Step 5: In-game verification**

On a loaded save, run `tly_reset` once. I read the log + check the filesystem:
- The log shows `One-time save backup written to: <save>_TLY_BACKUP_<timestamp>` exactly once.
- A second `tly_reset` does **not** write another backup (flag persists after a save; before saving it's in-memory `BackupDone = true`, so it also won't re-backup within the session).
- The backup folder exists under `%APPDATA%\StardewValley\Saves\` next to the original and contains the save files.

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear/Loop/SaveBackup.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(mod): one-time save-folder backup before the first reset (gated by BackupDone)"
```

---

## Task 10: Mod — `RunController` (wire the playable loop)

**Files:**
- Create: `src/TheLongestYear/Loop/RunController.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (own the controller; add loop debug commands)

The controller is the SMAPI glue around the pure `RunManager`. It builds the run's `YearPlan` from the seed, syncs `RunState` from the game date, presents the weekly champion offer at week start, evaluates the gate at day end, and executes the resulting `RunAction` (fail → reset on the next morning; advance month → clear championing; win → log). It also banks an **interim** JP award on run end (full JP + the donation surface are Plan 04), which lets us finally verify the Plan-01 persistence round-trip inside a real loop.

> Wiring sequencing (event order) is in-game-iteration territory. First implementation: evaluate the gate in `DayEnding` and set a pending reset; execute the reset on the next `DayStarted` (after the game's own save/day-advance has run, so the fail-day's JP banks before the rewind). Fix forward against the log.

- [ ] **Step 1: Implement `RunController`**

Create `src/TheLongestYear/Loop/RunController.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Drives the run loop: builds the YearPlan from the run seed, syncs RunState from the game date,
    /// offers the weekly champion, evaluates the day-end gate via RunManager, and executes the action
    /// (fail -> reset next morning, advance month -> clear championing, win -> log). Interim JP is banked
    /// on run end (full JP + donation surface are Plan 04).
    /// </summary>
    internal sealed class RunController
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly WorldResetService _reset;
        private readonly RunManager _runManager = new RunManager(new GateEvaluator());
        private readonly JpCalculator _jp;

        private YearPlan _plan;
        private bool _pendingReset;

        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _jp = new JpCalculator(config.Jp);
        }

        private RunState Run => _store.Run;

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed and build its plan.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            Run.Season = (CoreSeason)(int)Game1.season;
            Run.DayOfMonth = Game1.dayOfMonth;
            _plan = new ContractGenerator().Generate(CcItemCatalog.Items, Run.Seed);

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);
        }

        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (_pendingReset)
            {
                _pendingReset = false;
                _reset.PerformReset(_config.StartingMoney);
                Run.BeginNewRun(NewSeed());
                _plan = new ContractGenerator().Generate(CcItemCatalog.Items, Run.Seed);
                _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
                return;
            }

            // Sync state from the game date; a new month clears championing (incl. the current champion).
            var season = (CoreSeason)(int)Game1.season;
            if (season != Run.Season)
                Run.BeginNewMonth(season);
            Run.Season = season;
            Run.DayOfMonth = Game1.dayOfMonth;

            // Only at week start: the previous week's champion expires and a fresh offer is presented.
            // (Mid-week reloads keep the persisted CurrentChampion, so the day-7 gate still sees it.)
            if (IsWeekStart(Run.DayOfMonth))
            {
                Run.CurrentChampion = null;
                PresentOffer();
            }
        }

        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            RunAction action = _runManager.EvaluateDayEnd(Run, _plan);
            switch (action)
            {
                case RunAction.Continue:
                    break;

                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    break; // game advances the date; OnDayStarted clears championing

                case RunAction.FailReset:
                    AwardInterimJp("run failed");
                    _pendingReset = true;
                    break;

                case RunAction.Win:
                    AwardInterimJp("run WON — loop broken");
                    break;
            }
        }

        /// <summary>Champion one of this week's offered themes (driven by the UI in Plan 05; debug command now).</summary>
        public void ChampionByName(string themeName)
        {
            if (!Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            {
                _monitor.Log($"Unknown theme '{themeName}'. Options: {string.Join(", ", Enum.GetNames(typeof(Theme)))}.", LogLevel.Warn);
                return;
            }

            var offer = ChampionService.OfferForWeek(Run);
            if (!offer.Contains(theme))
            {
                _monitor.Log($"{theme} is not offered this week. Offer: {string.Join(", ", offer)}.", LogLevel.Warn);
                return;
            }

            Run.Champion(theme);
            var (bonus, liability) = ThemeModifiers.For(theme);
            _monitor.Log($"Championed {theme} (bonus {bonus}, liability {liability}). Required: {RequiredFor(theme)}.", LogLevel.Info);
        }

        /// <summary>Simulate a CC donation (the real donation surface is Plan 04).</summary>
        public void Donate(string itemId)
        {
            Run.RecordDonation(itemId);
            _monitor.Log($"Donated '{itemId}'. Ledger size {Run.DonatedItemIds.Count}.", LogLevel.Info);
        }

        public void PrintRunState()
        {
            _monitor.Log(
                $"Run {Run.RunNumber}: {Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}). " +
                $"Champion={Run.CurrentChampion?.ToString() ?? "none"}, " +
                $"championedThisMonth=[{string.Join(",", Run.ChampionedThemesThisMonth)}], " +
                $"donated={Run.DonatedItemIds.Count}, JP banked={_store.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>Log this week's champion offer (driven by the UI in Plan 05; debug command + week-start now).</summary>
        public void PresentOffer()
        {
            var offer = ChampionService.OfferForWeek(Run);
            _monitor.Log(
                $"Week {Run.WeekOfYear} champion offer: {string.Join(" OR ", offer)} " +
                $"(use 'tly_champion <theme>').",
                LogLevel.Info);
        }

        private void AwardInterimJp(string reason)
        {
            var lines = Run.DonatedItemIds
                .GroupBy(CcItemCatalog.RarityOf)
                .Select(g => new DonationLine(g.Key, g.Count()));
            long awarded = _jp.ForDonationBatch(lines, Run.WeekOfYear, bundlesCompleted: 0, roomsCompleted: 0);
            _store.State.JunimoPoints += awarded;
            _monitor.Log(
                $"Interim JP for {reason}: +{awarded} (now {_store.State.JunimoPoints}). Persists on this day's save.",
                LogLevel.Info);
        }

        private string RequiredFor(Theme theme)
        {
            var items = _plan.Get(Run.Season, theme).RequiredItemIds;
            return items.Count == 0 ? "(nothing)" : string.Join(", ", items);
        }

        private string DescribeWeek() => $"{Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}).";

        private static bool IsWeekStart(int dayOfMonth) => (dayOfMonth - 1) % Calendar.DaysPerWeek == 0;

        private static int NewSeed() => Guid.NewGuid().GetHashCode();
    }
}
```

- [ ] **Step 2: Wire `RunController` into `ModEntry`**

In `src/TheLongestYear/ModEntry.cs`:

Add the field:

```csharp
        private RunController _runController;
```

In `Entry`, subscribe to the day events (alongside the existing `Saving`/`SaveLoaded` subscriptions):

```csharp
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
```

Add the loop debug commands in `Entry`:

```csharp
            helper.ConsoleCommands.Add("tly_champion", "Champion one of this week's offered themes. Usage: tly_champion <theme>", this.CmdChampion);
            helper.ConsoleCommands.Add("tly_offer", "Show this week's champion offer.", this.CmdOffer);
            helper.ConsoleCommands.Add("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            helper.ConsoleCommands.Add("tly_runstate", "Print the current run state.", this.CmdRunState);
```

In `OnSaveLoaded`, after constructing `_reset`, construct and prime the controller:

```csharp
            _runController = new RunController(this.Monitor, _meta, _config, _reset);
            _runController.OnRunLoaded();
```

Add the forwarding event handlers + command handlers to the class:

```csharp
        private void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
            => _runController?.OnDayStarted(sender, e);

        private void OnDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
            => _runController?.OnDayEnding(sender, e);

        private void CmdChampion(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_champion <theme>", LogLevel.Warn); return; }
            _runController.ChampionByName(args[0]);
        }

        private void CmdOffer(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PresentOffer();
        }

        private void CmdDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_donate <itemId>", LogLevel.Warn); return; }
            _runController.Donate(args[0]);
        }

        private void CmdRunState(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PrintRunState();
        }
```

> `tly_offer` calls `PresentOffer` to log this week's two choices on demand; `PresentOffer` also fires automatically at each week start from `OnDayStarted`.

- [ ] **Step 3: Build and deploy**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds and deploys.

- [ ] **Step 4: In-game verification — a full playable loop**

On a loaded save, I deploy and the user plays; I read the log throughout. Use a contract with a single trivial requirement to exercise gates quickly:

1. On day 1 the log shows a week-1 champion offer.
2. `tly_runstate` → shows Spring day 1, week 1, no champion.
3. `tly_champion <one of the offered themes>` → logs the bonus/liability + that theme's required items.
4. `tly_donate <each required id>` for the championed theme.
5. Sleep through to day 7. At day end the log shows the weekly gate **passing** (Continue) because the championed contract is satisfied.
6. Repeat: on a week where the championed contract is **not** satisfied, the day-7 gate logs `run failed`, banks interim JP, and on the next morning performs the reset (`Loop reset complete. Run N begins`). The world is back to Spring 1.

I verify against `SMAPI-latest.txt`: gate decisions, JP award, reset, no exceptions. **Expect iteration** on event ordering (e.g. JP must bank on the failing day's save before the next-morning reset; the month-rollover championing clear must fire once).

- [ ] **Step 5: Persistence round-trip (the deferred Plan-01 check, now inside a real loop)**

1. Load save → `tly_runstate` notes JP banked.
2. Champion + donate + fail a run so interim JP is awarded; sleep to bank it (the fail day still saves).
3. Fully quit, relaunch, reload the same save → the log shows the higher JP banked and the run on a fresh attempt (`Run N`), confirming `RunState` + `MetaState` persisted atomically with the save.
4. Add JP via the failing loop again but quit **without** sleeping → on reload the extra JP is gone (anti-save-scum holds).

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(mod): RunController wires the playable loop (gates, championing, reset, interim JP)"
```

---

## Task 11: Two-loop integration check + finish the branch

**Files:** none (verification + git)

- [ ] **Step 1: Full unit suite green**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS — Plans 01–02 (50) plus the new Core tests (catalog, run state, champion, run manager, fingerprint, BackupDone).

- [ ] **Step 2: Mod builds clean**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds, deploys, no warnings beyond the expected ModBuildConfig output.

- [ ] **Step 3: Final in-game acceptance (I deploy; user plays; I read the log)**

1. `tly_leaktest` → PASSED.
2. Play at least **two** full reset cycles via the gates (champion → donate → pass/fail) and confirm the world, money, skills, CC, and mines return to baseline each loop with no leaked state and no exceptions in `SMAPI-latest.txt`.
3. JP persists across a quit/reload after a banking sleep; is discarded on a no-save quit.

- [ ] **Step 4: Finish the development branch**

Use the `superpowers:finishing-a-development-branch` skill. Default integration: merge to `master` with `--no-ff` once everything above is green; do **not** push.

```bash
git checkout master
git merge --no-ff feat/v1-plan-03-lifecycle-reset -m "merge: v1 Plan 03 — run lifecycle & in-place world reset"
```

- [ ] **Step 5: Update project memory**

Update `C:\Users\Jeff\.claude\projects\C--Users-Jeff-Documents-Projects-Stardee-Valoo\memory\project_the_longest_year.md`: Plan 03 done (lifecycle + in-place reset + leak test + save backup + interim JP), next is Plan 04 (Donations & JP wiring + the real Data/Bundles → CcItem adapter). Note the reset reuses `Game1.loadForNewGame(false)` + targeted Farmer/mine resets, and that the leak test command is `tly_leaktest`.

---

## Done criteria for Plan 03

- `dotnet test` green: `CcItemCatalog` (solvable plan), `RunState` (serialization + lifecycle), `ChampionService` (deterministic 1-of-2), `RunManager` (day-end → action), `WorldFingerprint` (diff), `MetaState.BackupDone`.
- `dotnet build` of the mod succeeds and deploys.
- In-game: `tly_reset` rewinds to a clean Spring-1 baseline; `tly_leaktest` PASSES; a full champion → donate → gate → fail → reset loop runs; interim JP banks on the run-end save and survives a restart (and is discarded on a no-save quit).
- A one-time save backup is written before the first reset and not repeated.
- Core still references no game assemblies.

## Self-review notes (spec coverage)

- **Weekly + monthly gates wired to day-end** (spec §5) → `RunManager` (Task 4) + `RunController.OnDayEnding` (Task 10), reusing Plan 01's `GateEvaluator`.
- **Weekly championing, 1-of-2, 4-of-5, bonus/liability surfaced** (spec §4) → `ChampionService` (Task 3) + `RunController` championing (Task 10); bonus/liability *effects* remain Plan 06 (ids only here).
- **In-place reset to Spring 1; reuse the game's own routines** (spec §7, §12) → `WorldResetService` via `Game1.loadForNewGame` + `FarmerReset` (Task 7).
- **Two-loop leak test** (spec §12) → `WorldFingerprint`/`Diff` (Task 5) + `WorldStateProbe`/`tly_leaktest` (Task 8).
- **One-time save backup before first reset** (spec §12) → `SaveBackup` gated by `BackupDone` (Task 9).
- **Contract set re-rolled each run** (spec §7, §10) → `RunController` regenerates the `YearPlan` with a new seed on `BeginNewRun` (Task 10).
- **Meta + run state persist only on Saving (anti-save-scum)** (spec §7) → `MetaStore` extended (Task 6); never written eagerly.
- **CC ground truth** (spec §10) → hand-authored `CcItemCatalog` (Task 1); real `Data/Bundles` adapter explicitly deferred to Plan 04.
- **Config in one place** (workspace rule) → `GameplayConfig.StartingMoney` + existing `Jp` settings (Tasks 7, 10).
- Deferred by design: donation surface + full rarity/depth JP + bundle/room bonuses (Plan 04), planning-hub/shop UI (Plan 05), upgrade/obtainability/foresight effects (Plan 06), Junimo Stash carve-out of the inventory wipe + narrative (Plan 07), difficulty smoothing (Plan 02 refinement).
```
