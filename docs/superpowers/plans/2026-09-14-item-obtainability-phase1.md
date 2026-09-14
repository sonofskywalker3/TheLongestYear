# Item Obtainability Model, Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a blind, runtime item obtainability model (every year-1 source per item, by week, counted in isolation) plus `tly_obtain <item>` and `tly_obtain compare`, with nothing wired into gameplay.

**Architecture:** Pure rules in `src/TheLongestYear.Core/Obtainability/` take plain input records and return an `ObtainabilityModel`. Item ids and item queries are resolved by one `ItemQueries` class; direct sources are computed once; grown, made and bartered items (crops, fruit, machines, recipes, ponds, geodes, trade-item shop rows) are resolved by repeated passes until nothing changes; sources the model cannot read become diagnostics, never silent drops. One glue class in the mod project reads the live game data assets at save load. A comparison class outside the blind folder lines the new model up against the existing item model and renders a Markdown report.

**Tech Stack:** C# net6.0, SMAPI 4 / Stardew Valley 1.6, xunit 2.4.1.

**Spec:** `docs/superpowers/specs/2026-09-14-item-obtainability-design.md`

**Review history:** a first draft was reviewed by Codex on 2026-09-14 and rejected (22 findings, most confirmed against the decompile). The second draft addressed them: day-level growth and processing, machine trigger/output semantics, item queries and diagnostics, a richer condition reader, the complete fishing treasure table, pond precedence, content equality for conditions, a convergence guard, and a comparison that reads every id the existing model knows. A second Codex pass found finer gaps, fixed in this version: `RandomItemId` replaces `ItemId` and marks its entries as chance, barter shop rows (`TradeItemId`) only count in weeks the trade item is had, festival reward odds, recipes with several outputs and "none" unlocks, `UseFirstValidOutput`, deluxe produce friendship, no synthesized machine triggers, fruit-tree item queries, even/odd and min/max condition forms, the rest of the treasure table, and settling that also compares source counts.

## Global Constraints

- Branch `story`. Do NOT change `manifest.json`'s `Version` (parallel-branch rule in the workspace CLAUDE.md).
- One commit per task, then `git push origin story` right after it.
- **Weeks in isolation (Jeff, 2026-09-14):** assume the player saved nothing. A week counts only if the item can be obtained in it from scratch: recipe ingredients must all be obtainable in the same week; growing and processing time is allowed, stored items are not. Structures that keep producing (fish ponds, animals, tappers, learned recipes) count from the first week they can be set up.
- **Blind:** these files may not mention (code OR comments, any letter case) any of the words in `ObtainabilityBlindGuardTests.Forbidden` (Task 9), which include `ItemAvailabilityModel`, `ItemAvailability`, `ItemEffort`, `AvailabilityWeeks`, `Core.Availability`, `DefaultItemSeasonPins`, `QuantityBasisTables`, `ItemPoolBuilder`, `GameDataPools`, `GameEffortData`, `LegendaryFishRules`, `LocationGating`, `MineAreas`, `ItemAvailabilityBuilder`, `BundleGenerationTuning`, `EffortData`, `PacingWeek`, `HardWeek`, `SeasonPins`, `UnlockWeeks`, `BasisByDeadline`. Blind files: everything in `src/TheLongestYear.Core/Obtainability/` and `src/TheLongestYear/Loop/GameObtainabilityData.cs`. Allowed Core types: `Season`, `Calendar`, `BundleParsing`.
- Core has no game references; file-scoped namespaces; `Nullable` is enabled there. Mod project: block-scoped `namespace TheLongestYear.Loop`, `internal sealed class`, Nullable off.
- New record parameters are optional and trail the existing ones, so earlier tasks' test constructors keep compiling.
- New files stay under 400 lines; split rather than grow. `ModEntry.cs` is already far past that and is not restructured here: add only the fields, the build call, the two short command methods and the registrations Tasks 10 and 11 show, with all real logic in the new classes.
- No em dashes anywhere (code comments, strings, docs, commit messages).
- Weeks are 1-16 (Spring week 1 = days 1-7 of Spring; Winter 28 is week 16).
- Test command: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false` (add `--filter "FullyQualifiedName~<ClassName>"` for one class). The suite is 2166 passing before this plan; it must stay green after every task.
- Mod build without deploying: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`.
- Nothing reads the new model for gameplay: no board, gate, goal, pacing, sabotage or save-state change.
- Decompile references: PC 1.6 at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley\`; GameData types at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\StardewValley.GameData\` (Android copy at `decompiled\decompiled\StardewValley.GameData\`). Every game fact in this plan cites its line; if code you write disagrees with the decompile, the decompile wins and you say so in the commit.

## Known limitations (accepted for phase 1; do not "fix" them in this plan)

These are read loosely on purpose and are listed in the spec. The comparison report is where they show up.

- Machine rule order: the game uses the first rule that applies; the model counts every rule that could.
- `PerItemCondition` on shop and spawn rows is not read.
- The fishing treasure raccoon seed depends on the catch season; it is an unresolved diagnostic.
- A fish pond counts from the week its fish is had; the days to reach a product's population are not counted.
- Magic Bait rows are flagged, not routed through the bait's own weeks.
- Fishing depth, `DAY_OF_WEEK` and negated weather forms are not narrowed.
- A machine trigger that accepts any placed item (no id, no tags) is skipped; an output-collected trigger reads as needing no input.

## File Structure

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/Obtainability/WeekMask.cs` | 16-bit week set and helpers |
| `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs` | `SourceKind`, `Reliability`, `ObtainConditions` (content equality), `ObtainSource`, `SourcePair` |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs` | `ObtainFilter`, the model and its queries |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` | plain input records the glue fills |
| `src/TheLongestYear.Core/Obtainability/ConditionSeasons.cs` | reads temporal, prerequisite and unknown clauses out of game-state-query strings |
| `src/TheLongestYear.Core/Obtainability/ItemQueries.cs` | resolves ids and item queries; unresolved ones become diagnostics |
| `src/TheLongestYear.Core/Obtainability/SpawnSources.cs` | forage, location fish, crab pot, artifact spots, garbage cans, fishing trash |
| `src/TheLongestYear.Core/Obtainability/ShopSources.cs` | shops, the Traveling Cart, barter rows, festival shops, Night Market boats, festival rewards |
| `src/TheLongestYear.Core/Obtainability/MineSources.cs` | mine nodes, monster drops, fishing treasure (code-only facts) |
| `src/TheLongestYear.Core/Obtainability/GrowSources.cs` | crops, greenhouse, Mixed Seeds, fruit trees (day by day) |
| `src/TheLongestYear.Core/Obtainability/MadeSources.cs` | machines, cooking, crafting, animals, fish ponds, tappers, geode contents |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` | orchestration, repeated passes, diagnostics |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs` | one-item description and source lines |
| `src/TheLongestYear.Core/ItemAvailability.cs` | gains `KnownIds` (NOT blind) |
| `src/TheLongestYear.Core/ObtainabilityComparison.cs` | comparison rows and Markdown report (NOT blind) |
| `src/TheLongestYear/Loop/GameObtainabilityData.cs` | reads Data assets into `ObtainabilityInputs` |
| `src/TheLongestYear/ModEntry.cs` | build at save load, `tly_obtain` command + bridge case |
| `tests/TheLongestYear.Tests/Obtainability*Tests.cs`, `ItemAvailabilityKnownIdsTests.cs` | one test file per unit above |

---

### Task 1: Week sets, source types and the model

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/WeekMask.cs`
- Create: `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs`
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityModelTests.cs`

**Interfaces:**
- Produces: `WeekMask` (`None`, `All`, `Of(int)`, `Range(int,int)`, `FromWeekOnwardOf(int)`, `ForSeason(Season)`, `ForSeasons(IEnumerable<Season>)`, `ForDays(int,int)`, `WeekOfDay(int)`, `Contains(int)`, `IsEmpty`, `Earliest`, `FromWeekOnward()`, `ShiftLater(int)`, `Except(WeekMask)`, `|`, `&`, `ToString()`), `SourceKind` (includes `Other`), `Reliability`, `ObtainConditions` (content equality), `ObtainSource`, `static IEnumerable<ObtainSource> SourcePair.Of(SourceKind kind, WeekMask dependable, WeekMask any, ObtainConditions conditions, string detail)` (used by Tasks 4, 6, 7), `ObtainFilter` (`Any`, `DependableOnly`, `Accepts`), `ObtainabilityModel` (`ItemIds`, `Count`, `Sources(string)`, `Weeks(string, ObtainFilter)`, `IsObtainable(string,int,ObtainFilter)`, `EarliestWeek(string, ObtainFilter)`).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityModelTests
{
    [Fact]
    public void A_season_is_its_four_weeks()
    {
        WeekMask winter = WeekMask.ForSeason(Season.Winter);
        Assert.False(winter.Contains(12));
        Assert.True(winter.Contains(13));
        Assert.True(winter.Contains(16));
        Assert.Equal(13, winter.Earliest);
    }

    [Fact]
    public void Days_map_to_weeks_across_the_year()
    {
        Assert.Equal(1, WeekMask.WeekOfDay(1));
        Assert.Equal(1, WeekMask.WeekOfDay(7));
        Assert.Equal(2, WeekMask.WeekOfDay(8));
        Assert.Equal(15, WeekMask.WeekOfDay(84 + 15)); // Winter 15
        Assert.Equal(16, WeekMask.WeekOfDay(112));
        Assert.Equal(WeekMask.Of(15), WeekMask.ForDays(84 + 15, 84 + 17)); // Night Market
    }

    [Fact]
    public void A_spring_and_winter_fish_is_absent_in_summer()
    {
        WeekMask m = WeekMask.ForSeasons(new[] { Season.Spring, Season.Winter });
        Assert.True(m.Contains(2));
        Assert.False(m.Contains(6));
        Assert.True(m.Contains(14));
        Assert.Equal("1-4,13-16", m.ToString());
    }

    [Fact]
    public void Shifting_later_drops_weeks_past_the_year()
    {
        Assert.Equal(WeekMask.Range(2, 16), WeekMask.All.ShiftLater(1));
        Assert.Equal(WeekMask.Range(15, 16), WeekMask.FromWeekOnwardOf(13).ShiftLater(2));
        Assert.Equal(WeekMask.Range(5, 16), WeekMask.Of(5).FromWeekOnward());
        Assert.Equal(WeekMask.Range(1, 12), WeekMask.All.Except(WeekMask.ForSeason(Season.Winter)));
    }

    [Fact]
    public void Conditions_compare_by_content_not_by_list_reference()
    {
        var a = ObtainConditions.None with { Requires = new[] { "shop:SeedShop" } };
        var b = ObtainConditions.None with { Requires = new List<string> { "shop:SeedShop" } };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Requires = new[] { "shop:Sandy" } });
    }

    [Fact]
    public void Filters_pick_reliability_and_skip_year_two_and_the_island_by_default()
    {
        var sources = new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = new[]
            {
                new ObtainSource(SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable, ObtainConditions.None, "SeedShop"),
                new ObtainSource(SourceKind.Cart, WeekMask.All, Reliability.Chance, ObtainConditions.None, "Traveler"),
                new ObtainSource(SourceKind.Shop, WeekMask.All, Reliability.Dependable, ObtainConditions.None with { YearTwo = true }, "year 2 row"),
                new ObtainSource(SourceKind.Forage, WeekMask.All, Reliability.Dependable, ObtainConditions.None with { GingerIsland = true }, "island"),
            },
        };
        var model = new ObtainabilityModel(sources);

        Assert.Equal(WeekMask.ForSeason(Season.Spring), model.Weeks("24", ObtainFilter.DependableOnly));
        Assert.Equal(WeekMask.All, model.Weeks("(O)24", ObtainFilter.Any));
        Assert.True(model.IsObtainable("(O)24", 14, ObtainFilter.Any));
        Assert.False(model.IsObtainable("(O)24", 14, ObtainFilter.DependableOnly));
        Assert.Equal(1, model.EarliestWeek("(O)24", ObtainFilter.DependableOnly));
        Assert.Null(model.EarliestWeek("(O)999", ObtainFilter.Any));
        Assert.Empty(model.Sources("(O)999"));
        var cartOnly = new ObtainFilter { Kinds = new[] { SourceKind.Cart } };
        Assert.Equal(WeekMask.All, model.Weeks("(O)24", cartOnly));
    }

    [Fact]
    public void Unresolved_sources_count_by_default_and_can_be_excluded()
    {
        var sources = new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)1"] = new[] { new ObtainSource(SourceKind.Other, WeekMask.All, Reliability.Chance,
                ObtainConditions.None with { Unresolved = true }, "machine output method") },
        };
        var model = new ObtainabilityModel(sources);
        Assert.Equal(WeekMask.All, model.Weeks("(O)1", ObtainFilter.Any));
        Assert.True(model.Weeks("(O)1", ObtainFilter.Any with { IncludeUnresolved = false }).IsEmpty);
    }

    [Fact]
    public void Keys_that_normalize_to_the_same_id_are_merged_not_overwritten()
    {
        var shop = new ObtainSource(SourceKind.Shop, WeekMask.Of(1), Reliability.Dependable, ObtainConditions.None, "a");
        var forage = new ObtainSource(SourceKind.Forage, WeekMask.Of(9), Reliability.Dependable, ObtainConditions.None, "b");
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["24"] = new[] { shop },
            ["(O)24"] = new[] { forage, shop },
        });
        Assert.Equal(2, model.Sources("(O)24").Count);
        Assert.Equal("1,9", model.Weeks("(O)24", ObtainFilter.Any).ToString());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~ObtainabilityModelTests"`
Expected: build FAILS, `The type or namespace name 'Obtainability' does not exist`.

- [ ] **Step 3: Write `WeekMask.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core.Obtainability;

/// <summary>A set of the year's 16 weeks, one bit each (bit 0 is week 1). Weeks are the unit the
/// obtainability model speaks in (Jeff, 2026-09-14): the same unit gates, goals and pacing use.</summary>
public readonly record struct WeekMask(ushort Bits)
{
    public const int FirstWeek = 1;
    public const int LastWeek = Calendar.WeeksPerYear;
    private const ushort AllBits = 0xFFFF;

    public static readonly WeekMask None = new(0);
    public static readonly WeekMask All = new(AllBits);

    public bool IsEmpty => Bits == 0;

    public bool Contains(int week)
        => week >= FirstWeek && week <= LastWeek && (Bits & (1 << (week - 1))) != 0;

    public static WeekMask Of(int week)
        => week < FirstWeek || week > LastWeek ? None : new((ushort)(1 << (week - 1)));

    public static WeekMask Range(int fromWeek, int toWeek)
    {
        ushort bits = 0;
        for (int w = Math.Max(FirstWeek, fromWeek); w <= Math.Min(LastWeek, toWeek); w++)
            bits |= (ushort)(1 << (w - 1));
        return new(bits);
    }

    public static WeekMask FromWeekOnwardOf(int week) => Range(week, LastWeek);

    public static WeekMask ForSeason(Season season)
    {
        int first = (int)season * Calendar.WeeksPerMonth + 1;
        return Range(first, first + Calendar.WeeksPerMonth - 1);
    }

    public static WeekMask ForSeasons(IEnumerable<Season> seasons)
    {
        WeekMask mask = None;
        foreach (Season s in seasons) mask |= ForSeason(s);
        return mask;
    }

    /// <summary>1-based day of the year (Spring 1 = 1, Winter 28 = 112) to its week.</summary>
    public static int WeekOfDay(int dayOfYear) => (dayOfYear - 1) / Calendar.DaysPerWeek + 1;

    public static WeekMask ForDays(int firstDayOfYear, int lastDayOfYear)
        => Range(WeekOfDay(firstDayOfYear), WeekOfDay(lastDayOfYear));

    public int? Earliest
    {
        get
        {
            for (int w = FirstWeek; w <= LastWeek; w++)
                if (Contains(w)) return w;
            return null;
        }
    }

    /// <summary>Every week from the earliest one on: a thing once had stays had (a fish pond, a
    /// learned recipe).</summary>
    public WeekMask FromWeekOnward() => Earliest is int e ? FromWeekOnwardOf(e) : None;

    /// <summary>Moves every week later by <paramref name="weeks"/>; weeks pushed past 16 are gone,
    /// because a loop ends at Winter 28.</summary>
    public WeekMask ShiftLater(int weeks) => weeks <= 0 ? this : new((ushort)((Bits << weeks) & AllBits));

    public WeekMask Except(WeekMask other) => new((ushort)(Bits & ~other.Bits));

    public static WeekMask operator |(WeekMask a, WeekMask b) => new((ushort)(a.Bits | b.Bits));
    public static WeekMask operator &(WeekMask a, WeekMask b) => new((ushort)(a.Bits & b.Bits));

    /// <summary>Compact ranges, e.g. "1-4,13-16". Empty is "none".</summary>
    public override string ToString()
    {
        if (IsEmpty) return "none";
        var sb = new StringBuilder();
        int w = FirstWeek;
        while (w <= LastWeek)
        {
            if (!Contains(w)) { w++; continue; }
            int start = w;
            while (w + 1 <= LastWeek && Contains(w + 1)) w++;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(start == w ? $"{start}" : $"{start}-{w}");
            w++;
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Write `ObtainTypes.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How an item is obtained. <see cref="Other"/> is a source the model knows exists but cannot
/// read (an unsupported item query, a machine output method): recorded so nothing silently vanishes.</summary>
public enum SourceKind
{
    Forage, ArtifactSpot, Fish, CrabPot, Crop, GreenhouseCrop, FruitTree, Shop, Cart, Machine,
    Cooking, Crafting, Animal, FishPond, MineNode, MonsterDrop, Geode, Tapper, Festival,
    NightMarket, Trash, GarbageCan, FishingTreasure, Other,
}

/// <summary>Dependable sources yield on purpose (a spawn, a shop row, a machine); chance sources
/// depend on luck (the cart, drops, geodes, treasure). The model tags, the consuming rule decides
/// (Jeff, 2026-09-14, option A).</summary>
public enum Reliability { Dependable, Chance }

/// <summary>What a source needs. <see cref="Requires"/> is informational ("location:Desert",
/// "machine:(BC)12", "mail:ccPantry"); <see cref="YearTwo"/>, <see cref="GingerIsland"/> and
/// <see cref="Unresolved"/> change what filters count. Equality compares <see cref="Requires"/> by
/// content, so the same source built twice is one source.</summary>
public sealed record ObtainConditions
{
    public static readonly ObtainConditions None = new();

    public string? Skill { get; init; }
    public int SkillLevel { get; init; }
    public IReadOnlyList<string> Requires { get; init; } = Array.Empty<string>();
    public bool RainOnly { get; init; }
    public bool FewDays { get; init; }
    public bool YearTwo { get; init; }
    public bool GingerIsland { get; init; }
    public int CatchLimit { get; init; }
    /// <summary>A condition or query the model could not read; the weeks are a guess, not a fact.</summary>
    public bool Unresolved { get; init; }

    public bool Equals(ObtainConditions? other)
        => other is not null
           && Skill == other.Skill && SkillLevel == other.SkillLevel
           && RainOnly == other.RainOnly && FewDays == other.FewDays && YearTwo == other.YearTwo
           && GingerIsland == other.GingerIsland && CatchLimit == other.CatchLimit && Unresolved == other.Unresolved
           && Requires.SequenceEqual(other.Requires);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Skill);
        hash.Add(SkillLevel);
        hash.Add(RainOnly);
        hash.Add(FewDays);
        hash.Add(YearTwo);
        hash.Add(GingerIsland);
        hash.Add(CatchLimit);
        hash.Add(Unresolved);
        foreach (string r in Requires) hash.Add(r);
        return hash.ToHashCode();
    }
}

/// <summary>One way to obtain an item: its kind, the weeks it works, how dependable it is, what it
/// needs, and a short origin note for the debug output.</summary>
public sealed record ObtainSource(
    SourceKind Kind, WeekMask Weeks, Reliability Reliability, ObtainConditions Conditions, string Detail);

/// <summary>Emits one dependable source for the dependable weeks and one chance source for weeks
/// reachable only by luck, so reliability survives derivation.</summary>
public static class SourcePair
{
    public static IEnumerable<ObtainSource> Of(
        SourceKind kind, WeekMask dependable, WeekMask any, ObtainConditions conditions, string detail)
    {
        if (!dependable.IsEmpty)
            yield return new ObtainSource(kind, dependable, Reliability.Dependable, conditions, detail);
        WeekMask luckOnly = any.Except(dependable);
        if (!luckOnly.IsEmpty)
            yield return new ObtainSource(kind, luckOnly, Reliability.Chance, conditions, detail);
    }
}
```

- [ ] **Step 5: Write `ObtainabilityModel.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Which sources a question counts. Null collections mean "all".</summary>
public sealed record ObtainFilter
{
    public static readonly ObtainFilter Any = new();
    public static readonly ObtainFilter DependableOnly = new() { Reliabilities = new[] { Reliability.Dependable } };

    public IReadOnlyCollection<Reliability>? Reliabilities { get; init; }
    public IReadOnlyCollection<SourceKind>? Kinds { get; init; }
    public bool IncludeYearTwo { get; init; }
    public bool IncludeGingerIsland { get; init; }
    public bool IncludeUnresolved { get; init; } = true;

    public bool Accepts(ObtainSource source)
        => (Reliabilities == null || Reliabilities.Contains(source.Reliability))
           && (Kinds == null || Kinds.Contains(source.Kind))
           && (IncludeYearTwo || !source.Conditions.YearTwo)
           && (IncludeGingerIsland || !source.Conditions.GingerIsland)
           && (IncludeUnresolved || !source.Conditions.Unresolved);
}

