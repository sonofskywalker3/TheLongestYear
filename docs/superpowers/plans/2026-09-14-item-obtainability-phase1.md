# Item Obtainability Model, Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a blind, runtime item obtainability model (every year-1 source per item, by week) plus `tly_obtain <item>` and `tly_obtain compare`, with nothing wired into gameplay.

**Architecture:** Pure rules in `src/TheLongestYear.Core/Obtainability/` take plain input records and return an `ObtainabilityModel`; direct sources are computed once, made items (crops, fruit, machines, recipes, ponds, geodes) are resolved by repeated passes until nothing changes. One glue class in the mod project reads the live game data assets into the input records at save load. A separate comparison class (outside the blind folder) lines the new model up against the existing item model and renders a Markdown report.

**Tech Stack:** C# net6.0, SMAPI 4 / Stardew Valley 1.6, xunit 2.4.1.

**Spec:** `docs/superpowers/specs/2026-09-14-item-obtainability-design.md`

## Global Constraints

- Branch `story`. Do NOT change `manifest.json`'s `Version` (parallel-branch rule in the workspace CLAUDE.md).
- One commit per task, then `git push origin story` right after it.
- **Blind:** no file under `src/TheLongestYear.Core/Obtainability/` and not `src/TheLongestYear/Loop/GameObtainabilityData.cs` may mention (code OR comments) any of: `ItemAvailabilityModel`, `ItemAvailability`, `ItemEffort`, `AvailabilityWeeks`, `TheLongestYear.Core.Availability`, `DefaultItemSeasonPins`, `QuantityBasisTables`, `ItemPoolBuilder`, `GameDataPools`, `GameEffortData`, `LegendaryFishRules`, `LocationGating`, `MineAreas`, `ItemAvailabilityBuilder`, `BundleGenerationTuning`. Allowed Core types: `Season`, `Calendar`, `BundleParsing`. Task 9 adds the guard test.
- Core has no game references; file-scoped namespaces; `Nullable` is enabled there. Mod project: block-scoped `namespace TheLongestYear.Loop`, `internal sealed class`, Nullable off.
- Files stay under 400 lines. Split rather than grow.
- No em dashes anywhere (code comments, strings, docs, commit messages).
- Weeks are 1-16 (Spring week 1 = days 1-7 of Spring; Winter 28 is week 16). A source's weeks mean "can newly be obtained in that week".
- Test command: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false` (add `--filter "FullyQualifiedName~<ClassName>"` for one class). The suite is 2166 passing at the start; it must stay green.
- Mod build without deploying: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`.
- Nothing reads the new model for gameplay: no board, gate, goal, pacing, sabotage or save-state change.
- Decompile references: PC 1.6 at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley\`; GameData types at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled\decompiled\StardewValley.GameData\`.

## File Structure

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/Obtainability/WeekMask.cs` | 16-bit week set and helpers |
| `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs` | `SourceKind`, `Reliability`, `ObtainConditions`, `ObtainSource` |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs` | `ObtainFilter`, the model and its queries |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` | plain input records the glue fills |
| `src/TheLongestYear.Core/Obtainability/ConditionSeasons.cs` | reads season/day/year/festival clauses out of game-state-query strings |
| `src/TheLongestYear.Core/Obtainability/SpawnSources.cs` | forage, location fish, crab pot, artifact spots, garbage cans, fishing trash |
| `src/TheLongestYear.Core/Obtainability/ShopSources.cs` | shops, the Traveling Cart, festival shops, Night Market boats, festival rewards |
| `src/TheLongestYear.Core/Obtainability/MineSources.cs` | mine nodes, monster drops, fishing treasure (code-only facts) |
| `src/TheLongestYear.Core/Obtainability/GrowSources.cs` | crops, greenhouse, Mixed Seeds, fruit trees |
| `src/TheLongestYear.Core/Obtainability/MadeSources.cs` | machines, cooking, crafting, animals, fish ponds, tappers, geode contents |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` | orchestration and the repeated-pass resolution |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs` | one-item description for the debug command |
| `src/TheLongestYear.Core/ObtainabilityComparison.cs` | comparison rows and Markdown report (NOT blind; reads the existing model through delegates) |
| `src/TheLongestYear/Loop/GameObtainabilityData.cs` | reads Data assets into `ObtainabilityInputs` |
| `src/TheLongestYear/ModEntry.cs` | build at save load, `tly_obtain` command + bridge case |
| `tests/TheLongestYear.Tests/Obtainability*Tests.cs` | one test file per Core file above |

---

### Task 1: Week sets, source types and the model

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/WeekMask.cs`
- Create: `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs`
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityModelTests.cs`

**Interfaces:**
- Produces: `WeekMask` (`None`, `All`, `Of(int)`, `Range(int,int)`, `ForSeason(Season)`, `ForSeasons(IEnumerable<Season>)`, `ForDays(int,int)`, `WeekOfDay(int)`, `Contains(int)`, `IsEmpty`, `Earliest`, `FromWeekOnward()`, `ShiftLater(int)`, `Except(WeekMask)`, `|`, `&`, `ToString()`), `SourceKind`, `Reliability`, `ObtainConditions`, `ObtainSource`, `ObtainFilter` (`Any`, `DependableOnly`, `Accepts`), `ObtainabilityModel` (`ItemIds`, `Count`, `Sources(string)`, `Weeks(string, ObtainFilter)`, `IsObtainable(string,int,ObtainFilter)`, `EarliestWeek(string, ObtainFilter)`).

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

namespace TheLongestYear.Core.Obtainability;

/// <summary>How an item is obtained.</summary>
public enum SourceKind
{
    Forage, ArtifactSpot, Fish, CrabPot, Crop, GreenhouseCrop, FruitTree, Shop, Cart, Machine,
    Cooking, Crafting, Animal, FishPond, MineNode, MonsterDrop, Geode, Tapper, Festival,
    NightMarket, Trash, GarbageCan, FishingTreasure,
}

/// <summary>Dependable sources yield on purpose (a spawn, a shop row, a machine); chance sources
/// depend on luck (the cart, drops, geodes, treasure). The model tags, the consuming rule decides
/// (Jeff, 2026-09-14, option A).</summary>
public enum Reliability { Dependable, Chance }

/// <summary>What a source needs. <see cref="Requires"/> is informational ("location:Desert",
/// "machine:(BC)12", "mail:ccPantry"); only <see cref="YearTwo"/> and <see cref="GingerIsland"/>
/// change what the default filters count.</summary>
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
}

/// <summary>One way to obtain an item: its kind, the weeks it works, how dependable it is, what it
/// needs, and a short origin note for the debug output.</summary>
public sealed record ObtainSource(
    SourceKind Kind, WeekMask Weeks, Reliability Reliability, ObtainConditions Conditions, string Detail);
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

    public bool Accepts(ObtainSource source)
        => (Reliabilities == null || Reliabilities.Contains(source.Reliability))
           && (Kinds == null || Kinds.Contains(source.Kind))
           && (IncludeYearTwo || !source.Conditions.YearTwo)
           && (IncludeGingerIsland || !source.Conditions.GingerIsland);
}

/// <summary>Every year-1 way to obtain every item, by week (spec 2026-09-14-item-obtainability).
/// Built at runtime from the installed game data; nothing reads it for gameplay in phase 1.</summary>
public sealed class ObtainabilityModel
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> _sources;

    public ObtainabilityModel(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        var normalized = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<ObtainSource>> kv in sources)
            normalized[BundleParsing.NormalizeItemId(kv.Key)] = kv.Value;
        _sources = normalized;
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
Expected: PASS, 5 tests.

- [ ] **Step 7: Run the whole suite, commit, push**

Run the full test command; expected 2171 passed.

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityModelTests.cs
git commit -m "obtainability: week sets, source types and the model's queries"
git push origin story
```

---

### Task 2: Reading seasons, days, years and festivals out of condition strings

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (only `FestivalDates` in this task; later tasks add the rest)
- Create: `src/TheLongestYear.Core/Obtainability/ConditionSeasons.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityConditionTests.cs`

**Interfaces:**
- Consumes: `WeekMask` (Task 1).
- Produces: `record FestivalDates(string Id, Season Season, int StartDay, int EndDay)` with `WeekMask Weeks`; `record ConditionReading(WeekMask Weeks, bool YearTwo, bool FewDays, bool Chance, IReadOnlyList<string> Other)`; `static ConditionReading ConditionSeasons.Read(string? condition, IReadOnlyDictionary<string, FestivalDates> festivals)`.

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

    [Fact]
    public void No_condition_is_every_week()
    {
        ConditionReading r = ConditionSeasons.Read(null, Festivals);
        Assert.Equal(WeekMask.All, r.Weeks);
        Assert.False(r.YearTwo || r.FewDays || r.Chance);
        Assert.Empty(r.Other);
    }

    [Theory]
    [InlineData("SEASON Spring", "1-4")]
    [InlineData("SEASON spring summer", "1-8")]
    [InlineData("LOCATION_SEASON Here fall", "9-12")]
    [InlineData("!SEASON Winter", "1-12")]
    [InlineData("SEASON Spring Fall, !SEASON Fall", "1-4")]
    public void Season_clauses_narrow_the_weeks(string condition, string expected)
        => Assert.Equal(expected, ConditionSeasons.Read(condition, Festivals).Weeks.ToString());

    [Fact]
    public void Year_two_is_flagged_not_counted()
    {
        Assert.True(ConditionSeasons.Read("YEAR 2", Festivals).YearTwo);
        Assert.False(ConditionSeasons.Read("YEAR 1", Festivals).YearTwo);
    }

    [Fact]
    public void Festival_and_day_clauses_are_few_days()
    {
        ConditionReading market = ConditionSeasons.Read("IS_PASSIVE_FESTIVAL_OPEN NightMarket", Festivals);
        Assert.Equal(WeekMask.Of(15), market.Weeks);
        Assert.True(market.FewDays);

        ConditionReading day = ConditionSeasons.Read("SEASON_DAY Summer 11", Festivals);
        Assert.Equal(WeekMask.Of(6), day.Weeks);
        Assert.True(day.FewDays);
    }

    [Fact]
    public void Random_clauses_are_chance_and_unknown_clauses_are_kept_as_notes()
    {
        ConditionReading r = ConditionSeasons.Read("RANDOM 0.1, PLAYER_HEARTS Current Abigail 4", Festivals);
        Assert.True(r.Chance);
        Assert.Equal(WeekMask.All, r.Weeks);
        Assert.Equal(new[] { "PLAYER_HEARTS Current Abigail 4" }, r.Other);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityConditionTests"`
Expected: build FAILS, `ConditionSeasons` not found.

- [ ] **Step 3: Write `ObtainabilityInputs.cs` (first record only)**

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

// ---- Plain input records. The glue (Loop/GameObtainabilityData) fills these from the live game
// ---- data assets at save load, so every rule here is testable without the game. Item ids are
// ---- QUALIFIED ("(O)24") except category references, which stay as negative numbers ("-75").

/// <summary>A passive festival's dates (Data/PassiveFestivals): Night Market, Squid Fest, Trout Derby.</summary>
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

/// <summary>What a game-state-query string says about WHEN: the weeks it can pass, whether it is a
/// year-2 row, whether it is open only a few days, and whether it is a dice roll. Clauses this reader
/// does not understand are kept verbatim in <see cref="Other"/> and do not narrow the weeks, because
/// a condition the model cannot judge must not silently hide a source.</summary>
public sealed record ConditionReading(
    WeekMask Weeks, bool YearTwo, bool FewDays, bool Chance, IReadOnlyList<string> Other);

public static class ConditionSeasons
{
    private const int FirstYearTwo = 2;

