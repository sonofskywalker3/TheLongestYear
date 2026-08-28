# The Even Year Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every item on a board gets a real first week it can exist, every pick-X-of-Y ramp follows its own items, every bundle has a Spring foothold, weekly goals are flat, and nothing is silently unknown.

**Architecture:** `TheLongestYear.Core` (pure, unit-tested) owns the availability rules, the model, the classifier and the slot filler; `src/TheLongestYear` (SMAPI mod) is glue: it builds the model from live game data, passes predicates into the engine and the sampler, and prints the dump. Each task changes one rule or one consumer and ends green.

**Tech Stack:** .NET 6, C# 10, xUnit (`dotnet test tests/TheLongestYear.Tests`), SMAPI 4 mod, bash sim driver (`tools/sim-year.sh`), headless driving per `docs/HEADLESS_DRIVING.md`.

**Spec:** `docs/superpowers/specs/2026-08-28-even-year-availability-design.md`

## Global Constraints

- No em dashes anywhere (code comments, docs, logs, commit messages).
- No `/sdcard/` paths.
- Patch bump in `src/TheLongestYear/manifest.json` on every commit that changes mod behaviour (0.16.78 onward); one behaviour per commit; a CHANGELOG line per behaviour under `## Unreleased`.
- Core never references Game1 or SMAPI. Every judgement number lives in `AvailabilityWeeks` (Core), never inline.
- Week numbers are 1 to 16 (`Calendar.WeekOfYear`); Spring = weeks 1 to 4, Summer 5 to 8, Fall 9 to 12, Winter 13 to 16. Unknown = week 13, `GateSeason.Winter`, reported.
- A floor moved earlier by an override is rejected (the existing Purple Mushroom rule), now compared in weeks.
- Tests: `dotnet test tests/TheLongestYear.Tests` must be green at every commit.
- Never run `tools/game.ps1` or `tools/screenshot.ps1`. Deploy with `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, then `git checkout -- test-output/log-archive`.
- After every sim: `tly_dumpavailability`, copy to `docs/board-availability.md`, give Jeff the full list and the unknown list, wait for his answer (memory `tly-sim-list-unknowns-each-run`).

---

## File map

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/AvailabilityWeeks.cs` (new) | Every week/gate judgement number: season to week helpers, mine area weeks, machine level weeks, housing tier weeks, kitchen week, pond delay, bush berry weeks, saplings. |
| `src/TheLongestYear.Core/ItemAvailability.cs` | `ItemAvailability` gains `EarliestWeek`, `GateSeason`; `ItemEffort` gains optional week and gate; model resolves weeks, week overrides, `IsPlaced`, `UnknownIds`. |
| `src/TheLongestYear.Core/Availability/LocationGating.cs` | `WeekFor` / `WeekForAny`; mines become Spring week 1. |
| `src/TheLongestYear.Core/Availability/MineAreas.cs` | `Week(area)`, `GateSeason(area)`. |
| `src/TheLongestYear.Core/Availability/{Fish,Metals,MineralNode,Geode,MonsterDrop,Artifact,AnimalProduct,Artisan,CookedDish,FishPond,CropForage}Availability.cs` | each rule emits a week (and a gate season where it differs). |
| `src/TheLongestYear.Core/Availability/EffortComposer.cs`, `ItemAvailabilityBuilder.cs` | pass spawn seasons and pools into the crop/forage/sapling rules; `WeekOf` resolver for recursive rules. |
| `src/TheLongestYear.Core/GoalObtainability.cs`, `SlotPoolBuilder.cs`, `BundleDeadlines.cs` | goals by week, deadlines clamp to `GateSeason`. |
| `src/TheLongestYear.Core/BundleClassifier.cs`, `GameplayConfig.cs` | ramp derived from items; `DefaultBundleQuotas` emptied; `AvailabilityWeekOverrides`. |
| `src/TheLongestYear.Core/BundleSlotFiller.cs`, `src/TheLongestYear/Loop/BundleEngine.cs`, `WorldResetService.cs` | Spring foothold. |
| `src/TheLongestYear.Core/BonusItemSampler.cs` | caps 5/5/5/6. |
| `src/TheLongestYear/Loop/RunController.cs`, `ModEntry.cs` | week-aware predicates, `tly_dumpavailability` week column and unknown section, gatecheck tag. |
| `tools/sim-year.sh` | dumps availability at the end of every run. |
| `tests/TheLongestYear.Tests/*` | one test class per rule change, listed per task. |

---

### Task 1: AvailabilityWeeks and the week fields on the records