/// <summary>Every year-1 way to obtain every item, by week (spec 2026-09-14-item-obtainability).
/// Built at runtime from the installed game data; nothing reads it for gameplay in phase 1.</summary>
public sealed class ObtainabilityModel
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> _sources;

    public ObtainabilityModel(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        _sources = sources
            .GroupBy(kv => BundleParsing.NormalizeItemId(kv.Key), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ObtainSource>)g.SelectMany(kv => kv.Value).Distinct().ToList(),
                StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> ItemIds => _sources.Keys.ToList();

    public int Count => _sources.Count;

    public IReadOnlyList<ObtainSource> Sources(string itemId)
        => _sources.TryGetValue(BundleParsing.NormalizeItemId(itemId), out IReadOnlyList<ObtainSource>? list)
            ? list
            : Array.Empty<ObtainSource>();

    public WeekMask Weeks(string itemId, ObtainFilter filter)
    {
        WeekMask mask = WeekMask.None;
        foreach (ObtainSource source in Sources(itemId))
            if (filter.Accepts(source)) mask |= source.Weeks;
        return mask;
    }

    public bool IsObtainable(string itemId, int week, ObtainFilter filter) => Weeks(itemId, filter).Contains(week);

    public int? EarliestWeek(string itemId, ObtainFilter filter) => Weeks(itemId, filter).Earliest;
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~ObtainabilityModelTests"`
Expected: PASS, 8 tests.

- [ ] **Step 7: Run the whole suite, commit, push**

Run the full test command; expected: all pass (2166 before this plan plus the new tests).

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityModelTests.cs
git commit -m "obtainability: week sets, source types and the model's queries"
git push origin story
```

---

### Task 2: Reading seasons, days, years, weather and festivals out of condition strings

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (only `FestivalDates` in this task; later tasks add the rest)
- Create: `src/TheLongestYear.Core/Obtainability/ConditionSeasons.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityConditionTests.cs`

**Interfaces:**
- Consumes: `WeekMask`, `ObtainConditions` (Task 1).
- Produces: `record FestivalDates(string Id, Season Season, int StartDay, int EndDay)` with `WeekMask Weeks`; `record ConditionReading(WeekMask Weeks, bool YearTwo, bool FewDays, bool Chance, bool RainOnly, bool Unresolved, bool IslandHint, IReadOnlyList<string> Other)`; `static ConditionReading ConditionSeasons.Read(string? condition, IReadOnlyDictionary<string, FestivalDates> festivals)`; `static ObtainConditions ConditionSeasons.Apply(ObtainConditions conditions, ConditionReading reading)` which folds a reading's flags and notes into a source's conditions.

A clause is one of three things: a **temporal** clause the reader understands (it narrows the weeks or sets a flag), a **prerequisite** clause the model does not judge (friendship, mail, a building; kept as a note, weeks unchanged, not unresolved), or an **unknown** clause (kept as a note, weeks unchanged, `Unresolved` set so a consumer can refuse the guess). A clause that can never pass in year 1 (`FALSE`, `!TRUE`, `!YEAR 1`, `YEAR 1` negated forms) empties the weeks.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityConditionTests
{
    private static readonly Dictionary<string, FestivalDates> Festivals = new()
    {
        ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17),
    };

    private static ConditionReading Read(string? condition) => ConditionSeasons.Read(condition, Festivals);

    [Fact]
    public void No_condition_is_every_week()
    {
        ConditionReading r = Read(null);
        Assert.Equal(WeekMask.All, r.Weeks);
        Assert.False(r.YearTwo || r.FewDays || r.Chance || r.RainOnly || r.Unresolved || r.IslandHint);
        Assert.Empty(r.Other);
    }

    [Theory]
    [InlineData("SEASON Spring", "1-4")]
    [InlineData("SEASON spring summer", "1-8")]
    [InlineData("LOCATION_SEASON Here fall", "9-12")]
    [InlineData("!SEASON Winter", "1-12")]
    [InlineData("SEASON Spring Fall, !SEASON Fall", "1-4")]
    [InlineData("DAYS_PLAYED 29", "5-16")]
    [InlineData("DAYS_PLAYED 1 28", "1-4")]
    [InlineData("DAY_OF_MONTH 1 2", "1,5,9,13")]
    [InlineData("DAY_OF_MONTH even", "1-16")]
    public void Temporal_clauses_narrow_the_weeks(string condition, string expected)
        => Assert.Equal(expected, Read(condition).Weeks.ToString());

    [Theory]
    [InlineData("FALSE")]
    [InlineData("!TRUE")]
    [InlineData("!YEAR 1")]
    public void Clauses_that_never_pass_in_year_one_empty_the_weeks(string condition)
        => Assert.True(Read(condition).Weeks.IsEmpty);

    [Fact]
    public void Year_two_is_flagged_not_counted()
    {
        Assert.True(Read("YEAR 2").YearTwo);
        Assert.False(Read("YEAR 1").YearTwo);
        Assert.False(Read("!YEAR 2").YearTwo);
        Assert.Equal(WeekMask.All, Read("!YEAR 2").Weeks);
        Assert.False(Read("YEAR 1 1").YearTwo);
        Assert.Equal(WeekMask.All, Read("YEAR 1 1").Weeks);
    }

    [Fact]
    public void Festival_and_day_clauses_are_few_days()
    {
        Assert.Equal(WeekMask.Of(15), Read("IS_PASSIVE_FESTIVAL_OPEN NightMarket").Weeks);
        Assert.True(Read("IS_PASSIVE_FESTIVAL_TODAY NightMarket").FewDays);
        Assert.Equal(WeekMask.Of(6), Read("SEASON_DAY Summer 11").Weeks);
        Assert.True(Read("DAY_OF_WEEK Friday").FewDays);
        Assert.Equal(WeekMask.All, Read("DAY_OF_WEEK Friday").Weeks);
    }

    [Fact]
    public void Wet_weather_is_rain_only_and_random_is_chance()
    {
        Assert.True(Read("WEATHER Here Rain Storm").RainOnly);
        Assert.False(Read("WEATHER Here Sun").RainOnly);
        Assert.True(Read("RANDOM 0.1").Chance);
    }

    [Fact]
    public void Prerequisites_are_notes_and_unknown_clauses_are_unresolved()
    {
        ConditionReading friend = Read("PLAYER_HEARTS Current Abigail 4");
        Assert.Equal(new[] { "PLAYER_HEARTS Current Abigail 4" }, friend.Other);
        Assert.False(friend.Unresolved);
        ConditionReading modded = Read("SOME_MOD_QUERY 5");
        Assert.True(modded.Unresolved);
        Assert.True(Read("ITEMX 3").Unresolved);          // a known prefix fragment is not a known query
        Assert.True(Read("!DAY_OF_MONTH 5").Unresolved);  // negated day lists are not narrowed, so they are flagged
        Assert.False(Read("ITEM_CONTEXT_TAG Target fish").Unresolved);
        Assert.Equal(WeekMask.All, modded.Weeks);
        Assert.True(Read("ANY \"SEASON Spring\" \"SEASON Fall\"").Unresolved);
        Assert.True(Read("PLAYER_HAS_MAIL Current Island_Resort").IslandHint);
    }

    [Fact]
    public void Apply_folds_flags_and_notes_into_conditions()
    {
        ObtainConditions c = ConditionSeasons.Apply(
            ObtainConditions.None with { Requires = new[] { "shop:Sandy" } },
            Read("YEAR 2, WEATHER Here Rain, SOME_MOD_QUERY"));
        Assert.True(c.YearTwo);
        Assert.True(c.RainOnly);
        Assert.True(c.Unresolved);
        Assert.Equal(new[] { "shop:Sandy", "SOME_MOD_QUERY" }, c.Requires);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityConditionTests"`
Expected: build FAILS, `ConditionSeasons` not found.

- [ ] **Step 3: Write `ObtainabilityInputs.cs` (first record only)**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

// ---- Plain input records. The glue (Loop/GameObtainabilityData) fills these from the live game
// ---- data assets at save load, so every rule here is testable without the game. Item ids are
// ---- QUALIFIED ("(O)24") except category references ("-75") and item queries, which travel as
// ---- their raw text and are expanded by ItemQueries (Task 4).

/// <summary>A festival's dates: passive festivals (Data/PassiveFestivals: Night Market, Squid Fest,
/// Trout Derby, Desert Festival) and day festivals (Data/Festivals/FestivalDates: Egg Festival...).</summary>
public sealed record FestivalDates(string Id, Season Season, int StartDay, int EndDay)
{
    public WeekMask Weeks => WeekMask.ForDays(
        Calendar.DayOfYear((int)Season, StartDay), Calendar.DayOfYear((int)Season, EndDay));
}
```

- [ ] **Step 4: Write `ConditionSeasons.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>What a game-state-query string says about WHEN. See Task 2's intro in the plan for the
/// three clause classes: temporal (narrows weeks or sets a flag), prerequisite (a note), unknown (a note
/// plus <see cref="Unresolved"/>).</summary>
public sealed record ConditionReading(
    WeekMask Weeks, bool YearTwo, bool FewDays, bool Chance, bool RainOnly, bool Unresolved, bool IslandHint,
    IReadOnlyList<string> Other);

public static class ConditionSeasons
{
    private const int YearOne = 1;
    private const string IslandMarker = "Island";

    /// <summary>Vanilla queries (GameStateQuery.cs) that gate on the player's progress, the item, the
    /// location or the clock rather than the calendar. The model does not judge them; they become notes.
    /// Exact names, so an unknown key that merely starts the same way stays unresolved.</summary>
    private static readonly HashSet<string> PrerequisiteQueries = new(StringComparer.Ordinal)
    {
        "IS_COMMUNITY_CENTER_COMPLETE", "IS_JOJA_MART_COMPLETE", "IS_GREEN_RAIN_DAY", "IS_HOST", "IS_CUSTOM_FARM_TYPE",
        "IS_EVENT", "IS_ISLAND_NORTH_BRIDGE_FIXED", "IS_VISITING_ISLAND", "IS_MULTIPLAYER", "IS_LOST_BOOK_FOUND",
        "MINE_LOWEST_LEVEL_REACHED", "WORLD_STATE_FIELD", "WORLD_STATE_ID", "LOCATION_ACCESSIBLE",
        "CAN_BUILD_CABIN", "CAN_BUILD_FOR_CABINS", "FARM_CAVE", "FARM_NAME", "FARM_TYPE", "HAS_TARGET_LOCATION",
        "LOCATION_CONTEXT", "LOCATION_HAS_CUSTOM_FIELD", "LOCATION_IS_INDOORS", "LOCATION_IS_OUTDOORS",
        "LOCATION_IS_MINES", "LOCATION_IS_SKULL_CAVE", "LOCATION_NAME", "LOCATION_UNIQUE_NAME", "TIME",
        "ITEM_CATEGORY", "ITEM_CONTEXT_TAG", "ITEM_EDIBILITY", "ITEM_HAS_EXPLICIT_OBJECT_CATEGORY", "ITEM_ID",
        "ITEM_ID_PREFIX", "ITEM_NUMERIC_ID", "ITEM_OBJECT_TYPE", "ITEM_PRICE", "ITEM_QUALITY", "ITEM_STACK", "ITEM_TYPE",
    };

    /// <summary>Prefixes of whole query families that are all prerequisites (PLAYER_HEARTS, BUILDINGS_CONSTRUCTED...).</summary>
    private static readonly string[] PrerequisitePrefixes = { "PLAYER_", "BUILDINGS_" };

    private static readonly string[] WetWeather = { "Rain", "Storm", "GreenRain" };

    public static ConditionReading Read(string? condition, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        WeekMask weeks = WeekMask.All;
        bool yearTwo = false, fewDays = false, chance = false, rainOnly = false, unresolved = false, island = false;
        var other = new List<string>();
        if (string.IsNullOrWhiteSpace(condition))
            return new ConditionReading(weeks, false, false, false, false, false, false, other);

        foreach (string rawClause in SplitClauses(condition))
        {
            string clause = rawClause.Trim();
            if (clause.Length == 0) continue;
            if (clause.Contains(IslandMarker, StringComparison.Ordinal)) island = true;
            bool negated = clause.StartsWith("!", StringComparison.Ordinal);
            string[] tokens = (negated ? clause.Substring(1) : clause).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string key = tokens[0].ToUpperInvariant();

            WeekMask? clauseWeeks = null;
            switch (key)
            {
                case "TRUE":
                    if (negated) weeks = WeekMask.None;
                    break;
                case "FALSE":
                    if (!negated) weeks = WeekMask.None;
                    break;
                case "SEASON":
                    clauseWeeks = SeasonsIn(tokens.Skip(1));
                    break;
                case "LOCATION_SEASON":
                    clauseWeeks = SeasonsIn(tokens.Skip(2));
                    break;
                case "SEASON_DAY":
                    clauseWeeks = SeasonDays(tokens);
                    fewDays = true;
                    break;
                case "DAY_OF_MONTH":
                    fewDays = true;
                    if (negated) { other.Add(clause); unresolved = true; break; }
                    // "even" / "odd" (GameStateQuery.cs 282-289): every week still has such days.
                    if (tokens.Skip(1).Any(t => t.Equals("even", StringComparison.OrdinalIgnoreCase) || t.Equals("odd", StringComparison.OrdinalIgnoreCase)))
                        break;
                    clauseWeeks = DaysOfMonth(tokens.Skip(1));
                    break;
                case "DAY_OF_WEEK":
                    fewDays = true;
                    break;
                case "DAYS_PLAYED":
                    // "DAYS_PLAYED min [max]" (GameStateQuery.cs 310-322); in a loop, days played is the day of the year.
                    if (negated) { other.Add(clause); unresolved = true; break; }
                    if (tokens.Length > 1 && int.TryParse(tokens[1], out int minDays))
                    {
                        int maxDays = tokens.Length > 2 && int.TryParse(tokens[2], out int m) ? m : Calendar.DaysPerYear;
                        weeks &= WeekMask.Range(
                            WeekMask.WeekOfDay(Math.Max(1, minDays)),
                            WeekMask.WeekOfDay(Math.Clamp(maxDays, 1, Calendar.DaysPerYear)));
                    }
                    break;
                case "IS_PASSIVE_FESTIVAL_OPEN":
                case "IS_PASSIVE_FESTIVAL_TODAY":
                    if (tokens.Length > 1 && festivals.TryGetValue(tokens[1], out FestivalDates? festival))
                    {
                        clauseWeeks = festival.Weeks;
                        fewDays = true;
                    }
                    else { other.Add(clause); unresolved = true; }
                    break;
                case "YEAR":
                    // "YEAR min [max]" (GameStateQuery.cs 392-404).
                    if (tokens.Length > 1 && int.TryParse(tokens[1], out int minYear))
                    {
                        int maxYear = tokens.Length > 2 && int.TryParse(tokens[2], out int my) ? my : int.MaxValue;
                        bool yearOnePasses = minYear <= YearOne && maxYear >= YearOne;
                        if (negated ? yearOnePasses : !yearOnePasses)
                        {
                            if (negated || maxYear < YearOne) weeks = WeekMask.None; else yearTwo = true;
                        }
                    }
                    break;
                case "WEATHER":
                    if (!negated && tokens.Length > 2 && tokens.Skip(2).All(w => WetWeather.Contains(w, StringComparer.OrdinalIgnoreCase)))
                        rainOnly = true;
                    else other.Add(clause);
                    break;
                case "RANDOM":
                case "SYNCED_RANDOM":
                case "SYNCED_CHOICE":
                case "SYNCED_SUMMER_RAIN_RANDOM":
                case "SYNCED_DAY_RANDOM":
                    chance = true;
                    break;
                default:
                    other.Add(clause);
                    if (!PrerequisiteQueries.Contains(key) && !PrerequisitePrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                        unresolved = true;
                    break;
            }

            if (clauseWeeks is WeekMask w)
                weeks &= negated ? WeekMask.All.Except(w) : w;
        }
        return new ConditionReading(weeks, yearTwo, fewDays, chance, rainOnly, unresolved, island, other);
    }

    public static ObtainConditions Apply(ObtainConditions conditions, ConditionReading reading)
        => conditions with
        {
            Requires = conditions.Requires.Concat(reading.Other).ToList(),
            YearTwo = conditions.YearTwo || reading.YearTwo,
            FewDays = conditions.FewDays || reading.FewDays,
            RainOnly = conditions.RainOnly || reading.RainOnly,
            Unresolved = conditions.Unresolved || reading.Unresolved,
            GingerIsland = conditions.GingerIsland || reading.IslandHint,
        };

    /// <summary>Splits on commas outside double quotes (ANY "a, b" "c" keeps its arguments together).</summary>
    private static IEnumerable<string> SplitClauses(string condition)
    {
        int start = 0;
        bool quoted = false;
        for (int i = 0; i < condition.Length; i++)
        {
            if (condition[i] == '"') quoted = !quoted;
            else if (condition[i] == ',' && !quoted)
            {
                yield return condition.Substring(start, i - start);
                start = i + 1;
            }
        }
        yield return condition.Substring(start);
    }

    private static WeekMask SeasonsIn(IEnumerable<string> tokens)
    {
        WeekMask mask = WeekMask.None;
        foreach (string token in tokens)
            if (Enum.TryParse(token, ignoreCase: true, out Season season))
                mask |= WeekMask.ForSeason(season);
        return mask;
    }

    /// <summary>"SEASON_DAY season day [season day...]": each pair is one day of one season.</summary>
    private static WeekMask SeasonDays(string[] tokens)
    {
        WeekMask mask = WeekMask.None;
        for (int i = 1; i + 1 < tokens.Length; i += 2)
            if (Enum.TryParse(tokens[i], ignoreCase: true, out Season season)
                && int.TryParse(tokens[i + 1], out int day) && day is >= 1 and <= Calendar.DaysPerMonth)
                mask |= WeekMask.Of(WeekMask.WeekOfDay(Calendar.DayOfYear((int)season, day)));
        return mask;
    }

    /// <summary>"DAY_OF_MONTH day [day...]": those days in every season.</summary>
    private static WeekMask DaysOfMonth(IEnumerable<string> tokens)
    {
        WeekMask mask = WeekMask.None;
        foreach (string token in tokens)
            if (int.TryParse(token, out int day) && day is >= 1 and <= Calendar.DaysPerMonth)
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    mask |= WeekMask.Of(WeekMask.WeekOfDay(Calendar.DayOfYear(season, day)));
        return mask;
    }
}
```

Note: `"DAY_OF_MONTH 1 2"` covers days 1 and 2 of every season, all in each season's first week, so the weeks are `1,5,9,13`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityConditionTests"`
Expected: PASS (9 theory cases, 3 theory cases, 6 facts: 18 tests).

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityConditionTests.cs
git commit -m "obtainability: read temporal, prerequisite and unknown clauses from conditions"
git push origin story
```

---

### Task 3: Item queries and spawn sources (forage, fish, crab pot, artifact spots, garbage cans, fishing trash)

Game data lists an item either as an id or as an **item query** ("RANDOM_ITEMS (O) 2 789",
"FLAVORED_ITEM Wine 454", "SECRET_NOTE_OR_ITEM (O)390"). `ItemQueries` turns both into concrete item
ids. A query it cannot read is never turned into a fake id and never silently dropped: the source is
emitted under an id that starts with `ItemQueries.UnresolvedPrefix`, and the builder (Task 8) moves those
into a diagnostics list the comparison report prints.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/ItemQueries.cs`
- Create: `src/TheLongestYear.Core/Obtainability/SpawnSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityQueryTests.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilitySpawnTests.cs`

**Interfaces:**
- Consumes: Tasks 1-2 (`WeekMask`, `ObtainSource`, `ObtainConditions`, `FestivalDates`, `ConditionSeasons.Read/Apply`).
- Produces:
  - `record ObjInfo(string QualifiedId, string Name, int Category, int Price, IReadOnlyList<string> ContextTags, bool ExcludeFromRandomSale)`
  - `record LocationSpawn(string Location, string ItemId, Season? Season, string? Condition, double Chance, int CatchLimit, bool RequireMagicBait, int MinFishingLevel, bool IsRandom = false)`
  - `record FishRow(string ItemId, bool IsTrap, string Weather, int MinFishingLevel, string TimeSpans)`
  - `record ArtifactSpotRow(string Location, string ItemId, string? Condition, double Chance)`
  - `record GarbageRow(string CanId, string ItemId, string? Condition)`
  - `record QueryResult(IReadOnlyList<string> ItemIds, bool Chance, bool Unresolved, string Note)`
  - `static class ItemQueries` with `const string UnresolvedPrefix = "?"`, `bool IsQuery(string)`, `QueryResult Resolve(string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects)`, `IEnumerable<string> ExpandRandomItems(string query, IReadOnlyDictionary<string, ObjInfo>)`, `string? FlavoredBaseId(string preserveType)`, `IEnumerable<(string ItemId, ObtainSource Source)> Emit(string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects, ObtainSource template)`
  - `static class SpawnSources` with `Forage(IEnumerable<LocationSpawn>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)`, `LocationFish(IEnumerable<LocationSpawn>, IReadOnlyDictionary<string, FishRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)`, `CrabPot(IEnumerable<FishRow>)`, `ArtifactSpots(IEnumerable<ArtifactSpotRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)`, `GarbageCans(IEnumerable<GarbageRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)`, `FishingTrash()`, `IsIslandLocation(string)`; all return `IEnumerable<(string ItemId, ObtainSource Source)>` except `IsIslandLocation`.

- [ ] **Step 1: Write the failing query tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityQueryTests
{
    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)775"] = new ObjInfo("(O)775", "Glacierfish", -4, 1000, new string[0], true),
        ["(O)800"] = new ObjInfo("(O)800", "Blobfish", -4, 900, new string[0], false),
        ["(O)Moss"] = new ObjInfo("(O)Moss", "Moss", -81, 5, new string[0], false),
    };

    [Fact]
    public void A_plain_id_is_normalized_and_dependable()
    {
        QueryResult r = ItemQueries.Resolve("24", Objects);
        Assert.Equal(new[] { "(O)24" }, r.ItemIds);
        Assert.False(r.Chance || r.Unresolved);
    }

    [Fact]
    public void Random_items_expand_by_range_and_flags_as_chance()
    {
        QueryResult r = ItemQueries.Resolve("RANDOM_ITEMS (O) 2 789 @isRandomSale @requirePrice", Objects);
        Assert.Equal(new[] { "(O)24" }, r.ItemIds);   // 775 excluded from sale, 800 out of range, Moss not numeric
        Assert.True(r.Chance);
    }

    [Fact]
    public void Flavored_items_resolve_to_their_base_object()
    {
        Assert.Equal(new[] { "(O)348" }, ItemQueries.Resolve("FLAVORED_ITEM Wine DROP_IN_ID", Objects).ItemIds);
        Assert.Equal(new[] { "(O)DriedMushrooms" }, ItemQueries.Resolve("FLAVORED_ITEM DriedMushroom 404", Objects).ItemIds);
        Assert.Equal("(O)447", ItemQueries.FlavoredBaseId("AgedRoe"));
        Assert.Null(ItemQueries.FlavoredBaseId("Mystery"));
    }

    [Fact]
    public void Book_and_note_queries_yield_their_fallback_and_say_so()
    {
        QueryResult book = ItemQueries.Resolve("LOST_BOOK_OR_ITEM (O)390", Objects);
        Assert.Equal(new[] { "(O)102", "(O)390" }, book.ItemIds);
        QueryResult note = ItemQueries.Resolve("SECRET_NOTE_OR_ITEM (O)390", Objects);
        Assert.Contains("(O)390", note.ItemIds);
        Assert.Contains("secret note", note.Note);
    }

    [Fact]
    public void Any_text_with_arguments_is_a_query_even_from_a_mod()
    {
        Assert.True(ItemQueries.IsQuery("MYMOD_SPECIAL_ITEM 3 4"));
        Assert.False(ItemQueries.IsQuery("(O)24"));
        Assert.False(ItemQueries.IsQuery("DeluxeBait"));
        QueryResult r = ItemQueries.Resolve("MYMOD_SPECIAL_ITEM 3 4", Objects);
        Assert.True(r.Unresolved);
        Assert.Empty(r.ItemIds);
    }

    [Fact]
    public void Unknown_queries_are_unresolved_and_emitted_under_the_marker()
    {
        QueryResult r = ItemQueries.Resolve("LOCATION_FISH Beach BOBBER_X", Objects);
        Assert.True(r.Unresolved);
        Assert.Empty(r.ItemIds);
        var template = new ObtainSource(SourceKind.Forage, WeekMask.All, Reliability.Dependable, ObtainConditions.None, "Forage at Beach");
        var emitted = ItemQueries.Emit("LOCATION_FISH Beach BOBBER_X", Objects, template).Single();
        Assert.StartsWith(ItemQueries.UnresolvedPrefix, emitted.ItemId);
        Assert.Equal(SourceKind.Other, emitted.Source.Kind);
        Assert.True(emitted.Source.Conditions.Unresolved);
    }
}
```

- [ ] **Step 2: Write the failing spawn tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilitySpawnTests
{
    private static readonly Dictionary<string, FestivalDates> Festivals = new()
    {
        ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17),
    };
    private static readonly Dictionary<string, ObjInfo> Objects = new();

    [Fact]
    public void Forage_takes_its_season_and_its_location()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)16", Season.Spring, null, 0.5, 0, false, 0) };
        var (id, source) = SpawnSources.Forage(rows, Objects, Festivals).Single();
        Assert.Equal("(O)16", id);
        Assert.Equal(SourceKind.Forage, source.Kind);
        Assert.Equal(WeekMask.ForSeason(Season.Spring), source.Weeks);
        Assert.Equal(Reliability.Dependable, source.Reliability);
        Assert.Contains("location:Forest", source.Conditions.Requires);
    }

    [Fact]
    public void A_condition_season_narrows_a_seasonless_row_and_the_island_is_flagged()
    {
        var rows = new[]
        {
            new LocationSpawn("Beach", "(O)392", null, "SEASON Winter", 1.0, 0, false, 0),
            new LocationSpawn("IslandWest", "(O)829", null, null, 1.0, 0, false, 0),
            new LocationSpawn("Town", "(O)20", null, "FALSE", 1.0, 0, false, 0),
        };
        var sources = SpawnSources.Forage(rows, Objects, Festivals).ToList();
        Assert.Equal(2, sources.Count);                                  // FALSE row yields nothing
        Assert.Equal(WeekMask.ForSeason(Season.Winter), sources[0].Source.Weeks);
        Assert.True(sources[1].Source.Conditions.GingerIsland);
    }

    [Fact]
    public void Festival_maps_only_open_on_their_days()
    {
        var rows = new[] { new LocationSpawn("Submarine", "(O)798", null, null, 1.0, 0, false, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var source = SpawnSources.LocationFish(rows, fishRows, Objects, Festivals).Single().Source;
        Assert.Equal(WeekMask.Of(15), source.Weeks);
        Assert.True(source.Conditions.FewDays);
    }

    [Fact]
    public void A_random_alternative_is_chance()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)16", Season.Spring, null, 1.0, 0, false, 0, IsRandom: true) };
        Assert.Equal(Reliability.Chance, SpawnSources.Forage(rows, Objects, Festivals).Single().Source.Reliability);
    }

    [Fact]
    public void A_fish_listed_through_a_query_keeps_its_fish_data()
    {
        var objects = new Dictionary<string, ObjInfo> { ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new string[0], false) };
        var rows = new[] { new LocationSpawn("Mountain", "RANDOM_ITEMS (O) 142 142", Season.Fall, null, 1.0, 0, false, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)142"] = new FishRow("(O)142", false, "rainy", 3, "600 2600") };
        var (id, source) = SpawnSources.LocationFish(rows, fishRows, objects, Festivals).Single();
        Assert.Equal("(O)142", id);
        Assert.Equal(3, source.Conditions.SkillLevel);
        Assert.True(source.Conditions.RainOnly);
        Assert.Equal(Reliability.Chance, source.Reliability);
    }

    [Fact]
    public void Fish_carry_the_higher_level_weather_time_and_catch_limit()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)775", Season.Winter, null, 1.0, 1, true, 8) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)775"] = new FishRow("(O)775", false, "rainy", 6, "600 1200 1800 2000") };
        var source = SpawnSources.LocationFish(rows, fishRows, Objects, Festivals).Single().Source;
        Assert.Equal(SourceKind.Fish, source.Kind);
        Assert.Equal("Fishing", source.Conditions.Skill);
        Assert.Equal(8, source.Conditions.SkillLevel);
        Assert.True(source.Conditions.RainOnly);
        Assert.Equal(1, source.Conditions.CatchLimit);
        Assert.Contains("item:(O)908 Magic Bait", source.Conditions.Requires);
        Assert.Contains("time 600-1200 1800-2000", source.Detail);
    }

    [Fact]
    public void Trap_fish_come_from_crab_pots_all_year()
    {
        var rows = new[] { new FishRow("(O)717", true, "", 0, ""), new FishRow("(O)142", false, "sunny", 0, "600 2600") };
        var (id, source) = SpawnSources.CrabPot(rows).Single();
        Assert.Equal("(O)717", id);
        Assert.Equal(SourceKind.CrabPot, source.Kind);
        Assert.Equal(WeekMask.All, source.Weeks);
        Assert.Contains("crafting:Crab Pot", source.Conditions.Requires);
    }

    [Fact]
    public void Luck_sources_are_chance()
    {
        var spots = SpawnSources.ArtifactSpots(new[] { new ArtifactSpotRow("Town", "(O)107", null, 0.05) }, Objects, Festivals).Single().Source;
        var cans = SpawnSources.GarbageCans(new[] { new GarbageRow("JoshHouse", "(O)168", null) }, Objects, Festivals).Single().Source;
        Assert.Equal(Reliability.Chance, spots.Reliability);
        Assert.Equal(SourceKind.GarbageCan, cans.Kind);
        Assert.Equal(new[] { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" },
            SpawnSources.FishingTrash().Select(t => t.ItemId).ToArray());
    }
}
```

- [ ] **Step 3: Run both to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityQueryTests|FullyQualifiedName~ObtainabilitySpawnTests"`
Expected: build FAILS, `ObjInfo` / `ItemQueries` not found.

- [ ] **Step 4: Append the records to `ObtainabilityInputs.cs`**