    public static ConditionReading Read(string? condition, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        WeekMask weeks = WeekMask.All;
        bool yearTwo = false, fewDays = false, chance = false;
        var other = new List<string>();
        if (string.IsNullOrWhiteSpace(condition))
            return new ConditionReading(weeks, false, false, false, other);

        foreach (string rawClause in condition.Split(','))
        {
            string clause = rawClause.Trim();
            if (clause.Length == 0) continue;
            bool negated = clause.StartsWith("!", StringComparison.Ordinal);
            string[] tokens = (negated ? clause.Substring(1) : clause)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string key = tokens[0].ToUpperInvariant();

            WeekMask? clauseWeeks = null;
            switch (key)
            {
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
                case "IS_PASSIVE_FESTIVAL_OPEN":
                    if (tokens.Length > 1 && festivals.TryGetValue(tokens[1], out FestivalDates? festival))
                    {
                        clauseWeeks = festival.Weeks;
                        fewDays = true;
                    }
                    else other.Add(clause);
                    break;
                case "YEAR":
                    if (!negated && tokens.Length > 1 && int.TryParse(tokens[1], out int minYear) && minYear >= FirstYearTwo)
                        yearTwo = true;
                    break;
                case "RANDOM":
                case "SYNCED_RANDOM":
                case "SYNCED_CHOICE":
                case "SYNCED_SUMMER_RAIN_RANDOM":
                    chance = true;
                    break;
                default:
                    other.Add(clause);
                    break;
            }

            if (clauseWeeks is WeekMask w)
                weeks &= negated ? WeekMask.All.Except(w) : w;
        }
        return new ConditionReading(weeks, yearTwo, fewDays, chance, other);
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
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityConditionTests"`
Expected: PASS, 9 tests (the theory counts 5).

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityConditionTests.cs
git commit -m "obtainability: read season, day, year and festival clauses from conditions"
git push origin story
```

---

### Task 3: Spawn sources (forage, fish, crab pot, artifact spots, garbage cans, fishing trash)

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/SpawnSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilitySpawnTests.cs`

**Interfaces:**
- Consumes: `WeekMask`, `ObtainSource`, `ObtainConditions`, `FestivalDates`, `ConditionSeasons.Read` (Tasks 1-2).
- Produces:
  - `record LocationSpawn(string Location, string ItemId, Season? Season, string? Condition, double Chance, int CatchLimit, bool RequireMagicBait)`
  - `record FishRow(string ItemId, bool IsTrap, string Weather, int MinFishingLevel)`
  - `record ArtifactSpotRow(string Location, string ItemId, string? Condition, double Chance)`
  - `record GarbageRow(string CanId, string ItemId, string? Condition)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> SpawnSources.Forage(IEnumerable<LocationSpawn>, IReadOnlyDictionary<string, FestivalDates>)`
  - `... SpawnSources.LocationFish(IEnumerable<LocationSpawn>, IReadOnlyDictionary<string, FishRow>, IReadOnlyDictionary<string, FestivalDates>)`
  - `... SpawnSources.CrabPot(IEnumerable<FishRow>)`
  - `... SpawnSources.ArtifactSpots(IEnumerable<ArtifactSpotRow>, IReadOnlyDictionary<string, FestivalDates>)`
  - `... SpawnSources.GarbageCans(IEnumerable<GarbageRow>, IReadOnlyDictionary<string, FestivalDates>)`
  - `... SpawnSources.FishingTrash()`
  - `static bool SpawnSources.IsIslandLocation(string location)`

- [ ] **Step 1: Write the failing tests**

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

    [Fact]
    public void Forage_takes_its_season_and_its_location()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)16", Season.Spring, null, 0.5, 0, false) };
        var (id, source) = SpawnSources.Forage(rows, Festivals).Single();
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
            new LocationSpawn("Beach", "(O)392", null, "SEASON Winter", 1.0, 0, false),
            new LocationSpawn("IslandWest", "(O)829", null, null, 1.0, 0, false),
        };
        var sources = SpawnSources.Forage(rows, Festivals).ToList();
        Assert.Equal(WeekMask.ForSeason(Season.Winter), sources[0].Source.Weeks);
        Assert.True(sources[1].Source.Conditions.GingerIsland);
    }

    [Fact]
    public void Festival_maps_only_open_on_their_days()
    {
        var rows = new[] { new LocationSpawn("Submarine", "(O)798", null, null, 1.0, 0, false) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0) };
        var source = SpawnSources.LocationFish(rows, fishRows, Festivals).Single().Source;
        Assert.Equal(WeekMask.Of(15), source.Weeks);
        Assert.True(source.Conditions.FewDays);
    }

    [Fact]
    public void Fish_carry_level_weather_and_catch_limit()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)775", Season.Winter, null, 1.0, 1, false) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)775"] = new FishRow("(O)775", false, "rainy", 6) };
        var source = SpawnSources.LocationFish(rows, fishRows, Festivals).Single().Source;
        Assert.Equal(SourceKind.Fish, source.Kind);
        Assert.Equal("Fishing", source.Conditions.Skill);
        Assert.Equal(6, source.Conditions.SkillLevel);
        Assert.True(source.Conditions.RainOnly);
        Assert.Equal(1, source.Conditions.CatchLimit);
    }

    [Fact]
    public void Trap_fish_come_from_crab_pots_all_year()
    {
        var rows = new[] { new FishRow("(O)717", true, "", 0), new FishRow("(O)142", false, "sunny", 0) };
        var (id, source) = SpawnSources.CrabPot(rows).Single();
        Assert.Equal("(O)717", id);
        Assert.Equal(SourceKind.CrabPot, source.Kind);
        Assert.Equal(WeekMask.All, source.Weeks);
        Assert.Contains("crafting:Crab Pot", source.Conditions.Requires);
    }

    [Fact]
    public void Luck_sources_are_chance()
    {
        var spots = SpawnSources.ArtifactSpots(new[] { new ArtifactSpotRow("Town", "(O)107", null, 0.05) }, Festivals).Single().Source;
        var cans = SpawnSources.GarbageCans(new[] { new GarbageRow("JoshHouse", "(O)168", null) }, Festivals).Single().Source;
        Assert.Equal(Reliability.Chance, spots.Reliability);
        Assert.Equal(SourceKind.GarbageCan, cans.Kind);
        Assert.Equal(new[] { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" },
            SpawnSources.FishingTrash().Select(t => t.ItemId).ToArray());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilitySpawnTests"`
Expected: build FAILS, `LocationSpawn` not found.

- [ ] **Step 3: Append the records to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One Data/Locations Forage or Fish row. <paramref name="Season"/> null means any season
/// unless <paramref name="Condition"/> names one.</summary>
public sealed record LocationSpawn(
    string Location, string ItemId, Season? Season, string? Condition, double Chance, int CatchLimit, bool RequireMagicBait);

/// <summary>One Data/Fish row, reduced: field 1 is difficulty or "trap", field 7 weather
/// ("sunny", "rainy", "both"), field 12 minimum fishing level.</summary>
public sealed record FishRow(string ItemId, bool IsTrap, string Weather, int MinFishingLevel);

public sealed record ArtifactSpotRow(string Location, string ItemId, string? Condition, double Chance);

/// <summary>One Data/GarbageCans item (from a can's Items, or BeforeAll/AfterAll with CanId "*").</summary>
public sealed record GarbageRow(string CanId, string ItemId, string? Condition);
```

- [ ] **Step 4: Write `SpawnSources.cs`**

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
        IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (LocationSpawn row in rows)
            if (Spawn(row, festivals, SourceKind.Forage, extra: null) is ObtainSource s)
                yield return (row.ItemId, s);
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> LocationFish(
        IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, FishRow> fishRows,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (LocationSpawn row in rows)
        {
            fishRows.TryGetValue(row.ItemId, out FishRow? fish);
            ObtainConditions Extra(ObtainConditions c) => c with
            {
                Skill = fish != null && fish.MinFishingLevel > 0 ? "Fishing" : c.Skill,
                SkillLevel = fish?.MinFishingLevel ?? 0,
                RainOnly = string.Equals(fish?.Weather, RainyWeather, StringComparison.OrdinalIgnoreCase),
                CatchLimit = row.CatchLimit,
                Requires = row.RequireMagicBait ? c.Requires.Append("item:(O)908 Magic Bait").ToList() : c.Requires,
            };
            if (Spawn(row, festivals, SourceKind.Fish, Extra) is ObtainSource s)
                yield return (row.ItemId, s);
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
        IEnumerable<ArtifactSpotRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (ArtifactSpotRow row in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            if (reading.Weeks.IsEmpty) continue;
            yield return (row.ItemId, new ObtainSource(
                SourceKind.ArtifactSpot, reading.Weeks, Reliability.Chance,
                Conditions(row.Location, reading),
                $"artifact spot, {row.Location}, chance {row.Chance:0.###}"));
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> GarbageCans(
        IEnumerable<GarbageRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (GarbageRow row in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            if (reading.Weeks.IsEmpty) continue;
            yield return (row.ItemId, new ObtainSource(
                SourceKind.GarbageCan, reading.Weeks, Reliability.Chance,
                ObtainConditions.None with { Requires = reading.Other, YearTwo = reading.YearTwo, FewDays = reading.FewDays },
                $"garbage can {row.CanId}"));
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FishingTrash()
    {
        foreach (string id in TrashIds)
            yield return (id, new ObtainSource(
                SourceKind.Trash, WeekMask.All, Reliability.Chance, ObtainConditions.None, "fishing trash"));
    }

    private static ObtainSource? Spawn(
        LocationSpawn row, IReadOnlyDictionary<string, FestivalDates> festivals, SourceKind kind,
        Func<ObtainConditions, ObtainConditions>? extra)
    {
        ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
        WeekMask weeks = reading.Weeks & (row.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All);
        bool fewDays = reading.FewDays;
        if (FestivalOnlyLocations.TryGetValue(row.Location, out string? festivalId)
            && festivals.TryGetValue(festivalId, out FestivalDates? festival))
        {
            weeks &= festival.Weeks;
            fewDays = true;
        }
        if (weeks.IsEmpty) return null;
        ObtainConditions conditions = Conditions(row.Location, reading) with { FewDays = fewDays };
        if (extra != null) conditions = extra(conditions);
        Reliability reliability = reading.Chance ? Reliability.Chance : Reliability.Dependable;
        return new ObtainSource(kind, weeks, reliability, conditions, $"{kind} at {row.Location}");
    }

    private static ObtainConditions Conditions(string location, ConditionReading reading)
        => ObtainConditions.None with
        {
            Requires = new[] { "location:" + location }.Concat(reading.Other).ToList(),
            YearTwo = reading.YearTwo,
            FewDays = reading.FewDays,
            GingerIsland = IsIslandLocation(location),
        };
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilitySpawnTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilitySpawnTests.cs
git commit -m "obtainability: spawn sources for forage, fish, crab pots, artifact spots, cans and trash"
git push origin story
```

---

### Task 4: Shop sources (shops, the cart, festival shops, Night Market boats, festival rewards)

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/ShopSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityShopTests.cs`

**Interfaces:**
- Consumes: Tasks 1-2.
- Produces:
  - `record ObjInfo(string QualifiedId, string Name, int Category, int Price, IReadOnlyList<string> ContextTags, bool ExcludeFromRandomSale)`
  - `record ShopRow(string ShopId, string ItemId, string? Condition, bool IsRecipe)` (ItemId may be a `RANDOM_ITEMS ...` query)
  - `static IEnumerable<(string ItemId, ObtainSource Source)> ShopSources.Stock(IEnumerable<ShopRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, FestivalDates>)`
  - `static IReadOnlyDictionary<string, WeekMask> ShopSources.RecipeWeeks(IEnumerable<ShopRow>, IReadOnlyDictionary<string, FestivalDates>)` keyed by the taught item's qualified id
  - `static IEnumerable<(string ItemId, ObtainSource Source)> ShopSources.FestivalRewards(IReadOnlyDictionary<string, FestivalDates>)`
  - `static IEnumerable<string> ShopSources.ExpandRandomItems(string query, IReadOnlyDictionary<string, ObjInfo>)`

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
    };

    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)775"] = new ObjInfo("(O)775", "Glacierfish", -4, 1000, new string[0], true),
        ["(O)800"] = new ObjInfo("(O)800", "Blobfish", -4, 900, new string[0], false),
        ["(O)Moss"] = new ObjInfo("(O)Moss", "Moss", -81, 5, new string[0], false),
    };

    [Fact]
    public void A_seasonal_seed_row_is_dependable_in_its_season()
    {
        var rows = new[] { new ShopRow("SeedShop", "(O)472", "SEASON Spring", false) };
        var (id, s) = ShopSources.Stock(rows, Objects, Festivals).Single();
        Assert.Equal("(O)472", id);
        Assert.Equal(SourceKind.Shop, s.Kind);
        Assert.Equal(Reliability.Dependable, s.Reliability);
        Assert.Equal(WeekMask.ForSeason(Season.Spring), s.Weeks);
        Assert.Contains("shop:SeedShop", s.Conditions.Requires);
    }

    [Fact]
    public void The_traveling_cart_expands_its_random_query_as_chance()
    {
        var rows = new[] { new ShopRow("Traveler", "RANDOM_ITEMS (O) 2 789 @isRandomSale @requirePrice", null, false) };
        var ids = ShopSources.Stock(rows, Objects, Festivals).ToList();
        Assert.Equal(new[] { "(O)24" }, ids.Select(x => x.ItemId).ToArray()); // 775 excluded from sale, 800 out of range, Moss not numeric
        Assert.All(ids, x => Assert.Equal(SourceKind.Cart, x.Source.Kind));
        Assert.All(ids, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
    }

    [Fact]
    public void Festival_shops_open_on_their_festival_day()
    {
        var rows = new[]
        {
            new ShopRow("Festival_Luau_Pierre", "(O)FishSmoker", null, false),
            new ShopRow("Festival_NightMarket_MagicBoat_Day2", "(O)Moss", null, false),
            new ShopRow("Festival_FestivalOfIce_TravelingMerchant", "RANDOM_ITEMS (O) 2 789 @isRandomSale", null, false),
        };
        var list = ShopSources.Stock(rows, Objects, Festivals).ToList();
        Assert.Equal(WeekMask.Of(6), list[0].Source.Weeks);          // Summer 11
        Assert.Equal(SourceKind.Festival, list[0].Source.Kind);
        Assert.Equal(WeekMask.Of(15), list[1].Source.Weeks);         // Winter 16
        Assert.Equal(SourceKind.NightMarket, list[1].Source.Kind);
        Assert.Equal(WeekMask.Of(14), list[2].Source.Weeks);         // Winter 8
        Assert.Equal(Reliability.Chance, list[2].Source.Reliability);
    }

    [Fact]
    public void Recipe_rows_teach_rather_than_sell()
    {
        var rows = new[] { new ShopRow("Saloon", "(O)196", "SEASON Fall", true) };
        Assert.Empty(ShopSources.Stock(rows, Objects, Festivals));
        Assert.Equal(WeekMask.ForSeason(Season.Fall), ShopSources.RecipeWeeks(rows, Festivals)["(O)196"]);
    }

    [Fact]
    public void Year_two_rows_are_flagged()
        => Assert.True(ShopSources.Stock(new[] { new ShopRow("SeedShop", "(O)476", "YEAR 2, SEASON Spring", false) }, Objects, Festivals)
            .Single().Source.Conditions.YearTwo);

    [Fact]
    public void Squid_fest_rewards_are_winter_12_to_13()
    {
        var rewards = ShopSources.FestivalRewards(Festivals).ToList();
        var book = rewards.Single(r => r.ItemId == "(O)Book_Crabbing").Source;
        Assert.Equal(WeekMask.Of(14), book.Weeks);
        Assert.True(book.Conditions.FewDays);
        Assert.Contains(rewards, r => r.ItemId == "(O)TentKit" && r.Source.Weeks == WeekMask.Of(7));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityShopTests"`
Expected: build FAILS, `ShopRow` not found.

- [ ] **Step 3: Append the records to `ObtainabilityInputs.cs`**

```csharp

/// <summary>Data/Objects essentials. <see cref="ContextTags"/> are the item's BASE tags as the game
/// computes them (ItemContextTagManager.GetBaseContextTags), including generated ones such as
/// "category_fruits" and "id_o_24", which machine rules match on.</summary>
public sealed record ObjInfo(
    string QualifiedId, string Name, int Category, int Price, IReadOnlyList<string> ContextTags, bool ExcludeFromRandomSale);

/// <summary>One Data/Shops stock row. <see cref="ItemId"/> is a qualified id or an item query
/// ("RANDOM_ITEMS (O) 2 789 @isRandomSale"). A recipe row teaches the recipe for that item.</summary>
public sealed record ShopRow(string ShopId, string ItemId, string? Condition, bool IsRecipe);
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
    private const string NightMarketShopPrefix = "Festival_NightMarket_";
    private const string TravelingMerchantSuffix = "_TravelingMerchant";
    private const string RandomItemsQuery = "RANDOM_ITEMS";
    private const string RandomSaleFlag = "@isRandomSale";
    private const string RequirePriceFlag = "@requirePrice";

    /// <summary>Festival shop to its day, from the festival-to-shop mapping in Event.cs 11818-11838.</summary>
    private static readonly IReadOnlyDictionary<string, (Season Season, int Day)> FestivalShopDays =
        new Dictionary<string, (Season, int)>(StringComparer.Ordinal)
        {
            ["Festival_EggFestival_Pierre"] = (Season.Spring, 13),
            ["Festival_FlowerDance_Pierre"] = (Season.Spring, 24),
            ["Festival_Luau_Pierre"] = (Season.Summer, 11),
            ["Festival_DanceOfTheMoonlightJellies_Pierre"] = (Season.Summer, 28),
            ["Festival_SpiritsEve_Pierre"] = (Season.Fall, 27),
            ["Festival_FestivalOfIce_TravelingMerchant"] = (Season.Winter, 8),
            ["Festival_FeastOfTheWinterStar_Pierre"] = (Season.Winter, 25),
        };

    /// <summary>Squid Fest rewards (GameLocation.cs 11458-11499) and Trout Derby rewards (11527-11562).</summary>
    private static readonly (string Festival, string ItemId)[] RewardTable =
    {
        ("SquidFest", "(O)DeluxeBait"), ("SquidFest", "(O)498"), ("SquidFest", "(O)MysteryBox"),
        ("SquidFest", "(O)242"), ("SquidFest", "(O)797"), ("SquidFest", "(O)395"),
        ("SquidFest", "(F)SquidKid_Painting"), ("SquidFest", "(O)Book_Crabbing"), ("SquidFest", "(O)265"),
        ("SquidFest", "(O)694"), ("SquidFest", "(O)166"), ("SquidFest", "(O)253"), ("SquidFest", "(H)SquidHat"),
        ("TroutDerby", "(O)TentKit"), ("TroutDerby", "(H)BucketHat"), ("TroutDerby", "(O)710"),
        ("TroutDerby", "(O)MysteryBox"), ("TroutDerby", "(O)72"), ("TroutDerby", "(F)MountedTrout_Painting"),
        ("TroutDerby", "(O)DeluxeBait"), ("TroutDerby", "(O)253"), ("TroutDerby", "(O)621"),
        ("TroutDerby", "(O)688"), ("TroutDerby", "(O)749"),
    };

    public static IEnumerable<(string ItemId, ObtainSource Source)> Stock(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (ShopRow row in rows)
        {
            if (row.IsRecipe) continue;
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            (SourceKind kind, WeekMask shopWeeks, bool fewDays) = Placement(row.ShopId, festivals);
            WeekMask weeks = reading.Weeks & shopWeeks;
            if (weeks.IsEmpty) continue;

            bool isRandom = row.ItemId.StartsWith(RandomItemsQuery, StringComparison.Ordinal);
            bool chance = reading.Chance || isRandom || kind == SourceKind.Cart
                          || row.ShopId.EndsWith(TravelingMerchantSuffix, StringComparison.Ordinal);
            var conditions = ObtainConditions.None with
            {
                Requires = new[] { "shop:" + row.ShopId }.Concat(reading.Other).ToList(),
                YearTwo = reading.YearTwo,
                FewDays = reading.FewDays || fewDays,
            };
            IEnumerable<string> ids = isRandom ? ExpandRandomItems(row.ItemId, objects) : new[] { row.ItemId };
            foreach (string id in ids)
                yield return (id, new ObtainSource(
                    kind, weeks, chance ? Reliability.Chance : Reliability.Dependable, conditions, $"shop {row.ShopId}"));
        }
    }

    public static IReadOnlyDictionary<string, WeekMask> RecipeWeeks(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var result = new Dictionary<string, WeekMask>(StringComparer.Ordinal);
        foreach (ShopRow row in rows.Where(r => r.IsRecipe))
        {
            WeekMask weeks = ConditionSeasons.Read(row.Condition, festivals).Weeks & Placement(row.ShopId, festivals).Weeks;
            result[row.ItemId] = result.TryGetValue(row.ItemId, out WeekMask existing) ? existing | weeks : weeks;
        }
        return result;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FestivalRewards(
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach ((string festivalId, string itemId) in RewardTable)
        {
            if (!festivals.TryGetValue(festivalId, out FestivalDates? festival)) continue;
            yield return (itemId, new ObtainSource(
                SourceKind.Festival, festival.Weeks, Reliability.Dependable,
                ObtainConditions.None with { FewDays = true, Requires = new[] { "festival:" + festivalId } },
                $"{festivalId} reward"));
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
            string bare = obj.QualifiedId.Substring(qualifier.Length);
            bool numeric = int.TryParse(bare, out int id);
            if (ranged && (!numeric || id < numbers[0] || id > numbers[1])) continue;
            if (randomSale && obj.ExcludeFromRandomSale) continue;
            if (requirePrice && obj.Price <= 0) continue;
            yield return obj.QualifiedId;
        }
    }

    private static (SourceKind Kind, WeekMask Weeks, bool FewDays) Placement(
        string shopId, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        if (shopId == CartShopId) return (SourceKind.Cart, WeekMask.All, false);
        if (shopId.StartsWith(NightMarketShopPrefix, StringComparison.Ordinal))
            return festivals.TryGetValue("NightMarket", out FestivalDates? market)
                ? (SourceKind.NightMarket, market.Weeks, true)
                : (SourceKind.NightMarket, WeekMask.All, true);
        if (FestivalShopDays.TryGetValue(shopId, out (Season Season, int Day) day))
            return (SourceKind.Festival, WeekMask.Of(WeekMask.WeekOfDay(Calendar.DayOfYear((int)day.Season, day.Day))), true);
        if (shopId.StartsWith(FestivalShopPrefix, StringComparison.Ordinal))
            return (SourceKind.Festival, WeekMask.All, true);
        return (SourceKind.Shop, WeekMask.All, false);
    }
}
```

Note for the implementer: the ordering in `A_seasonal...` and `Festival_shops_...` depends on `Stock` yielding rows in input order; the random expansion is ordered by id. Keep both.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityShopTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityShopTests.cs
git commit -m "obtainability: shop, cart, festival shop, Night Market and festival reward sources"
git push origin story
```

---

### Task 5: Mine sources (nodes, monster drops, fishing treasure)

These facts live only in the game's code, not its data, so they are tables here. Every value cites
the PC 1.6 decompile line it came from. Floors are recorded as a `Requires` note, never turned into
weeks: the model says what is possible, and a mine is open from week 1.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append one record)
- Create: `src/TheLongestYear.Core/Obtainability/MineSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityMineTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `record MonsterDropRow(string Monster, string ItemId, double Chance)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MineSources.Nodes()`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MineSources.MonsterDrops(IEnumerable<MonsterDropRow>)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MineSources.FishingTreasure()`
  - `static string? MineSources.MonsterFloor(string monster)` (null when the monster is not a mine spawn)

- [ ] **Step 1: Write the failing tests**

```csharp
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
        var copper = nodes.Single(n => n.ItemId == "(O)378" && n.Source.Kind == SourceKind.MineNode).Source;
        Assert.Equal(Reliability.Dependable, copper.Reliability);
        Assert.Equal(WeekMask.All, copper.Weeks);
        Assert.Contains("mines:floor 1", copper.Conditions.Requires);
        var iridium = nodes.Single(n => n.ItemId == "(O)386").Source;
        Assert.Contains("location:SkullCave", iridium.Conditions.Requires);
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
        var list = MineSources.MonsterDrops(rows).ToList();
        Assert.All(list, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
        Assert.Contains("mines:floor 40", list[0].Source.Conditions.Requires);
        Assert.Contains("monster:Some Modded Beast", list[1].Source.Conditions.Requires);
        Assert.Equal("floor 1", MineSources.MonsterFloor("Green Slime"));
        Assert.Null(MineSources.MonsterFloor("Some Modded Beast"));
    }

    [Fact]
    public void Treasure_is_chance_and_rice_shoots_only_in_spring()
    {
        var treasure = MineSources.FishingTreasure().ToList();
        Assert.All(treasure, t => Assert.Equal(SourceKind.FishingTreasure, t.Source.Kind));
        Assert.All(treasure, t => Assert.Equal(Reliability.Chance, t.Source.Reliability));
        Assert.Equal(WeekMask.ForSeason(Season.Spring), treasure.Single(t => t.ItemId == "(O)273").Source.Weeks);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMineTests"`
Expected: build FAILS, `MineSources` not found.

- [ ] **Step 3: Append the record to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One Data/Monsters drop (field 6 is "id chance id chance ...").</summary>
public sealed record MonsterDropRow(string Monster, string ItemId, double Chance);
```

- [ ] **Step 4: Write `MineSources.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Mine nodes, monster drops and fishing treasure. These come from game CODE, not data, so
/// the facts are tables; each line cites the PC 1.6 decompile.</summary>
public static class MineSources
{
    private const string SkullCave = "location:SkullCave";

    /// <summary>What breaking stones yields, and from which floor (MineShaft.cs createLitterObject
    /// 4321-4658, getMineArea 3757-3827, gem nodes 3962-3975, geodes from stones 3642-3663, coal
    /// 3665-3673).</summary>
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

    /// <summary>First floor each mine monster spawns on (MineShaft.cs getMonsterForThisLevel
    /// 3999-4318). Skull Cavern monsters use floor 121.</summary>
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

    /// <summary>Fishing treasure chest contents (FishingRod.cs 2426-2722). All chance.</summary>
    private static readonly (string ItemId, string Note)[] TreasureTable =
    {
        ("(O)378", "case 0 ore"), ("(O)388", "case 0 wood"), ("(O)390", "case 0 stone"), ("(O)382", "case 0 coal"),
        ("(O)380", "case 0 iron ore, depth 3"), ("(O)384", "case 0 gold ore, depth 4"), ("(O)386", "case 0 iridium ore, depth 5"),
        ("(O)687", "case 1 dressed spinner, Fishing 6"), ("(O)685", "case 1 bait"), ("(O)DeluxeBait", "case 1 deluxe bait, Fishing 6"),
        ("(O)102", "case 2 lost book"), ("(O)535", "case 2/3 geode"), ("(O)536", "case 3 frozen geode"), ("(O)537", "case 3 magma geode"),
        ("(O)72", "case 3 diamond"), ("(O)770", "case 3 mixed seeds"), ("(O)166", "case 3 treasure chest"),
        ("(O)74", "case 3 prismatic shard, Fishing 6"), ("(O)127", "case 3"), ("(O)126", "case 3"), ("(O)527", "case 3"),
        ("(O)MysteryBox", "mystery box roll"), ("(O)273", "rice shoot, Spring, not the beach (2446)"),
    };

    public static string? MonsterFloor(string monster)
        => MonsterFloors.TryGetValue(monster, out int floor) ? (floor >= 121 ? "Skull Cavern" : $"floor {floor}") : null;

    public static IEnumerable<(string ItemId, ObtainSource Source)> Nodes()
    {
        foreach ((string id, string where, Reliability reliability, string note) in NodeTable)
            yield return (id, new ObtainSource(
                SourceKind.MineNode, WeekMask.All, reliability,
                ObtainConditions.None with { Requires = new[] { where } }, note));
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> MonsterDrops(IEnumerable<MonsterDropRow> rows)
    {
        foreach (MonsterDropRow row in rows)
        {
            string requires = MonsterFloors.TryGetValue(row.Monster, out int floor)
                ? (floor >= 121 ? SkullCave : $"mines:floor {floor}")
                : "monster:" + row.Monster;
            yield return (row.ItemId, new ObtainSource(
                SourceKind.MonsterDrop, WeekMask.All, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { requires } },
                $"{row.Monster} drop, chance {row.Chance:0.###}"));
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FishingTreasure()
    {
        foreach ((string id, string note) in TreasureTable)
        {
            WeekMask weeks = id == "(O)273" ? WeekMask.ForSeason(Season.Spring) : WeekMask.All;
            yield return (id, new ObtainSource(
                SourceKind.FishingTreasure, weeks, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { "fishing:treasure chest" } }, note));
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMineTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityMineTests.cs
git commit -m "obtainability: mine node, monster drop and fishing treasure sources from game code"
git push origin story
```

---

### Task 6: Grown sources (crops, greenhouse, Mixed Seeds, fruit trees)

A grown item is obtainable in the weeks its seed or sapling is, pushed later by growth and held to its
seasons. These rules read a snapshot model (the previous resolution pass) for the seed's weeks.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/GrowSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityGrowTests.cs`

**Interfaces:**
- Consumes: `ObtainabilityModel`, `ObtainFilter`, `WeekMask` (Task 1).
- Produces:
  - `record CropRow(string SeedId, string HarvestId, IReadOnlyList<Season> Seasons, int GrowthDays, bool Regrows)`
  - `record FruitRow(string ItemId, Season? Season, double Chance)`
  - `record FruitTreeRow(string SaplingId, IReadOnlyList<Season> TreeSeasons, IReadOnlyList<FruitRow> Fruit)`
  - `static WeekMask GrowSources.Harvest(WeekMask seedWeeks, IReadOnlyList<Season> seasons, int growthDays, bool regrows)`
  - `static WeekMask GrowSources.Greenhouse(WeekMask seedWeeks, int growthDays)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> GrowSources.Crops(IEnumerable<CropRow>, ObtainabilityModel snapshot)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> GrowSources.FruitTrees(IEnumerable<FruitTreeRow>, ObtainabilityModel snapshot)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> SourcePair.Of(SourceKind kind, WeekMask dependable, WeekMask any, ObtainConditions conditions, string detail)` (in `GrowSources.cs`; reused by Task 7)

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityGrowTests
{
    private static ObtainabilityModel Snapshot(params (string Id, SourceKind Kind, WeekMask Weeks, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(r.Kind, r.Weeks, r.R, ObtainConditions.None, "test")).ToList()));

    [Fact]
    public void A_four_day_spring_crop_harvests_all_spring_but_not_past_it()
        => Assert.Equal("1-4", GrowSources.Harvest(WeekMask.ForSeason(Season.Spring), new[] { Season.Spring }, 4, false).ToString());

    [Fact]
    public void A_regrowing_two_season_crop_keeps_yielding_to_the_end_of_its_run()
        => Assert.Equal("7-12", GrowSources.Harvest(
            WeekMask.ForSeasons(new[] { Season.Summer, Season.Fall }), new[] { Season.Summer, Season.Fall }, 14, true).ToString());

    [Fact]
    public void The_greenhouse_grows_from_the_first_seed_week_plus_growth()
        => Assert.Equal("2-16", GrowSources.Greenhouse(WeekMask.ForSeason(Season.Spring), 4).ToString());

    [Fact]
    public void Crops_split_dependable_seed_weeks_from_chance_ones()
    {
        var snapshot = Snapshot(
            ("(O)472", SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable),
            ("(O)472", SourceKind.Cart, WeekMask.All, Reliability.Chance));
        var rows = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, false) };
        var sources = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = sources.Where(s => s.Kind == SourceKind.Crop).ToList();
        Assert.Single(outdoor);
        Assert.Equal(Reliability.Dependable, outdoor[0].Reliability);
        var greenhouse = sources.Where(s => s.Kind == SourceKind.GreenhouseCrop).ToList();
        Assert.Contains(greenhouse, s => s.Weeks.Contains(14));
        Assert.All(greenhouse, s => Assert.Contains("mail:ccPantry", s.Conditions.Requires));
    }

    [Fact]
    public void Mixed_seeds_give_that_seasons_pool_and_the_greenhouse_gives_every_pool_in_winter()
    {
        var snapshot = Snapshot(("(O)770", SourceKind.Forage, WeekMask.All, Reliability.Chance));
        var rows = new[]
        {
            new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, false),
            new CropRow("(O)487", "(O)270", new[] { Season.Summer, Season.Fall }, 14, true),
            new CropRow("(O)770", "(O)770", new Season[0], 1, false),
        };
        var parsnip = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        Assert.Contains(parsnip, s => s.Kind == SourceKind.Crop && s.Detail.Contains("Mixed Seeds") && s.Weeks.ToString() == "1-4");
        Assert.Contains(parsnip, s => s.Kind == SourceKind.GreenhouseCrop && s.Detail.Contains("Mixed Seeds") && s.Weeks.Contains(15));
        Assert.DoesNotContain(GrowSources.Crops(rows, snapshot), s => s.ItemId == "(O)770");
    }

    [Fact]
    public void A_fruit_tree_fruits_from_maturity_in_its_season_and_a_spring_sapling_misses_spring()
    {
        var snapshot = Snapshot(
            ("(O)633", SourceKind.Shop, WeekMask.All, Reliability.Dependable),   // apple sapling
            ("(O)628", SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable)); // cherry sapling
        var rows = new[]
        {
            new FruitTreeRow("(O)633", new[] { Season.Fall }, new[] { new FruitRow("(O)613", null, 1.0) }),
            new FruitTreeRow("(O)628", new[] { Season.Spring }, new[] { new FruitRow("(O)638", null, 1.0) }),
        };
        var all = GrowSources.FruitTrees(rows, snapshot).ToList();
        Assert.Equal("9-12", all.Single(s => s.ItemId == "(O)613" && s.Source.Kind == SourceKind.FruitTree).Source.Weeks.ToString());
        Assert.DoesNotContain(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.FruitTree);
        Assert.Contains(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.GreenhouseCrop);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityGrowTests"`
Expected: build FAILS, `GrowSources` not found.

- [ ] **Step 3: Append the records to `ObtainabilityInputs.cs`**

```csharp

/// <summary>One Data/Crops row: keyed by seed, <see cref="GrowthDays"/> is the sum of DaysInPhase,
/// <see cref="Seasons"/> empty means any season.</summary>
public sealed record CropRow(string SeedId, string HarvestId, IReadOnlyList<Season> Seasons, int GrowthDays, bool Regrows);

public sealed record FruitRow(string ItemId, Season? Season, double Chance);

/// <summary>One Data/FruitTrees row, keyed by sapling. A fruit's own season overrides the tree's.</summary>
public sealed record FruitTreeRow(string SaplingId, IReadOnlyList<Season> TreeSeasons, IReadOnlyList<FruitRow> Fruit);
```

- [ ] **Step 4: Write `GrowSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

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

/// <summary>Crops, greenhouse crops, Mixed Seeds and fruit trees.</summary>
public static class GrowSources
{
    private const string MixedSeedsId = "(O)770";
    private const string GreenhouseUnlock = "mail:ccPantry";   // Farm.cs 1132, GreenhouseBuilding.cs 47
    private const int FruitTreeMaturityDays = 28;              // FruitTree.cs 66
    private const int MinGrowthDays = 1;

    /// <summary>What Mixed Seeds become, by the season they are planted in (Crop.cs 294-320, 414-433).
    /// 473 resolves to 472. Winter picks a random other season's pool, which only the greenhouse can
    /// grow.</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> MixedSeedPools = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)472", "(O)474", "(O)475" },
        [Season.Summer] = new[] { "(O)487", "(O)483", "(O)482", "(O)484" },
        [Season.Fall] = new[] { "(O)487", "(O)488", "(O)489", "(O)490" },
    };

    public static WeekMask Harvest(WeekMask seedWeeks, IReadOnlyList<Season> seasons, int growthDays, bool regrows)
    {
        WeekMask inSeason = seasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(seasons);
        int growth = Math.Max(MinGrowthDays, growthDays);
        WeekMask result = WeekMask.None;
        for (int plantDay = 1; plantDay <= Calendar.DaysPerYear; plantDay++)
        {
            int plantWeek = WeekMask.WeekOfDay(plantDay);
            if (!seedWeeks.Contains(plantWeek) || !inSeason.Contains(plantWeek)) continue;
            int harvestDay = plantDay + growth;
            if (harvestDay > Calendar.DaysPerYear) continue;
            if (!StaysInSeason(plantDay, harvestDay, inSeason)) continue;
            int harvestWeek = WeekMask.WeekOfDay(harvestDay);
            result |= WeekMask.Of(harvestWeek);
            if (regrows)
                result |= WeekMask.Range(harvestWeek, RunEndWeek(harvestWeek, inSeason));
        }
        return result;
    }

    public static WeekMask Greenhouse(WeekMask seedWeeks, int growthDays)
        => seedWeeks.Earliest is int first
            ? WeekMask.FromWeekOnwardOf(first + (int)Math.Ceiling(Math.Max(MinGrowthDays, growthDays) / (double)Calendar.DaysPerWeek))
            : WeekMask.None;

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
                Harvest(dep, crop.Seasons, crop.GrowthDays, crop.Regrows),
                Harvest(any, crop.Seasons, crop.GrowthDays, crop.Regrows), outdoor, $"grown from {crop.SeedId}"))
                yield return (crop.HarvestId, s);
            var indoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId, GreenhouseUnlock } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop,
                Greenhouse(dep, crop.GrowthDays), Greenhouse(any, crop.GrowthDays), indoor, $"greenhouse, from {crop.SeedId}"))
                yield return (crop.HarvestId, s);
        }

        WeekMask mixed = snapshot.Weeks(MixedSeedsId, ObtainFilter.Any);
        if (mixed.IsEmpty) yield break;
        foreach ((Season season, string[] seeds) in MixedSeedPools)
            foreach (string seed in seeds)
            {
                if (!bySeed.TryGetValue(seed, out CropRow? crop)) continue;
                WeekMask planted = mixed & WeekMask.ForSeason(season);
                WeekMask outdoorWeeks = Harvest(planted, crop.Seasons, crop.GrowthDays, crop.Regrows);
                if (!outdoorWeeks.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.Crop, outdoorWeeks, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId } }, $"Mixed Seeds in {season}"));
                WeekMask indoorPlanted = planted | (mixed & WeekMask.ForSeason(Season.Winter));
                WeekMask indoorWeeks = Greenhouse(indoorPlanted, crop.GrowthDays);
                if (!indoorWeeks.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.GreenhouseCrop, indoorWeeks, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId, GreenhouseUnlock } },
                        $"Mixed Seeds in the greenhouse ({season} pool)"));
            }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FruitTrees(IEnumerable<FruitTreeRow> rows, ObtainabilityModel snapshot)
    {
        int maturityWeeks = FruitTreeMaturityDays / Calendar.DaysPerWeek;
        foreach (FruitTreeRow tree in rows)
        {
            WeekMask depMature = Mature(snapshot.Weeks(tree.SaplingId, ObtainFilter.DependableOnly), maturityWeeks);
            WeekMask anyMature = Mature(snapshot.Weeks(tree.SaplingId, ObtainFilter.Any), maturityWeeks);
            foreach (FruitRow fruit in tree.Fruit)
            {
                WeekMask season = fruit.Season is Season s ? WeekMask.ForSeason(s)
                    : tree.TreeSeasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(tree.TreeSeasons);
                var outdoor = ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId } };
                foreach (ObtainSource src in SourcePair.Of(SourceKind.FruitTree, depMature & season, anyMature & season,
                    outdoor, $"fruit tree from {tree.SaplingId}"))
                    yield return (fruit.ItemId, src);
                var indoor = ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId, GreenhouseUnlock } };
                foreach (ObtainSource src in SourcePair.Of(SourceKind.GreenhouseCrop, depMature, anyMature,
                    indoor, $"fruit tree in the greenhouse from {tree.SaplingId}"))
                    yield return (fruit.ItemId, src);
            }
        }
    }

    private static WeekMask Mature(WeekMask saplingWeeks, int maturityWeeks)
        => saplingWeeks.Earliest is int first ? WeekMask.FromWeekOnwardOf(first + maturityWeeks) : WeekMask.None;

    private static bool StaysInSeason(int fromDay, int toDay, WeekMask inSeason)
    {
        for (int day = fromDay; day <= toDay; day += Calendar.DaysPerWeek)
            if (!inSeason.Contains(WeekMask.WeekOfDay(day))) return false;
        return inSeason.Contains(WeekMask.WeekOfDay(toDay));
    }

    private static int RunEndWeek(int week, WeekMask inSeason)
    {
        int end = week;
        while (end + 1 <= WeekMask.LastWeek && inSeason.Contains(end + 1)) end++;
        return end;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityGrowTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityGrowTests.cs
git commit -m "obtainability: crops, greenhouse, Mixed Seeds and fruit trees from seed and sapling weeks"
git push origin story
```

---

### Task 7: Made sources (machines, recipes, animals, fish ponds, tappers, geode contents)

A made item is obtainable in the weeks its inputs are (Jeff, 2026-09-14, option A). Machines take one
input, so their weeks are the union over accepted inputs, pushed later by processing time. Recipes need
every ingredient in the same week ("newly obtainable": stockpiling is the consuming rule's question), so
their weeks are the intersection. Like Task 6 these read a snapshot model.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append records)
- Create: `src/TheLongestYear.Core/Obtainability/MadeSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs`

**Interfaces:**
- Consumes: Task 1, `SourcePair.Of` (Task 6), `ObjInfo` (Task 4).
- Produces:
  - `record MachineRow(string MachineId, string? RequiredItemId, IReadOnlyList<string> RequiredTags, IReadOnlyList<string> OutputIds, int MinutesUntilReady, int DaysUntilReady)`
  - `record RecipeRow(string Name, IReadOnlyList<string> Ingredients, string OutputId, string Unlock, bool IsCooking)`
  - `record AnimalRow(string AnimalId, string House, int PurchasePrice, IReadOnlyList<string> Produce, IReadOnlyList<string> DeluxeProduce)`
  - `record PondProduct(string ItemId, int RequiredPopulation, double Chance)`
  - `record PondRow(IReadOnlyList<string> RequiredTags, IReadOnlyList<PondProduct> Products)`
  - `record TapRow(string TreeId, string ItemId, int DaysUntilReady)`
  - `record GeodeDropRow(string GeodeId, string ItemId, double Chance)`
  - `static int MadeSources.ProcessingWeeks(int minutes, int days)`
  - `static IEnumerable<(string ItemId, ObtainSource Source)> MadeSources.Machines(IEnumerable<MachineRow>, IReadOnlyDictionary<string, ObjInfo>, ObtainabilityModel)`
  - `... MadeSources.Recipes(IEnumerable<RecipeRow>, IReadOnlyDictionary<string, ObjInfo>, IReadOnlyDictionary<string, WeekMask> recipeShopWeeks, ObtainabilityModel)`
  - `... MadeSources.Animals(IEnumerable<AnimalRow>)`
  - `... MadeSources.Ponds(IEnumerable<PondRow>, IReadOnlyDictionary<string, ObjInfo>, ObtainabilityModel)`
  - `... MadeSources.Tappers(IEnumerable<TapRow>)`
  - `... MadeSources.Geodes(IEnumerable<GeodeDropRow>, IReadOnlyCollection<string> geodesUsingDefaultTable, ObtainabilityModel)`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityMadeTests
{
    private static ObtainabilityModel Snapshot(params (string Id, WeekMask Weeks, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(SourceKind.Forage, r.Weeks, r.R, ObtainConditions.None, "test")).ToList()));

    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)613"] = new ObjInfo("(O)613", "Apple", -79, 100, new[] { "category_fruits", "id_o_613" }, false),
        ["(O)254"] = new ObjInfo("(O)254", "Melon", -79, 250, new[] { "category_fruits", "id_o_254" }, false),
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new[] { "fish_pond", "id_o_142" }, false),
    };

    [Fact]
    public void Processing_time_rounds_down_to_whole_weeks()
    {
        Assert.Equal(0, MadeSources.ProcessingWeeks(4000, 0));
        Assert.Equal(1, MadeSources.ProcessingWeeks(0, 7));
        Assert.Equal(1, MadeSources.ProcessingWeeks(10000, 0));
    }

    [Fact]
    public void A_keg_makes_wine_in_the_weeks_its_fruit_exists_plus_a_week()
    {
        var snapshot = Snapshot(("(O)613", WeekMask.ForSeason(Season.Fall), Reliability.Dependable),
                                ("(O)254", WeekMask.ForSeason(Season.Summer), Reliability.Dependable));
        var rows = new[] { new MachineRow("(BC)12", null, new[] { "category_fruits" }, new[] { "(O)348" }, 0, 7) };
        var wine = MadeSources.Machines(rows, Objects, snapshot).Single();
        Assert.Equal("(O)348", wine.ItemId);
        Assert.Equal("6-13", wine.Source.Weeks.ToString());
        Assert.Contains("machine:(BC)12", wine.Source.Conditions.Requires);
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
            new RecipeRow("Skill Craft", new[] { "(O)24" }, "(BC)902", "Farming 3", false),
        };
        var shopWeeks = new Dictionary<string, WeekMask> { ["(O)901"] = WeekMask.Of(3) };
        var list = MadeSources.Recipes(rows, Objects, shopWeeks, snapshot).ToList();
        var dish = list.Where(x => x.ItemId == "(O)900").Select(x => x.Source).Single();
        Assert.Equal("3-4", dish.Weeks.ToString());
        Assert.Equal(Reliability.Chance, dish.Reliability);
        Assert.Equal("3-4", list.Single(x => x.ItemId == "(O)901").Source.Weeks.ToString());
        var craft = list.Single(x => x.ItemId == "(BC)902").Source;
        Assert.Equal(SourceKind.Crafting, craft.Kind);
        Assert.Equal("Farming", craft.Conditions.Skill);
        Assert.Equal(3, craft.Conditions.SkillLevel);
    }

    [Fact]
    public void Animals_tappers_ponds_and_geodes()
    {
        var animals = MadeSources.Animals(new[] { new AnimalRow("White Chicken", "Coop", 800, new[] { "(O)176" }, new[] { "(O)174" }) }).ToList();
        Assert.Equal(2, animals.Count);
        Assert.All(animals, a => Assert.Equal(WeekMask.All, a.Source.Weeks));

        var tap = MadeSources.Tappers(new[] { new TapRow("1", "(O)725", 7) }).Single().Source;
        Assert.Contains("crafting:Tapper", tap.Conditions.Requires);

        var snapshot = Snapshot(("(O)142", WeekMask.Of(5), Reliability.Dependable),
                                ("(O)535", WeekMask.Range(2, 3), Reliability.Chance));
        var roe = MadeSources.Ponds(new[] { new PondRow(new[] { "fish_pond" }, new[] { new PondProduct("(O)812", 1, 0.5) }) }, Objects, snapshot).Single().Source;
        Assert.Equal("5-16", roe.Weeks.ToString());
        Assert.Equal(Reliability.Chance, roe.Reliability);

        var geode = MadeSources.Geodes(new GeodeDropRow[0], new[] { "(O)535" }, snapshot).ToList();
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

/// <summary>One Data/Machines output rule x trigger. No required item and no tags means the machine
/// needs no input (a Bee House, a Mushroom Log).</summary>
public sealed record MachineRow(
    string MachineId, string? RequiredItemId, IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> OutputIds, int MinutesUntilReady, int DaysUntilReady);

/// <summary>A cooking or crafting recipe. Ingredients are qualified ids or negative category numbers.
/// <see cref="Unlock"/> is the raw unlock field ("default", "none", "Farming 3", "s Farming 3",
/// "f Robin 7", "l 4").</summary>
public sealed record RecipeRow(string Name, IReadOnlyList<string> Ingredients, string OutputId, string Unlock, bool IsCooking);

public sealed record AnimalRow(string AnimalId, string House, int PurchasePrice, IReadOnlyList<string> Produce, IReadOnlyList<string> DeluxeProduce);

public sealed record PondProduct(string ItemId, int RequiredPopulation, double Chance);

/// <summary>One Data/FishPondData entry: fish with every required tag live in it.</summary>
public sealed record PondRow(IReadOnlyList<string> RequiredTags, IReadOnlyList<PondProduct> Products);

public sealed record TapRow(string TreeId, string ItemId, int DaysUntilReady);

/// <summary>One Data/Objects GeodeDrops entry for a geode item.</summary>
public sealed record GeodeDropRow(string GeodeId, string ItemId, double Chance);
```

- [ ] **Step 4: Write `MadeSources.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Machines, cooking and crafting, animals, fish ponds, tappers and geode contents.</summary>
public static class MadeSources
{
    private const int MinutesPerDay = 1440;
    private const string NotTag = "!";
    private const string SkillUnlockPrefix = "s";
    private const string GeodeOpener = "shop:Blacksmith";
    private static readonly string[] NoUnlockWords = { "default", "none", "null", "" };
    private static readonly string[] Skills = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };

    /// <summary>The code-only default geode contents (Utility.cs getTreasureFromGeode 6397-6647): half
    /// the time stone/clay or an area mineral, otherwise ore and coal by geode type.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> DefaultGeodeTable = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["(O)535"] = new[] { "(O)390", "(O)330", "(O)86", "(O)378", "(O)380", "(O)382" },
        ["(O)536"] = new[] { "(O)390", "(O)330", "(O)84", "(O)378", "(O)380", "(O)382", "(O)384" },
        ["(O)749"] = new[] { "(O)390", "(O)330", "(O)82", "(O)84", "(O)86", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" },
    };
    private static readonly string[] OtherGeodeDefault = { "(O)390", "(O)330", "(O)82", "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" };

    public static int ProcessingWeeks(int minutes, int days)
        => (Math.Max(0, minutes) / MinutesPerDay + Math.Max(0, days)) / Calendar.DaysPerWeek;

    public static IEnumerable<(string ItemId, ObtainSource Source)> Machines(
        IEnumerable<MachineRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot)
    {
        foreach (MachineRow rule in rows)
        {
            WeekMask dep, any;
            string input;
            if (rule.RequiredItemId == null && rule.RequiredTags.Count == 0)
            {
                dep = any = WeekMask.All;
                input = "no input";
            }
            else
            {
                IEnumerable<string> inputs = rule.RequiredItemId != null
                    ? new[] { rule.RequiredItemId }
                    : objects.Values.Where(o => MatchesTags(o, rule.RequiredTags)).Select(o => o.QualifiedId);
                dep = any = WeekMask.None;
                foreach (string id in inputs)
                {
                    dep |= snapshot.Weeks(id, ObtainFilter.DependableOnly);
                    any |= snapshot.Weeks(id, ObtainFilter.Any);
                }
                int shift = ProcessingWeeks(rule.MinutesUntilReady, rule.DaysUntilReady);
                dep = dep.ShiftLater(shift);
                any = any.ShiftLater(shift);
                input = rule.RequiredItemId ?? string.Join(" ", rule.RequiredTags);
            }
            var conditions = ObtainConditions.None with { Requires = new[] { "machine:" + rule.MachineId } };
            foreach (string output in rule.OutputIds)
                foreach (ObtainSource s in SourcePair.Of(SourceKind.Machine, dep, any, conditions, $"{rule.MachineId} from {input}"))
                    yield return (output, s);
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
            if (recipeShopWeeks.TryGetValue(recipe.OutputId, out WeekMask taught))
            {
                dep &= taught.FromWeekOnward();
                any &= taught.FromWeekOnward();
            }
            SourceKind kind = recipe.IsCooking ? SourceKind.Cooking : SourceKind.Crafting;
            ObtainConditions conditions = UnlockConditions(recipe);
            foreach (ObtainSource s in SourcePair.Of(kind, dep, any, conditions, $"recipe {recipe.Name}"))
                yield return (recipe.OutputId, s);
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Animals(IEnumerable<AnimalRow> rows)
    {
        foreach (AnimalRow animal in rows)
        {
            var requires = new List<string> { "building:" + animal.House };
            if (animal.PurchasePrice <= 0) requires.Add("animal:" + animal.AnimalId + " (not sold)");
            var regular = ObtainConditions.None with { Requires = requires };
            foreach (string id in animal.Produce)
                yield return (id, new ObtainSource(SourceKind.Animal, WeekMask.All, Reliability.Dependable, regular, $"{animal.AnimalId} produce"));
            var deluxe = ObtainConditions.None with { Requires = requires.Append("friendship:" + animal.AnimalId).ToList() };
            foreach (string id in animal.DeluxeProduce)
                yield return (id, new ObtainSource(SourceKind.Animal, WeekMask.All, Reliability.Dependable, deluxe, $"{animal.AnimalId} deluxe produce"));
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Ponds(
        IEnumerable<PondRow> rows, IReadOnlyDictionary<string, ObjInfo> objects, ObtainabilityModel snapshot)
    {
        foreach (PondRow pond in rows)
        {
            WeekMask any = WeekMask.None;
            foreach (ObjInfo fish in objects.Values.Where(o => MatchesTags(o, pond.RequiredTags)))
                any |= snapshot.Weeks(fish.QualifiedId, ObtainFilter.Any);
            WeekMask weeks = any.FromWeekOnward();
            if (weeks.IsEmpty) continue;
            foreach (PondProduct product in pond.Products)
                yield return (product.ItemId, new ObtainSource(
                    SourceKind.FishPond, weeks, product.Chance >= 1.0 ? Reliability.Dependable : Reliability.Chance,
                    ObtainConditions.None with { Requires = new[] { "building:Fish Pond", $"pond population {product.RequiredPopulation}" } },
                    $"fish pond ({string.Join(" ", pond.RequiredTags)})"));
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Tappers(IEnumerable<TapRow> rows)
    {
        foreach (TapRow tap in rows)
            yield return (tap.ItemId, new ObtainSource(
                SourceKind.Tapper, WeekMask.All, Reliability.Dependable,
                ObtainConditions.None with { Requires = new[] { "crafting:Tapper", "tree:" + tap.TreeId } },
                $"tapper on tree {tap.TreeId}, {tap.DaysUntilReady} days"));
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Geodes(
        IEnumerable<GeodeDropRow> rows, IReadOnlyCollection<string> geodesUsingDefaultTable, ObtainabilityModel snapshot)
    {
        var drops = rows.Select(r => (r.GeodeId, r.ItemId)).ToList();
        foreach (string geode in geodesUsingDefaultTable)
            foreach (string id in DefaultGeodeTable.TryGetValue(geode, out string[]? table) ? table : OtherGeodeDefault)
                drops.Add((geode, id));
        foreach ((string geode, string item) in drops.Distinct())
        {
            WeekMask weeks = snapshot.Weeks(geode, ObtainFilter.Any);
            if (weeks.IsEmpty) continue;
            yield return (item, new ObtainSource(
                SourceKind.Geode, weeks, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { "item:" + geode, GeodeOpener } }, $"opened from {geode}"));
        }
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

    private static ObtainConditions UnlockConditions(RecipeRow recipe)
    {
        string[] tokens = recipe.Unlock.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var requires = new List<string> { "recipe:" + recipe.Name };
        if (tokens.Length == 0 || NoUnlockWords.Contains(tokens[0].ToLowerInvariant()))
            return ObtainConditions.None with { Requires = requires };
        int start = tokens[0] == SkillUnlockPrefix ? 1 : 0;
        if (tokens.Length > start + 1 && Skills.Contains(tokens[start]) && int.TryParse(tokens[start + 1], out int level))
            return ObtainConditions.None with { Skill = tokens[start], SkillLevel = level, Requires = requires };
        // "f Robin 7" (friendship), "l 4" (farmhouse level) and anything else stay as a note.
        requires.Add("unlock:" + recipe.Unlock);
        return ObtainConditions.None with { Requires = requires };
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityMadeTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs
git commit -m "obtainability: machines, recipes, animals, ponds, tappers and geode contents from their inputs"
git push origin story
```

---

### Task 8: The builder and its repeated passes

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (append the `ObtainabilityInputs` bundle)
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityBuilderTests.cs`

**Interfaces:**
- Consumes: every source class from Tasks 3-7.
- Produces:
  - `sealed record ObtainabilityInputs` with init properties `Objects`, `Festivals`, `Forage`, `LocationFish`, `FishRows`, `ArtifactSpots`, `Garbage`, `Shops`, `MonsterDrops`, `Crops`, `FruitTrees`, `Machines`, `Recipes`, `Animals`, `Ponds`, `TapItems`, `GeodeDrops`, `GeodesUsingDefaultTable` (all default to empty)
  - `sealed record ObtainabilityBuild(ObtainabilityModel Model, int Passes, bool HitPassCap)`
  - `static ObtainabilityBuild ObtainabilityBuilder.Build(ObtainabilityInputs inputs)` and `const int MaxPasses = 32`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
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
        Crops = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, false) },
        Machines = new[]
        {
            new MachineRow("(BC)15", null, new[] { "category_vegetable" }, new[] { "(O)342" }, 4000, 0), // pickles
            new MachineRow("(BC)Preserves", null, new[] { "roe_item" }, new[] { "(O)447" }, 0, 3),        // aged roe
        },
        LocationFish = new[] { new LocationSpawn("Forest", "(O)142", Season.Fall, null, 1.0, 0, false) },
        FishRows = new Dictionary<string, FishRow> { ["(O)142"] = new FishRow("(O)142", false, "both", 0) },
        Ponds = new[] { new PondRow(new[] { "fish_carp" }, new[] { new PondProduct("(O)812", 1, 1.0) }) },
    };

    [Fact]
    public void A_chain_resolves_shop_seed_to_crop_to_pickles()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.False(build.HitPassCap);
        Assert.True(build.Passes >= 2);
        Assert.Equal("1-4", build.Model.Weeks("(O)24", ObtainFilter.DependableOnly).ToString());
        Assert.Equal("1-4", build.Model.Weeks("(O)342", ObtainFilter.DependableOnly).ToString());
    }

    [Fact]
    public void A_pond_chain_settles_without_looping()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(Farm());
        Assert.Equal("9-16", build.Model.Weeks("(O)812", ObtainFilter.Any).ToString());
        Assert.Equal("9-16", build.Model.Weeks("(O)447", ObtainFilter.Any).ToString());
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

Add `using System;` to the top of the file if it is not there yet, then append:

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

public sealed record ObtainabilityBuild(ObtainabilityModel Model, int Passes, bool HitPassCap);

/// <summary>Builds the model: direct sources once, then grown and made sources over the previous
/// pass's model until no item's weeks change. Every derived rule only ever adds weeks when its inputs
/// gain weeks, so the passes settle; the cap is a guard, logged by the caller when hit.</summary>
public static class ObtainabilityBuilder
{
    public const int MaxPasses = 32;

    public static ObtainabilityBuild Build(ObtainabilityInputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var direct = new List<(string ItemId, ObtainSource Source)>();
        direct.AddRange(SpawnSources.Forage(inputs.Forage, inputs.Festivals));
        direct.AddRange(SpawnSources.LocationFish(inputs.LocationFish, inputs.FishRows, inputs.Festivals));
        direct.AddRange(SpawnSources.CrabPot(inputs.FishRows.Values));
        direct.AddRange(SpawnSources.ArtifactSpots(inputs.ArtifactSpots, inputs.Festivals));
        direct.AddRange(SpawnSources.GarbageCans(inputs.Garbage, inputs.Festivals));
        direct.AddRange(SpawnSources.FishingTrash());
        direct.AddRange(ShopSources.Stock(inputs.Shops, inputs.Objects, inputs.Festivals));
        direct.AddRange(ShopSources.FestivalRewards(inputs.Festivals));
        direct.AddRange(MineSources.Nodes());
        direct.AddRange(MineSources.MonsterDrops(inputs.MonsterDrops));
        direct.AddRange(MineSources.FishingTreasure());
        direct.AddRange(MadeSources.Animals(inputs.Animals));
        direct.AddRange(MadeSources.Tappers(inputs.TapItems));
        IReadOnlyDictionary<string, WeekMask> recipeWeeks = ShopSources.RecipeWeeks(inputs.Shops, inputs.Festivals);

        ObtainabilityModel current = Assemble(direct);
        for (int pass = 1; pass <= MaxPasses; pass++)
        {
            var all = new List<(string ItemId, ObtainSource Source)>(direct);
            all.AddRange(GrowSources.Crops(inputs.Crops, current));
            all.AddRange(GrowSources.FruitTrees(inputs.FruitTrees, current));
            all.AddRange(MadeSources.Machines(inputs.Machines, inputs.Objects, current));
            all.AddRange(MadeSources.Recipes(inputs.Recipes, inputs.Objects, recipeWeeks, current));
            all.AddRange(MadeSources.Ponds(inputs.Ponds, inputs.Objects, current));
            all.AddRange(MadeSources.Geodes(inputs.GeodeDrops, inputs.GeodesUsingDefaultTable, current));
            ObtainabilityModel next = Assemble(all);
            if (SameWeeks(current, next)) return new ObtainabilityBuild(next, pass, false);
            current = next;
        }
        return new ObtainabilityBuild(current, MaxPasses, true);
    }

    private static ObtainabilityModel Assemble(IEnumerable<(string ItemId, ObtainSource Source)> sources)
        => new(sources
            .GroupBy(s => BundleParsing.NormalizeItemId(s.ItemId), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ObtainSource>)g.Select(s => s.Source).Distinct().ToList(), StringComparer.Ordinal));

    private static bool SameWeeks(ObtainabilityModel a, ObtainabilityModel b)
    {
        if (a.Count != b.Count) return false;
        foreach (string id in b.ItemIds)
            if (a.Weeks(id, ObtainFilter.Any) != b.Weeks(id, ObtainFilter.Any)
                || a.Weeks(id, ObtainFilter.DependableOnly) != b.Weeks(id, ObtainFilter.DependableOnly))
                return false;
        return true;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityBuilderTests"`
Expected: PASS, 3 tests. Pickles: parsnip is a spring vegetable in weeks 1-4, 4000 minutes is under a week, so weeks 1-4. Roe: carp is Fall (9-12), a pond keeps it from week 9 on, aged roe takes 3 days so stays 9-16.

- [ ] **Step 6: Run the whole suite, commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests/ObtainabilityBuilderTests.cs
git commit -m "obtainability: builder resolves grown and made items by repeated passes"
git push origin story
```
---

### Task 9: The blind guard

**Files:**
- Test: `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs`

**Interfaces:**
- Consumes: the source tree only.

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
/// fails if any blind file names the existing model, its tables or its builders, in code or comments.</summary>
public class ObtainabilityBlindGuardTests
{
    private static readonly string[] Forbidden =
    {
        "ItemAvailabilityModel", "ItemAvailability", "ItemEffort", "AvailabilityWeeks",
        "TheLongestYear.Core.Availability", "DefaultItemSeasonPins", "QuantityBasisTables",
        "ItemPoolBuilder", "GameDataPools", "GameEffortData", "LegendaryFishRules", "LocationGating",
        "MineAreas", "ItemAvailabilityBuilder", "BundleGenerationTuning",
    };

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static IEnumerable<string> BlindFiles()
    {
        string folder = Path.Combine(SrcRoot, "TheLongestYear.Core", "Obtainability");
        foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            yield return file;
        string glue = Path.Combine(SrcRoot, "TheLongestYear", "Loop", "GameObtainabilityData.cs");
        if (File.Exists(glue)) yield return glue;
    }

    [Fact]
    public void The_blind_files_exist()
        => Assert.True(BlindFiles().Count() >= 11, "expected the Obtainability folder's files");

    [Fact]
    public void No_blind_file_names_the_existing_model()
    {
        var hits = new List<string>();
        foreach (string file in BlindFiles())
        {
            string text = File.ReadAllText(file);
            foreach (string word in Forbidden)
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b"))
                    hits.Add($"{Path.GetFileName(file)}: {word}");
        }
        Assert.Empty(hits);
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityBlindGuardTests"`
Expected: PASS, 2 tests (Tasks 1-8 created 11 files under `Obtainability/`: WeekMask, ObtainTypes, ObtainabilityModel, ObtainabilityInputs, ConditionSeasons, SpawnSources, ShopSources, MineSources, GrowSources, MadeSources, ObtainabilityBuilder). If `No_blind_file_names_the_existing_model` fails, reword the offending comment; never add the word to an allow-list.

- [ ] **Step 3: Prove the guard can fail**

Temporarily add the comment `// ItemAvailabilityModel` to the end of `WeekMask.cs`, run the guard, expect FAIL with `WeekMask.cs: ItemAvailabilityModel`, then remove the comment and run it again to PASS.

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
- Modify: `src/TheLongestYear/ModEntry.cs` (field after line 52 `private TheLongestYear.Core.ItemPools _enginePools;`; call after line 603 `_reset.RebuildAvailabilityModel = BuildAvailabilityModelFor;`; command after line 338; bridge case after line 2615; methods before `private void CmdDumpEffort`)

**Interfaces:**
- Consumes: `ObtainabilityBuilder.Build`, `ObtainabilityInputs` and every input record (Tasks 2-8).
- Produces: `static string ObtainabilityText.Describe(string itemId, ObtainabilityModel model, Func<string, string?> nameOf)`; `internal sealed class GameObtainabilityData { public GameObtainabilityData(IMonitor); public ObtainabilityInputs Build(); }`; ModEntry field `_obtainability`, methods `BuildObtainabilityModel()` and `CmdObtain(string, string[])`.

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
                                                 Requires = new[] { "location:Forest" } }, "Fish at Forest"),
            },
        });
        string text = ObtainabilityText.Describe("775", model, id => "Glacierfish");
        Assert.Contains("(O)775 Glacierfish: 1 source(s)", text);
        Assert.Contains("dependable weeks 13-16", text);
        Assert.Contains("Fish, Dependable, weeks 13-16", text);
        Assert.Contains("Fishing 6", text);
        Assert.Contains("catch limit 1", text);
        Assert.Contains("rain only", text);
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

/// <summary>The one-item readout for tly_obtain.</summary>
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
        {
            sb.AppendLine().Append($"  - {s.Kind}, {s.Reliability}, weeks {s.Weeks}");
            ObtainConditions c = s.Conditions;
            if (c.Skill != null) sb.Append($", {c.Skill} {c.SkillLevel}");
            if (c.CatchLimit > 0) sb.Append($", catch limit {c.CatchLimit}");
            if (c.RainOnly) sb.Append(", rain only");
            if (c.FewDays) sb.Append(", few days");
            if (c.YearTwo) sb.Append(", year 2");
            if (c.GingerIsland) sb.Append(", Ginger Island");
            if (c.Requires.Count > 0) sb.Append($", needs {string.Join("; ", c.Requires)}");
            sb.Append($" | {s.Detail}");
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run it to verify it passes, commit nothing yet**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityTextTests"`
Expected: PASS, 1 test.

- [ ] **Step 5: Write `GameObtainabilityData.cs`**

Type and field names are from the 1.6 GameData decompile (`StardewValley.GameData.*`). Every section has its own try/catch so one bad table degrades to a smaller model instead of no model.

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
    /// assets only and never consults the mod's existing item tables.</summary>
    internal sealed class GameObtainabilityData
    {
        private const int FishDifficultyField = 1;
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
                        foreach (string item in Ids(drop?.ItemId, drop?.RandomItemId))
                            geodeDrops.Add(new GeodeDropRow(id, item, drop.Chance));
                }
            });

            Section("PassiveFestivals", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, PassiveFestivalData>>("Data/PassiveFestivals"))
                    if (kv.Value != null)
                        festivals[kv.Key] = new FestivalDates(kv.Key, (CoreSeason)(int)kv.Value.Season, kv.Value.StartDay, kv.Value.EndDay);
            });

            Section("Locations", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                {
                    LocationData loc = kv.Value;
                    if (loc == null) continue;
                    foreach (SpawnForageData f in loc.Forage ?? new List<SpawnForageData>())
                        foreach (string item in Ids(f?.ItemId, f?.RandomItemId))
                            forage.Add(new LocationSpawn(kv.Key, item, MapSeason(f.Season), f.Condition, f.Chance, 0, false));
                    foreach (SpawnFishData f in loc.Fish ?? new List<SpawnFishData>())
                        foreach (string item in Ids(f?.ItemId, f?.RandomItemId))
                            fish.Add(new LocationSpawn(kv.Key, item, MapSeason(f.Season), f.Condition, f.Chance, f.CatchLimit, f.RequireMagicBait));
                    foreach (ArtifactSpotDropData a in loc.ArtifactSpots ?? new List<ArtifactSpotDropData>())
                        foreach (string item in Ids(a?.ItemId, a?.RandomItemId))
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
                    fishRows[id] = new FishRow(id, trap, trap ? "" : Field(fields, FishWeatherField), level);
                }
            });

            Section("Monsters", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Monsters"))
                {
                    string[] pairs = Field((kv.Value ?? "").Split('/'), MonsterDropField).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i + 1 < pairs.Length; i += 2)
                        if (double.TryParse(pairs[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double chance))
                            monsterDrops.Add(new MonsterDropRow(kv.Key, BundleParsing.NormalizeItemId(pairs[i]), chance));
                }
            });

            Section("Crops", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, CropData>>("Data/Crops"))
                {
                    CropData c = kv.Value;
                    if (c?.HarvestItemId == null) continue;
                    crops.Add(new CropRow(BundleParsing.NormalizeItemId(kv.Key), BundleParsing.NormalizeItemId(c.HarvestItemId),
                        MapSeasons(c.Seasons), (c.DaysInPhase ?? new List<int>()).Sum(), c.RegrowDays > 0));
                }
            });

            Section("FruitTrees", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, FruitTreeData>>("Data/FruitTrees"))
                {
                    if (kv.Value == null) continue;
                    var fruit = new List<FruitRow>();
                    foreach (FruitTreeFruitData f in kv.Value.Fruit ?? new List<FruitTreeFruitData>())
                        foreach (string item in Ids(f?.ItemId, f?.RandomItemId))
                            fruit.Add(new FruitRow(item, MapSeason(f.Season), f.Chance));
                    fruitTrees.Add(new FruitTreeRow(BundleParsing.NormalizeItemId(kv.Key), MapSeasons(kv.Value.Seasons), fruit));
                }
            });

            Section("Shops", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops"))
                    foreach (ShopItemData item in kv.Value?.Items ?? new List<ShopItemData>())
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) continue;
                        string id = item.ItemId.Contains(' ') ? item.ItemId : BundleParsing.NormalizeItemId(item.ItemId);
                        shops.Add(new ShopRow(kv.Key, id, item.Condition, item.IsRecipe));
                        foreach (string random in item.RandomItemId ?? new List<string>())
                            if (!string.IsNullOrWhiteSpace(random))
                                shops.Add(new ShopRow(kv.Key, random.Contains(' ') ? random : BundleParsing.NormalizeItemId(random), item.Condition, item.IsRecipe));
                    }
            });

            Section("Machines", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, MachineData>>("Data/Machines"))
                    foreach (MachineOutputRule rule in kv.Value?.OutputRules ?? new List<MachineOutputRule>())
                    {
                        var outputs = (rule.OutputItem ?? new List<MachineItemOutput>())
                            .SelectMany(o => Ids(o?.ItemId, o?.RandomItemId)).Distinct().ToList();
                        if (outputs.Count == 0) continue;
                        var triggers = rule.Triggers ?? new List<MachineOutputTriggerRule>();
                        if (triggers.Count == 0) triggers = new List<MachineOutputTriggerRule> { new MachineOutputTriggerRule() };
                        foreach (MachineOutputTriggerRule t in triggers)
                            machines.Add(new MachineRow(kv.Key,
                                string.IsNullOrEmpty(t?.RequiredItemId) ? null : BundleParsing.NormalizeItemId(t.RequiredItemId),
                                (IReadOnlyList<string>)(t?.RequiredTags ?? new List<string>()),
                                outputs, rule.MinutesUntilReady, rule.DaysUntilReady));
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
                        a.PurchasePrice, Produce(a.ProduceItemIds), Produce(a.DeluxeProduceItemIds)));
                }
            });

            Section("FishPondData", () =>
            {
                foreach (FishPondData pond in Game1.content.Load<List<FishPondData>>("Data/FishPondData") ?? new List<FishPondData>())
                {
                    if (pond == null) continue;
                    var products = new List<PondProduct>();
                    foreach (FishPondReward reward in pond.ProducedItems ?? new List<FishPondReward>())
                        foreach (string item in Ids(reward?.ItemId, reward?.RandomItemId))
                            products.Add(new PondProduct(item, reward.RequiredPopulation, reward.Chance));
                    ponds.Add(new PondRow((IReadOnlyList<string>)(pond.RequiredTags ?? new List<string>()), products));
                }
            });

            Section("WildTrees", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, WildTreeData>>("Data/WildTrees"))
                    foreach (WildTreeTapItemData tap in kv.Value?.TapItems ?? new List<WildTreeTapItemData>())
                        foreach (string item in Ids(tap?.ItemId, tap?.RandomItemId))
                            if (item != BundleParsing.NormalizeItemId(PreviousOutputTapId))
                                taps.Add(new TapRow(kv.Key, item, tap.DaysUntilReady));
            });

            Section("GarbageCans", () =>
            {
                GarbageCanData data = Game1.content.Load<GarbageCanData>("Data/GarbageCans");
                void AddAll(string can, List<GarbageCanItemData> items)
                {
                    foreach (GarbageCanItemData g in items ?? new List<GarbageCanItemData>())
                        foreach (string item in Ids(g?.ItemId, g?.RandomItemId))
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
            string output = fields[RecipeOutputField].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(output)) return null;
            bool bigCraftable = !cooking && string.Equals(fields[CraftingBigCraftableField].Trim(), "true", StringComparison.OrdinalIgnoreCase);
            string outputId = bigCraftable && !output.StartsWith("(", StringComparison.Ordinal) ? "(BC)" + output : BundleParsing.NormalizeItemId(output);
            return new RecipeRow(name, ingredients, outputId, fields[unlockField].Trim(), cooking);
        }

        private static IReadOnlyList<string> Produce(List<FarmAnimalProduce> produce)
            => (produce ?? new List<FarmAnimalProduce>())
                .Where(p => !string.IsNullOrEmpty(p?.ItemId))
                .Select(p => BundleParsing.NormalizeItemId(p.ItemId)).ToList();

        /// <summary>Concrete item ids from a spawn entry's ItemId and RandomItemId. Item queries (ids
        /// with spaces) are skipped here; only shop rows keep queries, for the cart expansion.</summary>
        private static IEnumerable<string> Ids(string itemId, List<string> randomItemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId) && !itemId.Contains(' '))
                yield return BundleParsing.NormalizeItemId(itemId);
            foreach (string id in randomItemIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id) && !id.Contains(' '))
                    yield return BundleParsing.NormalizeItemId(id);
        }

        private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : "";

        private static CoreSeason? MapSeason(StardewValley.Season? season)
            => season is StardewValley.Season s ? (CoreSeason)(int)s : null;

        private static IReadOnlyList<CoreSeason> MapSeasons(List<StardewValley.Season> seasons)
            => (seasons ?? new List<StardewValley.Season>()).Select(s => (CoreSeason)(int)s).Distinct().ToList();
    }
}
```

If a property name above does not compile (for example `SpawnFishData.CatchLimit`, `FishPondReward.Chance`, `WildTreeTapItemData.RandomItemId`, `FarmAnimalProduce.ItemId`), open the type under `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled\decompiled\StardewValley.GameData\` and use the name it declares; do not guess.

- [ ] **Step 6: Wire it into `ModEntry.cs`**

After line 52 (`private TheLongestYear.Core.ItemPools _enginePools;`) add:

```csharp
        /// <summary>Phase 1 item obtainability model (spec 2026-09-14): built blind from game data at
        /// save load. Nothing reads it for gameplay; tly_obtain is its only reader. Null before a save
        /// is loaded or when the build failed.</summary>
        private TheLongestYear.Core.Obtainability.ObtainabilityModel _obtainability;
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
                this.Monitor.Log(
                    $"Obtainability model: {build.Model.Count} items in {timer.ElapsedMilliseconds} ms, {build.Passes} pass(es)" +
                    (build.HitPassCap ? ", STOPPED AT THE PASS CAP" : "") + ".",
                    build.HitPassCap ? LogLevel.Warn : LogLevel.Info);
            }
            catch (Exception ex)
            {
                _obtainability = null;
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

- [ ] **Step 7: Build the mod and run the whole suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: `0 Error(s)`.
Run the full test command. Expected: all pass, including `ObtainabilityBlindGuardTests` (which now also scans `GameObtainabilityData.cs`).

- [ ] **Step 8: Commit, push**

```bash
git add src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs tests/TheLongestYear.Tests/ObtainabilityTextTests.cs src/TheLongestYear/Loop/GameObtainabilityData.cs src/TheLongestYear/ModEntry.cs
git commit -m "obtainability: read game data at save load and add tly_obtain"
git push origin story
```
---

### Task 11: The comparison report, `tly_obtain compare`

This file is NOT blind: it reads the existing item model, but only through delegates, so Core stays
testable and the blind folder never names it.

**Files:**
- Create: `src/TheLongestYear.Core/ObtainabilityComparison.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityComparisonTests.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdObtain`, added in Task 10)
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `ObtainabilityModel`, `ObtainFilter` (Task 1); ModEntry fields `_availability`, `_enginePools`, `_obtainability`.
- Produces: `enum CompareVerdict { Agree, NewEarlier, NewLater, OnlyExisting, OnlyNew }`; `record CompareRow(string ItemId, int? ExistingPacing, int? ExistingHard, int? NewDependable, int? NewAny, CompareVerdict Verdict)`; `static IReadOnlyList<CompareRow> ObtainabilityComparison.Compare(IEnumerable<string> existingIds, Func<string, bool> existingPlaced, Func<string, (int Pacing, int Hard)> existingWeeks, ObtainabilityModel model)`; `static string ObtainabilityComparison.Render(IReadOnlyList<CompareRow> rows, ObtainabilityModel model, Func<string, string?> nameOf, string version)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityComparisonTests
{
    private static ObtainSource Source(WeekMask weeks) =>
        new(SourceKind.Forage, weeks, Reliability.Dependable, ObtainConditions.None, "test");

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)1"] = new[] { Source(WeekMask.FromWeekOnwardOf(2)) },
        ["(O)2"] = new[] { Source(WeekMask.FromWeekOnwardOf(9)) },
        ["(O)5"] = new[] { Source(WeekMask.Of(4)) },
        ["(O)6"] = new[] { Source(WeekMask.FromWeekOnwardOf(3)) },
    });

    private static readonly Dictionary<string, (int Pacing, int Hard)> Existing = new()
    {
        ["(O)1"] = (3, 2), ["(O)2"] = (5, 5), ["(O)3"] = (1, 1), ["(O)6"] = (8, 8),
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
    }

    [Fact]
    public void The_report_has_a_summary_and_a_section_per_verdict()
    {
        string text = ObtainabilityComparison.Render(Rows(), Model, id => "Name " + id, "0.18.4");
        Assert.Contains("# Item obtainability comparison", text);
        Assert.Contains("| NewLater | 1 |", text);
        Assert.Contains("## NewEarlier", text);
        Assert.Contains("## OnlyExisting", text);
        Assert.Contains("Name (O)2", text);
        Assert.DoesNotContain("\u2014", text);   // no em dashes
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityComparisonTests"`
Expected: build FAILS, `ObtainabilityComparison` not found.