**Files:**
- Create: `src/TheLongestYear.Core/AvailabilityWeeks.cs`
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs:17-26` (the two records)
- Test: `tests/TheLongestYear.Tests/AvailabilityWeeksTests.cs` (new)

**Interfaces:**
- Produces: `AvailabilityWeeks.SeasonOf(int week)`, `FirstWeekOf(Season)`, `LastWeekOf(Season)`, `UnknownWeek = 13`, `MineAreaWeek(int area)`, `MineAreaGateSeason(int area)`, `MachineLevelWeek(int level)`, `HousingTierWeek(int links)`, `KitchenWeek = 5`, `ShopDishWeek = 3`, `PondDelayWeeks = 4`, `SaplingWeek = 1`, `SalmonberryWeek = 3`, `BlackberryWeek = 10`, `SkullCavernWeek = 9`, `SewerWeek = 5`, `SwampWeek = 13`.
- `ItemAvailability(Season EarliestSeason, int Effort, string Basis, EffortSource Source = Derived, int EarliestWeek = 0, Season? GateSeason = null)`: when `EarliestWeek` is 0 the constructor-time helper `ItemAvailability.FromSeason(...)` is used by callers; `Week` property returns `EarliestWeek > 0 ? EarliestWeek : FirstWeekOf(EarliestSeason)`; `Gate` property returns `GateSeason ?? EarliestSeason`.
- `ItemEffort(int Effort, string Basis, int? EarliestWeek = null, Season? GateSeason = null)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/AvailabilityWeeksTests.cs
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class AvailabilityWeeksTests
{
    [Theory]
    [InlineData(1, Season.Spring)] [InlineData(4, Season.Spring)] [InlineData(5, Season.Summer)]
    [InlineData(9, Season.Fall)] [InlineData(13, Season.Winter)] [InlineData(16, Season.Winter)]
    public void Season_of_week(int week, Season expected) => Assert.Equal(expected, AvailabilityWeeks.SeasonOf(week));

    [Theory]
    [InlineData(Season.Spring, 1, 4)] [InlineData(Season.Summer, 5, 8)]
    [InlineData(Season.Fall, 9, 12)] [InlineData(Season.Winter, 13, 16)]
    public void First_and_last_week_of_season(Season season, int first, int last)
    {
        Assert.Equal(first, AvailabilityWeeks.FirstWeekOf(season));
        Assert.Equal(last, AvailabilityWeeks.LastWeekOf(season));
    }

    [Theory]
    [InlineData(MineAreas.Area0, 1, Season.Spring)] [InlineData(MineAreas.Area10, 1, Season.Spring)]
    [InlineData(MineAreas.Area40, 2, Season.Spring)] [InlineData(MineAreas.Area80, 3, Season.Summer)]
    [InlineData(MineAreas.SkullCavern, 9, Season.Fall)]
    public void Mine_area_week_and_gate(int area, int week, Season gate)
    {
        Assert.Equal(week, AvailabilityWeeks.MineAreaWeek(area));
        Assert.Equal(gate, AvailabilityWeeks.MineAreaGateSeason(area));
    }

    [Theory]
    [InlineData(0, 2)] [InlineData(2, 2)] [InlineData(3, 3)] [InlineData(4, 4)] [InlineData(5, 4)]
    [InlineData(6, 6)] [InlineData(7, 6)] [InlineData(8, 7)] [InlineData(9, 7)] [InlineData(10, 9)]
    public void Machine_level_week(int level, int week) => Assert.Equal(week, AvailabilityWeeks.MachineLevelWeek(level));

    [Theory]
    [InlineData(0, 2)] [InlineData(1, 5)] [InlineData(2, 9)] [InlineData(3, 9)]
    public void Housing_tier_week(int links, int week) => Assert.Equal(week, AvailabilityWeeks.HousingTierWeek(links));

    [Fact]
    public void Record_week_falls_back_to_the_first_week_of_its_season()
    {
        var legacy = new ItemAvailability(Season.Fall, 3, "test");
        Assert.Equal(9, legacy.Week);
        Assert.Equal(Season.Fall, legacy.Gate);
        var explicitWeek = new ItemAvailability(Season.Spring, 3, "test", EarliestWeek: 3, GateSeason: Season.Summer);
        Assert.Equal(3, explicitWeek.Week);
        Assert.Equal(Season.Summer, explicitWeek.Gate);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter AvailabilityWeeksTests`
Expected: build error, `AvailabilityWeeks` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/TheLongestYear.Core/AvailabilityWeeks.cs
using System;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

/// <summary>Every judgement number behind "the first week of the year an item can exist" (spec
/// 2026-08-28-even-year-availability). Facts read from game data live in the rules; what lives
/// here is the pacing Jeff ruled on: 30 mine floors a week, Skull Cavern from Fall, a kitchen by
/// Summer, and so on. Weeks are 1 to 16 (Calendar.WeekOfYear).</summary>
public static class AvailabilityWeeks
{
    /// <summary>An item no rule placed. Winter, the safe direction for a deadline.</summary>
    public const int UnknownWeek = 13;
    public const int KitchenWeek = 5;
    /// <summary>A dish the Saloon or a Cookout Kit can supply without a kitchen.</summary>
    public const int ShopDishWeek = 3;
    public const int PondDelayWeeks = 4;
    public const int SaplingWeek = 1;
    public const int ArtifactWeek = 1;
    public const int SalmonberryWeek = 3;
    public const int BlackberryWeek = 10;
    public const int SkullCavernWeek = 9;
    public const int SewerWeek = 5;
    public const int SwampWeek = 13;
    /// <summary>Jeff, 2026-08-28: 30 floors a week for the theme goals.</summary>
    public const int MineFloorsPerWeek = 30;

    public static Season SeasonOf(int week)
    {
        int clamped = Math.Clamp(week, 1, Calendar.WeeksPerYear);
        return (Season)((clamped - 1) / Calendar.WeeksPerMonth);
    }

    public static int FirstWeekOf(Season season) => (int)season * Calendar.WeeksPerMonth + 1;
    public static int LastWeekOf(Season season) => ((int)season + 1) * Calendar.WeeksPerMonth;

    /// <summary>Theme-goal week for a mine area: floors 1 to 39 week 1, 41 to 79 week 2,
    /// 81 to 119 week 3, Skull Cavern Fall week 9.</summary>
    public static int MineAreaWeek(int area) => area switch
    {
        MineAreas.Area0 or MineAreas.Area10 => 1,
        MineAreas.Area40 => 2,
        MineAreas.Area80 => 3,
        _ => SkullCavernWeek,
    };

    /// <summary>The gate is softer than the goal for the deep mine: below floor 80 a Spring
    /// gate may demand it, 80 and deeper waits for Summer (Jeff accepted this, 2026-08-28).</summary>
    public static Season MineAreaGateSeason(int area) => area switch
    {
        MineAreas.Area0 or MineAreas.Area10 or MineAreas.Area40 => Season.Spring,
        MineAreas.Area80 => Season.Summer,
        _ => Season.Fall,
    };

    /// <summary>Week a machine unlocked at a skill level is realistically running.</summary>
    public static int MachineLevelWeek(int level) => level switch
    {
        <= 2 => 2,
        3 => 3,
        4 or 5 => 4,
        6 or 7 => 6,
        8 or 9 => 7,
        _ => 9,
    };

    /// <summary>Week an animal building tier is realistically up: base coop or barn week 2,
    /// big week 5, deluxe week 9. links = upgrades above the base building.</summary>
    public static int HousingTierWeek(int links) => links switch
    {
        0 => 2,
        1 => 5,
        _ => 9,
    };
}
```

Then in `src/TheLongestYear.Core/ItemAvailability.cs` replace the two records:

```csharp
public sealed record ItemAvailability(
    Season EarliestSeason, int Effort, string Basis, EffortSource Source = EffortSource.Derived,
    int EarliestWeek = 0, Season? GateSeason = null)
{
    /// <summary>First week of the year the item can exist; a record built from a season alone
    /// reads as that season's first week.</summary>
    public int Week => EarliestWeek > 0 ? EarliestWeek : AvailabilityWeeks.FirstWeekOf(EarliestSeason);
    /// <summary>Season a day-28 gate may first demand the item.</summary>
    public Season Gate => GateSeason ?? EarliestSeason;
}

public sealed record ItemEffort(int Effort, string Basis, int? EarliestWeek = null, Season? GateSeason = null);
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: all green (existing positional constructions of both records still compile because the new parameters are optional).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/AvailabilityWeeks.cs src/TheLongestYear.Core/ItemAvailability.cs tests/TheLongestYear.Tests/AvailabilityWeeksTests.cs
git commit -m "even year 1/12: AvailabilityWeeks table and week fields on ItemAvailability and ItemEffort"
```

---

### Task 2: The model resolves weeks, week overrides and the unknown list

**Files:**
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs` (class `ItemAvailabilityModel`)
- Test: `tests/TheLongestYear.Tests/ItemAvailabilityModelWeekTests.cs` (new)

**Interfaces:**
- Constructor gains `IReadOnlyDictionary<string, int>? weekOverrides = null` (5th parameter).
- `For(id)` returns a record whose `EarliestWeek` is: derived week (Phase 1 records carry one from Task 3 on; before that, their season's first week), else the effort-only rule's `EarliestWeek`, else `UnknownWeek`; `GateSeason` likewise. A season override sets week = `FirstWeekOf(season)`, gate = season. A week override sets week and gate = `SeasonOf(week)`. An override earlier than a placed floor is rejected and recorded in `RejectedSeasonOverrides` (same set, same name).
- `bool IsPlaced(string id)`: Phase 1 derived, or effort-only with a week, or overridden.
- `IReadOnlyCollection<string> UnknownIds`: every id `For` has been asked about that is not placed.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/ItemAvailabilityModelWeekTests.cs
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemAvailabilityModelWeekTests
{
    private static readonly Dictionary<string, ItemAvailability> NoDerived = new();

    [Fact]
    public void Effort_only_week_becomes_the_floor_and_the_gate()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)66"] = new(1, "amethyst", EarliestWeek: 1) });
        ItemAvailability a = model.For("(O)66");
        Assert.Equal(1, a.Week);
        Assert.Equal(Season.Spring, a.EarliestSeason);
        Assert.Equal(Season.Spring, a.Gate);
        Assert.True(model.IsPlaced("(O)66"));
    }

    [Fact]
    public void Effort_only_gate_can_be_later_than_its_week()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)64"] = new(5, "ruby", EarliestWeek: 3, GateSeason: Season.Summer) });
        ItemAvailability a = model.For("(O)64");
        Assert.Equal(3, a.Week);
        Assert.Equal(Season.Summer, a.Gate);
    }

    [Fact]
    public void Effort_without_a_week_is_unknown()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)1"] = new(4, "no week") });
        ItemAvailability a = model.For("(O)1");
        Assert.Equal(AvailabilityWeeks.UnknownWeek, a.Week);
        Assert.Equal(Season.Winter, a.Gate);
        Assert.False(model.IsPlaced("(O)1"));
        Assert.Contains("(O)1", model.UnknownIds);
    }

    [Fact]
    public void Week_override_moves_a_floor_later_and_sets_the_gate()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)66"] = new(1, "amethyst", EarliestWeek: 1) },
            weekOverrides: new Dictionary<string, int> { ["(O)66"] = 6 });
        ItemAvailability a = model.For("(O)66");
        Assert.Equal(6, a.Week);
        Assert.Equal(Season.Summer, a.Gate);
        Assert.Contains("override", a.Basis);
    }

    [Fact]
    public void Week_override_earlier_than_a_placed_floor_is_rejected()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)64"] = new(5, "ruby", EarliestWeek: 3) },
            weekOverrides: new Dictionary<string, int> { ["(O)64"] = 1 });
        Assert.Equal(3, model.For("(O)64").Week);
        Assert.Contains("(O)64", model.RejectedSeasonOverrides);
    }

    [Fact]
    public void Season_pin_on_an_unknown_item_places_it_at_the_seasons_first_week()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            seasonOverrides: new Dictionary<string, Season> { ["(O)388"] = Season.Spring });
        ItemAvailability a = model.For("(O)388");
        Assert.Equal(1, a.Week);
        Assert.True(model.IsPlaced("(O)388"));
        Assert.DoesNotContain("(O)388", model.UnknownIds);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter ItemAvailabilityModelWeekTests`
Expected: build errors (`weekOverrides`, `IsPlaced`, `UnknownIds`).

- [ ] **Step 3: Implement in `ItemAvailabilityModel`**

Add fields and constructor parameter:

```csharp
    private readonly IReadOnlyDictionary<string, int> _weekOverrides;
    private readonly HashSet<string> _unknown = new(StringComparer.Ordinal);

    public ItemAvailabilityModel(
        IReadOnlyDictionary<string, ItemAvailability> derived,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null,
        IReadOnlyDictionary<string, ItemEffort>? effortDerived = null,
        IReadOnlyDictionary<string, int>? weekOverrides = null)
    {
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
        _seasonOverrides = seasonOverrides ?? new Dictionary<string, Season>(StringComparer.Ordinal);
        _effortOverrides = effortOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);
        _effortDerived = effortDerived ?? new Dictionary<string, ItemEffort>(StringComparer.Ordinal);
        _weekOverrides = weekOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Season> pin in _seasonOverrides)
        {
            int? floor = PlacedWeek(pin.Key);
            if (floor != null && AvailabilityWeeks.FirstWeekOf(pin.Value) < floor.Value)
                _rejectedSeasonOverrides.Add(pin.Key);
        }
        foreach (KeyValuePair<string, int> pin in _weekOverrides)
        {
            int? floor = PlacedWeek(pin.Key);
            if (floor != null && pin.Value < floor.Value)
                _rejectedSeasonOverrides.Add(pin.Key);
        }
    }

    /// <summary>The week a rule placed the id at, or null when no rule did.</summary>
    private int? PlacedWeek(string id)
    {
        if (_derived.TryGetValue(id, out ItemAvailability? d)) return d.Week;
        if (_effortDerived.TryGetValue(id, out ItemEffort? e) && e.EarliestWeek != null) return e.EarliestWeek;
        return null;
    }

    /// <summary>True when a rule or an accepted override says when the item first exists.</summary>
    public bool IsPlaced(string qualifiedItemId)
        => qualifiedItemId != null
           && (PlacedWeek(qualifiedItemId) != null
               || (_seasonOverrides.ContainsKey(qualifiedItemId) && !_rejectedSeasonOverrides.Contains(qualifiedItemId))
               || (_weekOverrides.ContainsKey(qualifiedItemId) && !_rejectedSeasonOverrides.Contains(qualifiedItemId)));

    /// <summary>Every id For() has been asked about that nothing placed. The list Jeff reads
    /// after every sim (memory tly-sim-list-unknowns-each-run).</summary>
    public IReadOnlyCollection<string> UnknownIds => _unknown;
```

Replace the body of `For` from `Season season = derived?.EarliestSeason ?? Season.Winter;` to the end with:

```csharp
        int week = derived?.Week ?? effortOnly?.EarliestWeek ?? AvailabilityWeeks.UnknownWeek;
        Season gate = derived?.Gate ?? effortOnly?.GateSeason ?? AvailabilityWeeks.SeasonOf(week);
        bool placed = PlacedWeek(qualifiedItemId) != null;
        int effort = derived?.Effort ?? effortOnly?.Effort ?? UnrecognisedEffort;
        string basis = derived?.Basis
            ?? (effortOnly != null ? $"{effortOnly.Basis}{(placed ? "" : "; " + EffortOnlyFloorNote)}" : UnrecognisedBasis);
        EffortSource source = known || effortKnown ? EffortSource.Derived : EffortSource.Price;
        if (hasSeasonOverride)
        {
            if (_rejectedSeasonOverrides.Contains(qualifiedItemId))
                basis = $"season override to {overrideSeason} REJECTED, earlier than derived floor week {week} (derived: {basis})";
            else
            {
                basis = $"season override to {overrideSeason} (derived: {basis})";
                week = AvailabilityWeeks.FirstWeekOf(overrideSeason);
                gate = overrideSeason;
                placed = true;
            }
        }
        if (_weekOverrides.TryGetValue(qualifiedItemId, out int overrideWeek))
        {
            if (_rejectedSeasonOverrides.Contains(qualifiedItemId))
                basis = $"week override to {overrideWeek} REJECTED, earlier than derived floor week {week} (derived: {basis})";
            else
            {
                basis = $"week override to {overrideWeek} (derived: {basis})";
                week = overrideWeek;
                gate = AvailabilityWeeks.SeasonOf(week);
                placed = true;
            }
        }
        if (hasEffortOverride)
        {
            basis = $"{basis}; effort override to {overrideEffort}";
            effort = overrideEffort;
            source = EffortSource.Override;
        }
        if (!placed) _unknown.Add(qualifiedItemId);
        return new ItemAvailability(AvailabilityWeeks.SeasonOf(week), effort, basis, source, week, gate);
```

Also change the early return for a wholly unrecognised id to
`return new ItemAvailability(Season.Winter, UnrecognisedEffort, UnrecognisedBasis, EffortSource.Price, AvailabilityWeeks.UnknownWeek, Season.Winter);`
preceded by `_unknown.Add(qualifiedItemId);`. Keep `IsDerived` as is (Phase 1 only); `HasDerivedEffort` unchanged.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green. `ItemAvailabilityTests` may assert the old "floor not derived (Winter)" basis for effort-only ids; keep that text for unplaced ids (the code above does).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/ItemAvailability.cs tests/TheLongestYear.Tests/ItemAvailabilityModelWeekTests.cs
git commit -m "even year 2/12: the availability model resolves weeks, week overrides, IsPlaced and the unknown list"
```

---

### Task 3: Phase 1 rules and LocationGating carry weeks; the mines open in Spring

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/LocationGating.cs`, `MineAreas.cs`, `MetalsAvailability.cs`, `FishAvailability.cs`
- Test: `tests/TheLongestYear.Tests/LocationGatingWeekTests.cs` (new); update `MetalsAvailabilityTests.cs` expectations

**Interfaces:**
- `LocationGating.WeekFor(string locationKey)` and `WeekForAny(IReadOnlyList<string>)`; `FloorFor`/`FloorForAny` stay and return `SeasonOf(week)`.
- Markers become: Desert 9, SkullCave 9, UndergroundMine 1, Sewer 5, BugLand 5, WitchSwamp 13, WitchHut 13.
- `MineAreas.Week(area)` = `AvailabilityWeeks.MineAreaWeek`, `MineAreas.GateSeason(area)` = `AvailabilityWeeks.MineAreaGateSeason`.
- `MetalsAvailability` rules become `(int Area, int Effort, string Basis)` for ores and bars (area drives week and gate); coal and refined quartz area 0; bone fragment area 80.
- `FishAvailability.Derive` sets `EarliestWeek = max(FirstWeekOf(spawnFloor), WeekForAny(locations))` and `GateSeason = SeasonOf(that week)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/LocationGatingWeekTests.cs
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class LocationGatingWeekTests
{
    [Theory]
    [InlineData("UndergroundMine20", 1)] [InlineData("Farm", 1)] [InlineData("Desert", 9)]
    [InlineData("SkullCave", 9)] [InlineData("Sewer", 5)] [InlineData("BugLand", 5)] [InlineData("WitchSwamp", 13)]
    public void Week_for_location(string key, int week) => Assert.Equal(week, LocationGating.WeekFor(key));

    [Fact]
    public void Easiest_location_wins()
        => Assert.Equal(1, LocationGating.WeekForAny(new List<string> { "Desert", "Beach" }));

    [Theory]
    [InlineData("(O)378", 1, Season.Spring)]  // copper
    [InlineData("(O)380", 2, Season.Spring)]  // iron
    [InlineData("(O)384", 3, Season.Summer)]  // gold
    [InlineData("(O)386", 9, Season.Fall)]    // iridium
    [InlineData("(O)336", 3, Season.Summer)]  // gold bar
    public void Metals_carry_week_and_gate(string id, int week, Season gate)
    {
        ItemAvailability a = MetalsAvailability.Derive(new PoolItem(id, 100, 1, new List<Season>(), new List<string>()))!;
        Assert.Equal(week, a.Week);
        Assert.Equal(gate, a.Gate);
    }

    [Fact]
    public void Desert_fish_is_week_9_whatever_its_spawn_seasons_say()
    {
        var item = new PoolItem("(O)164", 75, 1, new List<Season>(), new List<string> { "Desert" });
        ItemAvailability a = FishAvailability.Derive(item, null);
        Assert.Equal(9, a.Week);
        Assert.Equal(Season.Fall, a.Gate);
    }

    [Fact]
    public void Summer_only_fish_is_week_5()
    {
        var item = new PoolItem("(O)145", 75, 1, new List<Season> { Season.Summer }, new List<string> { "Forest" });
        Assert.Equal(5, FishAvailability.Derive(item, null).Week);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter LocationGatingWeekTests`
Expected: build error (`WeekFor`).

- [ ] **Step 3: Implement**

`LocationGating.cs`: replace the marker table and the two methods:

```csharp
    private static readonly (string Marker, int Week)[] GatedMarkers =
    {
        ("Desert",    AvailabilityWeeks.SkullCavernWeek),
        ("SkullCave", AvailabilityWeeks.SkullCavernWeek),
        // The landslide is cleared on day 1 by MountainUnlock; depth is handled per mine area.
        ("UndergroundMine", 1),
        ("Sewer",     AvailabilityWeeks.SewerWeek),
        ("BugLand",   AvailabilityWeeks.SewerWeek),
        ("WitchSwamp", AvailabilityWeeks.SwampWeek),
        ("WitchHut",   AvailabilityWeeks.SwampWeek),
    };

    public static int WeekFor(string locationKey)
    {
        if (string.IsNullOrEmpty(locationKey)) return 1;
        foreach ((string marker, int week) in GatedMarkers)
            if (locationKey.Contains(marker, StringComparison.Ordinal))
                return week;
        return 1;
    }

    public static int WeekForAny(IReadOnlyList<string> locationKeys)
    {
        if (locationKeys == null || locationKeys.Count == 0) return 1;
        int best = Calendar.WeeksPerYear;
        foreach (string key in locationKeys)
            best = Math.Min(best, WeekFor(key));
        return best;
    }

    public static Season FloorFor(string locationKey) => AvailabilityWeeks.SeasonOf(WeekFor(locationKey));
    public static Season FloorForAny(IReadOnlyList<string> locationKeys) => AvailabilityWeeks.SeasonOf(WeekForAny(locationKeys));
```

`MineAreas.cs`: add
```csharp
    public static int Week(int area) => AvailabilityWeeks.MineAreaWeek(area);
    public static Season GateSeason(int area) => AvailabilityWeeks.MineAreaGateSeason(area);
```

`MetalsAvailability.cs`: change `MetalRule` to `(int Area, int Effort, string Basis)`; entries: copper ore/bar and coal, refined quartz `MineAreas.Area0`; iron ore/bar `Area40`; gold ore/bar `Area80`; iridium ore/bar `SkullCavern`; bone fragment `Area80`. `Derive` returns
```csharp
        int week = MineAreas.Week(rule.Area);
        Season gate = MineAreas.GateSeason(rule.Area);
        return new ItemAvailability(AvailabilityWeeks.SeasonOf(week), rule.Effort,
            $"{rule.Basis}, week {week}, gate {gate}, effort {rule.Effort}", EffortSource.Derived, week, gate);
```

`FishAvailability.Derive`: replace the three floor lines with
```csharp
        int spawnWeek = item.Seasons.Count == 0 ? 1 : AvailabilityWeeks.FirstWeekOf(item.Seasons.Min());
        int locationWeek = LocationGating.WeekForAny(item.Locations);
        int week = Math.Max(spawnWeek, locationWeek);
        Season floor = AvailabilityWeeks.SeasonOf(week);
        string locationNote = locationWeek > 1 ? $", gated by location ({string.Join(", ", item.Locations)})" : "";
```
and pass `EarliestWeek: week, GateSeason: floor` in both `new ItemAvailability(...)` calls (add `EffortSource.Derived` as the fourth argument).

Update `MetalsAvailabilityTests.Each_Metal_Floors_At_Its_Mine_Depth`: iron stays Spring, gold becomes `Season.Spring` for `EarliestSeason` (week 3) while the gate is Summer; adjust the theory to assert `Gate` where the old test asserted `EarliestSeason` for gold and gold bar. Any other test asserting `LocationGating.FloorFor("UndergroundMine...") == Summer` changes to Spring.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green after the expectation updates named above. Do not loosen any other test; if one fails for another reason, stop and report.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Availability/LocationGating.cs src/TheLongestYear.Core/Availability/MineAreas.cs src/TheLongestYear.Core/Availability/MetalsAvailability.cs src/TheLongestYear.Core/Availability/FishAvailability.cs tests/TheLongestYear.Tests/LocationGatingWeekTests.cs tests/TheLongestYear.Tests/MetalsAvailabilityTests.cs
git commit -m "even year 3/12: fish and metals carry weeks and gate seasons; the mines open in Spring, 30 floors a week"
```

---

### Task 4: Mine rules emit weeks (nodes, geodes, monster drops)

**Files:**
- Modify: `MineralNodeAvailability.cs`, `GeodeAvailability.cs`, `MonsterDropAvailability.cs`
- Test: `tests/TheLongestYear.Tests/MineRuleWeekTests.cs` (new)

**Interfaces:** each `Derive` returns `ItemEffort` with `EarliestWeek = MineAreas.Week(area)` and `GateSeason = MineAreas.GateSeason(area)`, where the geode's area is: Geode `Area0`, Frozen Geode `Area40`, Magma Geode `Area80`, Omni Geode `Area40`. Minimum over sources is by week first, then effort.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/MineRuleWeekTests.cs
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MineRuleWeekTests
{
    [Theory]
    [InlineData("(O)80", 1, Season.Spring)]  // Quartz
    [InlineData("(O)84", 2, Season.Spring)]  // Frozen Tear
    [InlineData("(O)64", 3, Season.Summer)]  // Ruby
    [InlineData("(O)74", 9, Season.Fall)]    // Prismatic Shard
    public void Node_week_and_gate(string id, int week, Season gate)
    {
        ItemEffort e = MineralNodeAvailability.Derive(id)!;
        Assert.Equal(week, e.EarliestWeek);
        Assert.Equal(gate, e.GateSeason);
    }

    [Fact]
    public void Geode_mineral_takes_the_shallowest_geode_that_drops_it()
    {
        var drops = new List<RawGeodeDrop>
        {
            new("(O)537", "(O)541", 0.1),   // Magma, week 3
            new("(O)535", "(O)541", 0.05),  // Geode, week 1
        };
        ItemEffort e = GeodeAvailability.Derive("(O)541", drops)!;
        Assert.Equal(1, e.EarliestWeek);
        Assert.Equal(Season.Spring, e.GateSeason);
    }

    [Fact]
    public void Monster_drop_takes_the_shallowest_monster()
    {
        var drops = new List<RawMonsterDrop>
        {
            new("Serpent", "(O)766", 0.9),
            new("Green Slime", "(O)766", 0.9),
        };
        ItemEffort e = MonsterDropAvailability.Derive("(O)766", drops)!;
        Assert.Equal(1, e.EarliestWeek);
    }

    [Fact]
    public void Skull_cavern_only_drop_is_fall()
    {
        var drops = new List<RawMonsterDrop> { new("Pepper Rex", "(O)107", 0.1) };
        ItemEffort e = MonsterDropAvailability.Derive("(O)107", drops)!;
        Assert.Equal(9, e.EarliestWeek);
        Assert.Equal(Season.Fall, e.GateSeason);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter MineRuleWeekTests`
Expected: `EarliestWeek` is null, assertions fail.

- [ ] **Step 3: Implement**

`MineralNodeAvailability.Derive`:
```csharp
        int effort = MineAreas.Effort(rule.Area);
        return new ItemEffort(effort,
            $"node, {rule.Note}, {MineAreas.Label(rule.Area)}, week {MineAreas.Week(rule.Area)}, effort {effort}",
            MineAreas.Week(rule.Area), MineAreas.GateSeason(rule.Area));
```

`GeodeAvailability`: change `GeodeRule` to `(int Area, int Effort, string Label)` with Geode `Area0`, Frozen `Area40`, Magma `Area80`, Omni `Area40`. In `Derive`, pick by `(week, effort)`:
```csharp
            int week = MineAreas.Week(geode.Area);
            bool better = best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"geode, {geode.Label}, chance {drop.Chance:0.###} (+{step}), week {week}, effort {effort}",
                    week, MineAreas.GateSeason(geode.Area));
```

`MonsterDropAvailability.Derive`: same `(week, effort)` ordering with `week = MineAreas.Week(area.Value)` and `MineAreas.GateSeason(area.Value)`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green. `MineAndGeodeAvailabilityTests` asserts efforts only; they are unchanged.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Availability/MineralNodeAvailability.cs src/TheLongestYear.Core/Availability/GeodeAvailability.cs src/TheLongestYear.Core/Availability/MonsterDropAvailability.cs tests/TheLongestYear.Tests/MineRuleWeekTests.cs
git commit -m "even year 4/12: nodes, geodes and monster drops carry the mine area's week and gate"
```

---

### Task 5: Artifacts, animal products, artisan goods, dishes and pond products emit weeks

**Files:**
- Modify: `ArtifactAvailability.cs`, `AnimalProductAvailability.cs`, `ArtisanAvailability.cs`, `CookedDishAvailability.cs`, `FishPondAvailability.cs`, `EffortComposer.cs`
- Test: `tests/TheLongestYear.Tests/FarmRuleWeekTests.cs` (new)

**Interfaces:**
- `EffortComposer` gains `public int? WeekOf(string qualifiedId)` (season-derived: `Week`; effort-derived: `EarliestWeek`; unknown: null) and passes it to the recursive rules as a second resolver.
- `ArtisanAvailability.Derive(string id, EffortData data, Func<string,int?> effortOf, Func<string,int?> weekOf)`: week = `max(MachineLevelWeek(level), input week)`; `MachineUnlockLevel(string?)` returns the parsed level (0 for default, 10 for quest/friendship/null).
- `CookedDishAvailability.Derive(string id, EffortData data, Func<string,int?> effortOf, Func<string,int?> weekOf, bool hasKitchen)`: week = `max(hasKitchen ? 1 : KitchenWeek, max ingredient week)`; an ingredient with no week makes the dish unknown (`EarliestWeek = null`).
- `FishPondAvailability.Derive(string id, EffortData data, Func<string,int?> effortOf, Func<string,int?> weekOf)`: week = fish week + `PondDelayWeeks`.
- `AnimalProductAvailability.Derive`: week = `HousingTierWeek(links)`; deluxe produce adds one tier (min 9).
- `ArtifactAvailability.Derive`: week = `max(ArtifactWeek, LocationGating.WeekFor(location))`, minimum over spots by `(week, effort)`.
- Every rule sets `GateSeason = SeasonOf(week)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/FarmRuleWeekTests.cs
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class FarmRuleWeekTests
{
    private static readonly Func<string, int?> NoEffort = _ => 1;

    [Fact]
    public void Artifact_in_town_is_week_1_and_desert_only_is_week_9()
    {
        Assert.Equal(1, ArtifactAvailability.Derive("(O)100", new List<RawArtifactSpot> { new("Town", "(O)100", 0.1) })!.EarliestWeek);
        Assert.Equal(9, ArtifactAvailability.Derive("(O)100", new List<RawArtifactSpot> { new("Desert", "(O)100", 0.1) })!.EarliestWeek);
    }

    [Fact]
    public void Animal_products_follow_the_building_tier()
    {
        var buildings = new List<RawBuilding> { new("Coop", null), new("Big Coop", "Coop"), new("Deluxe Coop", "Big Coop") };
        var animals = new List<RawFarmAnimal>
        {
            new("Chicken", "Coop", 800, 1, new[] { "(O)176" }, new[] { "(O)174" }),
            new("Duck", "Big Coop", 1200, 2, new[] { "(O)442" }, new[] { "(O)444" }),
            new("Rabbit", "Deluxe Coop", 8000, 4, new[] { "(O)440" }, new[] { "(O)446" }),
        };
        Assert.Equal(2, AnimalProductAvailability.Derive("(O)176", animals, buildings)!.EarliestWeek); // Egg
        Assert.Equal(5, AnimalProductAvailability.Derive("(O)174", animals, buildings)!.EarliestWeek); // Large Egg, deluxe produce, one tier later
        Assert.Equal(5, AnimalProductAvailability.Derive("(O)442", animals, buildings)!.EarliestWeek); // Duck Egg
        Assert.Equal(9, AnimalProductAvailability.Derive("(O)446", animals, buildings)!.EarliestWeek); // Rabbit's Foot
    }

    [Theory]
    [InlineData("default", 0)] [InlineData("s Farming 3", 3)] [InlineData("s Farming 8", 8)] [InlineData("null", 10)] [InlineData("f Robin 6", 10)]
    public void Machine_unlock_level(string unlock, int level) => Assert.Equal(level, ArtisanAvailability.MachineUnlockLevel(unlock));

    [Fact]
    public void Artisan_good_is_the_later_of_machine_and_input()
    {
        var data = new EffortData
        {
            MachineRules = new List<RawMachineRule> { new("(BC)12", "(O)254", Array.Empty<string>(), new[] { "(O)348" }, 10000, -1) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "s Farming 8" },
        };
        // Keg is week 7; Melon is week 5; Melon Wine is week 7.
        ItemEffort e = ArtisanAvailability.Derive("(O)348", data, NoEffort, id => id == "(O)254" ? 5 : null)!;
        Assert.Equal(7, e.EarliestWeek);
        // Keg with a week-9 input is week 9.
        ItemEffort late = ArtisanAvailability.Derive("(O)348", data, NoEffort, _ => 9)!;
        Assert.Equal(9, late.EarliestWeek);
    }

    [Fact]
    public void Dish_needs_the_kitchen_and_its_ingredients()
    {
        var data = new EffortData
        {
            CookingRecipes = new List<RawCookingRecipe> { new("Fried Egg", new[] { "176" }, "(O)194", "default") },
            Objects = new Dictionary<string, RawObjectEntry>(),
        };
        Assert.Equal(5, CookedDishAvailability.Derive("(O)194", data, NoEffort, _ => 2, hasKitchen: false)!.EarliestWeek);
        Assert.Equal(2, CookedDishAvailability.Derive("(O)194", data, NoEffort, _ => 2, hasKitchen: true)!.EarliestWeek);
        Assert.Null(CookedDishAvailability.Derive("(O)194", data, NoEffort, _ => null, hasKitchen: false)!.EarliestWeek);
    }

    [Fact]
    public void Pond_product_is_the_fish_plus_a_season()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["128"] = new RawObjectEntry("Pufferfish", "Fish", -4, 200, new[] { "fish_ocean" }) },
            FishPonds = new List<RawFishPondRule> { new(new[] { "fish_ocean" }, new List<RawFishPondProduct> { new("(O)812", 1) }) },
        };
        ItemEffort e = FishPondAvailability.Derive("(O)812", data, NoEffort, _ => 5)!;
        Assert.Equal(9, e.EarliestWeek);
    }
}
```

Check `RawObjectEntry`'s positional parameters in `ItemPoolModel.cs:82` before running and adjust the constructor call in the pond test to match its actual parameter order.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter FarmRuleWeekTests`
Expected: build errors on the new signatures.