```csharp

/// <summary>Data/Objects essentials. <see cref="ContextTags"/> are the item's BASE tags as the game
/// computes them (ItemContextTagManager.GetBaseContextTags), including generated ones such as
/// "category_fruits" and "id_o_24", which machine and pond rules match on.</summary>
public sealed record ObjInfo(
    string QualifiedId, string Name, int Category, int Price, IReadOnlyList<string> ContextTags, bool ExcludeFromRandomSale);

/// <summary>One Data/Locations Forage or Fish row. <see cref="ItemId"/> may be an item query.
/// <see cref="Season"/> null means any season unless <see cref="Condition"/> names one.
/// <see cref="MinFishingLevel"/> is SpawnFishData.MinFishingLevel (0 for forage). <see cref="IsRandom"/>
/// marks one entry of a RandomItemId list: the game picks one of them (ItemQueryResolver.cs 804-817).</summary>
public sealed record LocationSpawn(
    string Location, string ItemId, Season? Season, string? Condition, double Chance, int CatchLimit,
    bool RequireMagicBait, int MinFishingLevel, bool IsRandom = false);

/// <summary>One Data/Fish row, reduced: field 1 difficulty or "trap", field 5 time spans
/// ("600 1200 1800 2000"), field 7 weather ("sunny", "rainy", "both"), field 12 minimum level.</summary>
public sealed record FishRow(string ItemId, bool IsTrap, string Weather, int MinFishingLevel, string TimeSpans);

public sealed record ArtifactSpotRow(string Location, string ItemId, string? Condition, double Chance);

/// <summary>One Data/GarbageCans item (from a can's Items, or BeforeAll/AfterAll with CanId "*").</summary>
public sealed record GarbageRow(string CanId, string ItemId, string? Condition);
```

- [ ] **Step 5: Write `ItemQueries.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

public sealed record QueryResult(IReadOnlyList<string> ItemIds, bool Chance, bool Unresolved, string Note);

/// <summary>Turns an item id or an item query (ItemQueryResolver.cs) into concrete item ids.</summary>
public static class ItemQueries
{
    public const string UnresolvedPrefix = "?";
    private const string RandomItems = "RANDOM_ITEMS";
    private const string FlavoredItem = "FLAVORED_ITEM";
    private const string LostBookOrItem = "LOST_BOOK_OR_ITEM";
    private const string SecretNoteOrItem = "SECRET_NOTE_OR_ITEM";
    private const string RandomSaleFlag = "@isRandomSale";
    private const string RequirePriceFlag = "@requirePrice";
    private const string LostBookId = "(O)102";

    /// <summary>Every query key ItemQueryResolver defines (ItemQueryResolver.cs 29-573).</summary>
    private static readonly HashSet<string> QueryKeys = new(StringComparer.Ordinal)
    {
        "ALL_ITEMS", "DISH_OF_THE_DAY", FlavoredItem, "ITEMS_LOST_ON_DEATH", "ITEMS_SOLD_BY_PLAYER",
        "LOCATION_FISH", LostBookOrItem, "MONSTER_SLAYER_REWARDS", "MOVIE_CONCESSIONS_FOR_GUEST",
        "RANDOM_ARTIFACT_FOR_DIG_SPOT", "RANDOM_BASE_SEASON_ITEM", RandomItems, SecretNoteOrItem,
        "SHOP_TOWN_KEY", "TOOL_UPGRADES", "PET_ADOPTION",
    };

    /// <summary>Preserve type to the object it creates (ObjectDataDefinition.cs 121-368).</summary>
    private static readonly IReadOnlyDictionary<string, string> FlavoredBases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AgedRoe"] = "(O)447", ["Honey"] = "(O)340", ["Jelly"] = "(O)344", ["Juice"] = "(O)350",
        ["Pickle"] = "(O)342", ["Roe"] = "(O)812", ["Wine"] = "(O)348", ["Bait"] = "(O)SpecificBait",
        ["DriedFruit"] = "(O)DriedFruit", ["DriedMushroom"] = "(O)DriedMushrooms", ["SmokedFish"] = "(O)SmokedFish",
    };

    /// <summary>A known query key, or any text with arguments: item ids never contain spaces, so a
    /// mod's own query ("MYMOD_ITEM 3") is a query this model does not know, never a fake id.</summary>
    public static bool IsQuery(string itemIdOrQuery)
    {
        string text = itemIdOrQuery.Trim();
        return QueryKeys.Contains(text.Split(' ', 2)[0]) || text.Contains(' ');
    }

    public static string? FlavoredBaseId(string preserveType)
        => FlavoredBases.TryGetValue(preserveType, out string? id) ? id : null;

    public static QueryResult Resolve(string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        string text = itemIdOrQuery.Trim();
        if (!IsQuery(text))
            return new QueryResult(new[] { BundleParsing.NormalizeItemId(text) }, false, false, "");

        string[] parts = text.Split(' ', 2);
        string key = parts[0];
        string args = parts.Length > 1 ? parts[1] : "";
        switch (key)
        {
            case RandomItems:
                return new QueryResult(ExpandRandomItems(text, objects).ToList(), true, false, "random items");
            case FlavoredItem:
            {
                string type = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                return FlavoredBaseId(type) is string id
                    ? new QueryResult(new[] { id }, false, false, $"flavored {type}")
                    : new QueryResult(Array.Empty<string>(), false, true, $"unknown flavor type {type}");
            }
            case LostBookOrItem:
            {
                QueryResult alt = args.Length > 0 ? Resolve(args, objects) : Empty("");
                return new QueryResult(new[] { LostBookId }.Concat(alt.ItemIds).ToList(), true, alt.Unresolved, "lost book while any are left, then " + args);
            }
            case SecretNoteOrItem:
            {
                QueryResult alt = args.Length > 0 ? Resolve(args, objects) : Empty("");
                return new QueryResult(alt.ItemIds, alt.Chance, alt.Unresolved, "secret note when unlocked, then " + args);
            }
            default:
                return Empty($"unsupported item query {text}", unresolved: true);
        }
    }

    /// <summary>"RANDOM_ITEMS (O) [min max] [@flags]": numeric object ids in the range, with
    /// @isRandomSale dropping ExcludeFromRandomSale items and @requirePrice dropping unpriced ones
    /// (ItemQueryResolver.cs 420-489, 623-641). A ranged query drops non-numeric ids.</summary>
    public static IEnumerable<string> ExpandRandomItems(string query, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        string[] tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string qualifier = tokens.Skip(1).FirstOrDefault(t => t.StartsWith("(", StringComparison.Ordinal)) ?? "(O)";
        int[] numbers = tokens.Skip(1).Select(t => int.TryParse(t, out int n) ? n : (int?)null)
            .Where(n => n != null).Select(n => n!.Value).ToArray();
        bool ranged = numbers.Length >= 2;
        bool randomSale = tokens.Contains(RandomSaleFlag);
        bool requirePrice = tokens.Contains(RequirePriceFlag);

        foreach (ObjInfo obj in objects.Values.OrderBy(o => o.QualifiedId, StringComparer.Ordinal))
        {
            if (!obj.QualifiedId.StartsWith(qualifier, StringComparison.Ordinal)) continue;
            bool numeric = int.TryParse(obj.QualifiedId.Substring(qualifier.Length), out int id);
            if (ranged && (!numeric || id < numbers[0] || id > numbers[1])) continue;
            if (randomSale && obj.ExcludeFromRandomSale) continue;
            if (requirePrice && obj.Price <= 0) continue;
            yield return obj.QualifiedId;
        }
    }

    /// <summary>One source per resolved id, copying <paramref name="template"/>; a random query makes
    /// the sources chance. An unresolved query becomes one <see cref="SourceKind.Other"/> source under
    /// an id starting with <see cref="UnresolvedPrefix"/>, which the builder moves to diagnostics.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> Emit(
        string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects, ObtainSource template)
    {
        QueryResult result = Resolve(itemIdOrQuery, objects);
        if (result.Unresolved && result.ItemIds.Count == 0)
        {
            yield return (UnresolvedPrefix + itemIdOrQuery, template with
            {
                Kind = SourceKind.Other,
                Conditions = template.Conditions with { Unresolved = true },
                Detail = $"{template.Detail}: {result.Note}",
            });
            yield break;
        }
        foreach (string id in result.ItemIds)
            yield return (id, template with
            {
                Reliability = result.Chance ? Reliability.Chance : template.Reliability,
                Conditions = result.Unresolved ? template.Conditions with { Unresolved = true } : template.Conditions,
                Detail = result.Note.Length == 0 ? template.Detail : $"{template.Detail} ({result.Note})",
            });
    }

    private static QueryResult Empty(string note, bool unresolved = false)
        => new(Array.Empty<string>(), false, unresolved, note);
}
```

- [ ] **Step 6: Write `SpawnSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Sources that come straight out of spawn tables: forage, location fish, crab pots,
/// artifact spots, garbage cans and fishing trash.</summary>
public static class SpawnSources
{
    private const string IslandPrefix = "Island";
    private const string RainyWeather = "rainy";
    private const string AllDayTimeSpans = "600 2600";
    private const string FishingSkill = "Fishing";

    /// <summary>Maps that only exist during a passive festival (BeachNightMarket.cs, Submarine.cs).</summary>
    private static readonly IReadOnlyDictionary<string, string> FestivalOnlyLocations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Submarine"] = "NightMarket",
            ["BeachNightMarket"] = "NightMarket",
        };

    /// <summary>Fishing trash (FishingRod.cs 495; MineShaft.cs 1193-1197): caught when nothing bites.</summary>
    private static readonly string[] TrashIds = { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" };

    public static bool IsIslandLocation(string location)
        => location.StartsWith(IslandPrefix, StringComparison.Ordinal);

    public static IEnumerable<(string ItemId, ObtainSource Source)> Forage(
        IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (LocationSpawn row in rows)
            if (Spawn(row, festivals, SourceKind.Forage, $"Forage at {row.Location}") is ObtainSource template)
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                    yield return emitted;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> LocationFish(
        IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, FishRow> fishRows,
        IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (LocationSpawn row in rows)
        {
            if (Spawn(row, festivals, SourceKind.Fish, $"Fish at {row.Location}") is not ObtainSource baseTemplate)
                continue;
            // Resolve first, then look each concrete fish up: a query listing fish must not lose their data.
            QueryResult resolved = ItemQueries.Resolve(row.ItemId, objects);
            if (resolved.ItemIds.Count == 0)
            {
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, baseTemplate))
                    yield return emitted;
                continue;
            }
            foreach (string id in resolved.ItemIds)
            {
                fishRows.TryGetValue(id, out FishRow? fish);
                string time = fish == null || string.IsNullOrWhiteSpace(fish.TimeSpans) || fish.TimeSpans.Trim() == AllDayTimeSpans
                    ? "" : $", time {TimeText(fish.TimeSpans)}";
                int level = Math.Max(row.MinFishingLevel, fish?.MinFishingLevel ?? 0);
                ObtainConditions c = baseTemplate.Conditions;
                yield return (id, baseTemplate with
                {
                    Reliability = resolved.Chance ? Reliability.Chance : baseTemplate.Reliability,
                    Detail = baseTemplate.Detail + time + (resolved.Note.Length == 0 ? "" : $" ({resolved.Note})"),
                    Conditions = c with
                    {
                        Skill = level > 0 ? FishingSkill : c.Skill,
                        SkillLevel = level,
                        RainOnly = c.RainOnly || string.Equals(fish?.Weather, RainyWeather, StringComparison.OrdinalIgnoreCase),
                        CatchLimit = row.CatchLimit,
                        Unresolved = c.Unresolved || resolved.Unresolved,
                        Requires = row.RequireMagicBait ? c.Requires.Append("item:(O)908 Magic Bait").ToList() : c.Requires,
                    },
                });
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> CrabPot(IEnumerable<FishRow> rows)
    {
        foreach (FishRow row in rows.Where(r => r.IsTrap))
            yield return (row.ItemId, new ObtainSource(
                SourceKind.CrabPot, WeekMask.All, Reliability.Dependable,
                ObtainConditions.None with { Requires = new[] { "crafting:Crab Pot" } },
                "Data/Fish trap row"));
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> ArtifactSpots(
        IEnumerable<ArtifactSpotRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (ArtifactSpotRow row in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            if (reading.Weeks.IsEmpty) continue;
            var template = new ObtainSource(
                SourceKind.ArtifactSpot, reading.Weeks, Reliability.Chance,
                LocationConditions(row.Location, reading),
                $"artifact spot, {row.Location}, chance {row.Chance:0.###}");
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> GarbageCans(
        IEnumerable<GarbageRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (GarbageRow row in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            if (reading.Weeks.IsEmpty) continue;
            var template = new ObtainSource(
                SourceKind.GarbageCan, reading.Weeks, Reliability.Chance,
                ConditionSeasons.Apply(ObtainConditions.None, reading), $"garbage can {row.CanId}");
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FishingTrash()
    {
        foreach (string id in TrashIds)
            yield return (id, new ObtainSource(
                SourceKind.Trash, WeekMask.All, Reliability.Chance, ObtainConditions.None, "fishing trash"));
    }

    /// <summary>The source every item of a spawn row shares, or null when the row can never spawn in year 1.</summary>
    private static ObtainSource? Spawn(
        LocationSpawn row, IReadOnlyDictionary<string, FestivalDates> festivals, SourceKind kind, string detail)
    {
        ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
        WeekMask weeks = reading.Weeks & (row.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All);
        ObtainConditions conditions = LocationConditions(row.Location, reading);
        if (FestivalOnlyLocations.TryGetValue(row.Location, out string? festivalId)
            && festivals.TryGetValue(festivalId, out FestivalDates? festival))
        {
            weeks &= festival.Weeks;
            conditions = conditions with { FewDays = true };
        }
        if (weeks.IsEmpty) return null;
        Reliability reliability = reading.Chance || row.IsRandom ? Reliability.Chance : Reliability.Dependable;
        return new ObtainSource(kind, weeks, reliability, conditions, detail);
    }

    private static ObtainConditions LocationConditions(string location, ConditionReading reading)
        => ConditionSeasons.Apply(
            ObtainConditions.None with { Requires = new[] { "location:" + location }, GingerIsland = IsIslandLocation(location) },
            reading);

    /// <summary>"600 1200 1800 2000" to "600-1200 1800-2000".</summary>
    private static string TimeText(string spans)
    {
        string[] t = spans.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var pairs = new List<string>();
        for (int i = 0; i + 1 < t.Length; i += 2) pairs.Add($"{t[i]}-{t[i + 1]}");
        return string.Join(" ", pairs);
    }
}
```

- [ ] **Step 7: Run both to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityQueryTests|FullyQualifiedName~ObtainabilitySpawnTests"`
Expected: PASS, 14 tests.

The fish-through-a-query test: `RANDOM_ITEMS (O) 142 142` expands to Carp only, is a random query (so chance), and the Carp `FishRow` supplies level 3 and rainy weather.

- [ ] **Step 8: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityQueryTests.cs tests/TheLongestYear.Tests/ObtainabilitySpawnTests.cs
git commit -m "obtainability: item queries and spawn sources for forage, fish, crab pots, artifact spots, cans and trash"
git push origin story
```

---

### Task 4: Shop sources (shops, the cart, festival shops, Night Market boats, festival rewards)

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append one record)
- Create: `src/TheLongestYear.Core/Obtainability/ShopSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityShopTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3 (`SourcePair.Of`, `ObtainabilityModel`, `ConditionSeasons`, `ItemQueries.Emit`, `ObjInfo`, `FestivalDates`).
- Produces:
  - `record ShopRow(string ShopId, string ItemId, string? Condition, bool IsRecipe, bool IsRandom = false, string? TradeItemId = null)` (ItemId may be an item query)
  - `static IEnumerable<(string ItemId, ObtainSource Source)> ShopSources.Stock(IEnumerable<ShopRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)` (skips recipe and barter rows)
  - `static IEnumerable<(string ItemId, ObtainSource Source)> ShopSources.Barter(IEnumerable<ShopRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>, ObtainabilityModel snapshot)` (a derived rule: the builder runs it every pass)
  - `static IReadOnlyDictionary<string, WeekMask> ShopSources.RecipeWeeks(IEnumerable<ShopRow>, IReadOnlyDictionary<string, FestivalDates>)` keyed by the taught item's qualified id; year-2, island and unresolved rows never teach
  - `static IEnumerable<(string ItemId, ObtainSource Source)> ShopSources.FestivalRewards(IReadOnlyDictionary<string, FestivalDates>)`
  - `static bool ShopSources.IsIslandShop(string shopId)`