- [ ] **Step 3: Write `ObtainabilityComparison.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

public enum CompareVerdict { Agree, NewEarlier, NewLater, OnlyExisting, OnlyNew }

public sealed record CompareRow(
    string ItemId, int? ExistingPacing, int? ExistingHard, int? NewDependable, int? NewAny, CompareVerdict Verdict);

/// <summary>Lines the blind obtainability model up against the existing item model (spec
/// 2026-09-14-item-obtainability, phase 1). The existing hard week is a fact ("the first week the
/// item can exist at all"), so it is compared with the new model's earliest week counting every
/// source. Pacing weeks are shown for context only.</summary>
public static class ObtainabilityComparison
{
    private const int MaxSourcesShown = 3;
    private static readonly CompareVerdict[] SectionOrder =
        { CompareVerdict.NewEarlier, CompareVerdict.NewLater, CompareVerdict.OnlyExisting, CompareVerdict.OnlyNew, CompareVerdict.Agree };

    public static IReadOnlyList<CompareRow> Compare(
        IEnumerable<string> existingIds, Func<string, bool> existingPlaced,
        Func<string, (int Pacing, int Hard)> existingWeeks, ObtainabilityModel model)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string id in existingIds) ids.Add(BundleParsing.NormalizeItemId(id));
        foreach (string id in model.ItemIds) ids.Add(id);

        var rows = new List<CompareRow>();
        foreach (string id in ids)
        {
            // Placed is checked first: asking the existing model about an unplaced id records it as unknown.
            (int Pacing, int Hard)? existing = existingPlaced(id) ? existingWeeks(id) : null;
            int? newAny = model.EarliestWeek(id, ObtainFilter.Any);
            int? newDep = model.EarliestWeek(id, ObtainFilter.DependableOnly);
            if (existing == null && newAny == null) continue;
            CompareVerdict verdict = existing == null ? CompareVerdict.OnlyNew
                : newAny == null ? CompareVerdict.OnlyExisting
                : newAny == existing.Value.Hard ? CompareVerdict.Agree
                : newAny < existing.Value.Hard ? CompareVerdict.NewEarlier
                : CompareVerdict.NewLater;
            rows.Add(new CompareRow(id, existing?.Pacing, existing?.Hard, newDep, newAny, verdict));
        }
        return rows;
    }

    public static string Render(IReadOnlyList<CompareRow> rows, ObtainabilityModel model, Func<string, string?> nameOf, string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Item obtainability comparison").AppendLine();
        sb.AppendLine($"Mod version {version}. Existing weeks are the current item model's pacing and hard weeks; new weeks are the blind obtainability model's earliest week counting dependable sources only, then any source. The verdict compares the existing hard week with the new any-source week.").AppendLine();
        sb.AppendLine("| Verdict | Items |").AppendLine("|---|---|");
        foreach (CompareVerdict v in SectionOrder)
            sb.AppendLine($"| {v} | {rows.Count(r => r.Verdict == v)} |");

        foreach (CompareVerdict v in SectionOrder)
        {
            List<CompareRow> section = rows.Where(r => r.Verdict == v).ToList();
            if (section.Count == 0) continue;
            sb.AppendLine().AppendLine($"## {v}").AppendLine();
            sb.AppendLine("| Item | Name | Existing pacing | Existing hard | New dependable | New any | New sources |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (CompareRow r in section)
            {
                string sources = v == CompareVerdict.Agree ? "" : string.Join("; ",
                    model.Sources(r.ItemId).Take(MaxSourcesShown).Select(s => $"{s.Kind} {s.Reliability} {s.Weeks}"));
                sb.AppendLine($"| {r.ItemId} | {nameOf(r.ItemId) ?? "?"} | {Week(r.ExistingPacing)} | {Week(r.ExistingHard)} | {Week(r.NewDependable)} | {Week(r.NewAny)} | {sources} |");
            }
        }
        return sb.ToString();
    }

    private static string Week(int? week) => week?.ToString() ?? "none";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test ... --filter "FullyQualifiedName~ObtainabilityComparisonTests"`