- [ ] **Step 3: Implement**

`ArtifactAvailability.Derive`: compute `int week = Math.Max(AvailabilityWeeks.ArtifactWeek, LocationGating.WeekFor(spot.Location));` and pick the best by `(week, effort)`; return `new ItemEffort(effort, basis + $", week {week}", week, AvailabilityWeeks.SeasonOf(week))`.

`AnimalProductAvailability`: add `public static int HousingLinks(string building, IReadOnlyList<RawBuilding> buildings)` (the loop from `HousingEffort` returning `links`), keep `HousingEffort = BaseHousingEffort + HousingLinks(...)`. In `Derive`: `int week = AvailabilityWeeks.HousingTierWeek(HousingLinks(animal.Building, buildings) + (regular ? 0 : 1));` choose best by `(week, effort)`, return with `week, AvailabilityWeeks.SeasonOf(week)`.

`ArtisanAvailability`: add
```csharp
    public const int QuestUnlockLevel = 10;
    /// <summary>Skill level a machine recipe needs; 0 for a default recipe, 10 for anything a
    /// quest, friendship or purchase gates (the last thing a first-year player gets).</summary>
    public static int MachineUnlockLevel(string? unlockCondition)
    {
        string text = (unlockCondition ?? "").Trim();
        if (text.Equals("default", StringComparison.OrdinalIgnoreCase)) return 0;
        if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase) || text.Equals("none", StringComparison.OrdinalIgnoreCase))
            return QuestUnlockLevel;
        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase)) return QuestUnlockLevel;
        if (tokens[0].Equals(SkillPrefix, StringComparison.OrdinalIgnoreCase)) tokens = tokens.Skip(1).ToArray();
        return tokens.Length >= 2 && int.TryParse(tokens[^1], out int level) ? level : QuestUnlockLevel;
    }
```
`Derive` gains `Func<string, int?> weekOf`; `InputEffort` returns a third value `int? Week` (the required item's `weekOf`, or the min week among tag matches that have one; `null` when no input); per rule `int? inputWeek`, `int machineWeek = AvailabilityWeeks.MachineLevelWeek(MachineUnlockLevel(unlock));` `int? week = inputWeek == null && HasInput(rule) ? null : Math.Max(machineWeek, inputWeek ?? 1);` where `HasInput` is `!string.IsNullOrEmpty(rule.RequiredItemId) || rule.RequiredTags.Count > 0`. Best by `(week ?? int.MaxValue, effort)`. Return `new ItemEffort(effort, basis + $", week {week?.ToString() ?? "unknown"}", week, week == null ? null : AvailabilityWeeks.SeasonOf(week.Value))`.

`CookedDishAvailability.Derive` gains `Func<string, int?> weekOf` before `hasKitchen`; `IngredientEffort` gets a sibling `IngredientWeek` (same category logic, min week among members with one); per recipe `int? week = ingredients all have weeks ? Math.Max(hasKitchen ? 1 : AvailabilityWeeks.KitchenWeek, maxIngredientWeek) : null`. Best by `(week ?? int.MaxValue, effort)`.

`FishPondAvailability.Derive` gains `Func<string, int?> weekOf`; `int? week = weekOf(fishId) is int w ? w + AvailabilityWeeks.PondDelayWeeks : null;` clamp to `Calendar.WeeksPerYear`.

`EffortComposer`: add
```csharp
    public int? WeekOf(string qualifiedId)
    {
        if (qualifiedId == null) return null;
        if (_seasonDerived.TryGetValue(qualifiedId, out ItemAvailability? season)) return season.Week;
        if (_memo.TryGetValue(qualifiedId, out ItemEffort? memo)) return memo?.EarliestWeek;
        if (!_visiting.Add(qualifiedId)) return null;
        try { ItemEffort? d = Derive(qualifiedId); _memo[qualifiedId] = d; return d?.EarliestWeek; }
        finally { _visiting.Remove(qualifiedId); }
    }
```
and pass `WeekOf` into the artisan, pond and dish rules in `Derive`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green; `EffortTiersAndComposerTests` and `EffortRuleTests` compile against the new signatures (add the `weekOf` argument as `_ => 1` where a test calls a rule directly).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Availability tests/TheLongestYear.Tests/FarmRuleWeekTests.cs tests/TheLongestYear.Tests/EffortRuleTests.cs tests/TheLongestYear.Tests/EffortTiersAndComposerTests.cs
git commit -m "even year 5/12: artifacts, animal products, artisan goods, dishes and pond products carry weeks"
```

---

### Task 6: Crops, forage and saplings get weeks from the pools

**Files:**
- Modify: `CropForageAvailability.cs`, `EffortComposer.cs`, `ItemAvailabilityBuilder.cs`, `EffortData.cs` (`RawCropGrowth` gains `Seasons`), `src/TheLongestYear/Loop/GameEffortData.cs:173` (fill the seasons from `Data/Crops`)
- Test: `tests/TheLongestYear.Tests/CropForageWeekTests.cs` (new)

**Interfaces:**
- `RawCropGrowth(string HarvestItemId, int GrowthDays, bool Regrows, bool Trellis, IReadOnlyList<Season> Seasons)` (new last parameter, default empty via a constructor overload so existing tests compile).
- `CropForageAvailability.DeriveCrop`: week = `FirstWeekOf(min season) + ceil(GrowthDays / 7)`, clamped to the season's last week; a crop with no seasons is unknown (week null). Festival-seed crops: Strawberry `(O)400` week 3, Rare Seed/Sweet Gem Berry `(O)417` week 13 (table `FestivalCropWeeks` in `AvailabilityWeeks`).
- `CropForageAvailability.DeriveForage(string id, IReadOnlyList<RawSpawnEntry> spawns)`: week = min over spawn entries of `max(FirstWeekOf(entry.Season ?? Spring), LocationGating.WeekFor(entry.Location))`; Salmonberry `(O)296` and Blackberry `(O)410` come from `AvailabilityWeeks.BushBerryWeeks` and are placed even with no spawn rows.
- New `CropForageAvailability.DeriveSapling(string id, IReadOnlyList<PoolItem> saplings)`: week `SaplingWeek`, effort 2.
- `EffortComposer` constructor gains `IReadOnlyList<PoolItem> saplings`; `ItemAvailabilityBuilder.Build` passes `pools.Saplings`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/CropForageWeekTests.cs
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class CropForageWeekTests
{
    [Fact]
    public void Parsnip_is_week_1_and_cauliflower_week_2()
    {
        var crops = new List<RawCropGrowth>
        {
            new("(O)24", 4, false, false, new[] { Season.Spring }),
            new("(O)190", 12, false, false, new[] { Season.Spring }),
        };
        Assert.Equal(1, CropForageAvailability.DeriveCrop("(O)24", crops)!.EarliestWeek);
        Assert.Equal(2, CropForageAvailability.DeriveCrop("(O)190", crops)!.EarliestWeek);
    }

    [Fact]
    public void Melon_is_summer_week_6_and_a_long_crop_never_leaves_its_season()
    {
        var crops = new List<RawCropGrowth>
        {
            new("(O)254", 12, false, false, new[] { Season.Summer }),
            new("(O)276", 13, false, false, new[] { Season.Fall }),  // Pumpkin
        };
        Assert.Equal(6, CropForageAvailability.DeriveCrop("(O)254", crops)!.EarliestWeek);
        Assert.Equal(10, CropForageAvailability.DeriveCrop("(O)276", crops)!.EarliestWeek);
    }

    [Fact]
    public void Strawberry_waits_for_the_egg_festival()
    {
        var crops = new List<RawCropGrowth> { new("(O)400", 8, true, false, new[] { Season.Spring }) };
        Assert.Equal(3, CropForageAvailability.DeriveCrop("(O)400", crops)!.EarliestWeek);
    }

    [Fact]
    public void Crop_with_no_seasons_is_unknown()
    {
        var crops = new List<RawCropGrowth> { new("(O)454", 28, false, false, new Season[0]) };
        Assert.Null(CropForageAvailability.DeriveCrop("(O)454", crops)!.EarliestWeek);
    }

    [Fact]
    public void Forage_takes_the_earliest_spawn_and_the_location_gate()
    {
        var spawns = new List<RawSpawnEntry>
        {
            new("(O)88", Season.Spring, null, "Desert"),      // Coconut, week 9 by location
            new("(O)78", null, null, "UndergroundMine20"),     // Cave Carrot, week 1
            new("(O)404", Season.Fall, null, "Forest"),        // Common Mushroom
            new("(O)404", Season.Spring, null, "Woods"),
        };
        Assert.Equal(9, CropForageAvailability.DeriveForage("(O)88", spawns)!.EarliestWeek);
        Assert.Equal(1, CropForageAvailability.DeriveForage("(O)78", spawns)!.EarliestWeek);
        Assert.Equal(1, CropForageAvailability.DeriveForage("(O)404", spawns)!.EarliestWeek);
    }

    [Fact]
    public void Bush_berries_are_placed_without_spawn_rows()
    {
        Assert.Equal(3, CropForageAvailability.DeriveForage("(O)296", new List<RawSpawnEntry>())!.EarliestWeek);
        Assert.Equal(10, CropForageAvailability.DeriveForage("(O)410", new List<RawSpawnEntry>())!.EarliestWeek);
    }

    [Fact]
    public void Sapling_is_week_1()
    {
        var saplings = new List<PoolItem> { new("(O)628", 3400, 1, new List<Season>(), new List<string>()) };
        Assert.Equal(1, CropForageAvailability.DeriveSapling("(O)628", saplings)!.EarliestWeek);
        Assert.Null(CropForageAvailability.DeriveSapling("(O)24", saplings));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter CropForageWeekTests`
Expected: build errors (5-argument `RawCropGrowth`, `DeriveSapling`).

- [ ] **Step 3: Implement**

`EffortData.cs`:
```csharp
public sealed record RawCropGrowth(string HarvestItemId, int GrowthDays, bool Regrows, bool Trellis, IReadOnlyList<Season> Seasons)
{
    public RawCropGrowth(string harvestItemId, int growthDays, bool regrows, bool trellis)
        : this(harvestItemId, growthDays, regrows, trellis, System.Array.Empty<Season>()) { }
}
```

`AvailabilityWeeks.cs`: add
```csharp
    /// <summary>Crops whose seeds only a festival sells: the harvest cannot come before the festival week.</summary>
    public static readonly IReadOnlyDictionary<string, int> FestivalCropWeeks = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)400"] = 3,    // Strawberry, Egg Festival Spring 13
        ["(O)417"] = 13,   // Sweet Gem Berry, Rare Seed from the cart, 24 days
    };
    public static readonly IReadOnlyDictionary<string, int> BushBerryWeeks = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)296"] = SalmonberryWeek,
        ["(O)410"] = BlackberryWeek,
    };
```

`CropForageAvailability.DeriveCrop`: after computing `effort`, compute the week:
```csharp
            int? week = null;
            if (crop.Seasons.Count > 0)
            {
                Season first = crop.Seasons.Min();
                int grow = Math.Max(1, (crop.GrowthDays + Calendar.DaysPerWeek - 1) / Calendar.DaysPerWeek);
                week = Math.Min(AvailabilityWeeks.FirstWeekOf(first) + grow - 1, AvailabilityWeeks.LastWeekOf(first));
                if (AvailabilityWeeks.FestivalCropWeeks.TryGetValue(qualifiedId, out int festival))
                    week = Math.Max(week.Value, festival);
            }
```
pick best by `(week ?? int.MaxValue, effort)`; return `new ItemEffort(effort, basis + $", week {week?.ToString() ?? "unknown"}", week, week == null ? null : AvailabilityWeeks.SeasonOf(week.Value))`.

`DeriveForage`: replace the early `if (locations.Count == 0) return null;` with a bush-berry branch:
```csharp
        if (locations.Count == 0)
        {
            if (AvailabilityWeeks.BushBerryWeeks.TryGetValue(qualifiedId, out int bushWeek))
                return new ItemEffort(BaseEffort, $"bush berry, week {bushWeek}", bushWeek, AvailabilityWeeks.SeasonOf(bushWeek));
            return null;
        }
        int week = spawns.Where(s => s.ItemId == qualifiedId)
            .Select(s => Math.Max(AvailabilityWeeks.FirstWeekOf(s.Season ?? Season.Spring), LocationGating.WeekFor(s.Location ?? "")))
            .Min();
```
and return with `week, AvailabilityWeeks.SeasonOf(week)` and `", week {week}"` in the basis.

Add:
```csharp
    private const int SaplingEffort = 2;
    public static ItemEffort? DeriveSapling(string qualifiedId, IReadOnlyList<PoolItem> saplings)
    {
        if (saplings == null) throw new ArgumentNullException(nameof(saplings));
        if (!saplings.Any(s => s.ItemId == qualifiedId)) return null;
        return new ItemEffort(SaplingEffort, $"sapling, sold daily, week {AvailabilityWeeks.SaplingWeek}",
            AvailabilityWeeks.SaplingWeek, Season.Spring);
    }
```

`EffortComposer`: constructor `(EffortData data, IReadOnlyDictionary<string, ItemAvailability> seasonDerived, bool hasKitchen, IReadOnlyList<PoolItem>? saplings = null)`, field `_saplings`; append `?? CropForageAvailability.DeriveSapling(qualifiedId, _saplings ?? Array.Empty<PoolItem>())` to `Derive`. `ItemAvailabilityBuilder.Build` passes `pools.Saplings`.

`GameEffortData.cs:173`: pass the crop's seasons (the `Data/Crops` entry's `Seasons` list mapped to `Season`) as the fifth argument. Read the surrounding code to find the crop entry variable; the mapping helper already used for pools (`SpawnSeasonMap` or the season parser in `GameDataPools`) is the one to reuse.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/AvailabilityWeeks.cs src/TheLongestYear.Core/Availability src/TheLongestYear/Loop/GameEffortData.cs tests/TheLongestYear.Tests/CropForageWeekTests.cs
git commit -m "even year 6/12: crops, forage and saplings carry weeks from the pools"
```

---

### Task 7: Goals by week, deadlines by gate season

**Files:**
- Modify: `src/TheLongestYear.Core/GoalObtainability.cs`, `src/TheLongestYear.Core/BundleDeadlines.cs:56-57`, `src/TheLongestYear/Loop/RunController.cs:1060-1075, 1120-1126, 1186-1207`, `src/TheLongestYear/ModEntry.cs:2101`
- Test: update `tests/TheLongestYear.Tests/GoalObtainabilityTests.cs`, `BundleDeadlinesTests.cs`

**Interfaces:**
- `GoalObtainability.IsObtainable(IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability, string itemId, int weekOfYear)`: catalog season of `SeasonOf(weekOfYear)` must contain; if `availability.IsPlaced(id)` then `For(id).Week <= weekOfYear`. Keep the old season overload as `IsObtainable(..., Season season) => IsObtainable(..., LastWeekOf(season))` for the day-28 hub preview of next season.
- `RunController.IsObtainableInWeek(string itemId, int weekOfYear)`; `IsObtainableInSeason(id, season)` stays and calls the week overload with `LastWeekOf(season)`.
- `SampleSlotsForTheme(theme, season, weekOfYear)` and `DescribeGoalPool` pass `id => IsObtainableInWeek(id, weekOfYear)`.
- `BundleDeadlines.For` clamps with `availability.Gate`.

- [ ] **Step 1: Write the failing tests**

Add to `GoalObtainabilityTests.cs`:
```csharp
    [Fact]
    public void A_week_3_item_is_not_a_week_1_goal_but_is_a_week_3_goal()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(),
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)64"] = new(5, "ruby", EarliestWeek: 3, GateSeason: Season.Summer) });
        Assert.False(GoalObtainability.IsObtainable(null, model, "(O)64", 1));
        Assert.True(GoalObtainability.IsObtainable(null, model, "(O)64", 3));
    }

    [Fact]
    public void An_unknown_item_is_still_a_goal_when_the_catalog_allows_it()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>());
        Assert.True(GoalObtainability.IsObtainable(null, model, "(O)24", 1));
    }