Festival dates come from data. The glue (Task 10) fills the festivals dictionary with passive festivals under their ids ("NightMarket", "SquidFest") and day festivals under their Data/Festivals/FestivalDates keys ("spring13", "winter8"). A `Festival_<Name>_...` shop is placed by the Event.cs 11818-11838 mapping from festival key to shop id, or by a passive festival whose id is `<Name>`. A festival shop neither resolves is kept all year but marked `Unresolved`, never silently treated as a fact.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityShopTests
{
    private static readonly Dictionary<string, FestivalDates> Festivals = new()
    {
        ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17),
        ["SquidFest"] = new FestivalDates("SquidFest", Season.Winter, 12, 13),
        ["TroutDerby"] = new FestivalDates("TroutDerby", Season.Summer, 20, 21),
        ["DesertFestival"] = new FestivalDates("DesertFestival", Season.Spring, 15, 17),
        ["summer11"] = new FestivalDates("summer11", Season.Summer, 11, 11),
        ["winter8"] = new FestivalDates("winter8", Season.Winter, 8, 8),
    };

    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)775"] = new ObjInfo("(O)775", "Glacierfish", -4, 1000, new string[0], true),
        ["(O)Moss"] = new ObjInfo("(O)Moss", "Moss", -81, 5, new string[0], false),
    };

    private static List<(string ItemId, ObtainSource Source)> Stock(params ShopRow[] rows)
        => ShopSources.Stock(rows, Objects, Festivals).ToList();

    [Fact]
    public void A_seasonal_seed_row_is_dependable_in_its_season()
    {
        var (id, s) = Stock(new ShopRow("SeedShop", "(O)472", "SEASON Spring", false)).Single();
        Assert.Equal("(O)472", id);
        Assert.Equal(SourceKind.Shop, s.Kind);
        Assert.Equal(Reliability.Dependable, s.Reliability);
        Assert.Equal(WeekMask.ForSeason(Season.Spring), s.Weeks);
        Assert.Contains("shop:SeedShop", s.Conditions.Requires);
    }

    [Fact]
    public void The_traveling_cart_expands_its_random_query_as_chance()
    {
        var ids = Stock(new ShopRow("Traveler", "RANDOM_ITEMS (O) 2 789 @isRandomSale @requirePrice", null, false));
        Assert.Equal(new[] { "(O)24" }, ids.Select(x => x.ItemId).ToArray());
        Assert.All(ids, x => Assert.Equal(SourceKind.Cart, x.Source.Kind));
        Assert.All(ids, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
    }

    [Fact]
    public void Festival_shops_open_on_their_festival_days_from_data()
    {
        var luau = Stock(new ShopRow("Festival_Luau_Pierre", "(O)Moss", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(6), luau.Weeks);                       // Summer 11
        Assert.Equal(SourceKind.Festival, luau.Kind);
        Assert.True(luau.Conditions.FewDays);

        var boat = Stock(new ShopRow("Festival_NightMarket_MagicBoat_Day2", "(O)Moss", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(15), boat.Weeks);
        Assert.Equal(SourceKind.NightMarket, boat.Kind);

        var ice = Stock(new ShopRow("Festival_FestivalOfIce_TravelingMerchant", "RANDOM_ITEMS (O) 2 789 @isRandomSale", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(14), ice.Weeks);                        // Winter 8
        Assert.Equal(Reliability.Chance, ice.Reliability);

        var desert = Stock(new ShopRow("Festival_DesertFestival_Vendor", "(O)Moss", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(3), desert.Weeks);                      // Spring 15-17 from the passive festival id
    }

    [Fact]
    public void An_unplaceable_festival_shop_is_unresolved_not_a_fact()
    {
        var s = Stock(new ShopRow("Festival_SomeModFair_Booth", "(O)Moss", null, false)).Single().Source;
        Assert.True(s.Conditions.Unresolved);
        Assert.Equal(WeekMask.All, s.Weeks);
    }

    [Fact]
    public void Island_shops_and_year_two_rows_are_flagged()
    {
        Assert.True(Stock(new ShopRow("IslandTrade", "(O)Moss", null, false)).Single().Source.Conditions.GingerIsland);
        Assert.True(ShopSources.IsIslandShop("VolcanoShop"));
        Assert.False(ShopSources.IsIslandShop("SeedShop"));
        Assert.True(Stock(new ShopRow("SeedShop", "(O)476", "YEAR 2, SEASON Spring", false)).Single().Source.Conditions.YearTwo);
    }

    [Fact]
    public void Recipe_rows_teach_rather_than_sell_and_year_two_rows_never_teach()
    {
        var rows = new[]
        {
            new ShopRow("Saloon", "(O)196", "SEASON Fall", true),
            new ShopRow("Saloon", "(O)197", "YEAR 2", true),
            new ShopRow("IslandTrade", "(O)198", null, true),
        };
        Assert.Empty(ShopSources.Stock(rows, Objects, Festivals));
        var taught = ShopSources.RecipeWeeks(rows, Festivals);
        Assert.Equal(WeekMask.ForSeason(Season.Fall), taught["(O)196"]);
        Assert.False(taught.ContainsKey("(O)197"));
        Assert.False(taught.ContainsKey("(O)198"));
    }

    [Fact]
    public void Squid_fest_and_trout_derby_rewards_use_their_festival_dates_and_their_odds()
    {
        var rewards = ShopSources.FestivalRewards(Festivals).ToList();
        var book = rewards.Single(r => r.ItemId == "(O)Book_Crabbing").Source;
        Assert.Equal(WeekMask.Of(14), book.Weeks);                       // Winter 12-13
        Assert.True(book.Conditions.FewDays);
        Assert.Equal(Reliability.Dependable, book.Reliability);
        var tent = rewards.Single(r => r.ItemId == "(O)TentKit").Source;
        Assert.Equal(WeekMask.Of(7), tent.Weeks);                        // Summer 20-21
        Assert.Equal(Reliability.Dependable, tent.Reliability);         // the first tag always gives it
        Assert.Equal(Reliability.Chance, rewards.Single(r => r.ItemId == "(O)710").Source.Reliability);   // a spin
        Assert.Contains(rewards, r => r.ItemId == "(O)498" && r.Source.Reliability == Reliability.Chance); // a 50/50
    }

    [Fact]
    public void A_random_shop_alternative_is_chance()
        => Assert.Equal(Reliability.Chance, Stock(new ShopRow("SeedShop", "(O)472", null, false, IsRandom: true)).Single().Source.Reliability);

    [Fact]
    public void A_barter_row_is_only_had_in_weeks_its_trade_item_is()
    {
        var row = new ShopRow("DesertTrade", "(O)Moss", null, false, TradeItemId: "(O)24");
        Assert.Empty(Stock(row));
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = new[] { new ObtainSource(SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable, ObtainConditions.None, "seeds") },
        });
        var (id, source) = ShopSources.Barter(new[] { row }, Objects, Festivals, snapshot).Single();
        Assert.Equal("(O)Moss", id);
        Assert.Equal(WeekMask.ForSeason(Season.Spring), source.Weeks);
        Assert.Contains("trade:(O)24", source.Conditions.Requires);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityShopTests"`
Expected: build FAILS, `ShopRow` not found.

- [ ] **Step 3: Append the record to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One Data/Shops stock row, or one of its RandomItemId entries (<see cref="IsRandom"/>). <see cref="ItemId"/>
/// is an id or an item query. A recipe row teaches the recipe for that item. <see cref="TradeItemId"/> is
/// ShopItemData.TradeItemId: the item is paid for with another item.</summary>
public sealed record ShopRow(string ShopId, string ItemId, string? Condition, bool IsRecipe, bool IsRandom = false, string? TradeItemId = null);
```

- [ ] **Step 4: Write `ShopSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Sources that are bought or handed out: shop rows, the Traveling Cart, festival shops,
/// Night Market boats, and the Squid Fest and Trout Derby rewards.</summary>
public static class ShopSources
{
    private const string CartShopId = "Traveler";
    private const string FestivalShopPrefix = "Festival_";
    private const string NightMarketId = "NightMarket";
    private const string TravelingMerchantSuffix = "_TravelingMerchant";
    private static readonly string[] IslandShopMarkers = { "Island", "Volcano", "Resort", "QiGem" };

    /// <summary>Festival key to the shop it opens (Event.cs 11818-11838). Keys are Data/Festivals/FestivalDates keys.</summary>
    private static readonly IReadOnlyDictionary<string, string> FestivalShopKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Festival_EggFestival_Pierre"] = "spring13",
        ["Festival_FlowerDance_Pierre"] = "spring24",
        ["Festival_Luau_Pierre"] = "summer11",
        ["Festival_DanceOfTheMoonlightJellies_Pierre"] = "summer28",
        ["Festival_SpiritsEve_Pierre"] = "fall27",
        ["Festival_FestivalOfIce_TravelingMerchant"] = "winter8",
        ["Festival_FeastOfTheWinterStar_Pierre"] = "winter25",
    };

    /// <summary>Squid Fest rewards (GameLocation.cs 11458-11499: score tiers, with a 50/50 between Winter
    /// Seeds and Mystery Boxes, and Mystery Boxes plus 265 only once the book is owned) and Trout Derby
    /// rewards (11524-11562: the first tag always gives a Tent Kit, later tags spin a wheel).</summary>
    private static readonly (string Festival, string ItemId, Reliability Reliability)[] RewardTable =
    {
        ("SquidFest", "(O)DeluxeBait", Reliability.Dependable), ("SquidFest", "(O)498", Reliability.Chance),
        ("SquidFest", "(O)MysteryBox", Reliability.Chance), ("SquidFest", "(O)242", Reliability.Dependable),
        ("SquidFest", "(O)797", Reliability.Dependable), ("SquidFest", "(O)395", Reliability.Dependable),
        ("SquidFest", "(F)SquidKid_Painting", Reliability.Dependable), ("SquidFest", "(O)Book_Crabbing", Reliability.Dependable),
        ("SquidFest", "(O)265", Reliability.Chance), ("SquidFest", "(O)694", Reliability.Dependable),
        ("SquidFest", "(O)166", Reliability.Dependable), ("SquidFest", "(O)253", Reliability.Dependable),
        ("SquidFest", "(H)SquidHat", Reliability.Dependable),
        ("TroutDerby", "(O)TentKit", Reliability.Dependable), ("TroutDerby", "(H)BucketHat", Reliability.Chance),
        ("TroutDerby", "(O)710", Reliability.Chance), ("TroutDerby", "(O)MysteryBox", Reliability.Chance),
        ("TroutDerby", "(O)72", Reliability.Chance), ("TroutDerby", "(F)MountedTrout_Painting", Reliability.Chance),
        ("TroutDerby", "(O)DeluxeBait", Reliability.Chance), ("TroutDerby", "(O)253", Reliability.Chance),
        ("TroutDerby", "(O)621", Reliability.Chance), ("TroutDerby", "(O)688", Reliability.Chance),
        ("TroutDerby", "(O)749", Reliability.Chance),
    };

    public static bool IsIslandShop(string shopId)
        => IslandShopMarkers.Any(m => shopId.Contains(m, StringComparison.Ordinal));

    public static IEnumerable<(string ItemId, ObtainSource Source)> Stock(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (ShopRow row in rows)
        {
            if (row.IsRecipe || row.TradeItemId != null) continue;
            if (Template(row, festivals) is not ObtainSource template) continue;
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    /// <summary>Barter rows (ShopItemData.TradeItemId): paid for with another item, so, counting weeks in
    /// isolation, only had in weeks the trade item can be had.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> Barter(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals, ObtainabilityModel snapshot)
    {
        foreach (ShopRow row in rows.Where(r => !r.IsRecipe && r.TradeItemId != null))
        {
            if (Template(row, festivals) is not ObtainSource template) continue;
            string trade = BundleParsing.NormalizeItemId(row.TradeItemId!);
            WeekMask dep = template.Reliability == Reliability.Dependable
                ? template.Weeks & snapshot.Weeks(trade, ObtainFilter.DependableOnly)
                : WeekMask.None;
            WeekMask any = template.Weeks & snapshot.Weeks(trade, ObtainFilter.Any);
            ObtainConditions conditions = template.Conditions with { Requires = template.Conditions.Requires.Append("trade:" + trade).ToList() };
            foreach (ObtainSource s in SourcePair.Of(template.Kind, dep, any, conditions, template.Detail + " (barter)"))
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, s))
                    yield return emitted;
        }
    }

    public static IReadOnlyDictionary<string, WeekMask> RecipeWeeks(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var result = new Dictionary<string, WeekMask>(StringComparer.Ordinal);
        foreach (ShopRow row in rows.Where(r => r.IsRecipe))
        {
            if (Template(row, festivals) is not ObtainSource template) continue;
            ObtainConditions c = template.Conditions;
            if (c.YearTwo || c.GingerIsland || c.Unresolved) continue;
            string id = BundleParsing.NormalizeItemId(row.ItemId);
            result[id] = result.TryGetValue(id, out WeekMask existing) ? existing | template.Weeks : template.Weeks;
        }
        return result;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FestivalRewards(
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach ((string festivalId, string itemId, Reliability reliability) in RewardTable)
        {
            if (!festivals.TryGetValue(festivalId, out FestivalDates? festival)) continue;
            yield return (itemId, new ObtainSource(
                SourceKind.Festival, festival.Weeks, reliability,
                ObtainConditions.None with { FewDays = true, Requires = new[] { "festival:" + festivalId } },
                $"{festivalId} reward"));
        }
    }

    /// <summary>The source every item of a row shares, or null when the row can never be stocked in year 1.</summary>
    private static ObtainSource? Template(ShopRow row, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
        (SourceKind kind, WeekMask placeWeeks, bool fewDays, bool unplaced) = Placement(row.ShopId, festivals);
        WeekMask weeks = reading.Weeks & placeWeeks;
        if (weeks.IsEmpty) return null;
        bool chance = reading.Chance || row.IsRandom || kind == SourceKind.Cart
                      || row.ShopId.EndsWith(TravelingMerchantSuffix, StringComparison.Ordinal);
        ObtainConditions conditions = ConditionSeasons.Apply(
            ObtainConditions.None with
            {
                Requires = new[] { "shop:" + row.ShopId },
                FewDays = fewDays,
                GingerIsland = IsIslandShop(row.ShopId),
                Unresolved = unplaced,
            },
            reading);
        return new ObtainSource(kind, weeks, chance ? Reliability.Chance : Reliability.Dependable, conditions, $"shop {row.ShopId}");
    }

    private static (SourceKind Kind, WeekMask Weeks, bool FewDays, bool Unplaced) Placement(
        string shopId, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        if (shopId == CartShopId) return (SourceKind.Cart, WeekMask.All, false, false);
        if (!shopId.StartsWith(FestivalShopPrefix, StringComparison.Ordinal)) return (SourceKind.Shop, WeekMask.All, false, false);

        string name = shopId.Substring(FestivalShopPrefix.Length).Split('_')[0];
        SourceKind kind = name == NightMarketId ? SourceKind.NightMarket : SourceKind.Festival;
        if (FestivalShopKeys.TryGetValue(shopId, out string? key) && festivals.TryGetValue(key, out FestivalDates? day))
            return (kind, day.Weeks, true, false);
        if (festivals.TryGetValue(name, out FestivalDates? passive))
            return (kind, passive.Weeks, true, false);
        return (kind, WeekMask.All, true, true);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityShopTests"`
Expected: PASS, 9 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityShopTests.cs
git commit -m "obtainability: shop, cart, festival shop, Night Market and festival reward sources"
git push origin story
```

---

### Task 5: Mine sources (nodes, monster drops, fishing treasure)

These facts live only in the game's code, not its data, so they are tables here. Every value cites
the PC 1.6 decompile line it came from. Floors and prerequisites are recorded as `Requires` notes,
never turned into weeks: the model says what is possible, and a mine is open from week 1.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append one record)
- Create: `src/TheLongestYear.Core/Obtainability/MineSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityMineTests.cs`

**Interfaces:**
- Consumes: Task 1; `ItemQueries.Emit` and `ObjInfo` (Task 3).
- Produces:
  - `record MonsterDropRow(string Monster, string ItemId, double Chance)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MineSources.Nodes()`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MineSources.MonsterDrops(IEnumerable<MonsterDropRow>, IReadOnlyDictionary<string, ObjInfo>)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MineSources.FishingTreasure()`
  - `static string? MineSources.MonsterFloor(string monster)` (null when the monster is not a mine spawn)

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityMineTests
{
    [Fact]
    public void Ores_are_dependable_from_their_floors_and_iridium_needs_skull_cavern()
    {
        var nodes = MineSources.Nodes().ToList();
        var copper = nodes.Single(n => n.ItemId == "(O)378").Source;
        Assert.Equal(Reliability.Dependable, copper.Reliability);
        Assert.Equal(WeekMask.All, copper.Weeks);
        Assert.Contains("mines:floor 1", copper.Conditions.Requires);
        Assert.Contains("location:SkullCave", nodes.Single(n => n.ItemId == "(O)386").Source.Conditions.Requires);
    }

    [Fact]
    public void Gems_and_geodes_from_stones_are_chance()
    {
        var nodes = MineSources.Nodes().ToList();
        Assert.Equal(Reliability.Chance, nodes.Single(n => n.ItemId == "(O)72").Source.Reliability);  // diamond
        Assert.Equal(Reliability.Chance, nodes.Single(n => n.ItemId == "(O)536").Source.Reliability); // frozen geode
    }

    [Fact]
    public void Monster_drops_carry_the_monster_floor()
    {
        var rows = new[]
        {
            new MonsterDropRow("Dust Spirit", "(O)382", 0.5),
            new MonsterDropRow("Some Modded Beast", "(O)766", 0.1),
        };
        var list = MineSources.MonsterDrops(rows, new Dictionary<string, ObjInfo>()).ToList();
        Assert.All(list, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
        Assert.Contains("mines:floor 40", list[0].Source.Conditions.Requires);
        Assert.Contains("monster:Some Modded Beast", list[1].Source.Conditions.Requires);
        Assert.Equal("floor 1", MineSources.MonsterFloor("Green Slime"));
        Assert.Null(MineSources.MonsterFloor("Some Modded Beast"));
    }

    [Fact]
    public void Treasure_is_complete_chance_and_gated_where_the_game_gates_it()
    {
        var all = MineSources.FishingTreasure().ToList();
        Assert.Contains(all, t => t.ItemId.StartsWith(ItemQueries.UnresolvedPrefix) && t.Source.Detail.Contains("raccoon"));
        var treasure = all.Where(t => !t.ItemId.StartsWith(ItemQueries.UnresolvedPrefix)).ToList();
        Assert.All(treasure, t => Assert.Equal(SourceKind.FishingTreasure, t.Source.Kind));
        Assert.Contains(treasure, t => t.ItemId == "(O)Book_Roe");
        Assert.Contains(treasure, t => t.ItemId == "(O)TroutDerbyTag");
        Assert.Contains(treasure, t => t.ItemId == "(O)812" && t.Source.Conditions.Requires.Contains("book:Book_Roe"));
        Assert.All(treasure, t => Assert.Equal(Reliability.Chance, t.Source.Reliability));
        Assert.Equal(WeekMask.ForSeason(Season.Spring), treasure.Single(t => t.ItemId == "(O)273").Source.Weeks);
        Assert.Contains(treasure, t => t.ItemId == "(O)774" && t.Source.Conditions.Requires.Contains("recipe:Wild Bait"));
        Assert.True(treasure.Single(t => t.ItemId == "(O)890").Source.Conditions.GingerIsland);   // Qi beans need Qi's orders
        Assert.Contains(treasure, t => t.ItemId == "(W)14");
        Assert.Contains(treasure, t => t.ItemId == "(B)504");
        Assert.Contains(treasure, t => t.ItemId == "(O)SkillBook_0");
        Assert.Contains(treasure, t => t.ItemId == "(O)119");
        Assert.Contains(treasure, t => t.ItemId == "(O)StardropTea" && t.Source.Conditions.Requires.Contains("fishing:golden treasure chest"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMineTests"`
Expected: build FAILS, `MineSources` not found.

- [ ] **Step 3: Append the record to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One Data/Monsters drop (field 6 is "id chance id chance ..."). <see cref="ItemId"/> may be a query.</summary>
public sealed record MonsterDropRow(string Monster, string ItemId, double Chance);
```

- [ ] **Step 4: Write `MineSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Mine nodes, monster drops and fishing treasure. These come from game CODE, not data, so
/// the facts are tables; each line cites the PC 1.6 decompile.</summary>
public static class MineSources
{
    private const string SkullCave = "location:SkullCave";
    private const string Treasure = "fishing:treasure chest";
    private const string GoldenTreasure = "fishing:golden treasure chest";
    private const int SkullCavernFloor = 121;
    private const int SkillBookCount = 5;

    /// <summary>What breaking stones yields, and from which floor (MineShaft.cs createLitterObject
    /// 4321-4658, getMineArea 3757-3827, gem nodes 3962-3975, geodes from stones 3642-3663, coal 3665-3673).</summary>
    private static readonly (string ItemId, string Where, Reliability Reliability, string Note)[] NodeTable =
    {
        ("(O)378", "mines:floor 1", Reliability.Dependable, "copper node 751, 2.9% per stone (4377)"),
        ("(O)380", "mines:floor 40", Reliability.Dependable, "iron node 290 (4428)"),
        ("(O)384", "mines:floor 80", Reliability.Dependable, "gold node 764 (4466)"),
        ("(O)386", SkullCave, Reliability.Dependable, "iridium node 765 (4549-4601)"),
        ("(O)382", "mines:floor 1", Reliability.Chance, "coal from stones, 5% then 25% (3665-3673)"),
        ("(O)66", "mines:floor 1", Reliability.Chance, "amethyst node 8 (3966)"),
        ("(O)68", "mines:floor 1", Reliability.Chance, "topaz node 10 (3966)"),
        ("(O)70", "mines:floor 40", Reliability.Chance, "jade node 6 (3970)"),
        ("(O)62", "mines:floor 40", Reliability.Chance, "aquamarine node 14 (3970)"),
        ("(O)64", "mines:floor 80", Reliability.Chance, "ruby node 4 (3975)"),
        ("(O)60", "mines:floor 80", Reliability.Chance, "emerald node 12 (3975)"),
        ("(O)72", "mines:floor 51", Reliability.Chance, "diamond node 2 above floor 50 (4607)"),
        ("(O)535", "mines:floor 1", Reliability.Chance, "geode from stones, floors 1-39 (3642)"),
        ("(O)536", "mines:floor 40", Reliability.Chance, "frozen geode from stones (3650)"),
        ("(O)537", "mines:floor 80", Reliability.Chance, "magma geode from stones (3655)"),
        ("(O)749", "mines:floor 21", Reliability.Chance, "omni geode above floor 20 and Skull Cavern (3660)"),
    };

    /// <summary>First floor each mine monster spawns on (MineShaft.cs getMonsterForThisLevel 3999-4318).
    /// Skull Cavern monsters use floor 121.</summary>
    private static readonly IReadOnlyDictionary<string, int> MonsterFloors = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Green Slime"] = 1, ["Big Slime"] = 1, ["Bug"] = 1, ["Duggy"] = 1, ["Rock Crab"] = 1,
        ["Fly"] = 15, ["Grub"] = 15, ["Bat"] = 31, ["Stone Golem"] = 31,
        ["Frost Bat"] = 40, ["Dust Spirit"] = 40, ["Ghost"] = 51, ["Skeleton"] = 70,
        ["Lava Bat"] = 80, ["Metal Head"] = 80, ["Shadow Brute"] = 80, ["Shadow Shaman"] = 80, ["Lava Crab"] = 80,
        ["Squid Kid"] = 90,
        ["Mummy"] = 121, ["Serpent"] = 121, ["Iridium Bat"] = 121, ["Carbon Ghost"] = 121,
        ["Pepper Rex"] = 126, ["Iridium Crab"] = 146,
    };

    /// <summary>Every fishing treasure outcome in FishingRod.openTreasureMenuEndFunction (2426-2730),
    /// with the gate the game checks. All chance. Line numbers per entry.</summary>
    private static readonly (string ItemId, string[] Requires, bool Island, string Note)[] TreasureTable = BuildTreasureTable();

    public static string? MonsterFloor(string monster)
        => MonsterFloors.TryGetValue(monster, out int floor) ? (floor >= SkullCavernFloor ? "Skull Cavern" : $"floor {floor}") : null;

    public static IEnumerable<(string ItemId, ObtainSource Source)> Nodes()
    {
        foreach ((string id, string where, Reliability reliability, string note) in NodeTable)
            yield return (id, new ObtainSource(
                SourceKind.MineNode, WeekMask.All, reliability,
                ObtainConditions.None with { Requires = new[] { where } }, note));
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> MonsterDrops(
        IEnumerable<MonsterDropRow> rows, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        foreach (MonsterDropRow row in rows)
        {
            string requires = MonsterFloors.TryGetValue(row.Monster, out int floor)
                ? (floor >= SkullCavernFloor ? SkullCave : $"mines:floor {floor}")
                : "monster:" + row.Monster;
            var template = new ObtainSource(
                SourceKind.MonsterDrop, WeekMask.All, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { requires } },
                $"{row.Monster} drop, chance {row.Chance:0.###}");
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FishingTreasure()
    {
        foreach ((string id, string[] requires, bool island, string note) in TreasureTable)
        {
            WeekMask weeks = id == "(O)273" ? WeekMask.ForSeason(Season.Spring) : WeekMask.All;
            yield return (id, new ObtainSource(
                SourceKind.FishingTreasure, weeks, Reliability.Chance,
                ObtainConditions.None with { Requires = requires, GingerIsland = island }, note));
        }
        // The golden chest's raccoon seed is chosen by code for the time of year; recorded as a diagnostic.
        yield return (ItemQueries.UnresolvedPrefix + "golden chest raccoon seed", new ObtainSource(
            SourceKind.Other, WeekMask.All, Reliability.Chance,
            ObtainConditions.None with { Requires = new[] { GoldenTreasure }, Unresolved = true },
            "Utility.getRaccoonSeedForCurrentTimeOfYear, golden chest table (2477)"));
    }

    private static (string, string[], bool, string)[] BuildTreasureTable()
    {
        var t = new List<(string, string[], bool, string)>();
        void Add(string id, string note, params string[] extra) => t.Add((id, new[] { Treasure }.Concat(extra).ToArray(), false, note));
        void Golden(string id, string note) => t.Add((id, new[] { GoldenTreasure }, false, note));

        Add("(O)273", "rice shoot, Spring, not the beach (2446-2448)");
        Add("(O)774", "wild bait (2450-2452, 2556-2558)", "recipe:Wild Bait");
        t.Add(("(O)890", new[] { Treasure, "special order:DROP_QI_BEANS" }, true, "Qi beans (2454-2456)"));
        Add("(O)MysteryBox", "mystery box roll (2458-2461)");
        Add("(O)GoldenMysteryBox", "golden mystery box (2460)", "mastery:2");
        Add("(O)GoldenAnimalCracker", "golden animal cracker (2462-2465)", "mastery:0");
        foreach (string id in new[] { "(O)337", "(O)213", "(O)872", "(O)687", "(O)ChallengeBait", "(O)703", "(O)StardropTea", "(O)797", "(O)733", "(O)728", "(O)SonarBobber" })
            Golden(id, "golden chest table (2466-2509)");
        for (int i = 0; i < SkillBookCount; i++) Golden($"(O)SkillBook_{i}", "golden chest table (2474)");
        Add("(O)378", "case 0 (2532)"); Add("(O)388", "case 0 (2536)"); Add("(O)390", "case 0 (2540)"); Add("(O)382", "case 0 (2542)");
        Add("(O)380", "case 0, distance 3 (2528)", "fishing:clear water 3");
        Add("(O)384", "case 0, distance 4 (2524)", "fishing:clear water 4");
        Add("(O)386", "case 0, distance 5 (2516-2518)", "fishing:clear water 5");
        Add("(O)687", "case 1 (2552-2554)", "skill:Fishing 6", "fishing:clear water 4");
        Add("(O)SonarBobber", "case 1 (2560-2562)", "skill:Fishing 6");
        Add("(O)DeluxeBait", "case 1 (2564-2566)", "skill:Fishing 6");
        Add("(O)685", "case 1 and the empty-chest fallback (2570, 2725)");
        Add("(O)102", "case 2 lost book (2574-2576)", "mail:lostBookFound");
        for (int id = 585; id <= 587; id++) Add($"(O){id}", "case 2 artifact (2580-2582)", "skill:Fishing 2", "artifact found");
        for (int id = 103; id <= 119; id++) Add($"(O){id}", "case 2 artifact (2584-2586)", "skill:Fishing 2", "artifact found");
        Add("(O)535", "case 2 and case 3 geode (2590, 2603)");
        Add("(O)536", "case 3 geode (2603)", "fishing:clear water 3");
        Add("(O)537", "case 3 geode (2603)", "fishing:clear water 4");
        foreach (string id in new[] { "(O)86", "(O)66", "(O)68" }) Add(id, "case 3 gem (2629)", "skill:Fishing 2");
        foreach (string id in new[] { "(O)84", "(O)70", "(O)62" }) Add(id, "case 3 gem (2625)", "skill:Fishing 2", "fishing:clear water 3");
        foreach (string id in new[] { "(O)82", "(O)64", "(O)60" }) Add(id, "case 3 gem (2621)", "skill:Fishing 2", "fishing:clear water 4");
        Add("(O)72", "case 3 diamond (2631-2634, 2706)", "skill:Fishing 2");
        Add("(O)770", "case 3 mixed seeds below Fishing 2 (2643-2646)");
        Add("(W)14", "case 3 Neptune's Glaive, once (2649-2654)", "skill:Fishing 2");
        Add("(W)51", "case 3 Broken Trident, once (2655-2660)", "skill:Fishing 2");
        foreach (string id in new[] { "(O)516", "(O)517", "(O)518", "(O)519" }) Add(id, "case 3 ring (2666-2669)", "skill:Fishing 2");
        for (int id = 529; id <= 534; id++) Add($"(O){id}", "case 3 ring (2672)", "skill:Fishing 2");
        Add("(O)166", "case 3 treasure chest (2676-2679)", "skill:Fishing 2");
        Add("(O)74", "case 3 prismatic shard (2680-2683)", "skill:Fishing 6");
        Add("(O)127", "case 3 (2684-2687)", "skill:Fishing 2");
        Add("(O)126", "case 3 (2688-2691)", "skill:Fishing 2");
        Add("(O)527", "case 3 ring (2692-2695)", "skill:Fishing 2");
        for (int id = 504; id <= 513; id++) Add($"(B){id}", "case 3 boots (2696-2699)", "skill:Fishing 2");
        Add("(O)928", "case 3 golden egg (2700-2703)", "skill:Fishing 2", "mail:Farm_Eternal");
        for (int i = 0; i < SkillBookCount; i++) Add($"(O)SkillBook_{i}", "case 3 skill book after 3 treasures (2708-2715)", "skill:Fishing 2");
        Add("(O)GoldenBobber", "Desert Festival quest on its third day (2727-2730)", "quest:98765", "festival:DesertFestival");
        Add("(O)812", "roe from the caught fish, once the Roe book is read (2732-2748)", "book:Book_Roe");
        Add("(O)Book_Roe", "the roe book, Fishing 5 and more than 2 treasures (2750-2754)", "skill:Fishing 5");
        Add("(O)TroutDerbyTag", "a trout derby tag caught during the derby (2756-2759)", "festival:TroutDerby");
        return t.ToArray();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMineTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityMineTests.cs
git commit -m "obtainability: mine node, monster drop and complete fishing treasure sources from game code"
git push origin story
```

---

### Task 6: Grown sources (crops, greenhouse, Mixed Seeds, fruit trees)

**Week-in-isolation rule (Jeff, 2026-09-14):** "we're only tracking when something can be made within a
specific week in isolation ... assume they've saved nothing and make sure they can still pivot." A grown
item counts in a week only when its seed or sapling can be obtained on the planting day itself; growing
time is allowed, stockpiled seed is not. Everything is computed day by day, so a four-day crop planted
on day 1 counts in week 1.

These rules read a snapshot model (the previous resolution pass) for the seed's weeks.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/GrowSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityGrowTests.cs`

**Interfaces:**
- Consumes: `ObtainabilityModel`, `ObtainFilter`, `WeekMask`, `SourcePair.Of` (Task 1); `ConditionSeasons` (Task 2); `ObjInfo`, `ItemQueries.Emit` (Task 3).
- Produces:
  - `record CropRow(string SeedId, string HarvestId, IReadOnlyList<Season> Seasons, int GrowthDays, int RegrowDays)` (`RegrowDays` <= 0 means no regrowth)
  - `record FruitRow(string ItemId, Season? Season, double Chance, string? Condition, bool IsRandom = false)` (ItemId may be an item query)
  - `record FruitTreeRow(string SaplingId, IReadOnlyList<Season> TreeSeasons, IReadOnlyList<FruitRow> Fruit)`
  - `static WeekMask GrowSources.Harvest(WeekMask seedWeeks, IReadOnlyList<Season> seasons, int growthDays, int regrowDays)`
  - `static WeekMask GrowSources.Greenhouse(WeekMask seedWeeks, int growthDays, int regrowDays)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> GrowSources.Crops(IEnumerable<CropRow>, ObtainabilityModel snapshot)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> GrowSources.FruitTrees(IEnumerable<FruitTreeRow>, ObtainabilityModel snapshot, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityGrowTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();

    private static ObtainabilityModel Snapshot(params (string Id, SourceKind Kind, WeekMask Weeks, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(r.Kind, r.Weeks, r.R, ObtainConditions.None, "test")).ToList()));

    [Fact]
    public void A_four_day_spring_crop_harvests_all_spring_but_not_past_it()
        => Assert.Equal("1-4", GrowSources.Harvest(WeekMask.ForSeason(Season.Spring), new[] { Season.Spring }, 4, 0).ToString());

    [Fact]
    public void A_regrowing_two_season_crop_keeps_yielding_on_its_interval()
    {
        var both = new[] { Season.Summer, Season.Fall };
        Assert.Equal("7-12", GrowSources.Harvest(WeekMask.ForSeasons(both), both, 14, 4).ToString());
        // Seed only in the first week of summer, regrowing every 21 days: harvests on days 43, 64 and 85 would
        // be weeks 7 and 10; day 85 is Winter, so it stops.
        Assert.Equal("7,10", GrowSources.Harvest(WeekMask.Of(5), both, 14, 21).ToString());
    }

    [Fact]
    public void The_greenhouse_needs_seed_on_the_planting_day_and_counts_growth_by_days()
    {
        Assert.Equal("1-5", GrowSources.Greenhouse(WeekMask.ForSeason(Season.Spring), 4, 0).ToString());
        Assert.Equal("1-16", GrowSources.Greenhouse(WeekMask.ForSeason(Season.Spring), 4, 1).ToString());
    }

    [Fact]
    public void Crops_split_dependable_seed_weeks_from_chance_ones()
    {
        var snapshot = Snapshot(
            ("(O)472", SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable),
            ("(O)472", SourceKind.Cart, WeekMask.All, Reliability.Chance));
        var rows = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var sources = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = sources.Where(s => s.Kind == SourceKind.Crop).ToList();
        Assert.Single(outdoor);
        Assert.Equal(Reliability.Dependable, outdoor[0].Reliability);
        var greenhouse = sources.Where(s => s.Kind == SourceKind.GreenhouseCrop).ToList();
        Assert.Contains(greenhouse, s => s.Reliability == Reliability.Dependable && s.Weeks.ToString() == "1-5");
        Assert.Contains(greenhouse, s => s.Reliability == Reliability.Chance && s.Weeks.Contains(14));
        Assert.All(greenhouse, s => Assert.Contains("mail:ccPantry", s.Conditions.Requires));
    }

    [Fact]
    public void Mixed_seeds_give_the_planting_days_pool_and_winter_greenhouse_gives_every_pool()
    {
        var snapshot = Snapshot(("(O)770", SourceKind.Forage, WeekMask.All, Reliability.Chance));
        var rows = new[]
        {
            new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0),
            new CropRow("(O)487", "(O)270", new[] { Season.Summer, Season.Fall }, 14, 4),
            new CropRow("(O)770", "(O)770", new Season[0], 1, 0),
        };
        var parsnip = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        Assert.Contains(parsnip, s => s.Kind == SourceKind.Crop && s.Detail.Contains("Mixed Seeds") && s.Weeks.ToString() == "1-4");
        var indoor = parsnip.Single(s => s.Kind == SourceKind.GreenhouseCrop && s.Detail.Contains("Mixed Seeds"));
        Assert.Equal("1-5,13-16", indoor.Weeks.ToString());     // spring pool in spring, every pool in winter, never summer or fall
        Assert.DoesNotContain(GrowSources.Crops(rows, snapshot), s => s.ItemId == "(O)770");
    }

    [Fact]
    public void A_fruit_tree_fruits_from_maturity_in_its_season_and_a_spring_sapling_misses_spring()
    {
        var snapshot = Snapshot(
            ("(O)633", SourceKind.Shop, WeekMask.All, Reliability.Dependable),                         // apple sapling
            ("(O)628", SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable),     // cherry sapling
            ("(O)69", SourceKind.Shop, WeekMask.All, Reliability.Dependable));                         // banana sapling
        var rows = new[]
        {
            new FruitTreeRow("(O)633", new[] { Season.Fall }, new[] { new FruitRow("(O)613", null, 1.0, null) }),
            new FruitTreeRow("(O)628", new[] { Season.Spring }, new[] { new FruitRow("(O)638", null, 1.0, null) }),
            new FruitTreeRow("(O)69", new[] { Season.Summer }, new[] { new FruitRow("(O)91", null, 0.5, "YEAR 2") }),
        };
        var all = GrowSources.FruitTrees(rows, snapshot, new Dictionary<string, ObjInfo>(), NoFestivals).ToList();
        Assert.Equal("9-12", all.Single(s => s.ItemId == "(O)613" && s.Source.Kind == SourceKind.FruitTree).Source.Weeks.ToString());
        Assert.DoesNotContain(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.FruitTree);
        Assert.Contains(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.GreenhouseCrop);
        var banana = all.First(s => s.ItemId == "(O)91").Source;
        Assert.Equal(Reliability.Chance, banana.Reliability);
        Assert.True(banana.Conditions.YearTwo);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityGrowTests"`
Expected: build FAILS, `GrowSources` not found.

- [ ] **Step 3: Append the records to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One Data/Crops row: keyed by seed, <see cref="GrowthDays"/> is the sum of DaysInPhase,
/// <see cref="RegrowDays"/> is CropData.RegrowDays (-1 or 0 means none), <see cref="Seasons"/> empty means any season.</summary>
public sealed record CropRow(string SeedId, string HarvestId, IReadOnlyList<Season> Seasons, int GrowthDays, int RegrowDays);

/// <summary>One fruit a tree grows: its own season overrides the tree's; Chance and Condition are FruitTreeFruitData's.
/// <see cref="ItemId"/> may be an item query; <see cref="IsRandom"/> marks one entry of a RandomItemId list.</summary>
public sealed record FruitRow(string ItemId, Season? Season, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/FruitTrees row, keyed by sapling.</summary>
public sealed record FruitTreeRow(string SaplingId, IReadOnlyList<Season> TreeSeasons, IReadOnlyList<FruitRow> Fruit);
```

- [ ] **Step 4: Write `GrowSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Crops, greenhouse crops, Mixed Seeds and fruit trees, day by day (see the week-in-isolation
/// rule in the plan's Task 6).</summary>
public static class GrowSources
{
    private const string MixedSeedsId = "(O)770";
    private const string GreenhouseUnlock = "mail:ccPantry";   // Farm.cs 1132, GreenhouseBuilding.cs 47
    private const int FruitTreeMaturityDays = 28;              // FruitTree.cs 66
    private const int MinGrowthDays = 1;

    /// <summary>What Mixed Seeds become, by the season of the planting day (Crop.cs 294-320, 414-433).
    /// 473 resolves to 472. Winter picks a random other season's pool; only the greenhouse grows it,
    /// because the greenhouse waives the season check but still reports the outside season (GameLocation.cs 649-651).</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> MixedSeedPools = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)472", "(O)474", "(O)475" },
        [Season.Summer] = new[] { "(O)487", "(O)483", "(O)482", "(O)484" },
        [Season.Fall] = new[] { "(O)487", "(O)488", "(O)489", "(O)490" },
    };

    public static WeekMask Harvest(WeekMask seedWeeks, IReadOnlyList<Season> seasons, int growthDays, int regrowDays)
    {
        WeekMask inSeason = seasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(seasons);
        return Grow(seedWeeks, growthDays, regrowDays, day => inSeason.Contains(WeekMask.WeekOfDay(day)));
    }

    public static WeekMask Greenhouse(WeekMask seedWeeks, int growthDays, int regrowDays)
        => Grow(seedWeeks, growthDays, regrowDays, _ => true);

    public static IEnumerable<(string ItemId, ObtainSource Source)> Crops(IEnumerable<CropRow> rows, ObtainabilityModel snapshot)
    {
        List<CropRow> all = rows.ToList();
        var bySeed = all.GroupBy(r => r.SeedId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (CropRow crop in all.Where(r => r.SeedId != MixedSeedsId))
        {
            WeekMask dep = snapshot.Weeks(crop.SeedId, ObtainFilter.DependableOnly);
            WeekMask any = snapshot.Weeks(crop.SeedId, ObtainFilter.Any);
            var outdoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.Crop,
                Harvest(dep, crop.Seasons, crop.GrowthDays, crop.RegrowDays),
                Harvest(any, crop.Seasons, crop.GrowthDays, crop.RegrowDays), outdoor, $"grown from {crop.SeedId}"))
                yield return (crop.HarvestId, s);
            var indoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId, GreenhouseUnlock } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop,
                Greenhouse(dep, crop.GrowthDays, crop.RegrowDays), Greenhouse(any, crop.GrowthDays, crop.RegrowDays),
                indoor, $"greenhouse, from {crop.SeedId}"))
                yield return (crop.HarvestId, s);
        }

        WeekMask mixed = snapshot.Weeks(MixedSeedsId, ObtainFilter.Any);
        if (mixed.IsEmpty) yield break;
        WeekMask winter = mixed & WeekMask.ForSeason(Season.Winter);
        foreach ((Season season, string[] seeds) in MixedSeedPools)
            foreach (string seed in seeds)
            {
                if (!bySeed.TryGetValue(seed, out CropRow? crop)) continue;
                WeekMask planted = mixed & WeekMask.ForSeason(season);
                WeekMask outdoorWeeks = Harvest(planted, crop.Seasons, crop.GrowthDays, crop.RegrowDays);
                if (!outdoorWeeks.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.Crop, outdoorWeeks, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId } }, $"Mixed Seeds in {season}"));
                WeekMask indoorWeeks = Greenhouse(planted | winter, crop.GrowthDays, crop.RegrowDays);
                if (!indoorWeeks.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.GreenhouseCrop, indoorWeeks, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId, GreenhouseUnlock } },
                        $"Mixed Seeds in the greenhouse ({season} pool)"));
            }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FruitTrees(
        IEnumerable<FruitTreeRow> rows, ObtainabilityModel snapshot, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (FruitTreeRow tree in rows)
        {
            WeekMask depMature = Mature(snapshot.Weeks(tree.SaplingId, ObtainFilter.DependableOnly));
            WeekMask anyMature = Mature(snapshot.Weeks(tree.SaplingId, ObtainFilter.Any));
            foreach (FruitRow fruit in tree.Fruit)
            {
                ConditionReading reading = ConditionSeasons.Read(fruit.Condition, festivals);
                WeekMask season = (fruit.Season is Season s ? WeekMask.ForSeason(s)
                    : tree.TreeSeasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(tree.TreeSeasons)) & reading.Weeks;
                bool luck = fruit.Chance < 1.0 || reading.Chance || fruit.IsRandom;
                ObtainConditions outdoor = ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId } }, reading);
                foreach (ObtainSource src in SourcePair.Of(SourceKind.FruitTree, luck ? WeekMask.None : depMature & season,
                    anyMature & season, outdoor, $"fruit tree from {tree.SaplingId}"))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
                ObtainConditions indoor = ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId, GreenhouseUnlock } }, reading);
                foreach (ObtainSource src in SourcePair.Of(SourceKind.GreenhouseCrop, luck ? WeekMask.None : depMature & reading.Weeks,
                    anyMature & reading.Weeks, indoor, $"fruit tree in the greenhouse from {tree.SaplingId}"))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
            }
        }
    }

    /// <summary>Every week a tree planted from a sapling obtainable that day is mature.</summary>
    private static WeekMask Mature(WeekMask saplingWeeks)
    {
        WeekMask result = WeekMask.None;
        for (int plantDay = 1; plantDay <= Calendar.DaysPerYear; plantDay++)
        {
            if (!saplingWeeks.Contains(WeekMask.WeekOfDay(plantDay))) continue;
            int matureDay = plantDay + FruitTreeMaturityDays;
            if (matureDay > Calendar.DaysPerYear) break;
            return WeekMask.FromWeekOnwardOf(WeekMask.WeekOfDay(matureDay));
        }
        return result;
    }

    /// <summary>The day-by-day core: plant on any day whose week has seed and which <paramref name="canGrow"/>,
    /// harvest after growth if every day in between can grow, then every regrow interval while it still can.</summary>
    private static WeekMask Grow(WeekMask seedWeeks, int growthDays, int regrowDays, Func<int, bool> canGrow)
    {
        int growth = Math.Max(MinGrowthDays, growthDays);
        WeekMask result = WeekMask.None;
        for (int plantDay = 1; plantDay <= Calendar.DaysPerYear; plantDay++)
        {
            if (!seedWeeks.Contains(WeekMask.WeekOfDay(plantDay)) || !canGrow(plantDay)) continue;
            int harvestDay = plantDay + growth;
            if (harvestDay > Calendar.DaysPerYear || !GrowsThrough(plantDay, harvestDay, canGrow)) continue;
            result |= WeekMask.Of(WeekMask.WeekOfDay(harvestDay));
            if (regrowDays <= 0) continue;
            for (int next = harvestDay + regrowDays; next <= Calendar.DaysPerYear && GrowsThrough(harvestDay, next, canGrow); next += regrowDays)
                result |= WeekMask.Of(WeekMask.WeekOfDay(next));
        }
        return result;
    }

    private static bool GrowsThrough(int fromDay, int toDay, Func<int, bool> canGrow)
    {
        for (int day = fromDay; day <= toDay; day++)
            if (!canGrow(day)) return false;
        return true;
    }
}
```

Notes for the implementer, checked against the tests:
- Spring seed, 4 days, no regrow: plant days 1-24 harvest days 5-28, all Spring: weeks 1-4.
- Corn from all of Summer and Fall, 14 days, regrow 4: first harvest day 43 (week 7), regrowth to day 84: weeks 7-12. From week 5 only, regrow 21: harvests on days 43 and 64 (weeks 7 and 10); day 85 is Winter and stops it.
- Greenhouse, Spring seed, 4 days: plant days 1-28 harvest days 5-32: weeks 1-5. With regrow 1 it yields every day to day 112: weeks 1-16.
- Mixed Seeds in the greenhouse for the Spring pool: Spring plantings harvest in weeks 1-5; Winter plantings (days 85-108) in weeks 13-16; nothing in Summer or Fall.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityGrowTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityGrowTests.cs
git commit -m "obtainability: crops, greenhouse, Mixed Seeds and fruit trees, day by day, with no stored seed"
git push origin story
```