Expected: PASS, 2 tests.

- [ ] **Step 5: Add the `compare` branch to `CmdObtain` in `ModEntry.cs`**

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
                IEnumerable<string> existingIds = new[]
                    {
                        pools.Crops, pools.Fish, pools.CrabPot, pools.Forage, pools.MonsterDrops, pools.Metals,
                        pools.ArtisanGoods, pools.Artifacts, pools.Books, pools.Saplings, pools.GeodeMinerals,
                        pools.Cooking, pools.TapperGoods,
                    }
                    .SelectMany(list => list).Select(item => item.ItemId);
                var rows = TheLongestYear.Core.ObtainabilityComparison.Compare(
                    existingIds,
                    id => _availability.IsPlaced(id),
                    id => { var a = _availability.For(id); return (a.PacingWeek, a.HardWeekOrPacing); },
                    _obtainability);
                string report = TheLongestYear.Core.ObtainabilityComparison.Render(
                    rows, _obtainability, id => ItemRegistry.GetData(id)?.DisplayName, this.ModManifest.Version.ToString());
                string fileName = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : "obtainability-compare.md";
                string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
                System.IO.File.WriteAllText(path, report);
                string counts = string.Join(", ", rows.GroupBy(r => r.Verdict).Select(g => $"{g.Key} {g.Count()}"));
                this.Monitor.Log($"tly_obtain compare: wrote {path} ({rows.Count} items: {counts}).", LogLevel.Info);
                return;
            }