```
Add to `BundleDeadlinesTests.cs`:
```csharp
    [Fact]
    public void Deadline_clamps_to_the_gate_season_not_the_goal_week()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>
        {
            ["(O)64"] = new ItemAvailability(Season.Spring, 1, "ruby", EffortSource.Derived, 3, Season.Summer),
        });
        var result = BundleDeadlines.For(new List<string> { "(O)64" }, model);
        Assert.Equal(Season.Summer, result["(O)64"]);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter "GoalObtainabilityTests|BundleDeadlinesTests"`
Expected: build error (int overload), and the clamp test fails with Spring.

- [ ] **Step 3: Implement**

`GoalObtainability.cs`:
```csharp
    public static bool IsObtainable(IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability, string itemId, int weekOfYear)
    {
        Season season = AvailabilityWeeks.SeasonOf(weekOfYear);
        if (catalogSeasons != null && !catalogSeasons.Contains(season)) return false;
        if (availability != null && availability.IsPlaced(itemId) && availability.For(itemId).Week > weekOfYear) return false;
        return true;
    }

    public static bool IsObtainable(IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability, string itemId, Season season)
        => IsObtainable(catalogSeasons, availability, itemId, AvailabilityWeeks.LastWeekOf(season));
```
(Keep the existing summary comment, note the change to `IsPlaced`.)

`BundleDeadlines.cs:56-57`: `if (availability.Gate > deadline) deadline = availability.Gate;`

`RunController.cs`: add
```csharp
        public bool IsObtainableInWeek(string itemId, int weekOfYear)
        {
            System.Collections.Generic.IReadOnlySet<CoreSeason> catalogSeasons = null;
            foreach (var item in _catalog)
                if (item.Id == itemId) { catalogSeasons = item.ObtainableSeasons; break; }
            return GoalObtainability.IsObtainable(catalogSeasons, Availability, itemId, weekOfYear);
        }