---

### Task 7: Made sources (machines, recipes, animals, fish ponds, tappers, geode contents)

**Week-in-isolation rule (Jeff, 2026-09-14):** "assume they've saved nothing." A made item counts in a
week only when its inputs can be obtained then, with processing time added day by day. A recipe needs
**every ingredient obtainable in the same week** (the intersection). Things that are built once and keep
producing (a fish pond, an animal, a tapper on a tree) count from the first week they can be set up, like
a fruit tree growing.

These rules read a snapshot model (the previous resolution pass).

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/MadeSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs`

**Interfaces:**
- Consumes: Task 1 (including `SourcePair.Of`); `ConditionSeasons` (Task 2); `ObjInfo`, `ItemQueries` (Task 3).
- Produces:
  - `record MachineOutput(string? ItemId, string? Condition, string? OutputMethod, bool IsRandom = false)`
  - `record MachineRow(string MachineId, string? RequiredItemId, IReadOnlyList<string> RequiredTags, string? TriggerCondition, IReadOnlyList<MachineOutput> Outputs, int MinutesUntilReady, int DaysUntilReady, bool UseFirstValidOutput = false)`
  - `record RecipeRow(string Name, IReadOnlyList<string> Ingredients, string OutputId, string Unlock, bool IsCooking, IReadOnlyList<string>? AlternateOutputIds = null)`
  - `record AnimalProduce(string ItemId, string? Condition, int MinimumFriendship)`
  - `record AnimalRow(string AnimalId, string House, int PurchasePrice, IReadOnlyList<AnimalProduce> Produce, IReadOnlyList<AnimalProduce> DeluxeProduce, int DeluxeMinimumFriendship = 200)`
  - `record PondProduct(string ItemId, int RequiredPopulation, double Chance, string? Condition, bool IsRandom = false)`
  - `record PondRow(string Id, IReadOnlyList<string> RequiredTags, int Precedence, IReadOnlyList<PondProduct> Products)`
  - `record TapRow(string TreeId, string ItemId, int DaysUntilReady, Season? Season, double Chance, string? Condition, bool IsRandom = false)`
  - `record GeodeDropRow(string GeodeId, string ItemId, double Chance, string? Condition)`
  - `static int MadeSources.ProcessingDays(int minutes, int days)`
  - `static WeekMask MadeSources.ShiftByDays(WeekMask inputWeeks, int days)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MadeSources.Machines(IEnumerable<MachineRow>, IReadOnlyDictionary<string, ObjInfo>, ObtainabilityModel, IReadOnlyDictionary<string, FestivalDates>)`
  - `... MadeSources.Recipes(IEnumerable<RecipeRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, WeekMask> recipeShopWeeks, ObtainabilityModel)`
  - `... MadeSources.Animals(IEnumerable<AnimalRow>, IReadOnlyDictionary<string, FestivalDates>)`
  - `... MadeSources.Ponds(IEnumerable<PondRow>, IReadOnlyDictionary<string, ObjInfo>, ObtainabilityModel, IReadOnlyDictionary<string, FestivalDates>)`
  - `... MadeSources.Tappers(IEnumerable<TapRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)`
  - `... MadeSources.Geodes(IEnumerable<GeodeDropRow>, IReadOnlyCollection<string> geodesUsingDefaultTable, IReadOnlyDictionary<string, ObjInfo>, ObtainabilityModel, IReadOnlyDictionary<string, FestivalDates>)`

Game facts used (PC 1.6 decompile):
- A trigger with both `RequiredItemId` and `RequiredTags` needs both (MachineDataUtility.cs 81-89).
- Ready time is `DaysUntilReady` when it is 0 or more, else `MinutesUntilReady` (Object.cs 2508-2510). Minutes become whole days rounded up.
- `OutputMethod` replaces the output item with code (MachineDataUtility.cs 204-213): recorded as unresolved.
- `DROP_IN` as the output id means the input item itself (MachineDataUtility.cs 214).
- A fish uses the pond definition with the LOWEST `Precedence` whose tags all match; the first one wins a tie (FishPond.cs 217-224).
- Default geode contents (Utility.cs 6397-6647) apply when `GeodeDropsDefaultItems` is set.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityMadeTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();

    private static ObtainabilityModel Snapshot(params (string Id, WeekMask Weeks, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(SourceKind.Forage, r.Weeks, r.R, ObtainConditions.None, "test")).ToList()));

    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)613"] = new ObjInfo("(O)613", "Apple", -79, 100, new[] { "category_fruits", "id_o_613" }, false),
        ["(O)254"] = new ObjInfo("(O)254", "Melon", -79, 250, new[] { "category_fruits", "id_o_254" }, false),
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new[] { "fish_carp", "fish_pond" }, false),
    };

    private static MachineRow Rule(string machine, string? id, string[] tags, int minutes, int days, params MachineOutput[] outputs)
        => new(machine, id, tags, null, outputs, minutes, days);

    [Fact]
    public void Ready_time_prefers_days_and_rounds_minutes_up_to_days()
    {
        Assert.Equal(7, MadeSources.ProcessingDays(10000, -1));
        Assert.Equal(3, MadeSources.ProcessingDays(4000, -1));
        Assert.Equal(2, MadeSources.ProcessingDays(10000, 2));
        Assert.Equal(WeekMask.Of(4), MadeSources.ShiftByDays(WeekMask.Of(3), 7));
        Assert.Equal("3-4", MadeSources.ShiftByDays(WeekMask.Of(3), 3).ToString());
        Assert.Equal(WeekMask.None, MadeSources.ShiftByDays(WeekMask.Of(16), 7));
    }

    [Fact]
    public void A_keg_makes_wine_the_week_after_its_fruit()
    {
        var snapshot = Snapshot(("(O)613", WeekMask.ForSeason(Season.Fall), Reliability.Dependable),
                                ("(O)254", WeekMask.ForSeason(Season.Summer), Reliability.Dependable));
        var rows = new[] { Rule("(BC)12", null, new[] { "category_fruits" }, 10000, -1, new MachineOutput("FLAVORED_ITEM Wine DROP_IN_ID", null, null)) };
        var wine = MadeSources.Machines(rows, Objects, snapshot, NoFestivals).Single();
        Assert.Equal("(O)348", wine.ItemId);
        Assert.Equal("6-13", wine.Source.Weeks.ToString());
        Assert.Contains("machine:(BC)12", wine.Source.Conditions.Requires);
    }

    [Fact]
    public void A_trigger_with_an_id_and_tags_needs_both()
    {
        var snapshot = Snapshot(("(O)613", WeekMask.All, Reliability.Dependable));
        var rows = new[] { Rule("(BC)X", "(O)613", new[] { "category_vegetable" }, 60, -1, new MachineOutput("(O)900", null, null)) };
        Assert.Empty(MadeSources.Machines(rows, Objects, snapshot, NoFestivals));
    }

    [Fact]
    public void Drop_in_outputs_the_input_and_output_methods_are_unresolved()
    {
        var snapshot = Snapshot(("(O)142", WeekMask.Of(9), Reliability.Dependable));
        var dropIn = MadeSources.Machines(new[] { Rule("(BC)Y", null, new[] { "fish_carp" }, 0, 1, new MachineOutput("DROP_IN", null, null)) },
            Objects, snapshot, NoFestivals).Single();
        Assert.Equal("(O)142", dropIn.ItemId);
        Assert.Equal("9-10", dropIn.Source.Weeks.ToString());

        var method = MadeSources.Machines(new[] { Rule("(BC)25", null, new[] { "fish_carp" }, 0, 1, new MachineOutput(null, null, "Object.OutputSeedMaker")) },
            Objects, snapshot, NoFestivals).Single();
        Assert.StartsWith(ItemQueries.UnresolvedPrefix, method.ItemId);
        Assert.True(method.Source.Conditions.Unresolved);
    }

    [Fact]
    public void Output_conditions_narrow_and_several_outputs_are_chance()
    {
        var snapshot = Snapshot(("(O)24", WeekMask.All, Reliability.Dependable));
        var rows = new[] { Rule("(BC)Z", "(O)24", new string[0], 0, 0,
            new MachineOutput("(O)901", "SEASON Summer", null), new MachineOutput("(O)902", null, null)) };
        var list = MadeSources.Machines(rows, Objects, snapshot, NoFestivals).ToList();
        var summer = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(WeekMask.ForSeason(Season.Summer), summer.Weeks);
        Assert.All(list, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
    }

    [Fact]
    public void First_valid_outputs_are_tried_in_order()
    {
        var snapshot = Snapshot(("(O)24", WeekMask.All, Reliability.Dependable));
        var rows = new[] { new MachineRow("(BC)F", "(O)24", new string[0], null, new[]
            { new MachineOutput("(O)901", "SEASON Summer", null), new MachineOutput("(O)902", null, null) },
            0, 0, UseFirstValidOutput: true) };
        var list = MadeSources.Machines(rows, Objects, snapshot, NoFestivals).ToList();
        var first = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(WeekMask.ForSeason(Season.Summer), first.Weeks);
        Assert.Equal(Reliability.Dependable, first.Reliability);
        var second = list.Single(x => x.ItemId == "(O)902").Source;
        Assert.Equal(WeekMask.All.Except(WeekMask.ForSeason(Season.Summer)), second.Weeks);   // never in summer
        Assert.Equal(Reliability.Dependable, second.Reliability);
    }

    [Fact]
    public void A_recipe_needs_every_ingredient_in_the_same_week_and_a_shop_recipe_once_learned()
    {
        var snapshot = Snapshot(("(O)24", WeekMask.ForSeason(Season.Spring), Reliability.Dependable),
                                ("(O)613", WeekMask.Range(3, 10), Reliability.Chance));
        var rows = new[]
        {
            new RecipeRow("Test Dish", new[] { "(O)24", "-79" }, "(O)900", "default", true),
            new RecipeRow("Shop Dish", new[] { "(O)24" }, "(O)901", "none", true),
            new RecipeRow("Skill Craft", new[] { "(O)24" }, "(BC)902", "s Farming 3", false),
            new RecipeRow("Split Seasons", new[] { "(O)24", "(O)254" }, "(O)903", "default", true),
            new RecipeRow("Taught Elsewhere", new[] { "(O)24" }, "(O)904", "none", true),
            new RecipeRow("Either Output", new[] { "(O)24" }, "(O)905", "default", false, new[] { "(O)906" }),
        };
        var shopWeeks = new Dictionary<string, WeekMask> { ["(O)901"] = WeekMask.Of(3) };
        var list = MadeSources.Recipes(rows, Objects, shopWeeks, snapshot).ToList();
        var dish = list.Single(x => x.ItemId == "(O)900").Source;
        Assert.Equal("3-4", dish.Weeks.ToString());
        Assert.Equal(Reliability.Chance, dish.Reliability);
        Assert.Equal("3-4", list.Single(x => x.ItemId == "(O)901").Source.Weeks.ToString());
        var craft = list.Single(x => x.ItemId == "(BC)902").Source;
        Assert.Equal(SourceKind.Crafting, craft.Kind);
        Assert.Equal("Farming", craft.Conditions.Skill);
        Assert.Equal(3, craft.Conditions.SkillLevel);
        Assert.DoesNotContain(list, x => x.ItemId == "(O)903");   // melon is not obtainable in spring and nothing is stored
        Assert.False(list.Single(x => x.ItemId == "(O)901").Source.Conditions.Unresolved);   // "none", but a shop teaches it
        Assert.True(list.Single(x => x.ItemId == "(O)904").Source.Conditions.Unresolved);    // "none" and no shop: taught somewhere unknown
        Assert.Equal(Reliability.Chance, list.Single(x => x.ItemId == "(O)905").Source.Reliability);
        Assert.Equal(Reliability.Chance, list.Single(x => x.ItemId == "(O)906").Source.Reliability);
    }

    [Fact]
    public void Ponds_use_only_the_lowest_precedence_match_and_carry_forward()
    {
        var snapshot = Snapshot(("(O)142", WeekMask.Of(5), Reliability.Dependable));
        var ponds = new[]
        {
            new PondRow("Generic", new[] { "fish_pond" }, 10, new[] { new PondProduct("(O)900", 1, 1.0, null) }),
            new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 1, 0.5, null), new PondProduct("(O)901", 5, 1.0, "SEASON Winter") }),
        };
        var list = MadeSources.Ponds(ponds, Objects, snapshot, NoFestivals).ToList();
        Assert.DoesNotContain(list, x => x.ItemId == "(O)900");
        var roe = list.Single(x => x.ItemId == "(O)812").Source;
        Assert.Equal("5-16", roe.Weeks.ToString());
        Assert.Equal(Reliability.Chance, roe.Reliability);
        Assert.Equal(WeekMask.ForSeason(Season.Winter), list.Single(x => x.ItemId == "(O)901").Source.Weeks);
    }

    [Fact]
    public void Animals_tappers_and_geodes_respect_their_conditions()
    {
        var animals = MadeSources.Animals(new[]
        {
            new AnimalRow("White Chicken", "Coop", 800,
                new[] { new AnimalProduce("(O)176", null, 0) },
                new[] { new AnimalProduce("(O)174", "SEASON Spring", 200) }),
        }, NoFestivals).ToList();
        Assert.Equal(WeekMask.All, animals.Single(a => a.ItemId == "(O)176").Source.Weeks);
        var large = animals.Single(a => a.ItemId == "(O)174").Source;
        Assert.Equal(WeekMask.ForSeason(Season.Spring), large.Weeks);
        Assert.Contains("friendship:White Chicken 200", large.Conditions.Requires);

        var taps = MadeSources.Tappers(new[] { new TapRow("7", "(O)422", 4, Season.Fall, 0.9, null) }, Objects, NoFestivals).Single().Source;
        Assert.Equal(WeekMask.ForSeason(Season.Fall), taps.Weeks);
        Assert.Equal(Reliability.Chance, taps.Reliability);

        var snapshot = Snapshot(("(O)535", WeekMask.Range(2, 3), Reliability.Chance));
        var geode = MadeSources.Geodes(new GeodeDropRow[0], new[] { "(O)535" }, Objects, snapshot, NoFestivals).ToList();
        Assert.Contains(geode, g => g.ItemId == "(O)86" && g.Source.Weeks.ToString() == "2-3");
        Assert.All(geode, g => Assert.Equal(SourceKind.Geode, g.Source.Kind));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMadeTests"`