```

If `ModEntry.cs` has no `using System.Linq;` / `using System.Collections.Generic;` at the top, add them.

- [ ] **Step 6: Ignore the report**

Append to `.gitignore`, after the `item-effort-model.md` line:

```
obtainability-compare.md
```

- [ ] **Step 7: Build, run the whole suite, commit, push**

Run the mod build command (expect `0 Error(s)`) and the full test command (expect all pass).

```bash
git add src/TheLongestYear.Core/ObtainabilityComparison.cs tests/TheLongestYear.Tests/ObtainabilityComparisonTests.cs src/TheLongestYear/ModEntry.cs .gitignore
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

Expected: `Obtainability model: <N> items in <ms> ms, <P> pass(es).` at Info, N in the thousands, no `STOPPED AT THE PASS CAP`, and no `Obtainability: reading Data/... failed` Warn lines. The `None_*` saves are the throwaway "Clone" lineage; never load `PuffPuff_*` or `Cheatside_*`. If the save sits on a failing day 28, the rewind opens on load; that does not affect this check. Never send `tly_skipscene` to it.

- [ ] **Step 2: Spot-check items**

```powershell
$n = pwsh -NoProfile -File tools/bridge.ps1 -Action count
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_obtain (O)775|tly_obtain (O)348|tly_obtain (O)798|tly_obtain (O)414|tly_obtain (O)378|tly_obtain (O)24|tly_obtain (O)142|tly_obtain (O)342"
```