```
and make `IsObtainableInSeason(itemId, season) => IsObtainableInWeek(itemId, AvailabilityWeeks.LastWeekOf(season))`. In `SampleSlotsForTheme` (line 1071) and `DescribeGoalPool` (line 1124) change the predicate to `id => IsObtainableInWeek(id, weekOfYear)` (`DescribeGoalPool` gets a `int weekOfYear` parameter; its caller in `ModEntry` (tly_themepool) passes `_meta.Run.WeekOfYear`). The day-28 pre-pick passes `WeekOfYear + 1` already, which is next season's first week: correct.

`ModEntry.cs:2101` (gatecheck ordering) keeps using `IsObtainableInSeason`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests` then `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: green, build 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/GoalObtainability.cs src/TheLongestYear.Core/BundleDeadlines.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/GoalObtainabilityTests.cs tests/TheLongestYear.Tests/BundleDeadlinesTests.cs
git commit -m "even year 7/12: weekly goals check the item's week, per-item deadlines clamp to the gate season"
```

---

### Task 8: The ramp follows the items

**Files:**
- Modify: `src/TheLongestYear.Core/BundleClassifier.cs`, `src/TheLongestYear.Core/GameplayConfig.cs:170-203`
- Test: `tests/TheLongestYear.Tests/RampFromItemsTests.cs` (new); update `BundleClassifierTests.cs`, `CuratedQuotaRampTests.cs`