Expected: build FAILS, `MachineRow` not found.

- [ ] **Step 3: Append the records to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One output of a machine rule: an id or item query, "DROP_IN", or an OutputMethod (code).
/// <see cref="IsRandom"/> marks one entry of a RandomItemId list.</summary>
public sealed record MachineOutput(string? ItemId, string? Condition, string? OutputMethod, bool IsRandom = false);

/// <summary>One Data/Machines output rule x trigger. No required item and no tags means the machine
/// needs no input (a Bee House, a Mushroom Log). Ready time: DaysUntilReady when 0 or more, else minutes.
/// <see cref="UseFirstValidOutput"/> is MachineOutputRule.UseFirstValidOutput: outputs are tried in order,
/// not picked at random.</summary>
public sealed record MachineRow(
    string MachineId, string? RequiredItemId, IReadOnlyList<string> RequiredTags, string? TriggerCondition,
    IReadOnlyList<MachineOutput> Outputs, int MinutesUntilReady, int DaysUntilReady, bool UseFirstValidOutput = false);

/// <summary>A cooking or crafting recipe. Ingredients are qualified ids or negative category numbers.
/// <see cref="Unlock"/> is the raw unlock field ("default", "none", "Farming 3", "s Farming 3", "f Robin 7", "l 4").
/// <see cref="AlternateOutputIds"/> are the other outputs of a recipe that picks one at random (CraftingRecipe.cs 57, 127-131).</summary>
public sealed record RecipeRow(
    string Name, IReadOnlyList<string> Ingredients, string OutputId, string Unlock, bool IsCooking,
    IReadOnlyList<string>? AlternateOutputIds = null);

public sealed record AnimalProduce(string ItemId, string? Condition, int MinimumFriendship);

/// <summary>One Data/FarmAnimals row. <see cref="DeluxeMinimumFriendship"/> is FarmAnimalData.DeluxeProduceMinimumFriendship.</summary>
public sealed record AnimalRow(
    string AnimalId, string House, int PurchasePrice, IReadOnlyList<AnimalProduce> Produce, IReadOnlyList<AnimalProduce> DeluxeProduce,
    int DeluxeMinimumFriendship = 200);

public sealed record PondProduct(string ItemId, int RequiredPopulation, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/FishPondData entry. A fish lives under the matching entry with the lowest Precedence.</summary>
public sealed record PondRow(string Id, IReadOnlyList<string> RequiredTags, int Precedence, IReadOnlyList<PondProduct> Products);

public sealed record TapRow(string TreeId, string ItemId, int DaysUntilReady, Season? Season, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/Objects GeodeDrops entry for a geode item.</summary>
public sealed record GeodeDropRow(string GeodeId, string ItemId, double Chance, string? Condition);
```

- [ ] **Step 4: Write `MadeSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Machines, cooking and crafting, animals, fish ponds, tappers and geode contents. See the
/// week-in-isolation rule in the plan's Task 7.</summary>
public static class MadeSources
{
    private const int MinutesPerDay = 1440;
    private const string NotTag = "!";
    private const string DropIn = "DROP_IN";
    private const string SkillUnlockPrefix = "s";
    private const string GeodeOpener = "shop:Blacksmith";
    private static readonly string[] KnownFromStartWords = { "default", "" };
    private static readonly string[] TaughtElsewhereWords = { "none", "null" };
    private static readonly string[] Skills = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };

    /// <summary>Code-only default geode contents (Utility.cs getTreasureFromGeode 6397-6647).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> DefaultGeodeTable = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["(O)535"] = new[] { "(O)390", "(O)330", "(O)86", "(O)378", "(O)380", "(O)382" },
        ["(O)536"] = new[] { "(O)390", "(O)330", "(O)84", "(O)378", "(O)380", "(O)382", "(O)384" },
        ["(O)749"] = new[] { "(O)390", "(O)330", "(O)82", "(O)84", "(O)86", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" },
    };
    private static readonly string[] OtherGeodeDefault = { "(O)390", "(O)330", "(O)82", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" };

    public static int ProcessingDays(int minutes, int days)
        => days >= 0 ? days : (int)Math.Ceiling(Math.Max(0, minutes) / (double)MinutesPerDay);

    /// <summary>Weeks an output can come out, when the input can be obtained on any day of
    /// <paramref name="inputWeeks"/> and takes <paramref name="days"/> to process; past Winter 28 is gone.</summary>
    public static WeekMask ShiftByDays(WeekMask inputWeeks, int days)
    {
        if (days <= 0) return inputWeeks;
        WeekMask result = WeekMask.None;
        for (int day = 1; day + days <= Calendar.DaysPerYear; day++)
            if (inputWeeks.Contains(WeekMask.WeekOfDay(day)))
                result |= WeekMask.Of(WeekMask.WeekOfDay(day + days));
        return result;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Machines(
        IEnumerable<MachineRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (MachineRow rule in rows)
        {
            ConditionReading trigger = ConditionSeasons.Read(rule.TriggerCondition, festivals);
            bool noInput = rule.RequiredItemId == null && rule.RequiredTags.Count == 0;
            List<string> inputs = noInput ? new List<string>() : Inputs(rule, objects).ToList();
            if (!noInput && inputs.Count == 0) continue;
            int days = ProcessingDays(rule.MinutesUntilReady, rule.DaysUntilReady);
            bool several = rule.Outputs.Count > 1 && !rule.UseFirstValidOutput;
            string inputText = noInput ? "no input" : rule.RequiredItemId ?? string.Join(" ", rule.RequiredTags);

            // With UseFirstValidOutput the game takes the first output whose condition passes, so a
            // later output only happens in weeks no earlier, surely valid output already covers.
            WeekMask shadowed = WeekMask.None;
            foreach (MachineOutput output in rule.Outputs)
            {
                ConditionReading outCond = ConditionSeasons.Read(output.Condition, festivals);
                WeekMask gate = (trigger.Weeks & outCond.Weeks).Except(shadowed);
                if (rule.UseFirstValidOutput && output.OutputMethod == null && !outCond.Chance && !string.IsNullOrWhiteSpace(output.ItemId))
                    shadowed |= gate;
                bool luck = several || output.IsRandom || trigger.Chance || outCond.Chance;
                ObtainConditions conditions = ConditionSeasons.Apply(ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "machine:" + rule.MachineId } }, trigger), outCond);
                string detail = $"{rule.MachineId} from {inputText}";

                if (output.OutputMethod != null)
                {
                    yield return (ItemQueries.UnresolvedPrefix + "machine " + rule.MachineId, new ObtainSource(
                        SourceKind.Other, WeekMask.All, Reliability.Chance, conditions with { Unresolved = true },
                        $"{detail}: output method {output.OutputMethod}"));
                    continue;
                }

                if (output.ItemId == DropIn)
                {
                    foreach (string input in inputs)
                        foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine,
                            luck ? WeekMask.None : ShiftByDays(snapshot.Weeks(input, ObtainFilter.DependableOnly), days) & gate,
                            ShiftByDays(snapshot.Weeks(input, ObtainFilter.Any), days) & gate, conditions, detail))
                            yield return (input, s);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(output.ItemId)) continue;

                WeekMask dep = noInput ? WeekMask.All : WeekMask.None, any = dep;
                foreach (string input in inputs)
                {
                    dep |= snapshot.Weeks(input, ObtainFilter.DependableOnly);
                    any |= snapshot.Weeks(input, ObtainFilter.Any);
                }
                dep = luck ? WeekMask.None : ShiftByDays(dep, days) & gate;
                any = ShiftByDays(any, days) & gate;
                foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, dep, any, conditions, detail))
                    foreach (var emitted in ItemQueries.Emit(output.ItemId, objects, s))
                        yield return emitted;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Recipes(
        IEnumerable<RecipeRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, WeekMask> recipeShopWeeks, ObtainabilityModel snapshot)
    {
        foreach (RecipeRow recipe in rows)
        {
            WeekMask dep = WeekMask.All, any = WeekMask.All;
            foreach (string ingredient in recipe.Ingredients)
            {
                (WeekMask d, WeekMask a) = IngredientWeeks(ingredient, objects, snapshot);
                dep &= d;
                any &= a;
            }
            bool taughtByShop = recipeShopWeeks.TryGetValue(recipe.OutputId, out WeekMask taught);
            if (taughtByShop)
            {
                dep &= taught.FromWeekOnward();
                any &= taught.FromWeekOnward();
            }
            var outputs = new List<string> { recipe.OutputId };
            if (recipe.AlternateOutputIds != null) outputs.AddRange(recipe.AlternateOutputIds);
            if (outputs.Count > 1) dep = WeekMask.None;   // one output picked at random
            SourceKind kind = recipe.IsCooking ? SourceKind.Cooking : SourceKind.Crafting;
            ObtainConditions conditions = UnlockConditions(recipe, taughtByShop);
            foreach (string output in outputs)
                foreach (ObtainSource s in SourcePair.Of(kind, dep, any, conditions, $"recipe {recipe.Name}"))
                    yield return (output, s);
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Animals(
        IEnumerable<AnimalRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (AnimalRow animal in rows)
        {
            var requires = new List<string> { "building:" + animal.House };
            if (animal.PurchasePrice <= 0) requires.Add("animal:" + animal.AnimalId + " (not sold)");
            foreach ((AnimalProduce produce, bool deluxe) in animal.Produce.Select(p => (p, false)).Concat(animal.DeluxeProduce.Select(p => (p, true))))
            {
                ConditionReading reading = ConditionSeasons.Read(produce.Condition, festivals);
                if (reading.Weeks.IsEmpty) continue;
                var extra = new List<string>(requires);
                int friendship = Math.Max(produce.MinimumFriendship, deluxe ? animal.DeluxeMinimumFriendship : 0);
                if (friendship > 0) extra.Add($"friendship:{animal.AnimalId} {friendship}");
                if (deluxe) extra.Add("deluxe produce");
                // Several produce entries in one list: the animal produces one of them.
                bool picked = (deluxe ? animal.DeluxeProduce.Count : animal.Produce.Count) > 1;
                ObtainConditions conditions = ConditionSeasons.Apply(ObtainConditions.None with { Requires = extra }, reading);
                yield return (produce.ItemId, new ObtainSource(SourceKind.Animal, reading.Weeks,
                    reading.Chance || picked ? Reliability.Chance : Reliability.Dependable, conditions, $"{animal.AnimalId} produce"));
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Ponds(
        IEnumerable<PondRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        List<PondRow> ordered = rows.ToList();
        var fishByPond = new Dictionary<PondRow, List<ObjInfo>>();
        foreach (ObjInfo fish in objects.Values)
        {
            PondRow? winner = null;
            foreach (PondRow pond in ordered)
                if ((winner == null || pond.Precedence < winner.Precedence) && MatchesTags(fish, pond.RequiredTags))
                    winner = pond;
            if (winner == null) continue;
            if (!fishByPond.TryGetValue(winner, out List<ObjInfo>? list)) fishByPond[winner] = list = new List<ObjInfo>();
            list.Add(fish);
        }

        foreach ((PondRow pond, List<ObjInfo> fishes) in fishByPond)
        {
            WeekMask dep = WeekMask.None, any = WeekMask.None;
            foreach (ObjInfo fish in fishes)
            {
                dep |= snapshot.Weeks(fish.QualifiedId, ObtainFilter.DependableOnly);
                any |= snapshot.Weeks(fish.QualifiedId, ObtainFilter.Any);
            }
            dep = dep.FromWeekOnward();
            any = any.FromWeekOnward();
            foreach (PondProduct product in pond.Products)
            {
                ConditionReading reading = ConditionSeasons.Read(product.Condition, festivals);
                bool luck = product.Chance < 1.0 || reading.Chance || product.IsRandom;
                ObtainConditions conditions = ConditionSeasons.Apply(ObtainConditions.None with
                {
                    Requires = new[] { "building:Fish Pond", $"pond population {product.RequiredPopulation}" },
                }, reading);
                foreach (ObtainSource s in SourcePair.Of(SourceKind.FishPond,
                    luck ? WeekMask.None : dep & reading.Weeks, any & reading.Weeks, conditions, $"fish pond {pond.Id}"))
                    foreach (var emitted in ItemQueries.Emit(product.ItemId, objects, s))
                        yield return emitted;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Tappers(
        IEnumerable<TapRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (TapRow tap in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(tap.Condition, festivals);
            WeekMask weeks = reading.Weeks & (tap.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All);
            if (weeks.IsEmpty) continue;
            var template = new ObtainSource(SourceKind.Tapper, weeks,
                tap.Chance < 1.0 || reading.Chance || tap.IsRandom ? Reliability.Chance : Reliability.Dependable,
                ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "crafting:Tapper", "tree:" + tap.TreeId } }, reading),
                $"tapper on tree {tap.TreeId}, {tap.DaysUntilReady} days");
            foreach (var emitted in ItemQueries.Emit(tap.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Geodes(
        IEnumerable<GeodeDropRow> rows, IReadOnlyCollection<string> geodesUsingDefaultTable,
        IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var drops = rows.Select(r => (r.GeodeId, r.ItemId, r.Condition)).ToList();
        foreach (string geode in geodesUsingDefaultTable)
            foreach (string id in DefaultGeodeTable.TryGetValue(geode, out string[]? table) ? table : OtherGeodeDefault)
                drops.Add((geode, id, null));
        foreach ((string geode, string item, string? condition) in drops.Distinct())
        {
            ConditionReading reading = ConditionSeasons.Read(condition, festivals);
            WeekMask weeks = snapshot.Weeks(geode, ObtainFilter.Any) & reading.Weeks;
            if (weeks.IsEmpty) continue;
            var template = new ObtainSource(SourceKind.Geode, weeks, Reliability.Chance,
                ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + geode, GeodeOpener } }, reading),
                $"opened from {geode}");
            foreach (var emitted in ItemQueries.Emit(item, objects, template))
                yield return emitted;
        }
    }

    private static IEnumerable<string> Inputs(MachineRow rule, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        if (rule.RequiredItemId != null && rule.RequiredTags.Count == 0)
            return new[] { BundleParsing.NormalizeItemId(rule.RequiredItemId) };
        string? id = rule.RequiredItemId == null ? null : BundleParsing.NormalizeItemId(rule.RequiredItemId);
        return objects.Values
            .Where(o => (id == null || o.QualifiedId == id) && MatchesTags(o, rule.RequiredTags))
            .Select(o => o.QualifiedId);
    }

    private static (WeekMask Dependable, WeekMask Any) IngredientWeeks(
        string ingredient, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot)
    {
        if (!int.TryParse(ingredient, out int category) || category >= 0)
            return (snapshot.Weeks(ingredient, ObtainFilter.DependableOnly), snapshot.Weeks(ingredient, ObtainFilter.Any));
        WeekMask dep = WeekMask.None, any = WeekMask.None;
        foreach (ObjInfo o in objects.Values.Where(o => o.Category == category))
        {
            dep |= snapshot.Weeks(o.QualifiedId, ObtainFilter.DependableOnly);
            any |= snapshot.Weeks(o.QualifiedId, ObtainFilter.Any);
        }
        return (dep, any);
    }

    private static bool MatchesTags(ObjInfo item, IReadOnlyList<string> tags)
        => tags.Count > 0 && tags.All(tag => tag.StartsWith(NotTag, StringComparison.Ordinal)
            ? !item.ContextTags.Contains(tag.Substring(1))
            : item.ContextTags.Contains(tag));

    private static ObtainConditions UnlockConditions(RecipeRow recipe, bool taughtByShop)
    {
        string[] tokens = recipe.Unlock.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var requires = new List<string> { "recipe:" + recipe.Name };
        string first = tokens.Length == 0 ? "" : tokens[0].ToLowerInvariant();
        if (KnownFromStartWords.Contains(first))
            return ObtainConditions.None with { Requires = requires };
        if (TaughtElsewhereWords.Contains(first))
        {
            // No automatic unlock: a shop, a letter, a friend or an event teaches it. A shop the model
            // read is enough; otherwise the week the player learns it is unknown.
            requires.Add(taughtByShop ? "unlock:shop" : "unlock:none (taught some other way)");
            return ObtainConditions.None with { Requires = requires, Unresolved = !taughtByShop };
        }
        int start = tokens[0] == SkillUnlockPrefix ? 1 : 0;
        if (tokens.Length > start + 1 && Skills.Contains(tokens[start]) && int.TryParse(tokens[start + 1], out int level))
            return ObtainConditions.None with { Skill = tokens[start], SkillLevel = level, Requires = requires };
        // "f Robin 7" (friendship), "l 4" (farmhouse level) and anything else stay as a note.
        requires.Add("unlock:" + recipe.Unlock);
        return ObtainConditions.None with { Requires = requires };
    }
}
```

Notes for the implementer, checked against the tests:
- Wine: apple Fall (9-12) and melon Summer (5-8), 10000 minutes is 7 days, so each week moves one later: 6-9 and 10-13, together 6-13. `FLAVORED_ITEM Wine DROP_IN_ID` resolves to `(O)348`.
- DROP_IN carp in week 9 with one day: days 57-63 come out on days 58-64, weeks 9-10.
- The pond "Generic" matches carp but has precedence 10; "Carp" has 0 and wins, so only its products appear. Carp in week 5 carries forward: 5-16.
- Recipes: "Taught Elsewhere" has unlock "none" and no shop row, so it is unresolved; "Either Output" picks (O)905 or (O)906, so both are chance with parsnip's weeks 1-4.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMadeTests"`
Expected: PASS, 9 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs
git commit -m "obtainability: machines, recipes, animals, ponds, tappers and geode contents, week in isolation"
git push origin story
```

---

### Task 8: The builder, repeated passes to convergence, and diagnostics

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append the `ObtainabilityInputs` bundle)
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityBuilderTests.cs`

**Interfaces:**
- Consumes: every source class from Tasks 3-7, with the signatures those tasks produce.
- Produces:
  - `sealed record ObtainabilityInputs` with init properties `Objects`, `Festivals`, `Forage`, `LocationFish`, `FishRows`, `ArtifactSpots`, `Garbage`, `Shops`, `MonsterDrops`, `Crops`, `FruitTrees`, `Machines`, `Recipes`, `Animals`, `Ponds`, `TapItems`, `GeodeDrops`, `GeodesUsingDefaultTable` (all default to empty)
  - `sealed record ObtainabilityBuild(ObtainabilityModel Model, int Passes, bool HitPassCap, IReadOnlyList<string> Unresolved)`
  - `static ObtainabilityBuild ObtainabilityBuilder.Build(ObtainabilityInputs inputs)` and `const int MaxPasses = 1000`

Every derived rule only ever gains weeks when its inputs gain weeks (unions, intersections, shifts and
"from the first week on" are all monotone), and there are finitely many item-week bits, so the passes
always settle. Settling compares each item's source count as well as its weeks, so a pass that only adds
a source (a new chance route in weeks already covered) still gets one more pass. `MaxPasses` is a guard against a future non-monotone rule, far above any real chain; the
caller logs a warning if it is ever hit.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityBuilderTests
{
    private static ObtainabilityInputs Farm() => new()
    {
        Objects = new Dictionary<string, ObjInfo>
        {
            ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
            ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new[] { "fish_carp" }, false),
            ["(O)812"] = new ObjInfo("(O)812", "Roe", -4, 30, new[] { "roe_item" }, false),
        },
        Shops = new[] { new ShopRow("SeedShop", "(O)472", "SEASON Spring", false) },
        Crops = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) },
        Machines = new[]
        {
            new MachineRow("(BC)15", null, new[] { "category_vegetable" }, null, new[] { new MachineOutput("FLAVORED_ITEM Pickle DROP_IN_ID", null, null) }, 4000, -1),
            new MachineRow("(BC)Preserves", null, new[] { "roe_item" }, null, new[] { new MachineOutput("(O)447", null, null) }, 0, 3),
        },
        LocationFish = new[] { new LocationSpawn("Forest", "(O)142", Season.Fall, null, 1.0, 0, false, 0) },
        FishRows = new Dictionary<string, FishRow> { ["(O)142"] = new FishRow("(O)142", false, "both", 0, "600 2600") },
        Ponds = new[] { new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 1, 1.0, null) }) },
    };

    [Fact]
    public void A_chain_resolves_shop_seed_to_crop_to_pickles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.False(build.HitPassCap);
        Assert.True(build.Passes >= 2);
        Assert.Equal("1-4", build.Model.Weeks("(O)24", ObtainFilter.DependableOnly).ToString());
        Assert.Equal("1-5", build.Model.Weeks("(O)342", ObtainFilter.DependableOnly).ToString());   // 3 days of pickling
    }

    [Fact]
    public void A_pond_chain_settles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.Equal("9-16", build.Model.Weeks("(O)812", ObtainFilter.Any).ToString());
        Assert.Equal("9-16", build.Model.Weeks("(O)447", ObtainFilter.Any).ToString());
    }

    [Fact]
    public void A_true_dependency_cycle_settles_without_looping()
    {
        var inputs = new ObtainabilityInputs
        {
            Objects = new Dictionary<string, ObjInfo>
            {
                ["(O)1"] = new ObjInfo("(O)1", "A", -2, 1, new[] { "tag_a" }, false),
                ["(O)2"] = new ObjInfo("(O)2", "B", -2, 1, new[] { "tag_b" }, false),
            },
            Forage = new[] { new LocationSpawn("Town", "(O)1", null, "SEASON Summer", 1.0, 0, false, 0) },
            Machines = new[]
            {
                new MachineRow("(BC)A", null, new[] { "tag_a" }, null, new[] { new MachineOutput("(O)2", null, null) }, 0, 0),
                new MachineRow("(BC)B", null, new[] { "tag_b" }, null, new[] { new MachineOutput("(O)1", null, null) }, 0, 0),
            },
        };
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        Assert.False(build.HitPassCap);
        Assert.Equal("5-8", build.Model.Weeks("(O)1", ObtainFilter.Any).ToString());
        Assert.Equal("5-8", build.Model.Weeks("(O)2", ObtainFilter.Any).ToString());
    }

    [Fact]
    public void Unresolved_queries_go_to_diagnostics_not_the_model()
    {
        var inputs = new ObtainabilityInputs
        {
            Forage = new[] { new LocationSpawn("Beach", "LOCATION_FISH Beach BOBBER_X", null, null, 1.0, 0, false, 0) },
        };
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        Assert.Contains(build.Unresolved, u => u.Contains("LOCATION_FISH Beach BOBBER_X"));
        Assert.DoesNotContain(build.Model.ItemIds, id => id.StartsWith(ItemQueries.UnresolvedPrefix));
    }

    [Fact]
    public void Direct_code_sources_are_always_present()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(new ObtainabilityInputs());
        Assert.False(build.Model.Weeks("(O)378", ObtainFilter.DependableOnly).IsEmpty);   // copper ore node
        Assert.False(build.Model.Weeks("(O)168", ObtainFilter.Any).IsEmpty);              // fishing trash
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityBuilderTests"`
Expected: build FAILS, `ObtainabilityBuilder` not found.

- [ ] **Step 3: Append the bundle to `ObtainabilityInputs.cs`**

```csharp

/// <summary>Everything the builder reads, filled by the glue at save load.</summary>
public sealed record ObtainabilityInputs
{
    public IReadOnlyDictionary<string, ObjInfo> Objects { get; init; } = new Dictionary<string, ObjInfo>();
    public IReadOnlyDictionary<string, FestivalDates> Festivals { get; init; } = new Dictionary<string, FestivalDates>();
    public IReadOnlyList<LocationSpawn> Forage { get; init; } = Array.Empty<LocationSpawn>();
    public IReadOnlyList<LocationSpawn> LocationFish { get; init; } = Array.Empty<LocationSpawn>();
    public IReadOnlyDictionary<string, FishRow> FishRows { get; init; } = new Dictionary<string, FishRow>();
    public IReadOnlyList<ArtifactSpotRow> ArtifactSpots { get; init; } = Array.Empty<ArtifactSpotRow>();
    public IReadOnlyList<GarbageRow> Garbage { get; init; } = Array.Empty<GarbageRow>();
    public IReadOnlyList<ShopRow> Shops { get; init; } = Array.Empty<ShopRow>();
    public IReadOnlyList<MonsterDropRow> MonsterDrops { get; init; } = Array.Empty<MonsterDropRow>();
    public IReadOnlyList<CropRow> Crops { get; init; } = Array.Empty<CropRow>();
    public IReadOnlyList<FruitTreeRow> FruitTrees { get; init; } = Array.Empty<FruitTreeRow>();
    public IReadOnlyList<MachineRow> Machines { get; init; } = Array.Empty<MachineRow>();
    public IReadOnlyList<RecipeRow> Recipes { get; init; } = Array.Empty<RecipeRow>();
    public IReadOnlyList<AnimalRow> Animals { get; init; } = Array.Empty<AnimalRow>();
    public IReadOnlyList<PondRow> Ponds { get; init; } = Array.Empty<PondRow>();
    public IReadOnlyList<TapRow> TapItems { get; init; } = Array.Empty<TapRow>();
    public IReadOnlyList<GeodeDropRow> GeodeDrops { get; init; } = Array.Empty<GeodeDropRow>();
    public IReadOnlyCollection<string> GeodesUsingDefaultTable { get; init; } = Array.Empty<string>();
}
```