Then read the new lines of `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` after line `$n`. Check by hand, and write down any that look wrong:
- `(O)775` Glacierfish: Fish, Winter weeks 13-16, Fishing 6, catch limit 1.
- `(O)348` Wine: Machine from a keg, weeks shifted one week after its fruit's weeks.
- `(O)798` Midnight Squid: week 15 only, few days (Night Market submarine).
- `(O)414` Crystal Fruit: Winter forage; also Cart (chance) if the cart can sell it.
- `(O)378` Copper Ore: MineNode dependable every week.
- `(O)24` Parsnip: Crop weeks 1-4, GreenhouseCrop reaching Winter, Mixed Seeds sources.
- `(O)142` Carp: its fish seasons.
- `(O)342` Pickles: Machine weeks from any vegetable.

- [ ] **Step 3: Write the comparison report**

```powershell
$n = pwsh -NoProfile -File tools/bridge.ps1 -Action count
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_obtain compare"
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "tly_obtain compare: wrote" -FromLine $n -TimeoutSec 60
```

Expected: the log line with the path (`...\Mods\TheLongestYear\obtainability-compare.md`) and counts per verdict. Read the report's summary and the first rows of each disagreement section.

- [ ] **Step 4: Record it**

Add a top section to `STATUS.md`: date, what phase 1 built, the build line (items, ms, passes), the eight spot checks with pass or what was wrong, the verdict counts, the report path, and the three biggest disagreement patterns (for example "fish: new model earlier because it ignores fishing level as a week"). Add an Open entry to `TODO.md`: "Obtainability phase 2: Jeff reads obtainability-compare.md and rules on the disagreements before anything reads the model". No em dashes.

```bash
git add STATUS.md TODO.md
git commit -m "docs: obtainability phase 1 live check and comparison summary"
git push origin story
```

- [ ] **Step 5: Report to Jeff and stop**

Tell Jeff, in plain terms: the model built (item count, time), the spot checks, the verdict counts, where the report is, and the biggest disagreement patterns in one line each. Do not change any existing figure and do not wire the model into anything: phase 2 starts only when Jeff has read the report.