**Interfaces:**
- `BundleClassifier.RampFromItems(int numberOfSlots, IReadOnlyList<string> ingredients, ItemAvailabilityModel model)` returns `int[4]`: `even[s] = (int)Math.Round(X * (s + 1) / 4.0, MidpointRounding.AwayFromZero)`, `reachable[s] = count of ingredients with model.For(id).Gate <= s`, `ramp[s] = min(even[s], reachable[s])`, monotone non-decreasing, `ramp[3] = X`.
- `Classify`: when `availability != null`, a Percentage bundle (X < Y) uses `RampFromItems` unless `bundleQuotas` names it (user override, still `ShiftRampToSlotCount`-clamped). `DerivedDefaultQuota` remains only for the `availability == null` legacy path.
- `GameplayConfig.DefaultBundleQuotas` becomes an empty dictionary (the user's `BundleQuotas` is the only override).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/RampFromItemsTests.cs
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RampFromItemsTests
{
    private static ItemAvailabilityModel Model(params (string Id, Season Gate)[] items)
        => new(items.ToDictionary(i => i.Id,
            i => new ItemAvailability(i.Gate, 1, "test", EffortSource.Derived, AvailabilityWeeks.FirstWeekOf(i.Gate), i.Gate),
            System.StringComparer.Ordinal));

    [Fact]
    public void Even_split_when_everything_is_spring()
    {
        var model = Model(("a", Season.Spring), ("b", Season.Spring), ("c", Season.Spring), ("d", Season.Spring), ("e", Season.Spring), ("f", Season.Spring));
        Assert.Equal(new[] { 1, 2, 3, 4 }, BundleClassifier.RampFromItems(4, new[] { "a", "b", "c", "d", "e", "f" }, model));
    }

    [Fact]
    public void Ramp_never_asks_for_more_than_is_reachable()
    {
        // Preserver's-like: two Spring goods, two Summer, two Fall; X = 4.
        var model = Model(("a", Season.Spring), ("b", Season.Spring), ("c", Season.Summer), ("d", Season.Summer), ("e", Season.Fall), ("f", Season.Fall));
        Assert.Equal(new[] { 1, 2, 3, 4 }, BundleClassifier.RampFromItems(4, new[] { "a", "b", "c", "d", "e", "f" }, model));
        var late = Model(("a", Season.Fall), ("b", Season.Fall), ("c", Season.Winter), ("d", Season.Winter));
        Assert.Equal(new[] { 0, 0, 2, 2 }, BundleClassifier.RampFromItems(2, new[] { "a", "b", "c", "d" }, late));
    }

    [Fact]
    public void Winter_always_demands_x_and_the_ramp_is_monotone()
    {
        var model = Model(("a", Season.Winter), ("b", Season.Winter), ("c", Season.Winter));
        Assert.Equal(new[] { 0, 0, 0, 2 }, BundleClassifier.RampFromItems(2, new[] { "a", "b", "c" }, model));
    }

    [Fact]
    public void Unknown_items_count_as_winter()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>());
        Assert.Equal(new[] { 0, 0, 0, 3 }, BundleClassifier.RampFromItems(3, new[] { "x", "y", "z", "w" }, model));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter RampFromItemsTests`
Expected: build error (`RampFromItems`).

- [ ] **Step 3: Implement**

```csharp
    /// <summary>Spec 2026-08-28-even-year: the ramp follows the items. An even quarter split of X,
    /// never above what the bundle's ingredients can supply by each season's gate, monotone, and
    /// X in Winter so the bundle must be completed to win.</summary>
    public static int[] RampFromItems(int numberOfSlots, IReadOnlyList<string> ingredients, ItemAvailabilityModel model)
    {
        if (numberOfSlots < 1) throw new ArgumentOutOfRangeException(nameof(numberOfSlots));
        if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
        if (model == null) throw new ArgumentNullException(nameof(model));
        var ramp = new int[Calendar.MonthsPerYear];
        for (int s = 0; s < ramp.Length; s++)
        {
            var season = (Season)s;
            int even = (int)Math.Round(numberOfSlots * (s + 1) / (double)Calendar.MonthsPerYear, MidpointRounding.AwayFromZero);
            int reachable = ingredients.Count(id => model.For(id).Gate <= season);
            ramp[s] = Math.Clamp(Math.Min(even, reachable), 0, numberOfSlots);
        }
        ramp[^1] = numberOfSlots;
        for (int s = 1; s < ramp.Length; s++) ramp[s] = Math.Max(ramp[s], ramp[s - 1]);
        return ramp;
    }
```
In `Classify`, before the `KIND 2: PerItem` block and after the named-quota block, insert:
```csharp
        if (availability != null && parsed.NumberOfSlots < ingredients.Count)
        {
            return BundleRequirement.CreatePercentage(
                name, theme, ingredients,
                numberOfSlots: parsed.NumberOfSlots,
                cumulativeRequiredBySeason: RampFromItems(parsed.NumberOfSlots, ingredients, availability),
                ingredientStacks: ingredientStacks,
                ingredientQualities: ingredientQualities);
        }
```
`GameplayConfig.DefaultBundleQuotas` becomes `new Dictionary<string, int[]>()` with a comment pointing at the spec; delete the old entries. Update `CuratedQuotaRampTests`: the `Curated` member data and the Gil's test are deleted; replace with one test asserting `GameplayConfig.DefaultBundleQuotas` is empty and one asserting a user `BundleQuotas` entry still wins over `RampFromItems` in `Classify`. Keep `QuotaRampShiftTests` unchanged. In `BundleClassifierTests`, tests that classified a named curated bundle (Chef's) against `DefaultBundleQuotas` now pass `availability` and assert the item-derived ramp, or pass an explicit quota dictionary.

Also log the derived ramp at board build: in `BundleCatalogBuilder` line 188 the existing "using derived ramp" log already prints `req.CumulativeRequiredBySeason`; leave it.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/BundleClassifier.cs src/TheLongestYear.Core/GameplayConfig.cs tests/TheLongestYear.Tests/RampFromItemsTests.cs tests/TheLongestYear.Tests/BundleClassifierTests.cs tests/TheLongestYear.Tests/CuratedQuotaRampTests.cs
git commit -m "even year 8/12: pick-X-of-Y ramps derive from their own items; the curated quota table is retired"
```

---

### Task 9: The engine gives every bundle a Spring foothold

**Files:**
- Modify: `src/TheLongestYear.Core/BundleSlotFiller.cs:35-111`, `src/TheLongestYear/Loop/BundleEngine.cs:121-125, 239`, `src/TheLongestYear/Loop/WorldResetService.cs:599`, `src/TheLongestYear/ModEntry.cs` (gatecheck)
- Test: add to `tests/TheLongestYear.Tests/BundleSlotFillerTests.cs`

**Interfaces:**
- `BundleSlotFiller.Fill(..., IReadOnlySet<string>? avoid = null, Func<string, bool>? springReady = null)`. After sampling, if `springReady != null`: `need = Math.Max(1, (targetCount + 3) / 4)`; `have = chosen.Count(c => springReady(c.ItemId))`; while `have < need` and a candidate not yet chosen with `springReady` exists, replace the last non-Spring pick with the next Spring candidate drawn by `WeightedSampler.Sample(springCandidates, 1, rng)`; log `$"'{spec.Name}': swapped {swaps} slot(s) for a Spring foothold"`. If none exist, log `$"'{spec.Name}': no Spring foothold in its pool"` and continue.
- `BundleEngine` gets `public Func<string, bool> SpringReady { get; set; }`; pass it into `Fill`. `WorldResetService` sets `engine.SpringReady = id => AvailabilityModel != null && AvailabilityModel.IsPlaced(id) && AvailabilityModel.For(id).Gate == Season.Spring;` right after constructing the engine (and the two ModEntry engine sites do the same with `_availability`).
- `tly_gatecheck` prints `[no spring foothold]` for a bundle whose ingredients have no `Gate == Spring` item.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Fill_keeps_at_least_one_spring_item_when_the_pool_has_one()
    {
        var pools = new ItemPools { Metals = new[] { Item("(O)386", weight: 100), Item("(O)384", weight: 100), Item("(O)337", weight: 100), Item("(O)378", weight: 1) } };
        var spec = Spec("Blacksmith's", 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(7),
            springReady: id => id == "(O)378");
        Assert.Contains(filled.Slots, s => s.ItemId == "(O)378");
        Assert.Equal(3, filled.Slots.Select(s => s.ItemId).Distinct().Count());
    }

    [Fact]
    public void Fill_without_a_spring_candidate_still_fills()
    {
        var pools = new ItemPools { Metals = new[] { Item("(O)386"), Item("(O)384"), Item("(O)337") } };
        var spec = Spec("Blacksmith's", 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(7),
            springReady: _ => false);
        Assert.Equal(3, filled.Slots.Count);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter BundleSlotFillerTests`
Expected: build error (`springReady`).

- [ ] **Step 3: Implement**

In `Fill`, after `List<PoolItem> chosen = WeightedSampler.Sample(candidates, targetCount, rng, capped, cap);` add:
```csharp
        if (springReady != null)
        {
            int need = Math.Max(1, (targetCount + Calendar.MonthsPerYear - 1) / Calendar.MonthsPerYear);
            int have = chosen.Count(c => springReady(c.ItemId));
            var chosenIds = new HashSet<string>(chosen.Select(c => c.ItemId), StringComparer.Ordinal);
            List<PoolItem> springPool = candidates.Where(c => springReady(c.ItemId) && !chosenIds.Contains(c.ItemId)).ToList();
            int swaps = 0;
            while (have < need && springPool.Count > 0)
            {
                int victim = chosen.FindLastIndex(c => !springReady(c.ItemId));
                if (victim < 0) break;
                PoolItem pick = WeightedSampler.Sample(springPool, 1, rng)[0];
                springPool.Remove(pick);
                chosen[victim] = pick;
                have++; swaps++;
            }
            if (swaps > 0) log?.Invoke($"'{spec.Name}': swapped {swaps} slot(s) for a Spring foothold.");
            else if (have < need) log?.Invoke($"'{spec.Name}': no Spring foothold in its pool.");
        }
```
`BundleEngine`: property `public Func<string, bool> SpringReady { get; set; }`; the `Fill` call passes `asked, SpringReady`. `WorldResetService.cs` after line 600 and `ModEntry.cs` lines 2772/2778/3425 set the property as described in Interfaces.

`ModEntry.CmdGateCheck`: where each bundle's status tag is chosen, add `bool foothold = req.Ingredients.Any(id => _availability != null && _availability.IsPlaced(id) && _availability.For(id).Gate == TheLongestYear.Core.Season.Spring);` and append `" [no spring foothold]"` to the line when false; count them in the RESULT line.

- [ ] **Step 4: Run tests and build**

Run: `dotnet test tests/TheLongestYear.Tests` and `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: green, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/BundleSlotFiller.cs src/TheLongestYear/Loop/BundleEngine.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/BundleSlotFillerTests.cs
git commit -m "even year 9/12: the engine keeps a Spring foothold in every re-rolled bundle; gatecheck flags bundles without one"
```

---

### Task 10: Flat weekly goals and the week overrides config

**Files:**
- Modify: `src/TheLongestYear.Core/BonusItemSampler.cs:19`, `src/TheLongestYear.Core/GameplayConfig.cs` (new `AvailabilityWeekOverrides`), `src/TheLongestYear/ModEntry.cs:486-488` (pass overrides), `src/TheLongestYear/manifest.json`, `CHANGELOG.md`
- Test: `tests/TheLongestYear.Tests/GameplayConfigFillerTests.cs` (add a default test)

**Interfaces:**
- `BonusItemSampler.DefaultMaxCountBySeason = { 5, 5, 5, 6 }`.
- `GameplayConfig.AvailabilityWeekOverrides : Dictionary<string, int>` (qualified id to week 1..16), default empty; `ModEntry` passes it as `weekOverrides` to `ItemAvailabilityBuilder.Build` (add the parameter to `Build` and thread it to the model constructor).

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Week_overrides_default_empty_and_caps_are_flat()
    {
        var config = new GameplayConfig();
        Assert.Empty(config.AvailabilityWeekOverrides);
        Assert.Equal(new[] { 5, 5, 5, 6 }, BonusItemSampler.DefaultMaxCountBySeason);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests --filter GameplayConfigFillerTests`
Expected: build error (`AvailabilityWeekOverrides`).

- [ ] **Step 3: Implement**

`BonusItemSampler.cs:19`: `new[] { 5, 5, 5, 6 }`. `GameplayConfig.cs` next to `ThemeFillerBySeason`:
```csharp
    /// <summary>Spec 2026-08-28-even-year: move one item's first week (1 to 16). Later only; an
    /// override earlier than the derived floor is rejected and listed by tly_itemmodel.</summary>
    public Dictionary<string, int> AvailabilityWeekOverrides { get; set; } = new();
```
`ItemAvailabilityBuilder.Build` gains `IReadOnlyDictionary<string, int>? weekOverrides = null` and passes it to the model; `ModEntry.cs:486` passes `weekOverrides: _config.AvailabilityWeekOverrides`. Bump the manifest to the next patch; add CHANGELOG lines under Unreleased / Changed: one for the week-granular floors and mines (Tasks 1 to 7), one for the item-derived ramp (Task 8), one for the Spring foothold (Task 9), one for the flat caps and `AvailabilityWeekOverrides` (this task).

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/BonusItemSampler.cs src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json CHANGELOG.md tests/TheLongestYear.Tests/GameplayConfigFillerTests.cs
git commit -m "vX.Y.Z: flat weekly goal caps 5/5/5/6 and AvailabilityWeekOverrides; CHANGELOG for the even-year build"
```

---

### Task 11: Nothing is silently unknown: the dump and the sim

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdDumpAvailability`), `tools/sim-year.sh`, `src/TheLongestYear/manifest.json`, `CHANGELOG.md`

**Interfaces:**
- Table columns: `| Item | Id | Week | Gate | Placed | Catalog seasons | Due | Effort | Basis |`; `Placed` is `derived` (Phase 1), `rule` (effort-only with a week), `override`, or `UNKNOWN`.
- Closing section `## Unknown items (N)` listing `- Name (id), in <bundle>` for every ingredient with `!IsPlaced`, plus `## Rejected overrides (N)` from `RejectedSeasonOverrides`.
- `tools/sim-year.sh` ends with `tly_dumpavailability|tly_gatecheck`, waits for `tly_gatecheck RESULT`, copies `board-availability.md` to `docs/board-availability.md`, and prints the unknown section to stdout.

- [ ] **Step 1: Implement the dump**

Replace the row builder in `CmdDumpAvailability` with:
```csharp
                    TheLongestYear.Core.ItemAvailability a = _availability.For(id);
                    string placed = _availability.IsDerived(id) ? "derived"
                        : !_availability.IsPlaced(id) ? "UNKNOWN"
                        : a.Basis.Contains("override", StringComparison.Ordinal) ? "override" : "rule";
                    if (placed == "UNKNOWN") unknown.Add($"- {DisplayName(id)} ({id}), in {req.Name}");
                    // due / catalogSeasons unchanged
                    sb.AppendLine($"| {DisplayName(id)} | {id} | {a.Week} | {a.Gate} | {placed} | {catalogSeasons} | {due} | {a.Effort} ({a.Source}) | {a.Basis.Replace("|", "/")} |");
```
with `var unknown = new List<string>();` declared before the bundle loop and, after it:
```csharp
            sb.AppendLine($"## Unknown items ({unknown.Count})");
            sb.AppendLine();
            foreach (string line in unknown) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine($"## Rejected overrides ({_availability.RejectedSeasonOverrides.Count})");
            sb.AppendLine();
            foreach (string id in _availability.RejectedSeasonOverrides) sb.AppendLine($"- {DisplayName(id)} ({id}): {_availability.For(id).Basis}");
```
Update the header sentence to describe Week, Gate and Placed. Log the unknown count in the `tly_dumpavailability: wrote ...` line.

- [ ] **Step 2: Extend the sim**

Append to `tools/sim-year.sh` before `say "=== $LABEL: done"`:
```bash
n=$(count)
drv -Action send -Lines "tly_dumpavailability|tly_gatecheck" >/dev/null
drv -Action wait -Pattern "tly_gatecheck RESULT" -TimeoutSec 90 -FromLine "$n" >/dev/null
cp "/c/Program Files (x86)/Steam/steamapps/common/Stardew Valley/Mods/TheLongestYear/board-availability.md" "$REPO/docs/board-availability.md"
say "=== $LABEL: gate audit"; show "$n" "tly_gatecheck|^\s+\[|RESULT"
say "=== $LABEL: unknown items (Jeff must confirm each one)"
sed -n '/^## Unknown items/,/^## Rejected/p' "$REPO/docs/board-availability.md"
```

- [ ] **Step 3: Build and check**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` and `bash -n tools/sim-year.sh`
Expected: 0 errors, no syntax error.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs tools/sim-year.sh src/TheLongestYear/manifest.json CHANGELOG.md
git commit -m "vX.Y.Z: tly_dumpavailability shows week, gate and placed, lists unknown items and rejected overrides; sim-year dumps the board every run"
```

---

### Task 12: Deploy, sim, put the lists in front of Jeff

**Files:**
- Modify: `STATUS.md`, `docs/board-availability.md` (generated, gitignore it: add `docs/board-availability.md` to `.gitignore` next to `item-effort-model.md`)

- [ ] **Step 1: Deploy and load**

```bash
cd "C:/Users/Jeff/Documents/Projects/Stardee Valoo/TheLongestYear"
pwsh -NoProfile -File tools/deploy.ps1 -Minimized; git checkout -- test-output/log-archive
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "Debug bridge: 'pause when window is inactive'" -FromLine 0 -TimeoutSec 180
save=$(ls "$APPDATA/StardewValley/Saves" | grep -o '^None_[0-9]*' | head -1)
n=$(pwsh -NoProfile -File tools/bridge.ps1 -Action count)
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_loadsave $save"
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "Run \d+ ready" -FromLine $n -TimeoutSec 150; sleep 45
```
Expected: `Run N ready`. The save is the Rodger throwaway lineage only.

- [ ] **Step 2: Run both sims (each in the background, one after the other)**

`bash tools/sim-year.sh minimal simG > <scratchpad>/simG.txt` then `bash tools/sim-year.sh goals simH > <scratchpad>/simH.txt`.

- [ ] **Step 3: Check the pass bands from the spec**

For each sim: gates per season between 20% and 30% of required (sum the gatecheck Spring/Summer/Fall/Winter columns); every offered theme asks 3 to 6 in every week; no week where an offered theme asks under 2 while lines remain; every `tly_playseason ... gate WOULD PASS`; `tly_gatecheck RESULT: no impossible gates`. Record the per-week askable table for both sims in `STATUS.md` under a new dated heading, and the gate totals.

- [ ] **Step 4: Report to Jeff and stop**

Give Jeff, in chat: the two askable tables, the gate totals, the `[no spring foothold]` bundles, the full per-bundle item list (path `docs/board-availability.md`, plus the Spring-week items inline), and the **Unknown items** list verbatim, asking him to confirm or assign each one. Do not mark the build verified until he answers; his answers become `AvailabilityWeeks` rows or `AvailabilityWeekOverrides` defaults in the next commit.

- [ ] **Step 5: Commit STATUS**

```bash
git add STATUS.md .gitignore
git commit -m "STATUS: even-year sims G and H, unknown list pending Jeff's ruling"
```

---

## Self-review

- Spec 1 (floors by week): Tasks 1 to 6 cover every row of the category table; the "still unknown" row is Task 2's `UnknownIds` plus Task 11's section.
- Spec 2 (goals by week, gates by season): Task 7.
- Spec 3 (ramp follows items): Task 8, including the difficulty shift (the derivation runs on `parsed.NumberOfSlots`, which `RequiredSlots.Apply` has already shifted by the time the board is classified).
- Spec 4 (foothold): Task 9, including the gatecheck tag.
- Spec 5 (flat goals): Task 10.
- Spec 6 (nothing unknown): Task 11 and Task 12 step 4.
- Verification bands: Task 12 step 3.
- Type consistency: `ItemAvailability.Week`/`Gate` (Task 1) are what Tasks 2, 7, 8, 9, 11 read; `ItemEffort.EarliestWeek`/`GateSeason` (Task 1) are what Tasks 4 to 6 write and Task 2 reads; `IsPlaced` (Task 2) is what Tasks 7, 9, 11 call; `LocationGating.WeekFor` (Task 3) is what Tasks 5 and 6 call; `EffortComposer.WeekOf` (Task 5) is what the artisan, dish and pond rules consume.