- [ ] **Step 4: Write `ObtainabilityBuilder.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The built model, how many passes it took, and every source the model could not read.</summary>
public sealed record ObtainabilityBuild(ObtainabilityModel Model, int Passes, bool HitPassCap, IReadOnlyList<string> Unresolved);

/// <summary>Builds the model: direct sources once, then grown and made sources over the previous pass's
/// model until no item's weeks change (see the plan's Task 8 intro for why this settles).</summary>
public static class ObtainabilityBuilder
{
    public const int MaxPasses = 1000;

    public static ObtainabilityBuild Build(ObtainabilityInputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var f = inputs.Festivals;
        var o = inputs.Objects;
        var direct = new List<(string ItemId, ObtainSource Source)>();
        direct.AddRange(SpawnSources.Forage(inputs.Forage, o, f));
        direct.AddRange(SpawnSources.LocationFish(inputs.LocationFish, inputs.FishRows, o, f));
        direct.AddRange(SpawnSources.CrabPot(inputs.FishRows.Values));
        direct.AddRange(SpawnSources.ArtifactSpots(inputs.ArtifactSpots, o, f));
        direct.AddRange(SpawnSources.GarbageCans(inputs.Garbage, o, f));
        direct.AddRange(SpawnSources.FishingTrash());
        direct.AddRange(ShopSources.Stock(inputs.Shops, o, f));
        direct.AddRange(ShopSources.FestivalRewards(f));
        direct.AddRange(MineSources.Nodes());
        direct.AddRange(MineSources.MonsterDrops(inputs.MonsterDrops, o));
        direct.AddRange(MineSources.FishingTreasure());
        direct.AddRange(MadeSources.Animals(inputs.Animals, f));
        direct.AddRange(MadeSources.Tappers(inputs.TapItems, o, f));
        IReadOnlyDictionary<string, WeekMask> recipeWeeks = ShopSources.RecipeWeeks(inputs.Shops, f);

        ObtainabilityModel current = Assemble(direct, out List<string> lastUnresolved);
        for (int pass = 1; pass <= MaxPasses; pass++)
        {
            var all = new List<(string ItemId, ObtainSource Source)>(direct);
            all.AddRange(GrowSources.Crops(inputs.Crops, current));
            all.AddRange(GrowSources.FruitTrees(inputs.FruitTrees, current, o, f));
            all.AddRange(ShopSources.Barter(inputs.Shops, o, f, current));
            all.AddRange(MadeSources.Machines(inputs.Machines, o, current, f));
            all.AddRange(MadeSources.Recipes(inputs.Recipes, o, recipeWeeks, current));
            all.AddRange(MadeSources.Ponds(inputs.Ponds, o, current, f));
            all.AddRange(MadeSources.Geodes(inputs.GeodeDrops, inputs.GeodesUsingDefaultTable, o, current, f));
            ObtainabilityModel next = Assemble(all, out List<string> unresolved);
            if (SameWeeks(current, next)) return new ObtainabilityBuild(next, pass, false, unresolved);
            current = next;
            lastUnresolved = unresolved;   // from the last full pass, derived sources included
        }
        return new ObtainabilityBuild(current, MaxPasses, true, lastUnresolved);
    }

    private static ObtainabilityModel Assemble(
        IEnumerable<(string ItemId, ObtainSource Source)> sources, out List<string> unresolved)
    {
        var real = new List<(string ItemId, ObtainSource Source)>();
        unresolved = new List<string>();
        foreach (var s in sources)
        {
            if (s.ItemId.StartsWith(ItemQueries.UnresolvedPrefix, StringComparison.Ordinal))
                unresolved.Add($"{s.ItemId.Substring(ItemQueries.UnresolvedPrefix.Length)} | {s.Source.Detail}");
            else
                real.Add(s);
        }
        unresolved = unresolved.Distinct().OrderBy(u => u, StringComparer.Ordinal).ToList();
        return new ObtainabilityModel(real
            .GroupBy(s => BundleParsing.NormalizeItemId(s.ItemId), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ObtainSource>)g.Select(s => s.Source).ToList(), StringComparer.Ordinal));
    }

    private static bool SameWeeks(ObtainabilityModel a, ObtainabilityModel b)
    {
        if (a.Count != b.Count) return false;
        foreach (string id in b.ItemIds)
            if (a.Sources(id).Count != b.Sources(id).Count
                || a.Weeks(id, ObtainFilter.Any) != b.Weeks(id, ObtainFilter.Any)
                || a.Weeks(id, ObtainFilter.DependableOnly) != b.Weeks(id, ObtainFilter.DependableOnly))
                return false;
        return true;
    }
}
```

Notes for the implementer, checked against the tests:
- Pickles: parsnip weeks 1-4; 4000 minutes is 3 days; days 1-28 come out on days 4-31: weeks 1-5. `FLAVORED_ITEM Pickle DROP_IN_ID` resolves to `(O)342`.
- Carp is Fall (9-12); the pond carries forward from week 9: 9-16. Aged roe takes 3 days from roe obtainable in 9-16: days 57-109 come out on 60-112, weeks 9-16.
- The cycle: A is Summer (5-8), machine A makes B in the same weeks, machine B makes A in the same weeks; the second pass adds nothing new, so it settles.
- `ObtainabilityModel`'s constructor already removes duplicate sources (Task 1).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityBuilderTests"`
Expected: PASS, 5 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityBuilderTests.cs
git commit -m "obtainability: builder resolves grown and made items to convergence and collects unresolved sources"
git push origin story
```

---

### Task 9: The blind guard

**Files:**
- Test: `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs`

**Interfaces:**
- Consumes: the source tree only. Task 10 extends `BlindFiles` with the glue file and `ObtainabilityText.cs`.

- [ ] **Step 1: Write the guard test**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Tests;

/// <summary>Phase 1 of the obtainability model is built BLIND (spec 2026-09-14): it must come up with
/// its answers from game data alone, so it can be compared honestly against the existing model. This
/// fails if any blind file is missing, or names the existing model, its tables or its builders, in code
/// or comments, in any letter case.</summary>
public class ObtainabilityBlindGuardTests
{
    private static readonly string[] Forbidden =
    {
        "ItemAvailabilityModel", "ItemAvailability", "ItemEffort", "AvailabilityWeeks",
        "Core.Availability", "DefaultItemSeasonPins", "QuantityBasisTables", "ItemPoolBuilder",
        "GameDataPools", "GameEffortData", "LegendaryFishRules", "LocationGating", "MineAreas",
        "ItemAvailabilityBuilder", "BundleGenerationTuning", "EffortData", "PacingWeek", "HardWeek",
        "SeasonPins", "UnlockWeeks", "BasisByDeadline",
    };

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>Every file that must be blind, relative to src/.</summary>
    private static readonly string[] BlindFiles =
    {
        "TheLongestYear.Core/Obtainability/WeekMask.cs",
        "TheLongestYear.Core/Obtainability/ObtainTypes.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityModel.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs",
        "TheLongestYear.Core/Obtainability/ConditionSeasons.cs",
        "TheLongestYear.Core/Obtainability/ItemQueries.cs",
        "TheLongestYear.Core/Obtainability/SpawnSources.cs",
        "TheLongestYear.Core/Obtainability/ShopSources.cs",
        "TheLongestYear.Core/Obtainability/MineSources.cs",
        "TheLongestYear.Core/Obtainability/GrowSources.cs",
        "TheLongestYear.Core/Obtainability/MadeSources.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs",
    };

    private static string PathOf(string relative) => Path.Combine(SrcRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Every_blind_file_exists()
        => Assert.Empty(BlindFiles.Where(f => !File.Exists(PathOf(f))));

    [Fact]
    public void Nothing_else_hides_in_the_blind_folder_unlisted()
    {
        string folder = Path.Combine(SrcRoot, "TheLongestYear.Core", "Obtainability");
        var listed = BlindFiles.Select(PathOf).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unlisted = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFullPath).Where(f => !listed.Contains(f)).Select(Path.GetFileName).ToList();
        Assert.Empty(unlisted);
    }

    [Fact]
    public void No_blind_file_names_the_existing_model()
    {
        var hits = new List<string>();
        foreach (string file in BlindFiles.Select(PathOf).Where(File.Exists))
        {
            string text = File.ReadAllText(file);
            foreach (string word in Forbidden)
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase))
                    hits.Add($"{Path.GetFileName(file)}: {word}");
        }
        Assert.Empty(hits);
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityBlindGuardTests"`
Expected: PASS, 3 tests. If `No_blind_file_names_the_existing_model` fails, reword the offending comment; never add the word to an allow-list.

- [ ] **Step 3: Prove the guard can fail**

Temporarily add the comment `// itemavailabilitymodel` (lower case, to prove case-insensitivity) to the end of `WeekMask.cs`, run the guard, expect FAIL with `WeekMask.cs: ItemAvailabilityModel`, then remove the comment and run it again to PASS.

- [ ] **Step 4: Run the whole suite, commit, push**

```bash
git add tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs
git commit -m "obtainability: guard test keeps the phase 1 build blind to the existing model"
git push origin story
```

---

### Task 10: Reading the game data, building at save load, `tly_obtain <item>`

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityTextTests.cs`
- Create: `src/TheLongestYear/Loop/GameObtainabilityData.cs`
- Modify: `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs` (add two files to `BlindFiles`)
- Modify: `src/TheLongestYear/ModEntry.cs` (fields after line 52 `private TheLongestYear.Core.ItemPools _enginePools;`; call after line 603 `_reset.RebuildAvailabilityModel = BuildAvailabilityModelFor;`; command after line 338; bridge case after line 2615; methods before `private void CmdDumpEffort`)

**Interfaces:**
- Consumes: `ObtainabilityBuilder.Build`, `ObtainabilityInputs` and every input record (Tasks 2-8).
- Produces: `static string ObtainabilityText.Describe(string itemId, ObtainabilityModel model, Func<string, string?> nameOf)`; `internal sealed class GameObtainabilityData { public GameObtainabilityData(IMonitor); public ObtainabilityInputs Build(); }`; ModEntry fields `_obtainability` and `_obtainabilityUnresolved`, methods `BuildObtainabilityModel()` and `CmdObtain(string, string[])`.

- [ ] **Step 1: Write the failing text test**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityTextTests
{
    [Fact]
    public void Describe_lists_every_source_with_its_weeks_and_conditions()
    {
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)775"] = new[]
            {
                new ObtainSource(SourceKind.Fish, WeekMask.ForSeason(Season.Winter), Reliability.Dependable,
                    ObtainConditions.None with { Skill = "Fishing", SkillLevel = 6, CatchLimit = 1, RainOnly = true,
                                                 Unresolved = true, Requires = new[] { "location:Forest" } }, "Fish at Forest"),
            },
        });
        string text = ObtainabilityText.Describe("775", model, id => "Glacierfish");
        Assert.Contains("(O)775 Glacierfish: 1 source(s)", text);
        Assert.Contains("dependable weeks 13-16", text);
        Assert.Contains("Fish, Dependable, weeks 13-16", text);
        Assert.Contains("Fishing 6", text);
        Assert.Contains("catch limit 1", text);
        Assert.Contains("rain only", text);
        Assert.Contains("UNRESOLVED", text);
        Assert.Contains("location:Forest", text);
        Assert.Contains("no source", ObtainabilityText.Describe("(O)1", model, id => null));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityTextTests"`
Expected: build FAILS, `ObtainabilityText` not found.

- [ ] **Step 3: Write `ObtainabilityText.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The one-item readout for tly_obtain, and the source line the comparison report reuses.</summary>
public static class ObtainabilityText
{
    public static string Describe(string itemId, ObtainabilityModel model, Func<string, string?> nameOf)
    {
        string id = BundleParsing.NormalizeItemId(itemId);
        string name = nameOf(id) ?? "?";
        IReadOnlyList<ObtainSource> sources = model.Sources(id);
        if (sources.Count == 0) return $"{id} {name}: no source in the obtainability model.";

        var sb = new StringBuilder();
        sb.Append($"{id} {name}: {sources.Count} source(s); dependable weeks {model.Weeks(id, ObtainFilter.DependableOnly)}, ")
          .Append($"any weeks {model.Weeks(id, ObtainFilter.Any)}");
        foreach (ObtainSource s in sources)
            sb.AppendLine().Append("  - ").Append(SourceLine(s));
        return sb.ToString();
    }

    public static string SourceLine(ObtainSource s)
    {
        var sb = new StringBuilder($"{s.Kind}, {s.Reliability}, weeks {s.Weeks}");
        ObtainConditions c = s.Conditions;
        if (c.Skill != null) sb.Append($", {c.Skill} {c.SkillLevel}");
        if (c.CatchLimit > 0) sb.Append($", catch limit {c.CatchLimit}");
        if (c.RainOnly) sb.Append(", rain only");
        if (c.FewDays) sb.Append(", few days");
        if (c.YearTwo) sb.Append(", year 2");
        if (c.GingerIsland) sb.Append(", Ginger Island");
        if (c.Unresolved) sb.Append(", UNRESOLVED");
        if (c.Requires.Count > 0) sb.Append($", needs {string.Join("; ", c.Requires)}");
        return sb.Append($" | {s.Detail}").ToString();
    }
}
```

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityTextTests"`
Expected: PASS, 1 test.

- [ ] **Step 5: Write `GameObtainabilityData.cs`**

Type and field names are from the 1.6 GameData decompile (`StardewValley.GameData.*`), every one checked
there. Item ids and item queries pass through as raw text; Core's `ItemQueries` normalizes ids and
expands queries. Every section has its own try/catch so one bad table degrades to a smaller model.

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FarmAnimals;
using StardewValley.GameData.FishPonds;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.GarbageCans;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.GameData.WildTrees;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Reads the live game data (including other mods' edits) into the obtainability model's
    /// input records (spec 2026-09-14-item-obtainability). Phase 1 is blind: this class reads Data
    /// assets only and never consults the mod's own item tables.</summary>
    internal sealed class GameObtainabilityData
    {
        private const int FishDifficultyField = 1;
        private const int FishTimeField = 5;
        private const int FishWeatherField = 7;
        private const int FishMinLevelField = 12;
        private const string TrapMarker = "trap";
        private const int MonsterDropField = 6;
        private const int RecipeIngredientsField = 0;
        private const int RecipeOutputField = 2;
        private const int CookingUnlockField = 3;
        private const int CraftingBigCraftableField = 3;
        private const int CraftingUnlockField = 4;
        private const string PreviousOutputTapId = "PREVIOUS_OUTPUT_ID";
        private const string AnyCan = "*";

        private readonly IMonitor _monitor;

        public GameObtainabilityData(IMonitor monitor) => _monitor = monitor;

        public ObtainabilityInputs Build()
        {
            var objects = new Dictionary<string, ObjInfo>(StringComparer.Ordinal);
            var geodeDrops = new List<GeodeDropRow>();
            var defaultGeodes = new List<string>();
            var festivals = new Dictionary<string, FestivalDates>(StringComparer.Ordinal);
            var forage = new List<LocationSpawn>();
            var fish = new List<LocationSpawn>();
            var artifactSpots = new List<ArtifactSpotRow>();
            var fishRows = new Dictionary<string, FishRow>(StringComparer.Ordinal);
            var monsterDrops = new List<MonsterDropRow>();
            var crops = new List<CropRow>();
            var fruitTrees = new List<FruitTreeRow>();
            var shops = new List<ShopRow>();
            var machines = new List<MachineRow>();
            var recipes = new List<RecipeRow>();
            var animals = new List<AnimalRow>();
            var ponds = new List<PondRow>();
            var taps = new List<TapRow>();
            var garbage = new List<GarbageRow>();

            Section("Objects", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ObjectData>>("Data/Objects"))
                {
                    ObjectData o = kv.Value;
                    if (o == null) continue;
                    string id = BundleParsing.NormalizeItemId(kv.Key);
                    var tags = ItemContextTagManager.GetBaseContextTags(id)?.ToList() ?? new List<string>();
                    objects[id] = new ObjInfo(id, o.Name ?? "", o.Category, o.Price, tags, o.ExcludeFromRandomSale);
                    if (o.GeodeDropsDefaultItems) defaultGeodes.Add(id);
                    foreach (ObjectGeodeDropData drop in o.GeodeDrops ?? new List<ObjectGeodeDropData>())
                        foreach ((string item, _) in Entries(drop?.ItemId, drop?.RandomItemId))
                            geodeDrops.Add(new GeodeDropRow(id, item, drop.Chance, drop.Condition));   // geode contents are chance already
                }
            });

            Section("PassiveFestivals", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, PassiveFestivalData>>("Data/PassiveFestivals"))
                    if (kv.Value != null)
                        festivals[kv.Key] = new FestivalDates(kv.Key, (CoreSeason)(int)kv.Value.Season, kv.Value.StartDay, kv.Value.EndDay);
            });

            Section("Festivals/FestivalDates", () =>
            {
                // Keys are "<season><day>" ("spring13"); the value is a display name and is not used.
                foreach (string key in Game1.content.Load<Dictionary<string, string>>("Data/Festivals/FestivalDates").Keys)
                {
                    string seasonName = new string(key.TakeWhile(char.IsLetter).ToArray());
                    if (Enum.TryParse(seasonName, ignoreCase: true, out CoreSeason season)
                        && int.TryParse(key.Substring(seasonName.Length), out int day))
                        festivals[key] = new FestivalDates(key, season, day, day);
                }
            });

            Section("Locations", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                {
                    LocationData loc = kv.Value;
                    if (loc == null) continue;
                    foreach (SpawnForageData f in loc.Forage ?? new List<SpawnForageData>())
                        foreach ((string item, bool random) in Entries(f?.ItemId, f?.RandomItemId))
                            forage.Add(new LocationSpawn(kv.Key, item, MapSeason(f.Season), f.Condition, f.Chance, 0, false, 0, random));
                    foreach (SpawnFishData f in loc.Fish ?? new List<SpawnFishData>())
                        foreach ((string item, bool random) in Entries(f?.ItemId, f?.RandomItemId))
                            fish.Add(new LocationSpawn(kv.Key, item, MapSeason(f.Season), f.Condition, f.Chance,
                                Math.Max(0, f.CatchLimit), f.RequireMagicBait, f.MinFishingLevel, random)); // CatchLimit defaults to -1
                    foreach (ArtifactSpotDropData a in loc.ArtifactSpots ?? new List<ArtifactSpotDropData>())
                        foreach ((string item, _) in Entries(a?.ItemId, a?.RandomItemId))   // artifact spot drops are chance already
                            artifactSpots.Add(new ArtifactSpotRow(kv.Key, item, a.Condition, a.Chance));
                }
            });

            Section("Fish", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Fish"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    string id = BundleParsing.NormalizeItemId(kv.Key);
                    bool trap = Field(fields, FishDifficultyField) == TrapMarker;
                    int level = trap ? 0 : (int.TryParse(Field(fields, FishMinLevelField), out int l) ? l : 0);
                    fishRows[id] = new FishRow(id, trap, trap ? "" : Field(fields, FishWeatherField), level,
                        trap ? "" : Field(fields, FishTimeField));
                }
            });

            Section("Monsters", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Monsters"))
                {
                    string[] pairs = Field((kv.Value ?? "").Split('/'), MonsterDropField).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i + 1 < pairs.Length; i += 2)
                        if (double.TryParse(pairs[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double chance))
                            monsterDrops.Add(new MonsterDropRow(kv.Key, pairs[i], chance));
                }
            });

            Section("Crops", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, CropData>>("Data/Crops"))
                {
                    CropData c = kv.Value;
                    if (c?.HarvestItemId == null) continue;
                    crops.Add(new CropRow(BundleParsing.NormalizeItemId(kv.Key), BundleParsing.NormalizeItemId(c.HarvestItemId),
                        MapSeasons(c.Seasons), (c.DaysInPhase ?? new List<int>()).Sum(), c.RegrowDays));
                }
            });

            Section("FruitTrees", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, FruitTreeData>>("Data/FruitTrees"))
                {
                    if (kv.Value == null) continue;
                    var fruit = new List<FruitRow>();
                    foreach (FruitTreeFruitData f in kv.Value.Fruit ?? new List<FruitTreeFruitData>())
                        foreach ((string item, bool random) in Entries(f?.ItemId, f?.RandomItemId))
                            fruit.Add(new FruitRow(item, MapSeason(f.Season), f.Chance, f.Condition, random));   // may be a query; Core emits it
                    fruitTrees.Add(new FruitTreeRow(BundleParsing.NormalizeItemId(kv.Key), MapSeasons(kv.Value.Seasons), fruit));
                }
            });

            Section("Shops", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops"))
                    foreach (ShopItemData item in kv.Value?.Items ?? new List<ShopItemData>())
                        foreach ((string entry, bool random) in Entries(item?.ItemId, item?.RandomItemId))
                            shops.Add(new ShopRow(kv.Key, entry, item.Condition, item.IsRecipe, random,
                                string.IsNullOrWhiteSpace(item.TradeItemId) ? null : item.TradeItemId));
            });

            Section("Machines", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, MachineData>>("Data/Machines"))
                    foreach (MachineOutputRule rule in kv.Value?.OutputRules ?? new List<MachineOutputRule>())
                    {
                        var outputs = new List<MachineOutput>();
                        foreach (MachineItemOutput o in rule.OutputItem ?? new List<MachineItemOutput>())
                        {
                            if (o == null) continue;
                            if (!string.IsNullOrWhiteSpace(o.OutputMethod)) { outputs.Add(new MachineOutput(null, o.Condition, o.OutputMethod)); continue; }
                            foreach ((string entry, bool random) in Entries(o.ItemId, o.RandomItemId))
                                outputs.Add(new MachineOutput(entry, o.Condition, null, random));
                        }
                        if (outputs.Count == 0) continue;
                        // A rule with no triggers never fires; nothing is synthesized for it.
                        foreach (MachineOutputTriggerRule t in rule.Triggers ?? new List<MachineOutputTriggerRule>())
                        {
                            if (t == null || t.Trigger == MachineOutputTrigger.None) continue;
                            bool noInput = string.IsNullOrEmpty(t.RequiredItemId) && (t.RequiredTags == null || t.RequiredTags.Count == 0);
                            // "Any item placed in" has no id or tags to read; skipping it keeps it from
                            // reading as a machine that needs no input (a known limitation).
                            if (noInput && t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) continue;
                            machines.Add(new MachineRow(kv.Key,
                                string.IsNullOrEmpty(t.RequiredItemId) ? null : t.RequiredItemId,
                                (IReadOnlyList<string>)(t.RequiredTags ?? new List<string>()),
                                t.Condition, outputs, rule.MinutesUntilReady, rule.DaysUntilReady, rule.UseFirstValidOutput));
                        }
                    }
            });

            Section("CookingRecipes", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes"))
                    if (Recipe(kv.Key, kv.Value, cooking: true) is RecipeRow r) recipes.Add(r);
            });

            Section("CraftingRecipes", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CraftingRecipes"))
                    if (Recipe(kv.Key, kv.Value, cooking: false) is RecipeRow r) recipes.Add(r);
            });

            Section("FarmAnimals", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals"))
                {
                    FarmAnimalData a = kv.Value;
                    if (a == null) continue;
                    animals.Add(new AnimalRow(kv.Key, string.IsNullOrEmpty(a.RequiredBuilding) ? (a.House ?? "") : a.RequiredBuilding,
                        a.PurchasePrice, Produce(a.ProduceItemIds), Produce(a.DeluxeProduceItemIds), a.DeluxeProduceMinimumFriendship));
                }
            });

            Section("FishPondData", () =>
            {
                foreach (FishPondData pond in Game1.content.Load<List<FishPondData>>("Data/FishPondData") ?? new List<FishPondData>())
                {
                    if (pond == null) continue;
                    var products = new List<PondProduct>();
                    // FishPondReward has only ItemId, RequiredPopulation, Chance and quantities: no condition, no random list.
                    foreach (FishPondReward reward in pond.ProducedItems ?? new List<FishPondReward>())
                        if (!string.IsNullOrWhiteSpace(reward?.ItemId))
                            products.Add(new PondProduct(reward.ItemId.Trim(), reward.RequiredPopulation, reward.Chance, null));
                    ponds.Add(new PondRow(pond.Id ?? "", (IReadOnlyList<string>)(pond.RequiredTags ?? new List<string>()), pond.Precedence, products));
                }
            });

            Section("WildTrees", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, WildTreeData>>("Data/WildTrees"))
                    foreach (WildTreeTapItemData tap in kv.Value?.TapItems ?? new List<WildTreeTapItemData>())
                        foreach ((string item, bool random) in Entries(tap?.ItemId, tap?.RandomItemId))
                            if (item != PreviousOutputTapId)
                                taps.Add(new TapRow(kv.Key, item, tap.DaysUntilReady, MapSeason(tap.Season), tap.Chance, tap.Condition, random));
            });

            Section("GarbageCans", () =>
            {
                GarbageCanData data = Game1.content.Load<GarbageCanData>("Data/GarbageCans");
                void AddAll(string can, List<GarbageCanItemData> items)
                {
                    foreach (GarbageCanItemData g in items ?? new List<GarbageCanItemData>())
                        foreach ((string item, _) in Entries(g?.ItemId, g?.RandomItemId))   // garbage is chance already
                            garbage.Add(new GarbageRow(can, item, g.Condition));
                }
                AddAll(AnyCan, data?.BeforeAll);
                AddAll(AnyCan, data?.AfterAll);
                foreach (var kv in data?.GarbageCans ?? new Dictionary<string, GarbageCanEntryData>())
                    AddAll(kv.Key, kv.Value?.Items);
            });

            return new ObtainabilityInputs
            {
                Objects = objects, Festivals = festivals, Forage = forage, LocationFish = fish, FishRows = fishRows,
                ArtifactSpots = artifactSpots, Garbage = garbage, Shops = shops, MonsterDrops = monsterDrops,
                Crops = crops, FruitTrees = fruitTrees, Machines = machines, Recipes = recipes, Animals = animals,
                Ponds = ponds, TapItems = taps, GeodeDrops = geodeDrops, GeodesUsingDefaultTable = defaultGeodes,
            };
        }

        private void Section(string asset, Action read)
        {
            try { read(); }
            catch (Exception ex)
            {
                _monitor?.Log($"Obtainability: reading Data/{asset} failed ({ex.GetType().Name}: {ex.Message}); that part of the model is missing.", LogLevel.Warn);
            }
        }

        private static RecipeRow Recipe(string name, string row, bool cooking)
        {
            string[] fields = (row ?? "").Split('/');
            int unlockField = cooking ? CookingUnlockField : CraftingUnlockField;
            if (fields.Length <= unlockField) return null;
            string[] ingredientPairs = fields[RecipeIngredientsField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ingredients = new List<string>();
            for (int i = 0; i + 1 < ingredientPairs.Length; i += 2)
                ingredients.Add(int.TryParse(ingredientPairs[i], out int n) && n < 0 ? ingredientPairs[i] : BundleParsing.NormalizeItemId(ingredientPairs[i]));
            // The output field is "id count id count ..."; with several ids the game picks one at random
            // each craft (CraftingRecipe.cs 127-131, 192).
            string[] outputPairs = fields[RecipeOutputField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool bigCraftable = !cooking && string.Equals(fields[CraftingBigCraftableField].Trim(), "true", StringComparison.OrdinalIgnoreCase);
            var outputIds = new List<string>();
            for (int i = 0; i < outputPairs.Length; i += 2)
                outputIds.Add(bigCraftable && !outputPairs[i].StartsWith("(", StringComparison.Ordinal) ? "(BC)" + outputPairs[i] : BundleParsing.NormalizeItemId(outputPairs[i]));
            if (outputIds.Count == 0) return null;
            return new RecipeRow(name, ingredients, outputIds[0], fields[unlockField].Trim(), cooking,
                outputIds.Count > 1 ? outputIds.Skip(1).ToList() : null);
        }

        private static IReadOnlyList<AnimalProduce> Produce(List<FarmAnimalProduce> produce)
            => (produce ?? new List<FarmAnimalProduce>())
                .Where(p => !string.IsNullOrEmpty(p?.ItemId))
                .Select(p => new AnimalProduce(BundleParsing.NormalizeItemId(p.ItemId), p.Condition, p.MinimumFriendship)).ToList();

        /// <summary>What a spawn entry can give, ids and item queries alike. A non-empty RandomItemId replaces
        /// ItemId (ItemQueryResolver.cs 804-817) and one entry is picked, so each is random when there are
        /// several; otherwise the ItemId is the one fixed result.</summary>
        private static IEnumerable<(string Id, bool IsRandom)> Entries(string itemId, List<string> randomItemIds)
        {
            List<string> random = (randomItemIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
            if (random.Count > 0)
            {
                foreach (string id in random) yield return (id, random.Count > 1);
                yield break;
            }
            if (!string.IsNullOrWhiteSpace(itemId)) yield return (itemId.Trim(), false);
        }

        private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : "";

        private static CoreSeason? MapSeason(StardewValley.Season? season)
            => season is StardewValley.Season s ? (CoreSeason)(int)s : null;

        private static IReadOnlyList<CoreSeason> MapSeasons(List<StardewValley.Season> seasons)
            => (seasons ?? new List<StardewValley.Season>()).Select(s => (CoreSeason)(int)s).Distinct().ToList();
    }
}
```

If a property above does not compile, open the type under `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled\decompiled\StardewValley.GameData\` and use the name it declares; do not guess. Crop ids are normalized here because Core emits them directly; everything else, fruit included, goes through `ItemQueries`. `Entries` follows the game: a non-empty `RandomItemId` list replaces `ItemId`. Machine rules with no triggers are skipped, never given a made-up trigger.

- [ ] **Step 6: Add the two new blind files to the guard**

In `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs`, append to `BlindFiles`:

```csharp
        "TheLongestYear.Core/Obtainability/ObtainabilityText.cs",
        "TheLongestYear/Loop/GameObtainabilityData.cs",
```

- [ ] **Step 7: Wire it into `ModEntry.cs`**

After line 52 (`private TheLongestYear.Core.ItemPools _enginePools;`) add:

```csharp
        /// <summary>Phase 1 item obtainability model (spec 2026-09-14): built blind from game data at
        /// save load. Nothing reads it for gameplay; tly_obtain is its only reader. Null before a save
        /// is loaded or when the build failed.</summary>
        private TheLongestYear.Core.Obtainability.ObtainabilityModel _obtainability;
        /// <summary>Sources the obtainability build could not read (unsupported item queries, machine
        /// output methods), for tly_obtain compare.</summary>
        private IReadOnlyList<string> _obtainabilityUnresolved = System.Array.Empty<string>();
```

After line 603 (`_reset.RebuildAvailabilityModel = BuildAvailabilityModelFor;`) add:

```csharp
            BuildObtainabilityModel();
```

After line 338 (the `tly_dumpeffort` registration) add:

```csharp
            helper.ConsoleCommands.Add("tly_obtain", "Item obtainability model (phase 1, not used by gameplay). Usage: tly_obtain <itemId> | tly_obtain compare [fileName]", this.CmdObtain);
```

After line 2615 (`case "tly_dumpeffort": ...`) add:

```csharp
                case "tly_obtain": this.CmdObtain(command, args); break;
```

Before `private void CmdDumpEffort` add:

```csharp
        private void BuildObtainabilityModel()
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var inputs = new TheLongestYear.Loop.GameObtainabilityData(this.Monitor).Build();
                var build = TheLongestYear.Core.Obtainability.ObtainabilityBuilder.Build(inputs);
                _obtainability = build.Model;
                _obtainabilityUnresolved = build.Unresolved;
                this.Monitor.Log(
                    $"Obtainability model: {build.Model.Count} items in {timer.ElapsedMilliseconds} ms, {build.Passes} pass(es), " +
                    $"{build.Unresolved.Count} unresolved source(s)" + (build.HitPassCap ? ", STOPPED AT THE PASS CAP" : "") + ".",
                    build.HitPassCap ? LogLevel.Warn : LogLevel.Info);
            }
            catch (Exception ex)
            {
                _obtainability = null;
                _obtainabilityUnresolved = System.Array.Empty<string>();
                this.Monitor.Log($"Obtainability model: build failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
            }
        }

        /// <summary><c>tly_obtain &lt;itemId&gt;</c>: every source the obtainability model has for one item.</summary>
        private void CmdObtain(string command, string[] args)
        {
            if (!Context.IsWorldReady || _obtainability == null)
            {
                this.Monitor.Log("Load a save first (the obtainability model is built at save load).", LogLevel.Warn);
                return;
            }
            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: tly_obtain <itemId> | tly_obtain compare [fileName]", LogLevel.Info);
                return;
            }
            this.Monitor.Log(
                TheLongestYear.Core.Obtainability.ObtainabilityText.Describe(args[0], _obtainability, id => ItemRegistry.GetData(id)?.DisplayName),
                LogLevel.Info);
        }
```

If `ModEntry.cs` has no `using System.Collections.Generic;` at the top, add it.

- [ ] **Step 8: Build the mod and run the whole suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: `0 Error(s)`.
Run the full test command. Expected: all pass, including `ObtainabilityBlindGuardTests` (now 14 files).

- [ ] **Step 9: Commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs tests/TheLongestYear.Tests/ObtainabilityTextTests.cs tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs src/TheLongestYear/Loop/GameObtainabilityData.cs src/TheLongestYear/ModEntry.cs
git commit -m "obtainability: read game data at save load and add tly_obtain"
git push origin story
```

---

### Task 11: The comparison report, `tly_obtain compare`

`ObtainabilityComparison` is NOT blind: it reads the existing item model, but only through delegates,
so Core stays testable and the blind folder never names it. The existing model gains a read-only
`KnownIds` so the comparison sees every item that model knows, not a proxy list.

**Files:**
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs` (add `KnownIds` to `ItemAvailabilityModel`)
- Test: `tests/TheLongestYear.Tests/ItemAvailabilityKnownIdsTests.cs`
- Create: `src/TheLongestYear.Core/ObtainabilityComparison.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityComparisonTests.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdObtain`, added in Task 10)
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `ObtainabilityModel`, `ObtainFilter` (Task 1), `ObtainabilityText.SourceLine` (Task 10); ModEntry fields `_availability`, `_enginePools`, `_obtainability`, `_obtainabilityUnresolved`.
- Produces: `IReadOnlyCollection<string> ItemAvailabilityModel.KnownIds`; `enum CompareVerdict { Agree, NewEarlier, NewLater, OnlyExisting, OnlyNew }`; `record CompareRow(string ItemId, int? ExistingPacing, int? ExistingHard, string ExistingBasis, int? NewDependable, int? NewAny, CompareVerdict Verdict)`; `static IReadOnlyList<CompareRow> ObtainabilityComparison.Compare(IEnumerable<string> existingIds, Func<string, bool> existingPlaced, Func<string, (int Pacing, int Hard, string Basis)> existingWeeks, ObtainabilityModel model)`; `static string ObtainabilityComparison.Render(IReadOnlyList<CompareRow> rows, ObtainabilityModel model, IReadOnlyList<string> unresolved, Func<string, string?> nameOf, string version)`.

- [ ] **Step 1: Write the failing `KnownIds` test**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class ItemAvailabilityKnownIdsTests
{
    [Fact]
    public void Known_ids_cover_derived_effort_and_accepted_overrides_but_not_rejected_ones()
    {
        var derived = new Dictionary<string, ItemAvailability> { ["(O)1"] = new ItemAvailability(Season.Spring, 3, "fish", EarliestWeek: 2) };
        var effort = new Dictionary<string, ItemEffort> { ["(O)2"] = new ItemEffort(4, "crop", EarliestWeek: 5) };
        var seasonPins = new Dictionary<string, Season> { ["(O)3"] = Season.Fall, ["(O)1"] = Season.Spring };
        var weekPins = new Dictionary<string, int> { ["(O)4"] = 9, ["(O)2"] = 1 };   // (O)2 at week 1 is earlier than its rule: rejected
        var model = new ItemAvailabilityModel(derived, seasonPins, null, effort, weekPins);
        Assert.Equal(new[] { "(O)1", "(O)2", "(O)3", "(O)4" }, model.KnownIds);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test ... --filter "FullyQualifiedName~ItemAvailabilityKnownIdsTests"`
Expected: build FAILS, `KnownIds` not found.

- [ ] **Step 3: Add `KnownIds` to `ItemAvailabilityModel`**

In `src/TheLongestYear.Core/ItemAvailability.cs`, after the `DerivedEffortCount` property, add:

```csharp
    /// <summary>Every id this model has an answer for from its own data: rule-derived, effort-derived,
    /// or an accepted season or week override. Sorted. Read by the obtainability comparison (spec
    /// 2026-09-14-item-obtainability), which must see everything this model knows.</summary>
    public IReadOnlyCollection<string> KnownIds
    {
        get
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            ids.UnionWith(_derived.Keys);
            ids.UnionWith(_effortDerived.Keys);
            foreach (string id in _seasonOverrides.Keys) if (!_rejectedSeasonOverrides.Contains(id)) ids.Add(id);
            foreach (string id in _weekOverrides.Keys) if (!_rejectedSeasonOverrides.Contains(id)) ids.Add(id);
            return ids;
        }
    }
```

Note: `(O)2`'s week pin is rejected (week 1 is earlier than its rule's week 5), but `(O)2` is still known because its effort rule placed it. The test covers both.

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test ... --filter "FullyQualifiedName~ItemAvailabilityKnownIdsTests"`
Expected: PASS, 1 test.

- [ ] **Step 5: Write the failing comparison tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityComparisonTests
{
    private static ObtainSource Source(WeekMask weeks, string detail = "test") =>
        new(SourceKind.Forage, weeks, Reliability.Dependable, ObtainConditions.None with { Requires = new[] { "location:Town" } }, detail);

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)1"] = new[] { Source(WeekMask.FromWeekOnwardOf(2)) },
        ["(O)2"] = new[] { Source(WeekMask.FromWeekOnwardOf(9), "Forage at Town"), Source(WeekMask.Of(10), "second"), Source(WeekMask.Of(11), "third"), Source(WeekMask.Of(12), "fourth") },
        ["(O)5"] = new[] { Source(WeekMask.Of(4)) },
        ["(O)6"] = new[] { Source(WeekMask.FromWeekOnwardOf(3)) },
    });

    private static readonly Dictionary<string, (int Pacing, int Hard, string Basis)> Existing = new()
    {
        ["(O)1"] = (3, 2, "rule a"), ["(O)2"] = (5, 5, "rule b"), ["(O)3"] = (1, 1, "rule c"), ["(O)6"] = (8, 8, "rule d"),
    };

    private static IReadOnlyList<CompareRow> Rows() => ObtainabilityComparison.Compare(
        new[] { "(O)1", "2", "(O)3", "(O)4", "(O)6" }, id => Existing.ContainsKey(id), id => Existing[id], Model);

    [Fact]
    public void Verdicts_compare_the_existing_hard_week_with_the_new_earliest_week()
    {
        var byId = Rows().ToDictionary(r => r.ItemId, r => r.Verdict);
        Assert.Equal(CompareVerdict.Agree, byId["(O)1"]);
        Assert.Equal(CompareVerdict.NewLater, byId["(O)2"]);
        Assert.Equal(CompareVerdict.OnlyExisting, byId["(O)3"]);
        Assert.False(byId.ContainsKey("(O)4"));                 // neither side knows it
        Assert.Equal(CompareVerdict.OnlyNew, byId["(O)5"]);
        Assert.Equal(CompareVerdict.NewEarlier, byId["(O)6"]);
        Assert.Equal("rule c", Rows().Single(r => r.ItemId == "(O)3").ExistingBasis);
    }

    [Fact]
    public void The_report_has_a_summary_every_source_the_existing_basis_and_diagnostics()
    {
        string text = ObtainabilityComparison.Render(Rows(), Model, new[] { "LOCATION_FISH Beach X | Forage at Beach" }, id => "Name " + id, "0.18.4");
        Assert.Contains("# Item obtainability comparison", text);
        Assert.Contains("| NewLater | 1 |", text);
        Assert.Contains("## NewEarlier", text);
        Assert.Contains("## OnlyExisting", text);
        Assert.Contains("Name (O)2", text);
        Assert.Contains("fourth", text);                       // all four sources, not the first three
        Assert.Contains("needs location:Town", text);          // conditions shown
        Assert.Contains("existing basis: rule b", text);
        Assert.Contains("## Unresolved sources (1)", text);
        Assert.Contains("LOCATION_FISH Beach X", text);
        Assert.DoesNotContain("\u2014", text);                 // no em dashes
    }
}
```

- [ ] **Step 6: Run them to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityComparisonTests"`
Expected: build FAILS, `ObtainabilityComparison` not found.

- [ ] **Step 7: Write `ObtainabilityComparison.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

public enum CompareVerdict { Agree, NewEarlier, NewLater, OnlyExisting, OnlyNew }

public sealed record CompareRow(
    string ItemId, int? ExistingPacing, int? ExistingHard, string ExistingBasis, int? NewDependable, int? NewAny, CompareVerdict Verdict);

/// <summary>Lines the blind obtainability model up against the existing item model (spec
/// 2026-09-14-item-obtainability, phase 1). The existing hard week is a fact ("the first week the item
/// can exist at all"), so it is compared with the new model's earliest week counting every source.</summary>
public static class ObtainabilityComparison
{
    private static readonly CompareVerdict[] SectionOrder =
        { CompareVerdict.NewEarlier, CompareVerdict.NewLater, CompareVerdict.OnlyExisting, CompareVerdict.OnlyNew, CompareVerdict.Agree };

    public static IReadOnlyList<CompareRow> Compare(
        IEnumerable<string> existingIds, Func<string, bool> existingPlaced,
        Func<string, (int Pacing, int Hard, string Basis)> existingWeeks, ObtainabilityModel model)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string id in existingIds) ids.Add(BundleParsing.NormalizeItemId(id));
        foreach (string id in model.ItemIds) ids.Add(id);

        var rows = new List<CompareRow>();
        foreach (string id in ids)
        {
            // Placed is checked first: asking the existing model about an unplaced id records it as unknown.
            (int Pacing, int Hard, string Basis)? existing = existingPlaced(id) ? existingWeeks(id) : null;
            int? newAny = model.EarliestWeek(id, ObtainFilter.Any);
            int? newDep = model.EarliestWeek(id, ObtainFilter.DependableOnly);
            if (existing == null && newAny == null) continue;
            CompareVerdict verdict = existing == null ? CompareVerdict.OnlyNew
                : newAny == null ? CompareVerdict.OnlyExisting
                : newAny == existing.Value.Hard ? CompareVerdict.Agree
                : newAny < existing.Value.Hard ? CompareVerdict.NewEarlier
                : CompareVerdict.NewLater;
            rows.Add(new CompareRow(id, existing?.Pacing, existing?.Hard, existing?.Basis ?? "", newDep, newAny, verdict));
        }
        return rows;
    }

    public static string Render(
        IReadOnlyList<CompareRow> rows, ObtainabilityModel model, IReadOnlyList<string> unresolved,
        Func<string, string?> nameOf, string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Item obtainability comparison").AppendLine();
        sb.AppendLine($"Mod version {version}. Existing weeks are the current item model's pacing and hard weeks. New weeks are the blind obtainability model's earliest week, dependable sources only, then any source. The verdict compares the existing hard week with the new any-source week. Weeks are counted in isolation: nothing saved from earlier.").AppendLine();
        sb.AppendLine("| Verdict | Items |").AppendLine("|---|---|");
        foreach (CompareVerdict v in SectionOrder)
            sb.AppendLine($"| {v} | {rows.Count(r => r.Verdict == v)} |");
        sb.AppendLine($"| Unresolved sources | {unresolved.Count} |");

        foreach (CompareVerdict v in SectionOrder.Where(v => v != CompareVerdict.Agree))
        {
            List<CompareRow> section = rows.Where(r => r.Verdict == v).ToList();
            if (section.Count == 0) continue;
            sb.AppendLine().AppendLine($"## {v}").AppendLine();
            foreach (CompareRow r in section)
            {
                sb.AppendLine($"### {r.ItemId} {nameOf(r.ItemId) ?? "?"}");
                sb.AppendLine($"- existing pacing {Week(r.ExistingPacing)}, hard {Week(r.ExistingHard)}; existing basis: {(r.ExistingBasis.Length == 0 ? "none" : r.ExistingBasis)}");
                sb.AppendLine($"- new dependable {Week(r.NewDependable)}, any {Week(r.NewAny)}");
                foreach (ObtainSource s in model.Sources(r.ItemId))
                    sb.AppendLine($"  - {ObtainabilityText.SourceLine(s)}");
            }
        }

        List<CompareRow> agree = rows.Where(r => r.Verdict == CompareVerdict.Agree).ToList();
        if (agree.Count > 0)
        {
            sb.AppendLine().AppendLine("## Agree").AppendLine();
            sb.AppendLine("| Item | Name | Week |").AppendLine("|---|---|---|");
            foreach (CompareRow r in agree)
                sb.AppendLine($"| {r.ItemId} | {nameOf(r.ItemId) ?? "?"} | {Week(r.NewAny)} |");
        }

        sb.AppendLine().AppendLine($"## Unresolved sources ({unresolved.Count})").AppendLine();
        foreach (string u in unresolved) sb.AppendLine($"- {u}");
        return sb.ToString();
    }

    private static string Week(int? week) => week?.ToString() ?? "none";
}
```

- [ ] **Step 8: Run them to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityComparisonTests"`
Expected: PASS, 2 tests.

- [ ] **Step 9: Add the `compare` branch to `CmdObtain` in `ModEntry.cs`**

Insert immediately before the `this.Monitor.Log(TheLongestYear.Core.Obtainability.ObtainabilityText.Describe(...` call:

```csharp
            if (args[0] == "compare")
            {
                if (_availability == null || _enginePools == null)
                {
                    this.Monitor.Log("tly_obtain compare: the existing item model is not built yet.", LogLevel.Warn);
                    return;
                }
                TheLongestYear.Core.ItemPools pools = _enginePools;
                IEnumerable<string> poolIds = new[]
                    {
                        pools.Crops, pools.Fish, pools.CrabPot, pools.Forage, pools.MonsterDrops, pools.Metals,
                        pools.ArtisanGoods, pools.Artifacts, pools.Books, pools.Saplings, pools.GeodeMinerals,
                        pools.Cooking, pools.TapperGoods, pools.WinterOnly,
                    }
                    .SelectMany(list => list).Select(item => item.ItemId);
                var rows = TheLongestYear.Core.ObtainabilityComparison.Compare(
                    _availability.KnownIds.Concat(poolIds),
                    id => _availability.IsPlaced(id),
                    id => { var a = _availability.For(id); return (a.PacingWeek, a.HardWeekOrPacing, a.Basis); },
                    _obtainability);
                string report = TheLongestYear.Core.ObtainabilityComparison.Render(
                    rows, _obtainability, _obtainabilityUnresolved, id => ItemRegistry.GetData(id)?.DisplayName,
                    this.ModManifest.Version.ToString());
                string fileName = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : "obtainability-compare.md";
                string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
                System.IO.File.WriteAllText(path, report);
                string counts = string.Join(", ", rows.GroupBy(r => r.Verdict).Select(g => $"{g.Key} {g.Count()}"));
                this.Monitor.Log($"tly_obtain compare: wrote {path} ({rows.Count} items: {counts}; {_obtainabilityUnresolved.Count} unresolved).", LogLevel.Info);
                return;
            }
```

If `ModEntry.cs` has no `using System.Linq;` at the top, add it.

- [ ] **Step 10: Ignore the report**

Append to `.gitignore`, after the `item-effort-model.md` line:

```
obtainability-compare.md
```

- [ ] **Step 11: Build, run the whole suite, commit, push**

Run the mod build command (expect `0 Error(s)`) and the full test command (expect all pass).

```bash
git add src/TheLongestYear.Core/ItemAvailability.cs tests/TheLongestYear.Tests/ItemAvailabilityKnownIdsTests.cs src/TheLongestYear.Core/ObtainabilityComparison.cs tests/TheLongestYear.Tests/ObtainabilityComparisonTests.cs src/TheLongestYear/ModEntry.cs .gitignore
git commit -m "obtainability: tly_obtain compare writes the blind-versus-existing report"
git push origin story
```

---

### Task 12: Live check on the throwaway save, then report

**Run by the controller, not a subagent.** Deploying closes the game. Before step 1, check whether
`StardewModdingAPI` is running; if it is, Jeff may be playing in it: ask him before closing it. Say in
the same message that this launch is an automated run, not one for him to look at (user memory
`label-whose-game-launch-it-is.md`). Runbook: `docs/HEADLESS_DRIVING.md`.

**Files:**
- Modify: `STATUS.md` (new top section)
- Modify: `TODO.md` (new Open entry)

- [ ] **Step 1: Deploy and load the throwaway save**

```powershell
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear"
pwsh -NoProfile -File tools/deploy.ps1 -Minimized
git checkout -- test-output/log-archive
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "Debug bridge: 'pause when window is inactive'" -FromLine 0 -TimeoutSec 180
$save = (Get-ChildItem "$env:APPDATA\StardewValley\Saves" -Directory -Filter "None_*" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
$n = pwsh -NoProfile -File tools/bridge.ps1 -Action count
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_loadsave $save"
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "Obtainability model:" -FromLine $n -TimeoutSec 120
```

Expected: `Obtainability model: <N> items in <ms> ms, <P> pass(es), <U> unresolved source(s).` at Info, N in the thousands, no `STOPPED AT THE PASS CAP`, no `Obtainability: reading Data/... failed` Warn lines. The `None_*` saves are the throwaway "Clone" lineage; never load `PuffPuff_*` or `Cheatside_*`. If the save sits on a failing day 28 the rewind opens on load; that does not affect this check. Never send `tly_skipscene` to it.

- [ ] **Step 2: Spot-check items**

```powershell
$n = pwsh -NoProfile -File tools/bridge.ps1 -Action count
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_obtain (O)775|tly_obtain (O)147|tly_obtain (O)348|tly_obtain (O)798|tly_obtain (O)414|tly_obtain (O)378|tly_obtain (O)24|tly_obtain (O)142|tly_obtain (O)342"
```

Then read the new lines of `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` after line `$n`. Check by hand and write down any that look wrong:
- `(O)775` Glacierfish: Fish, Winter weeks 13-16, Fishing 6, catch limit 1.
- `(O)147` Herring: Fish in Spring AND Winter (weeks 1-4 and 13-16) and NOT Summer or Fall. This is the spec's Spring-and-Winter fish.
- `(O)348` Wine: Machine from a keg, each fruit's weeks moved one week later (10000 minutes is 7 days).
- `(O)798` Midnight Squid: week 15 only, few days (Night Market submarine).
- `(O)414` Crystal Fruit: Winter forage; also Cart (chance).
- `(O)378` Copper Ore: MineNode dependable every week.
- `(O)24` Parsnip: Crop weeks 1-4 from Spring seeds; GreenhouseCrop from Spring seeds only in weeks 1-5 (no stored seed); Mixed Seeds chance sources, including Winter greenhouse weeks.
- `(O)142` Carp: its fish seasons.
- `(O)342` Pickles: Machine weeks from any vegetable, up to three days later.

- [ ] **Step 3: Write the comparison report**

```powershell
$n = pwsh -NoProfile -File tools/bridge.ps1 -Action count
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_obtain compare"
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "tly_obtain compare: wrote" -FromLine $n -TimeoutSec 60
```

Expected: the log line with the path (`...\Mods\TheLongestYear\obtainability-compare.md`), counts per verdict and the unresolved count. Read the report's summary, the first entries of each disagreement section, and the Unresolved sources list.

- [ ] **Step 4: Record it**

Add a top section to `STATUS.md`: date, what phase 1 built, the build line (items, ms, passes, unresolved), the nine spot checks with pass or what was wrong, the verdict counts, the report path, the three biggest disagreement patterns (for example "fish: new model earlier because fishing level is a condition, not a week"), and the most common unresolved query kinds. Add an Open entry to `TODO.md`: "Obtainability phase 2: Jeff reads obtainability-compare.md and rules on the disagreements before anything reads the model". No em dashes.

```bash
git add STATUS.md TODO.md
git commit -m "docs: obtainability phase 1 live check and comparison summary"
git push origin story
```

- [ ] **Step 5: Report to Jeff and stop**

Tell Jeff, in plain terms: the model built (item count, time, unresolved count), the spot checks, the verdict counts, where the report is, and the biggest disagreement patterns in one line each. Do not change any existing figure and do not wire the model into anything: phase 2 starts only when Jeff has read the report.
