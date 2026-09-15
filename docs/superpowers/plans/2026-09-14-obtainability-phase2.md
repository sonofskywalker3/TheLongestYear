# Obtainability Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change the blind obtainability model from "which weeks does the item come out" to "start on day N, when does it first land", close the gaps the phase 1 report exposed, and rerun the comparison so Jeff can rule on what remains.

**Architecture:** A `DayTable` (112 start days to a landing day) replaces `WeekMask` inside every `ObtainSource`; the tables are monotone ("earliest landing starting on or after day d") so chaining is one lookup (`Then`), waiting is free, and the repeated-pass build still converges because a landing day only ever moves earlier. `WeekMask` stays as the readers' season vocabulary. Hand-typed code facts live in one new blind file, `CodeSources.cs`, each entry citing the PC 1.6 decompile. The glue reads two more assets (Buildings, MonsterSlayerQuests), expands `LOCATION_FISH`, drops `fishingGame`, and refuses to publish a model when any section failed. The comparison switches to "dependable landing week from day 1 against the existing hard week" and prints detail only where a ruling is needed.

**Tech Stack:** C# / .NET (SMAPI mod), xUnit tests. No new packages.

**Spec:** `docs/superpowers/specs/2026-09-14-obtainability-phase2-design.md` (read it first; phase 1 spec `docs/superpowers/specs/2026-09-14-item-obtainability-design.md` explains the source kinds and reliability).

## Global Constraints

- Branch `story` only. Never change `manifest.json`'s `Version`. Push `origin story` after every commit. Never merge to master, never release, never touch Nexus.
- No em dash characters anywhere (code, comments, strings, docs, commit messages). Use a comma, a colon, or a new sentence.
- The blind guard (`tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs`) stays in force for `src/TheLongestYear.Core/Obtainability/` and `src/TheLongestYear/Loop/GameObtainabilityData.cs`: those files may not name `ItemAvailabilityModel`, `ItemAvailability`, `ItemEffort`, `AvailabilityWeeks`, `Core.Availability`, `DefaultItemSeasonPins`, `QuantityBasisTables`, `ItemPoolBuilder`, `GameDataPools`, `GameEffortData`, `LegendaryFishRules`, `LocationGating`, `MineAreas`, `ItemAvailabilityBuilder`, `BundleGenerationTuning`, `EffortData`, `PacingWeek`, `HardWeek`, `SeasonPins`, `UnlockWeeks`, `BasisByDeadline` in code or comments. `ObtainabilityComparison.cs` is the only place that may name the existing model. Hand-typed tables are typed from the decompile, never copied from the existing model's tables.
- Nothing reads the model for gameplay in this plan. No board, gate, goal, pacing or sabotage code changes.
- Test command (must stay green after every task; 2246 passing at the start):
  `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
- The PC 1.6 decompile at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley\` wins over any plan text; say so in the commit when it does.
- If a test's expected value looks wrong, stop and report the task, the test and your reasoning; never change an assertion to match your code.
- Live checks (Task 10 only): the launch is an automated run, not Jeff's; only `None_*` throwaway saves through `tly_loadsave`; never `PuffPuff_*` or `Cheatside_*`; never `tly_skipscene`, the mouse or the keyboard. Runbook: `docs/HEADLESS_DRIVING.md`.
- Commit messages end with:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_014nCkXCiwy6ympotfpBwy41
  ```

---

## File map

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/Obtainability/DayTable.cs` (new) | The 112-slot monotone start-to-landing table and its maths. |
| `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs` | `ObtainSource` carries `DayTable Lands` and `Setup`; `SetupStep`; `SourcePair` splits by table. |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs` | Queries: `Lands`, `CanObtain`, `LandingWeekFromDay1`, `Table`. |
| `src/TheLongestYear.Core/Obtainability/ConditionSeasons.cs` | Adds day-level reading (`DaysOfYear`), `Availability(...)`, fixes the island negation. |
| `src/TheLongestYear.Core/Obtainability/SpawnSources.cs`, `ShopSources.cs`, `MineSources.cs` | Same-day sources become `DayTable.InWeeks(...)`; Magic Bait routes through the bait; raccoon seeds move to CodeSources. |
| `src/TheLongestYear.Core/Obtainability/GrowSources.cs` | Crops, greenhouse, Mixed Seeds, fruit trees, tea bush as `Then` chains. |
| `src/TheLongestYear.Core/Obtainability/MadeSources.cs` | Machines (`Delay`), recipes (`Latest`), animals, ponds, tappers, geodes with setup steps; Seed Maker and Mushroom Log output methods. |
| `src/TheLongestYear.Core/Obtainability/CodeSources.cs` (new) | Hand-typed code facts: mine fish, Moss, Mushroom Log mushrooms, season seeds from fishing treasure, guild rewards from the slayer rows. |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` | New records: `SlayerQuestRow`, `Buildings`, `MachineOutput.Method`, `PondRow.SpawnTime`, `AnimalRow.DaysToProduce`. |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` | Same pass loop; equality on tables; new rules wired in. |
| `src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs` | Source lines print landing weeks; `Describe` takes a start day. |
| `src/TheLongestYear/Loop/GameObtainabilityData.cs` | Reads Buildings and MonsterSlayerQuests, expands `LOCATION_FISH`, drops `fishingGame`, maps output methods, throws `ObtainabilityReadException` when a section fails. |
| `src/TheLongestYear.Core/ObtainabilityComparison.cs` | Dependable-from-day-1 verdict, new section layout. |
| `src/TheLongestYear/ModEntry.cs` (`BuildObtainabilityModel`, `CmdObtain`) | Refuse-to-publish path; `tly_obtain <item> [day]`. |
| `tests/TheLongestYear.Tests/Obtainability*Tests.cs` | Rewritten to the start-day meaning; new `ObtainabilityDayTableTests.cs`, `ObtainabilityCodeSourcesTests.cs`. |

---

### Task 1: DayTable

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/DayTable.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityDayTableTests.cs`
- Modify: `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs` (add the file to `BlindFiles`)

**Interfaces:**
- Produces: `public sealed class DayTable : IEquatable<DayTable>` with
  `const int Days = 112`; `static DayTable None`, `static DayTable Always`;
  `static DayTable Available(Func<int,bool> availableOnDay)`; `static DayTable InWeeks(WeekMask weeks)`;
  `static DayTable Exact(Func<int,int?> landingForStart)`;
  `int? Lands(int startDay)`; `bool IsEmpty`; `DayTable Delay(int days)`; `DayTable Then(DayTable next)`;
  `DayTable Earliest(DayTable other)`; `DayTable Latest(DayTable other)`; `DayTable Except(DayTable dependable)`;
  `int? LandingWeek(int startDay)`; `bool CanObtain(int startDay, int deadlineDay)`;
  `static int SeasonStartDay(Season season)`; `string ToString()`.

The invariant: `Lands(d)` is the earliest landing day starting on any day at or after `d` ("waiting is free, held items keep"). Every constructor and operator preserves it.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityDayTableTests
{
    private static bool Summer(int day) => day >= 29 && day <= 56;

    [Fact]
    public void Available_lands_on_the_first_available_day_at_or_after_the_start()
    {
        DayTable t = DayTable.Available(Summer);
        Assert.Equal(29, t.Lands(1));      // wait for Summer
        Assert.Equal(30, t.Lands(30));     // already Summer
        Assert.Null(t.Lands(57));          // Fall onward: never
        Assert.Equal(5, t.LandingWeek(1));
        Assert.False(t.IsEmpty);
        Assert.True(DayTable.None.IsEmpty);
        Assert.Equal(1, DayTable.Always.Lands(1));
        Assert.Equal(112, DayTable.Always.Lands(112));
    }

    [Fact]
    public void InWeeks_matches_the_week_mask_day_by_day()
    {
        DayTable t = DayTable.InWeeks(WeekMask.ForSeason(Season.Winter));
        Assert.Equal(85, t.Lands(1));
        Assert.Equal(13, t.LandingWeek(1));
        Assert.Null(DayTable.InWeeks(WeekMask.None).Lands(1));
    }

    [Fact]
    public void Exact_closes_over_waiting_so_a_dead_planting_day_is_skipped()
    {
        // A crop that only works when planted on day 10 (lands day 14) or day 20 (lands day 24).
        DayTable t = DayTable.Exact(p => p == 10 ? 14 : p == 20 ? 24 : (int?)null);
        Assert.Equal(14, t.Lands(1));
        Assert.Equal(14, t.Lands(10));
        Assert.Equal(24, t.Lands(11));
        Assert.Null(t.Lands(21));
    }

    [Fact]
    public void Delay_adds_days_and_drops_landings_past_winter_28()
    {
        DayTable t = DayTable.Always.Delay(7);
        Assert.Equal(8, t.Lands(1));
        Assert.Equal(112, t.Lands(105));
        Assert.Null(t.Lands(106));
        Assert.Same(t, t.Delay(0));
    }

    [Fact]
    public void Then_chains_the_landing_of_one_table_into_the_start_of_the_next()
    {
        DayTable seed = DayTable.Available(d => d <= 28);          // sold in Spring only
        DayTable grow = DayTable.Exact(p => p + 4 <= 28 ? p + 4 : (int?)null);   // 4 days, must finish in Spring
        DayTable crop = seed.Then(grow);
        Assert.Equal(5, crop.Lands(1));
        Assert.Equal(28, crop.Lands(24));
        Assert.Null(crop.Lands(25));     // seed on 25, but no planting day finishes in Spring
        Assert.Null(crop.Lands(29));     // no seed at all after Spring
    }

    [Fact]
    public void Earliest_takes_the_sooner_landing_and_Latest_needs_both()
    {
        DayTable a = DayTable.Available(d => d >= 10);
        DayTable b = DayTable.Available(d => d >= 20 && d <= 30);
        Assert.Equal(10, a.Earliest(b).Lands(1));
        Assert.Equal(20, a.Latest(b).Lands(1));
        Assert.Null(a.Latest(b).Lands(31));
        Assert.Equal(40, a.Earliest(b).Lands(40));
    }

    [Fact]
    public void Except_keeps_only_starts_where_the_dependable_table_is_later_or_never()
    {
        DayTable any = DayTable.Available(d => d >= 1);
        DayTable dependable = DayTable.Available(d => d >= 29);
        DayTable luckOnly = any.Except(dependable);
        Assert.Equal(1, luckOnly.Lands(1));
        Assert.Null(luckOnly.Lands(29));   // dependable lands the same day, nothing luck-only left
    }

    [Fact]
    public void CanObtain_checks_the_deadline_and_equality_is_by_content()
    {
        DayTable t = DayTable.Available(Summer);
        Assert.True(t.CanObtain(1, 29));
        Assert.False(t.CanObtain(1, 28));
        Assert.Equal(DayTable.Available(Summer), t);
        Assert.NotEqual(DayTable.Always, t);
        Assert.Equal(29, DayTable.SeasonStartDay(Season.Summer));
    }

    [Fact]
    public void ToString_reports_the_landing_week_from_each_season_start()
    {
        Assert.Equal("lands wk5/wk5/never/never", DayTable.Available(Summer).ToString());
        Assert.Equal("lands wk1/wk5/wk9/wk13", DayTable.Always.ToString());
        Assert.Equal("never", DayTable.None.ToString());
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter ObtainabilityDayTableTests`
Expected: build error, `DayTable` not defined.

- [ ] **Step 3: Implement DayTable**

```csharp
using System;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Start day to landing day, 112 slots (Spring 1 = 1 .. Winter 28 = 112). The table is
/// monotone: <see cref="Lands"/>(d) is the earliest landing starting on ANY day at or after d, so
/// waiting is free and an item once had is held. That is the phase 2 meaning (spec
/// 2026-09-14-obtainability-phase2, decision 1 and 2): "start from nothing on day d, when does it
/// first land". Weeks are the language outside this type.</summary>
public sealed class DayTable : IEquatable<DayTable>
{
    public const int Days = Calendar.DaysPerYear;
    private const byte Never = 0;
    private const int FirstDay = 1;

    private readonly byte[] _lands;   // _lands[d - 1] = landing day for start day d, or Never

    private DayTable(byte[] lands) => _lands = lands;

    public static readonly DayTable None = new(new byte[Days]);
    public static readonly DayTable Always = Available(_ => true);

    /// <summary>A same-day source: lands on the first day at or after the start that is available.</summary>
    public static DayTable Available(Func<int, bool> availableOnDay)
    {
        var lands = new byte[Days];
        int next = Never;
        for (int day = Days; day >= FirstDay; day--)
        {
            if (availableOnDay(day)) next = day;
            lands[day - 1] = (byte)next;
        }
        return new DayTable(lands);
    }

    public static DayTable InWeeks(WeekMask weeks) => Available(day => weeks.Contains(WeekMask.WeekOfDay(day)));

    /// <summary>From an exact "start on p, land on x or never" rule; closes over waiting so a start
    /// day that fails still lands on the best later start.</summary>
    public static DayTable Exact(Func<int, int?> landingForStart)
    {
        var lands = new byte[Days];
        int best = Never;
        for (int day = Days; day >= FirstDay; day--)
        {
            int? landing = landingForStart(day);
            if (landing is int l && l >= day && l <= Days && (best == Never || l < best)) best = l;
            lands[day - 1] = (byte)best;
        }
        return new DayTable(lands);
    }

    public int? Lands(int startDay)
        => startDay < FirstDay || startDay > Days || _lands[startDay - 1] == Never ? null : _lands[startDay - 1];

    public bool IsEmpty => _lands[0] == Never;   // monotone: if day 1 never lands, nothing does

    public int? LandingWeek(int startDay) => Lands(startDay) is int l ? WeekMask.WeekOfDay(l) : null;

    public bool CanObtain(int startDay, int deadlineDay) => Lands(startDay) is int l && l <= deadlineDay;

    public DayTable Delay(int days) => days <= 0 ? this : Map(l => l + days <= Days ? l + days : (int?)null);

    /// <summary>Land this, then start <paramref name="next"/> on that day.</summary>
    public DayTable Then(DayTable next) => Map(l => next.Lands(l));

    /// <summary>Either route: the sooner landing per start day.</summary>
    public DayTable Earliest(DayTable other) => Combine(other, (a, b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value));

    /// <summary>Both needed (a recipe's ingredients): the later landing per start day, never if either never.</summary>
    public DayTable Latest(DayTable other) => Combine(other, (a, b) => a is null || b is null ? null : Math.Max(a.Value, b.Value));

    /// <summary>The starts where this table lands and <paramref name="dependable"/> lands later or never:
    /// what luck alone adds.</summary>
    public DayTable Except(DayTable dependable)
        => Combine(dependable, (a, d) => a is null ? null : d is null || d.Value > a.Value ? a : null);

    public static int SeasonStartDay(Season season) => (int)season * Calendar.DaysPerMonth + 1;

    private DayTable Map(Func<int, int?> f)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
            if (_lands[i] != Never && f(_lands[i]) is int l && l >= FirstDay && l <= Days) lands[i] = (byte)l;
        return new DayTable(lands);
    }

    private DayTable Combine(DayTable other, Func<int?, int?, int?> f)
    {
        var lands = new byte[Days];
        for (int i = 0; i < Days; i++)
        {
            int? a = _lands[i] == Never ? null : _lands[i];
            int? b = other._lands[i] == Never ? null : other._lands[i];
            if (f(a, b) is int l) lands[i] = (byte)l;
        }
        return new DayTable(lands);
    }

    public bool Equals(DayTable? other) => other is not null && _lands.AsSpan().SequenceEqual(other._lands);
    public override bool Equals(object? obj) => Equals(obj as DayTable);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_lands);
        return hash.ToHashCode();
    }

    /// <summary>"lands wk1/wk5/wk9/wk13": the landing week starting on each season's first day.</summary>
    public override string ToString()
    {
        if (IsEmpty) return "never";
        var parts = Enum.GetValues<Season>().Select(s => LandingWeek(SeasonStartDay(s)) is int w ? $"wk{w}" : "never");
        return "lands " + string.Join("/", parts);
    }
}
```

Add `"TheLongestYear.Core/Obtainability/DayTable.cs",` to `BlindFiles` in `ObtainabilityBlindGuardTests.cs`.

- [ ] **Step 4: Run the tests**

Run the full test command. Expected: all pass (2246 + 9).

- [ ] **Step 5: Commit and push**

```bash
git add src/TheLongestYear.Core/Obtainability/DayTable.cs tests/TheLongestYear.Tests/ObtainabilityDayTableTests.cs tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs
git commit -m "obtainability: DayTable, the monotone start-day to landing-day table"
git push origin story
```

---

### Task 2: ObtainSource, SetupStep, SourcePair and the model queries

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` (`SameWeeks` only)
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityText.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityModelTests.cs` (rewrite), `tests/TheLongestYear.Tests/ObtainabilityTextTests.cs` (rewrite)

This task changes the record shape, so the whole solution stops compiling until Tasks 3 to 5 convert the rule files. To keep every commit green, do the mechanical conversion of the rule files IN THIS TASK with the one-line pattern below, and leave the meaning changes (crops, machines, setup steps) to Tasks 4 and 5. The pattern is exact and needs no judgement:

| Phase 1 expression | Phase 2 expression |
|---|---|
| `WeekMask.All` as a source's weeks | `DayTable.Always` |
| `reading.Weeks` / `weeks` / `festival.Weeks` / `WeekMask.ForSeason(x)` as a source's weeks | `DayTable.InWeeks(<same>)` |
| `snapshot.Weeks(id, filter)` | `snapshot.Table(id, filter)` |
| `mask & other` on source tables | `DayTable.InWeeks(mask & other)` when both are masks; `table.Then(DayTable.InWeeks(mask))` when one is a table |
| `a | b` on tables | `a.Earliest(b)` |
| `MadeSources.ShiftByDays(t, days)` | `t.Delay(days)` |
| `t.FromWeekOnward()` | drop it: a monotone table already means "from then on" |
| `WeekMask.None` as a table | `DayTable.None` |
| `.IsEmpty` on a table | unchanged |

**Interfaces:**
- Produces:
  ```csharp
  public sealed record SetupStep(string Name, int Days);
  public sealed record ObtainSource(SourceKind Kind, DayTable Lands, Reliability Reliability, ObtainConditions Conditions, string Detail)
  { public IReadOnlyList<SetupStep> Setup { get; init; } = Array.Empty<SetupStep>(); /* Equals/GetHashCode compare Setup by content */ }
  public static class SourcePair { public static IEnumerable<ObtainSource> Of(SourceKind kind, DayTable dependable, DayTable any, ObtainConditions conditions, string detail, IReadOnlyList<SetupStep>? setup = null); }
  // ObtainabilityModel
  public DayTable Table(string itemId, ObtainFilter filter);           // Earliest over accepted sources
  public int? Lands(string itemId, int startDay, ObtainFilter filter);
  public bool CanObtain(string itemId, int startDay, int deadlineDay, ObtainFilter filter);
  public int? LandingWeekFromDay1(string itemId, ObtainFilter filter);
  // ObtainabilityText
  public static string Describe(string itemId, ObtainabilityModel model, Func<string, string?> nameOf, int startDay = 1);
  public static string SourceLine(ObtainSource s);   // "Fish, Dependable, lands wk5/wk5/never/never, needs ... | detail"
  ```
- `ObtainabilityModel.Weeks(...)`, `IsObtainable(...)`, `EarliestWeek(...)` are REMOVED. Anything that called them is converted in this task.

- [ ] **Step 1: Rewrite the model and text tests**

Replace `tests/TheLongestYear.Tests/ObtainabilityModelTests.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityModelTests
{
    private static ObtainSource Src(SourceKind kind, DayTable lands, Reliability r, ObtainConditions? c = null, string detail = "test")
        => new(kind, lands, r, c ?? ObtainConditions.None, detail);

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)147"] = new[]
        {
            Src(SourceKind.Fish, DayTable.InWeeks(WeekMask.ForSeason(Season.Spring) | WeekMask.ForSeason(Season.Winter)), Reliability.Dependable),
            Src(SourceKind.Cart, DayTable.Always, Reliability.Chance),
        },
        ["(O)91"] = new[] { Src(SourceKind.FruitTree, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { YearTwo = true }) },
        ["(O)829"] = new[] { Src(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { GingerIsland = true }) },
        ["(O)999"] = new[] { Src(SourceKind.Other, DayTable.Always, Reliability.Chance, ObtainConditions.None with { Unresolved = true }) },
    });

    [Fact]
    public void Ids_are_normalized_on_the_way_in_and_out()
    {
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { ["24"] = new[] { Src(SourceKind.Crop, DayTable.Always, Reliability.Dependable) } });
        Assert.Single(model.Sources("(O)24"));
        Assert.Single(model.Sources("24"));
        Assert.Contains("(O)24", model.ItemIds);
    }

    [Fact]
    public void Dependable_only_waits_for_the_season_while_any_source_lands_at_once()
    {
        Assert.Equal(1, Model.Lands("(O)147", 1, ObtainFilter.DependableOnly));
        Assert.Equal(85, Model.Lands("(O)147", 29, ObtainFilter.DependableOnly));   // Summer 1: wait for Winter
        Assert.Equal(29, Model.Lands("(O)147", 29, ObtainFilter.Any));
        Assert.Equal(13, Model.Table("(O)147", ObtainFilter.DependableOnly).LandingWeek(29));
        Assert.Equal(1, Model.LandingWeekFromDay1("(O)147", ObtainFilter.DependableOnly));
    }

    [Fact]
    public void CanObtain_applies_the_callers_deadline()
    {
        Assert.False(Model.CanObtain("(O)147", 29, 84, ObtainFilter.DependableOnly));
        Assert.True(Model.CanObtain("(O)147", 29, 85, ObtainFilter.DependableOnly));
        Assert.True(Model.CanObtain("(O)147", 29, 29, ObtainFilter.Any));
        Assert.False(Model.CanObtain("(O)nothing", 1, 112, ObtainFilter.Any));
    }

    [Fact]
    public void Year_two_and_island_and_unresolved_sources_count_only_when_asked()
    {
        Assert.Null(Model.Lands("(O)91", 1, ObtainFilter.Any));
        Assert.Equal(1, Model.Lands("(O)91", 1, ObtainFilter.Any with { IncludeYearTwo = true }));
        Assert.Null(Model.Lands("(O)829", 1, ObtainFilter.Any));
        Assert.Equal(1, Model.Lands("(O)829", 1, ObtainFilter.Any with { IncludeGingerIsland = true }));
        Assert.Equal(1, Model.Lands("(O)999", 1, ObtainFilter.Any));
        Assert.Null(Model.Lands("(O)999", 1, ObtainFilter.Any with { IncludeUnresolved = false }));
    }

    [Fact]
    public void Kind_filters_pick_sources()
    {
        var cartOnly = ObtainFilter.Any with { Kinds = new[] { SourceKind.Cart } };
        Assert.Equal(1, Model.Lands("(O)147", 1, cartOnly));
        Assert.Null(Model.Lands("(O)147", 1, cartOnly with { Reliabilities = new[] { Reliability.Dependable } }));
    }

    [Fact]
    public void Identical_sources_collapse_and_setup_is_part_of_identity()
    {
        var a = Src(SourceKind.Animal, DayTable.Always, Reliability.Dependable) with { Setup = new[] { new SetupStep("building:Coop", 3) } };
        var b = Src(SourceKind.Animal, DayTable.Always, Reliability.Dependable) with { Setup = new[] { new SetupStep("building:Coop", 3) } };
        var c = Src(SourceKind.Animal, DayTable.Always, Reliability.Dependable);
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { ["(O)176"] = new[] { a, b, c } });
        Assert.Equal(2, model.Sources("(O)176").Count);
    }

    [Fact]
    public void SourcePair_splits_luck_only_starts_from_dependable_ones()
    {
        DayTable dep = DayTable.InWeeks(WeekMask.ForSeason(Season.Summer));
        var pair = SourcePair.Of(SourceKind.Crop, dep, DayTable.Always, ObtainConditions.None, "x").ToList();
        Assert.Equal(2, pair.Count);
        Assert.Equal(Reliability.Dependable, pair[0].Reliability);
        Assert.Equal(29, pair[0].Lands.Lands(1));
        Assert.Equal(Reliability.Chance, pair[1].Reliability);
        Assert.Equal(1, pair[1].Lands.Lands(1));
        Assert.Null(pair[1].Lands.Lands(29));   // in Summer luck adds nothing
        Assert.Single(SourcePair.Of(SourceKind.Crop, dep, dep, ObtainConditions.None, "x"));
        Assert.Empty(SourcePair.Of(SourceKind.Crop, DayTable.None, DayTable.None, ObtainConditions.None, "x"));
    }
}
```

Replace `tests/TheLongestYear.Tests/ObtainabilityTextTests.cs` with:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityTextTests
{
    [Fact]
    public void Describe_prints_every_source_with_its_landing_weeks_from_the_start_day()
    {
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)147"] = new[]
            {
                new ObtainSource(SourceKind.Fish, DayTable.InWeeks(WeekMask.ForSeason(Season.Winter)), Reliability.Dependable,
                    ObtainConditions.None with { Skill = "Fishing", SkillLevel = 3, RainOnly = true, Requires = new[] { "location:Beach" } }, "Fish at Beach")
                { Setup = new[] { new SetupStep("building:Fish Pond", 2) } },
                new ObtainSource(SourceKind.Cart, DayTable.Always, Reliability.Chance, ObtainConditions.None, "shop Traveler"),
            },
        });
        string text = ObtainabilityText.Describe("(O)147", model, _ => "Herring", startDay: 29);
        Assert.Contains("(O)147 Herring: 2 source(s); from day 29 dependable lands week 13, any lands week 5", text);
        Assert.Contains("Fish, Dependable, lands wk13/wk13/wk13/wk13, Fishing 3, rain only, needs location:Beach, setup building:Fish Pond 2d | Fish at Beach", text);
        Assert.Contains("Cart, Chance, lands wk1/wk5/wk9/wk13 | shop Traveler", text);
        Assert.Contains("no source", ObtainabilityText.Describe("(O)1", model, _ => null));
        Assert.DoesNotContain("\u2014", text);
    }
}
```

Note the Fish source line: a Winter fish CAN be had starting Spring 1, by waiting, which is why every season start reads `wk13`.

- [ ] **Step 2: Run to verify they fail**

Expected: build errors (`DayTable Lands`, `SetupStep`, `Table`, `Lands` not defined).

- [ ] **Step 3: Change the types**

In `ObtainTypes.cs` replace `ObtainSource` and `SourcePair` with:

```csharp
/// <summary>A step the player must do first, with the game's own days figure ("building:Coop" 3,
/// "animal:Chicken" 1, "sapling" 28). Informational in the blind model (spec decision 3): the
/// comparison assumes it is done; a consumer adds the days for what the real farm lacks.</summary>
public sealed record SetupStep(string Name, int Days);

/// <summary>One way to obtain an item: its kind, the start-to-landing table, how dependable it is,
/// what it needs, its setup steps, and a short origin note for the debug output.</summary>
public sealed record ObtainSource(
    SourceKind Kind, DayTable Lands, Reliability Reliability, ObtainConditions Conditions, string Detail)
{
    public IReadOnlyList<SetupStep> Setup { get; init; } = Array.Empty<SetupStep>();

    public bool Equals(ObtainSource? other)
        => other is not null && Kind == other.Kind && Lands.Equals(other.Lands) && Reliability == other.Reliability
           && Conditions.Equals(other.Conditions) && Detail == other.Detail && Setup.SequenceEqual(other.Setup);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind); hash.Add(Lands); hash.Add(Reliability); hash.Add(Conditions); hash.Add(Detail);
        foreach (SetupStep s in Setup) hash.Add(s);
        return hash.ToHashCode();
    }
}

/// <summary>Emits one dependable source and one chance source for the starts luck alone serves,
/// so reliability survives derivation.</summary>
public static class SourcePair
{
    public static IEnumerable<ObtainSource> Of(
        SourceKind kind, DayTable dependable, DayTable any, ObtainConditions conditions, string detail,
        IReadOnlyList<SetupStep>? setup = null)
    {
        IReadOnlyList<SetupStep> steps = setup ?? Array.Empty<SetupStep>();
        if (!dependable.IsEmpty)
            yield return new ObtainSource(kind, dependable, Reliability.Dependable, conditions, detail) { Setup = steps };
        DayTable luckOnly = any.Except(dependable);
        if (!luckOnly.IsEmpty)
            yield return new ObtainSource(kind, luckOnly, Reliability.Chance, conditions, detail) { Setup = steps };
    }
}
```

In `ObtainabilityModel.cs` replace `Weeks`, `IsObtainable`, `EarliestWeek` with:

```csharp
    /// <summary>The earliest landing per start day over every accepted source.</summary>
    public DayTable Table(string itemId, ObtainFilter filter)
    {
        DayTable table = DayTable.None;
        foreach (ObtainSource source in Sources(itemId))
            if (filter.Accepts(source)) table = table.Earliest(source.Lands);
        return table;
    }

    public int? Lands(string itemId, int startDay, ObtainFilter filter) => Table(itemId, filter).Lands(startDay);

    public bool CanObtain(string itemId, int startDay, int deadlineDay, ObtainFilter filter)
        => Table(itemId, filter).CanObtain(startDay, deadlineDay);

    public int? LandingWeekFromDay1(string itemId, ObtainFilter filter) => Table(itemId, filter).LandingWeek(1);
```

In `ObtainabilityBuilder.cs` change `SameWeeks` to compare tables:

```csharp
    private static bool SameTables(ObtainabilityModel a, ObtainabilityModel b)
    {
        if (a.Count != b.Count) return false;
        foreach (string id in b.ItemIds)
            if (a.Sources(id).Count != b.Sources(id).Count
                || !a.Table(id, ObtainFilter.Any).Equals(b.Table(id, ObtainFilter.Any))
                || !a.Table(id, ObtainFilter.DependableOnly).Equals(b.Table(id, ObtainFilter.DependableOnly)))
                return false;
        return true;
    }
```
and rename its call site. Keep the class comment's claim true: a landing day only moves earlier as passes run (Earliest), so the loop settles.

In `ObtainabilityText.cs`:

```csharp
    public static string Describe(string itemId, ObtainabilityModel model, Func<string, string?> nameOf, int startDay = 1)
    {
        string id = BundleParsing.NormalizeItemId(itemId);
        string name = nameOf(id) ?? "?";
        IReadOnlyList<ObtainSource> sources = model.Sources(id);
        if (sources.Count == 0) return $"{id} {name}: no source in the obtainability model.";

        var sb = new StringBuilder();
        sb.Append($"{id} {name}: {sources.Count} source(s); from day {startDay} dependable lands {Week(model.Table(id, ObtainFilter.DependableOnly).LandingWeek(startDay))}, ")
          .Append($"any lands {Week(model.Table(id, ObtainFilter.Any).LandingWeek(startDay))}");
        foreach (ObtainSource s in sources)
            sb.AppendLine().Append("  - ").Append(SourceLine(s));
        return sb.ToString();
    }

    public static string SourceLine(ObtainSource s)
    {
        var sb = new StringBuilder($"{s.Kind}, {s.Reliability}, {s.Lands}");
        ObtainConditions c = s.Conditions;
        if (c.Skill != null) sb.Append($", {c.Skill} {c.SkillLevel}");
        if (c.CatchLimit > 0) sb.Append($", catch limit {c.CatchLimit}");
        if (c.RainOnly) sb.Append(", rain only");
        if (c.FewDays) sb.Append(", few days");
        if (c.YearTwo) sb.Append(", year 2");
        if (c.GingerIsland) sb.Append(", Ginger Island");
        if (c.Unresolved) sb.Append(", UNRESOLVED");
        if (c.Requires.Count > 0) sb.Append($", needs {string.Join("; ", c.Requires)}");
        if (s.Setup.Count > 0) sb.Append($", setup {string.Join("; ", s.Setup.Select(x => $"{x.Name} {x.Days}d"))}");
        return sb.Append($" | {s.Detail}").ToString();
    }

    private static string Week(int? week) => week is int w ? $"week {w}" : "never";
```

- [ ] **Step 4: Mechanical conversion of the rule files, using the table above**

Apply the pattern to `SpawnSources.cs`, `ShopSources.cs`, `MineSources.cs`, `GrowSources.cs`, `MadeSources.cs`, `ObtainabilityBuilder.cs`. Keep the phase 1 MEANING in Grow and Made for now (they will be rewritten in Tasks 4 and 5); the aim of this step is only to compile. Concretely:

- `SpawnSources.Spawn`: build `weeks` as before, then `new ObtainSource(kind, DayTable.InWeeks(weeks), ...)`.
- `SpawnSources.CrabPot`, `FishingTrash`, `MineSources.Nodes`, `MonsterDrops`: `DayTable.Always`.
- `MineSources.FishingTreasure`: `DayTable.InWeeks(weeks)`.
- `ShopSources.Template`, `FestivalRewards`: `DayTable.InWeeks(weeks)` / `DayTable.InWeeks(festival.Weeks)`.
- `ShopSources.Barter`: `DayTable dep = template.Reliability == Dependable ? template.Lands.Latest(snapshot.Table(trade, DependableOnly)) : DayTable.None; DayTable any = template.Lands.Latest(snapshot.Table(trade, Any));`
- `ShopSources.RecipeWeeks` keeps returning `WeekMask` (readers use it as a season mask); `MadeSources.Recipes` converts with `DayTable.InWeeks(taught)` and `Latest`.
- `GrowSources`: keep `Harvest`/`Greenhouse`/`Grow`/`Mature` returning `WeekMask` for now and wrap results with `DayTable.InWeeks(...)`; take seed tables as `snapshot.Table(...)` and pass `LandingWeeksOf(table)` where `LandingWeeksOf` is a private helper `WeekMask.Range(table.LandingWeek(1) ?? 17, 16)` (a stopgap that Task 4 deletes).
- `MadeSources`: replace `ShiftByDays(x, days)` with `x.Delay(days)`; `|=` with `Earliest`; `&=` with `Latest`; `& gate` with `.Then(DayTable.InWeeks(gate))`; `FromWeekOnward()` dropped.
- `ObtainabilityComparison.cs` (not blind): temporarily `newDep = model.LandingWeekFromDay1(id, DependableOnly)`, `newAny = model.LandingWeekFromDay1(id, Any)`; Task 9 finishes it.
- `ModEntry.CmdObtain`: unchanged call to `Describe` (default `startDay`).

Then fix the OTHER test files only as far as compiling requires (`Weeks` to `Lands`, `WeekMask.X` to `DayTable.InWeeks(WeekMask.X)`, `model.Weeks(id, f).ToString()` to `model.Table(id, f).ToString()`) and update their expected strings to the `lands wk../..` form. Where a phase 1 expectation encoded the FINISH-week meaning (crop tests, builder chain tests, made tests), mark the test `[Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]` rather than inventing a value; Tasks 4 and 5 un-skip and rewrite them. List every skipped test in the commit message.

- [ ] **Step 5: Run the full suite**

Expected: green, with the listed skips. Record the pass and skip counts.

- [ ] **Step 6: Commit and push**

```bash
git add -A src/TheLongestYear.Core/Obtainability src/TheLongestYear.Core/ObtainabilityComparison.cs tests/TheLongestYear.Tests
git commit -m "obtainability: sources carry a DayTable and setup steps; model answers by start day

Skipped pending the start-day rewrite in tasks 4 and 5: <list>"
git push origin story
```

---

### Task 3: Conditions at day level, the island negation fix, and the same-day sources

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ConditionSeasons.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/SpawnSources.cs`, `ShopSources.cs`, `MineSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityConditionTests.cs`, `ObtainabilitySpawnTests.cs`, `ObtainabilityShopTests.cs`, `ObtainabilityMineTests.cs`

**Interfaces:**
- `ConditionReading` gains `IReadOnlySet<int>? DaysOfYear` (null = every day of `Weeks`; otherwise only these days of the year, 1..112, already limited to `Weeks`).
- Produces `public static DayTable ConditionSeasons.Availability(ConditionReading reading, WeekMask alsoWithin)`: `DayTable.Available(d => alsoWithin.Contains(week(d)) && reading.Weeks.Contains(week(d)) && (reading.DaysOfYear == null || reading.DaysOfYear.Contains(d)))`.
- `SpawnSources.LocationFish` gains a snapshot parameter for Magic Bait routing and moves into the builder's pass loop: `LocationFish(rows, fishRows, objects, festivals, ObtainabilityModel snapshot)`.
- `MineSources.FishingTreasure()` no longer yields the raccoon-seed diagnostic (Task 6 adds the real rows).

- [ ] **Step 1: Write the failing tests**

Add to `ObtainabilityConditionTests.cs`:

```csharp
    [Fact]
    public void A_negated_island_clause_does_not_hint_the_island()
    {
        ConditionReading r = ConditionSeasons.Read("!IS_VISITING_ISLAND", NoFestivals);
        Assert.False(r.IslandHint);
        Assert.True(ConditionSeasons.Read("IS_VISITING_ISLAND", NoFestivals).IslandHint);
        Assert.False(ConditionSeasons.Apply(ObtainConditions.None, r).GingerIsland);
    }

    [Fact]
    public void Day_pinned_clauses_land_on_those_days_only()
    {
        ConditionReading r = ConditionSeasons.Read("SEASON_DAY Winter 15 Winter 16 Winter 17", NoFestivals);
        Assert.NotNull(r.DaysOfYear);
        DayTable t = ConditionSeasons.Availability(r, WeekMask.All);
        Assert.Equal(99, t.Lands(1));      // Winter 15
        Assert.Equal(101, t.Lands(101));
        Assert.Null(t.Lands(102));
        Assert.True(r.FewDays);

        ConditionReading dom = ConditionSeasons.Read("DAY_OF_MONTH 1", NoFestivals);
        Assert.Equal(29, ConditionSeasons.Availability(dom, WeekMask.All).Lands(2));

        ConditionReading played = ConditionSeasons.Read("DAYS_PLAYED 10 20", NoFestivals);
        Assert.Equal(10, ConditionSeasons.Availability(played, WeekMask.All).Lands(1));
        Assert.Null(ConditionSeasons.Availability(played, WeekMask.All).Lands(21));

        ConditionReading season = ConditionSeasons.Read("SEASON Summer", NoFestivals);
        Assert.Null(season.DaysOfYear);
        Assert.Equal(29, ConditionSeasons.Availability(season, WeekMask.All).Lands(1));
        Assert.Null(ConditionSeasons.Availability(season, WeekMask.ForSeason(Season.Fall)).Lands(1));
    }
```

(`NoFestivals` already exists in that file as `new Dictionary<string, FestivalDates>()`; if it is named differently, use the existing name.)

Add to `ObtainabilitySpawnTests.cs`:

```csharp
    [Fact]
    public void A_magic_bait_row_routes_through_the_bait_and_inherits_its_island_flag()
    {
        var bait = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)908"] = new[] { new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { GingerIsland = true }, "shop QiGemShop") },
        });
        var rows = new[] { new LocationSpawn("Beach", "(O)798", null, "SEASON Winter", 1.0, 0, true, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var squid = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), NoFestivals, bait).Single(s => s.ItemId == "(O)798").Source;
        Assert.True(squid.Conditions.GingerIsland);
        Assert.Contains("item:(O)908 Magic Bait", squid.Conditions.Requires);
        Assert.Equal(85, squid.Lands.Lands(1));
        var none = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), NoFestivals, new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>())).ToList();
        Assert.Empty(none);   // no bait anywhere: the row cannot be fished
    }

    [Fact]
    public void A_night_market_fish_lands_on_the_first_market_day()
    {
        var festivals = new Dictionary<string, FestivalDates> { ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17) };
        var rows = new[] { new LocationSpawn("Submarine", "(O)798", null, null, 1.0, 0, false, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var src = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), festivals, new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>())).Single().Source;
        Assert.Equal(99, src.Lands.Lands(1));
        Assert.Equal(101, src.Lands.Lands(101));
        Assert.Null(src.Lands.Lands(102));
        Assert.True(src.Conditions.FewDays);
    }
```

Add to `ObtainabilityMineTests.cs`:

```csharp
    [Fact]
    public void Fishing_treasure_no_longer_reports_the_raccoon_seed_as_unresolved()
        => Assert.DoesNotContain(MineSources.FishingTreasure(), s => s.ItemId.StartsWith(ItemQueries.UnresolvedPrefix));
```

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors on `DaysOfYear`, `Availability`, the new `LocationFish` parameter; the island test fails on `IslandHint`.

- [ ] **Step 3: Implement**

`ConditionSeasons.cs`:
- Add `IReadOnlySet<int>? DaysOfYear` as the last field of `ConditionReading`.
- In `Read`, keep a `HashSet<int>? days = null;` and a local `void PinDays(IEnumerable<int> d) { days ??= new HashSet<int>(); ... }`. Because clauses AND together, pin by intersection: the first pinning clause sets the set; later pinning clauses intersect. Pin from:
  - `SEASON_DAY`: `Calendar.DayOfYear((int)season, day)` per pair.
  - `DAY_OF_MONTH n...` (not even/odd, not negated): every season's day n.
  - `DAYS_PLAYED min [max]`: the range clamped to 1..112.
  - `IS_PASSIVE_FESTIVAL_OPEN/TODAY`: the festival's `Calendar.DayOfYear(start)..DayOfYear(end)`.
  - Keep the `WeekMask` narrowing exactly as it is, so `Weeks` still summarizes.
- Fix the island line: replace
  `if (clause.Contains(IslandMarker, StringComparison.Ordinal)) island = true;`
  with
  ```csharp
  bool negated = clause.StartsWith("!", StringComparison.Ordinal);
  if (!negated && clause.Contains(IslandMarker, StringComparison.Ordinal)) island = true;
  ```
  (and delete the later duplicate `bool negated` line).
- Add:
  ```csharp
  public static DayTable Availability(ConditionReading reading, WeekMask alsoWithin)
      => DayTable.Available(day =>
      {
          int week = WeekMask.WeekOfDay(day);
          return alsoWithin.Contains(week) && reading.Weeks.Contains(week)
                 && (reading.DaysOfYear == null || reading.DaysOfYear.Contains(day));
      });
  ```

`SpawnSources.cs`:
- `Spawn` returns `ConditionSeasons.Availability(reading, seasonMask & festivalMask)` as the table; keep the `FewDays` flag for festival-only maps. For festival-only maps use the festival's exact days: build `WeekMask festivalMask = festival.Weeks` AND pin days `Calendar.DayOfYear(start)..DayOfYear(end)` by wrapping: `table = table.Latest(DayTable.Available(d => d >= startDoy && d <= endDoy))`.
- `LocationFish(..., ObtainabilityModel snapshot)`: when `row.RequireMagicBait`, compute `DayTable bait = snapshot.Table("(O)908", ObtainFilter.Any)` and `DayTable baitDep = snapshot.Table("(O)908", ObtainFilter.DependableOnly)`; if `bait.IsEmpty` skip the row; else the fish table is `bait.Then(rowTable)` (dependable) / `baitDep.Then(rowTable)`, emitted through `SourcePair.Of`, and the conditions get `GingerIsland = true` when EVERY accepted bait source is island-flagged (read `snapshot.Sources("(O)908")`: if all have `Conditions.GingerIsland`, flag it). Keep the `item:(O)908 Magic Bait` note.
- `ArtifactSpots`, `GarbageCans`: `ConditionSeasons.Availability(reading, WeekMask.All)`.

`ShopSources.cs`:
- `Template`: table = `ConditionSeasons.Availability(reading, placeWeeks)`; for festival day shops pin the exact day: `.Latest(DayTable.Available(d => d == festivalDayOfYear))` where the day comes from `FestivalDates` (`Calendar.DayOfYear((int)Season, StartDay)`); for passive festivals pin the range.
- `FestivalRewards`: same pinning by the festival's date range.
- Return type of `Template` stays `ObtainSource?`.

`MineSources.cs`: delete the raccoon-seed diagnostic `yield return` at the end of `FishingTreasure` (Task 6 adds the real sources in `CodeSources`). The `(O)273` rice shoot line becomes `DayTable.InWeeks(WeekMask.ForSeason(Season.Spring))`.

`ObtainabilityBuilder.cs`: move `SpawnSources.LocationFish(...)` from `direct` into the pass loop (it now takes `current`).

Un-skip and rewrite any test skipped in Task 2 that belongs to these four test files, using landing days: e.g. a phase 1 `Assert.Equal("5-8", src.Weeks.ToString())` for a Summer fish becomes `Assert.Equal("lands wk5/wk5/never/never", src.Lands.ToString())`.

- [ ] **Step 4: Run the full suite**

Expected: green; the only remaining skips are in Grow, Made and Builder tests.

- [ ] **Step 5: Commit and push**

```bash
git add -A src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests
git commit -m "obtainability: day-level conditions, island negation fix, magic bait routed through the bait"
git push origin story
```

---

### Task 4: Grow sources as start-day chains (crops, greenhouse, Mixed Seeds, fruit trees, tea bush)

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/GrowSources.cs` (rewrite)
- Test: `tests/TheLongestYear.Tests/ObtainabilityGrowTests.cs` (rewrite)

**Interfaces:**
- Produces:
  ```csharp
  public static DayTable GrowSources.PlantTable(IReadOnlyList<Season> seasons, int growthDays);   // exact: plant day p to harvest day, outdoors
  public static DayTable GrowSources.GreenhouseTable(int growthDays);                              // exact: p + growth
  public static IEnumerable<(string ItemId, ObtainSource Source)> Crops(IEnumerable<CropRow> rows, ObtainabilityModel snapshot);
  public static IEnumerable<(string ItemId, ObtainSource Source)> FruitTrees(IEnumerable<FruitTreeRow> rows, ObtainabilityModel snapshot, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals);
  public static IEnumerable<(string ItemId, ObtainSource Source)> TeaBush(ObtainabilityModel snapshot);
  ```
- Consumes: `snapshot.Table(seedId, filter)`, `DayTable.Then/Exact/Delay/Available`, `SourcePair.Of`.

Facts from the decompile (cite in comments): fruit trees mature 28 days after planting (`FruitTree.cs` 66); tea bush `Bush.inBloom` (`Bush.cs` 209-225): age 20 or more, day of month 22 or later, not Winter unless sheltered; Tea Sapling is `(O)251`, Tea Leaves `(O)815` (`Bush.cs` 447). Regrowth never gives an EARLIER landing, so it is not part of the table; keep `RegrowDays` in the detail text only.

- [ ] **Step 1: Rewrite the grow tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityGrowTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();

    private static ObtainabilityModel Snapshot(params (string Id, SourceKind Kind, DayTable Lands, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(r.Kind, r.Lands, r.R, ObtainConditions.None, "test")).ToList()));

    private static DayTable Spring => DayTable.InWeeks(WeekMask.ForSeason(Season.Spring));

    [Fact]
    public void A_spring_crop_planted_late_in_spring_never_lands_and_the_greenhouse_needs_the_seed_first()
    {
        Assert.Equal(5, GrowSources.PlantTable(new[] { Season.Spring }, 4).Lands(1));
        Assert.Equal(28, GrowSources.PlantTable(new[] { Season.Spring }, 4).Lands(24));
        Assert.Null(GrowSources.PlantTable(new[] { Season.Spring }, 4).Lands(25));
        Assert.Equal(29, GrowSources.GreenhouseTable(4).Lands(25));
        Assert.Equal(112, GrowSources.GreenhouseTable(4).Lands(108));
        Assert.Null(GrowSources.GreenhouseTable(4).Lands(109));
    }

    [Fact]
    public void A_two_season_crop_grows_across_the_season_line()
    {
        DayTable t = GrowSources.PlantTable(new[] { Season.Summer, Season.Fall }, 14);
        Assert.Equal(43, t.Lands(1));     // wait for Summer 1, plus 14
        Assert.Equal(84, t.Lands(70));    // Fall 14 + 14 = Fall 28
        Assert.Null(t.Lands(71));
    }

    [Fact]
    public void Crops_chain_from_the_seeds_own_table_and_split_reliability()
    {
        var snapshot = Snapshot(
            ("(O)472", SourceKind.Shop, Spring, Reliability.Dependable),
            ("(O)472", SourceKind.Cart, DayTable.Always, Reliability.Chance));
        var rows = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var sources = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = sources.Single(s => s.Kind == SourceKind.Crop && s.Reliability == Reliability.Dependable);
        Assert.Equal(5, outdoor.Lands.Lands(1));
        Assert.Null(outdoor.Lands.Lands(25));                   // seed on Spring 25 cannot finish; no later seed
        Assert.DoesNotContain(sources, s => s.Kind == SourceKind.Crop && s.Reliability == Reliability.Chance);   // the cart seed is a Spring seed too: outdoors it adds nothing
        var greenhouse = sources.Where(s => s.Kind == SourceKind.GreenhouseCrop).ToList();
        var dep = greenhouse.Single(s => s.Reliability == Reliability.Dependable);
        Assert.Equal(32, dep.Lands.Lands(28));                 // seed bought Spring 28, greenhouse, lands Summer 4
        Assert.Null(dep.Lands.Lands(29));                      // hit in week 5: the seed is gone
        var luck = greenhouse.Single(s => s.Reliability == Reliability.Chance);
        Assert.Equal(33, luck.Lands.Lands(29));                // the cart could sell it any day
        Assert.All(greenhouse, s => Assert.Contains("mail:ccPantry", s.Conditions.Requires));
    }

    [Fact]
    public void Mixed_seeds_give_the_planting_days_pool_and_winter_greenhouse_gives_every_pool()
    {
        var snapshot = Snapshot(("(O)770", SourceKind.Forage, DayTable.Always, Reliability.Chance));
        var rows = new[]
        {
            new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0),
            new CropRow("(O)487", "(O)270", new[] { Season.Summer, Season.Fall }, 14, 4),
            new CropRow("(O)770", "(O)770", new Season[0], 1, 0),
        };
        var parsnip = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = parsnip.Single(s => s.Kind == SourceKind.Crop && s.Detail.Contains("Mixed Seeds"));
        Assert.Equal(Reliability.Chance, outdoor.Reliability);
        Assert.Equal(5, outdoor.Lands.Lands(1));
        Assert.Null(outdoor.Lands.Lands(25));
        var indoor = parsnip.Single(s => s.Kind == SourceKind.GreenhouseCrop && s.Detail.Contains("Mixed Seeds"));
        Assert.Equal(89, indoor.Lands.Lands(29));               // Summer 1 hit: the spring pool next comes in Winter (85 + 4)
        Assert.DoesNotContain(GrowSources.Crops(rows, snapshot), s => s.ItemId == "(O)770");
    }

    [Fact]
    public void A_fruit_tree_matures_28_days_after_the_sapling_then_waits_for_its_season()
    {
        var snapshot = Snapshot(
            ("(O)633", SourceKind.Shop, DayTable.Always, Reliability.Dependable),   // apple sapling, Fall fruit
            ("(O)628", SourceKind.Shop, Spring, Reliability.Dependable),            // cherry sapling, Spring fruit
            ("(O)69", SourceKind.Shop, DayTable.Always, Reliability.Dependable));   // banana sapling
        var rows = new[]
        {
            new FruitTreeRow("(O)633", new[] { Season.Fall }, new[] { new FruitRow("(O)613", null, 1.0, null) }),
            new FruitTreeRow("(O)628", new[] { Season.Spring }, new[] { new FruitRow("(O)638", null, 1.0, null) }),
            new FruitTreeRow("(O)69", new[] { Season.Summer }, new[] { new FruitRow("(O)91", null, 0.5, "YEAR 2") }),
        };
        var all = GrowSources.FruitTrees(rows, snapshot, new Dictionary<string, ObjInfo>(), NoFestivals).ToList();
        var apple = all.Single(s => s.ItemId == "(O)613" && s.Source.Kind == SourceKind.FruitTree).Source;
        Assert.Equal(57, apple.Lands.Lands(1));                 // mature by day 29, first Fall day 57
        Assert.Equal(84, apple.Lands.Lands(56));                // planted Summer 28, mature Fall 28
        Assert.Null(apple.Lands.Lands(57));
        Assert.Contains(apple.Setup, s => s.Name == "sapling" && s.Days == 28);
        Assert.DoesNotContain(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.FruitTree);   // a Spring sapling never fruits outdoors this year
        var cherryIndoor = all.Single(s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.GreenhouseCrop).Source;
        Assert.Equal(29, cherryIndoor.Lands.Lands(1));
        var banana = all.First(s => s.ItemId == "(O)91").Source;
        Assert.Equal(Reliability.Chance, banana.Reliability);
        Assert.True(banana.Conditions.YearTwo);
    }

    [Fact]
    public void A_tea_bush_gives_leaves_from_day_22_after_20_days_and_only_sheltered_in_winter()
    {
        var snapshot = Snapshot(("(O)251", SourceKind.Crafting, DayTable.Always, Reliability.Dependable));
        var leaves = GrowSources.TeaBush(snapshot).Where(s => s.ItemId == "(O)815").Select(s => s.Source).ToList();
        var outdoor = leaves.Single(s => s.Kind == SourceKind.Crop);
        Assert.Equal(22, outdoor.Lands.Lands(1));               // planted day 1, age 20 on day 21, bloom from day 22
        Assert.Equal(23, outdoor.Lands.Lands(3));               // planted day 3, age 20 on day 23, and Spring 23 is already past the 22nd
        Assert.Null(outdoor.Lands.Lands(65));                   // Fall 9: age 20 lands Fall 29 = Winter, outdoors never
        var sheltered = leaves.Single(s => s.Kind == SourceKind.GreenhouseCrop);
        Assert.Equal(106, sheltered.Lands.Lands(65));           // Winter 22
        Assert.Contains(outdoor.Setup, s => s.Name == "tea bush" && s.Days == 20);
        Assert.Empty(GrowSources.TeaBush(new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>())));
    }
}
```

Check the arithmetic against the decompile rule before running (age 20 means planted 20 days ago, bloom from day 22 of the month): planted day 1 is age 20 on day 21, the first bloom day at or after that with day-of-month 22 or more is day 22; planted day 3 is age 20 on day 23, which is already a bloom day. If any expectation does not hold, stop and report it; do not edit it.

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors (`PlantTable`, `GreenhouseTable`, `TeaBush`).

- [ ] **Step 3: Rewrite GrowSources**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Crops, greenhouse crops, Mixed Seeds, fruit trees and the tea bush as start-day chains:
/// seed table, then plant, then land (spec 2026-09-14-obtainability-phase2 section 1).</summary>
public static class GrowSources
{
    private const string MixedSeedsId = "(O)770";
    private const string TeaSaplingId = "(O)251";
    private const string TeaLeavesId = "(O)815";
    private const string GreenhouseUnlock = "mail:ccPantry";   // Farm.cs 1132, GreenhouseBuilding.cs 47
    private const int FruitTreeMaturityDays = 28;              // FruitTree.cs 66
    private const int TeaBushAgeDays = 20;                     // Bush.cs 220 (getAge() >= 20)
    private const int TeaBloomFirstDayOfMonth = 22;            // Bush.cs 220 (dayOfMonth >= 22)
    private const int MinGrowthDays = 1;
    private static readonly SetupStep SaplingStep = new("sapling", FruitTreeMaturityDays);
    private static readonly SetupStep TeaBushStep = new("tea bush", TeaBushAgeDays);

    /// <summary>What Mixed Seeds become, by the season of the planting day (Crop.cs 294-320, 414-433).
    /// 473 resolves to 472. Winter picks a random other season's pool; only the greenhouse grows it,
    /// because the greenhouse waives the season check but still reports the outside season (GameLocation.cs 649-651).</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> MixedSeedPools = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)472", "(O)474", "(O)475" },
        [Season.Summer] = new[] { "(O)487", "(O)483", "(O)482", "(O)484" },
        [Season.Fall] = new[] { "(O)487", "(O)488", "(O)489", "(O)490" },
    };

    /// <summary>Plant on day p outdoors: lands p + growth when every day through harvest is in season.</summary>
    public static DayTable PlantTable(IReadOnlyList<Season> seasons, int growthDays)
    {
        WeekMask inSeason = seasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(seasons);
        int growth = Math.Max(MinGrowthDays, growthDays);
        return DayTable.Exact(plant =>
        {
            int harvest = plant + growth;
            if (harvest > Calendar.DaysPerYear) return null;
            for (int day = plant; day <= harvest; day++)
                if (!inSeason.Contains(WeekMask.WeekOfDay(day))) return null;
            return harvest;
        });
    }

    public static DayTable GreenhouseTable(int growthDays)
    {
        int growth = Math.Max(MinGrowthDays, growthDays);
        return DayTable.Exact(plant => plant + growth <= Calendar.DaysPerYear ? plant + growth : null);
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> Crops(IEnumerable<CropRow> rows, ObtainabilityModel snapshot)
    {
        List<CropRow> all = rows.ToList();
        var bySeed = all.GroupBy(r => r.SeedId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (CropRow crop in all.Where(r => r.SeedId != MixedSeedsId))
        {
            DayTable dep = snapshot.Table(crop.SeedId, ObtainFilter.DependableOnly);
            DayTable any = snapshot.Table(crop.SeedId, ObtainFilter.Any);
            DayTable outdoors = PlantTable(crop.Seasons, crop.GrowthDays);
            DayTable indoors = GreenhouseTable(crop.GrowthDays);
            string regrow = crop.RegrowDays > 0 ? $", regrows every {crop.RegrowDays} days" : "";
            var outdoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.Crop, dep.Then(outdoors), any.Then(outdoors), outdoor, $"grown from {crop.SeedId}{regrow}"))
                yield return (crop.HarvestId, s);
            var indoor = ObtainConditions.None with { Requires = new[] { "item:" + crop.SeedId, GreenhouseUnlock } };
            foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop, dep.Then(indoors), any.Then(indoors), indoor, $"greenhouse, from {crop.SeedId}{regrow}"))
                yield return (crop.HarvestId, s);
        }

        DayTable mixed = snapshot.Table(MixedSeedsId, ObtainFilter.Any);
        if (mixed.IsEmpty) yield break;
        foreach ((Season season, string[] seeds) in MixedSeedPools)
            foreach (string seed in seeds)
            {
                if (!bySeed.TryGetValue(seed, out CropRow? crop)) continue;
                // Outdoors the pool is the planting day's season; in the greenhouse that season or Winter.
                DayTable plantOutdoors = DayTable.Available(d => WeekMask.ForSeason(season).Contains(WeekMask.WeekOfDay(d)));
                DayTable plantIndoors = DayTable.Available(d => (WeekMask.ForSeason(season) | WeekMask.ForSeason(Season.Winter)).Contains(WeekMask.WeekOfDay(d)));
                DayTable outdoorLands = mixed.Then(plantOutdoors).Then(PlantTable(crop.Seasons, crop.GrowthDays));
                if (!outdoorLands.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.Crop, outdoorLands, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId } }, $"Mixed Seeds in {season}"));
                DayTable indoorLands = mixed.Then(plantIndoors).Then(GreenhouseTable(crop.GrowthDays));
                if (!indoorLands.IsEmpty)
                    yield return (crop.HarvestId, new ObtainSource(SourceKind.GreenhouseCrop, indoorLands, Reliability.Chance,
                        ObtainConditions.None with { Requires = new[] { "item:" + MixedSeedsId, GreenhouseUnlock } },
                        $"Mixed Seeds in the greenhouse ({season} pool)"));
            }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FruitTrees(
        IEnumerable<FruitTreeRow> rows, ObtainabilityModel snapshot, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var setup = new[] { SaplingStep };
        foreach (FruitTreeRow tree in rows)
        {
            DayTable depMature = snapshot.Table(tree.SaplingId, ObtainFilter.DependableOnly).Delay(FruitTreeMaturityDays);
            DayTable anyMature = snapshot.Table(tree.SaplingId, ObtainFilter.Any).Delay(FruitTreeMaturityDays);
            foreach (FruitRow fruit in tree.Fruit)
            {
                ConditionReading reading = ConditionSeasons.Read(fruit.Condition, festivals);
                WeekMask season = fruit.Season is Season s ? WeekMask.ForSeason(s)
                    : tree.TreeSeasons.Count == 0 ? WeekMask.All : WeekMask.ForSeasons(tree.TreeSeasons);
                DayTable fruitsOutdoors = ConditionSeasons.Availability(reading, season);
                DayTable fruitsIndoors = ConditionSeasons.Availability(reading, WeekMask.All);
                bool luck = fruit.Chance < 1.0 || reading.Chance || fruit.IsRandom;
                ObtainConditions outdoor = ConditionSeasons.Apply(ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId } }, reading);
                foreach (ObtainSource src in SourcePair.Of(SourceKind.FruitTree, luck ? DayTable.None : depMature.Then(fruitsOutdoors),
                    anyMature.Then(fruitsOutdoors), outdoor, $"fruit tree from {tree.SaplingId}", setup))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
                ObtainConditions indoor = ConditionSeasons.Apply(
                    ObtainConditions.None with { Requires = new[] { "item:" + tree.SaplingId, GreenhouseUnlock } }, reading);
                foreach (ObtainSource src in SourcePair.Of(SourceKind.GreenhouseCrop, luck ? DayTable.None : depMature.Then(fruitsIndoors),
                    anyMature.Then(fruitsIndoors), indoor, $"fruit tree in the greenhouse from {tree.SaplingId}", setup))
                    foreach (var emitted in ItemQueries.Emit(fruit.ItemId, objects, src))
                        yield return emitted;
            }
        }
    }

    /// <summary>Tea Leaves from a Tea Sapling (Bush.cs 209-225): the bush is age 20 or more, on days 22 to
    /// 28 of a month, not Winter unless sheltered (greenhouse or indoor pot).</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> TeaBush(ObtainabilityModel snapshot)
    {
        DayTable dep = snapshot.Table(TeaSaplingId, ObtainFilter.DependableOnly);
        DayTable any = snapshot.Table(TeaSaplingId, ObtainFilter.Any);
        if (any.IsEmpty) yield break;
        static bool Bloom(int day) => Calendar.DayOfMonthOf(day) >= TeaBloomFirstDayOfMonth;
        DayTable outdoors = DayTable.Available(d => Bloom(d) && !WeekMask.ForSeason(Season.Winter).Contains(WeekMask.WeekOfDay(d)));
        DayTable sheltered = DayTable.Available(Bloom);
        var setup = new[] { TeaBushStep };
        var outdoor = ObtainConditions.None with { Requires = new[] { "item:" + TeaSaplingId } };
        foreach (ObtainSource s in SourcePair.Of(SourceKind.Crop, dep.Delay(TeaBushAgeDays).Then(outdoors), any.Delay(TeaBushAgeDays).Then(outdoors), outdoor, "tea bush, days 22 to 28", setup))
            yield return (TeaLeavesId, s);
        var indoor = ObtainConditions.None with { Requires = new[] { "item:" + TeaSaplingId, GreenhouseUnlock } };
        foreach (ObtainSource s in SourcePair.Of(SourceKind.GreenhouseCrop, dep.Delay(TeaBushAgeDays).Then(sheltered), any.Delay(TeaBushAgeDays).Then(sheltered), indoor, "sheltered tea bush, days 22 to 28", setup))
            yield return (TeaLeavesId, s);
    }
}
```

`Calendar.DayOfMonthOf(int dayOfYear)` does not exist yet: add to `src/TheLongestYear.Core/Calendar.cs`:
```csharp
    /// <summary>1-based day of the month for a 1-based day of the year.</summary>
    public static int DayOfMonthOf(int dayOfYear) => (dayOfYear - 1) % DaysPerMonth + 1;
```
(`Calendar.DayOfYear(int monthIndex, int dayOfMonth)` already exists.) Wire `GrowSources.TeaBush(current)` into the builder's pass loop next to `Crops`. Delete the Task 2 stopgap helper. Un-skip and rewrite the Builder tests' crop and pond chains to landing days: in `ObtainabilityBuilderTests.A_chain_resolves_shop_seed_to_crop_to_pickles`, the Parsnip crop table lands day 5 from day 1 and never from day 25; Pickles (the FLAVORED_ITEM Pickle machine, 4000 minutes = 3 days) land day 8 from day 1 (`Lands("(O)342", 1, DependableOnly with Kinds Machine) == 8`) and, through the greenhouse parsnip bought Spring 28, day 35 from day 28. The pond chain is finished in Task 5; leave it skipped until then.

- [ ] **Step 4: Run the full suite**

Expected: green; skips remaining only in Made and the pond builder test.

- [ ] **Step 5: Commit and push**

```bash
git add -A src/TheLongestYear.Core tests/TheLongestYear.Tests
git commit -m "obtainability: crops, trees, mixed seeds and the tea bush as start-day chains"
git push origin story
```

---

### Task 5: Made sources with setup steps, Seed Maker and Mushroom Log

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/MadeSources.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (`MachineOutput.Method`, `OutputMethodKind`, `PondRow.SpawnTime`, `AnimalRow.DaysToProduce`, `ObtainabilityInputs.Buildings`)
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` (pass `inputs.Crops` and `inputs.Buildings` to Machines/Animals/Ponds)
- Test: `tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs`, `ObtainabilityBuilderTests.cs`

**Interfaces:**
- `public enum OutputMethodKind { None, SeedMaker, MushroomLog, Cask, Unknown }`
- `MachineOutput(string? ItemId, string? Condition, string? OutputMethod, bool IsRandom = false, OutputMethodKind Method = OutputMethodKind.None)`
- `PondRow(..., IReadOnlyList<PondProduct> Products, int SpawnTime = 1)` (FishPondData.SpawnTime, days between new fish)
- `AnimalRow(..., int DeluxeMinimumFriendship = 200, int DaysToProduce = 1)`
- `ObtainabilityInputs.Buildings : IReadOnlyDictionary<string, int>` (building name to BuildDays)
- `MadeSources.Machines(rows, objects, snapshot, festivals, IEnumerable<CropRow> crops)`; `Animals(rows, festivals, buildings)`; `Ponds(rows, objects, snapshot, festivals, buildings)`
- `public static IReadOnlyList<SetupStep> MadeSources.AnimalSetup(AnimalRow animal, IReadOnlyDictionary<string,int> buildings, int friendship)`:
  `building:<House> <BuildDays or 0>`, `animal:<Id> 1` (bought today, produces from tomorrow), and when friendship > 0 `friendship:<Id> <points> <ceil(points / 15)>` days (FarmAnimal.cs 733: petting gives 15 a day).

Decompile facts to cite: `FarmAnimal.dayUpdate` 1005 (`daysSinceLastLay >= DaysToProduce`), 733 (petting adds 15 friendship), `FarmAnimalData.DaysToProduce` (default 1); `FishPond` spawns one fish every `SpawnTime` days up to capacity (FishPondData.SpawnTime); `Object.OutputSeedMaker` 2241-2272 (first crop whose harvest matches, 0.5% Ancient Seeds `(O)499`, 2% Mixed Seeds); `Object.OutputMushroomLog` 2274-2335 (Common 404, Red 420, Purple 422 by chance; Morel 257 from oak type 1, Chanterelle 281 from maple type 3); `Cask.OutputCask` 78-140 (quality only, dropped).

- [ ] **Step 1: Write the failing tests** (add to `ObtainabilityMadeTests.cs`; rewrite the existing machine/recipe/pond/animal tests there to landing days with the same helpers)

```csharp
    [Fact]
    public void A_machine_lands_its_input_plus_processing_and_never_makes_its_input_earlier()
    {
        var snapshot = Snapshot(("(O)613", SourceKind.FruitTree, DayTable.Available(d => d >= 57), Reliability.Dependable));
        var rows = new[]
        {
            new MachineRow("(BC)12", null, new[] { "category_fruits" }, null, new[] { new MachineOutput("(O)348", null, null) }, 10000, -1),
            new MachineRow("(BC)Dehydrator", null, new[] { "category_fruits" }, null, new[] { new MachineOutput("DROP_IN", null, null) }, 0, 1),
        };
        var all = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, new CropRow[0]).ToList();
        var wine = all.Single(s => s.ItemId == "(O)348").Source;
        Assert.Equal(64, wine.Lands.Lands(1));                  // apple day 57 + 7 days
        Assert.Null(wine.Lands.Lands(106));
        var apple = all.Single(s => s.ItemId == "(O)613").Source;   // DROP_IN keyed under the input
        Assert.Equal(58, apple.Lands.Lands(1));                 // one day later than the apple itself, never earlier
    }

    [Fact]
    public void A_seed_maker_returns_a_crops_seed_and_a_mushroom_log_gives_mushrooms()
    {
        var snapshot = Snapshot(("(O)24", SourceKind.Crop, DayTable.Available(d => d >= 5), Reliability.Dependable));
        var crops = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var rows = new[]
        {
            new MachineRow("(BC)25", null, new[] { "!seedmaker_banned" }, null, new[] { new MachineOutput(null, null, "OutputSeedMaker", false, OutputMethodKind.SeedMaker) }, 20, -1),
            new MachineRow("(BC)MushroomLog", null, new string[0], null, new[] { new MachineOutput(null, null, "OutputMushroomLog", false, OutputMethodKind.MushroomLog) }, 0, 6),
            new MachineRow("(BC)163", "(O)348", new string[0], null, new[] { new MachineOutput(null, null, "OutputCask", false, OutputMethodKind.Cask) }, 0, 14),
        };
        var all = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, crops).ToList();
        var seeds = all.Single(s => s.ItemId == "(O)472" && s.Source.Kind == SourceKind.Machine).Source;
        Assert.Equal(6, seeds.Lands.Lands(1));                  // parsnip day 5, 20 minutes rounds up to 1 day
        Assert.Equal(Reliability.Dependable, seeds.Reliability);
        Assert.Contains(all, s => s.ItemId == "(O)770" && s.Source.Reliability == Reliability.Chance);
        Assert.Contains(all, s => s.ItemId == "(O)499" && s.Source.Reliability == Reliability.Chance);
        foreach (string mushroom in new[] { "(O)404", "(O)420", "(O)422", "(O)257", "(O)281" })
        {
            var m = all.Single(s => s.ItemId == mushroom && s.Source.Detail.Contains("MushroomLog")).Source;
            Assert.Equal(Reliability.Chance, m.Reliability);
            Assert.Equal(7, m.Lands.Lands(1));
        }
        Assert.DoesNotContain(all, s => s.Source.Detail.Contains("(BC)163"));   // casks change quality only
        Assert.DoesNotContain(all, s => s.ItemId.StartsWith(ItemQueries.UnresolvedPrefix) && s.Source.Detail.Contains("Cask"));
    }

    [Fact]
    public void An_animal_records_its_setup_days_and_lands_after_days_to_produce()
    {
        var buildings = new Dictionary<string, int> { ["Coop"] = 3 };
        var rows = new[]
        {
            new AnimalRow("Chicken", "Coop", 800,
                new[] { new AnimalProduce("(O)176", null, 0) }, new[] { new AnimalProduce("(O)174", null, 0) }, 200, DaysToProduce: 1),
        };
        var all = MadeSources.Animals(rows, NoFestivals, buildings).ToList();
        var egg = all.Single(s => s.ItemId == "(O)176").Source;
        Assert.Equal(2, egg.Lands.Lands(1));
        Assert.Contains(egg.Setup, s => s.Name == "building:Coop" && s.Days == 3);
        Assert.Contains(egg.Setup, s => s.Name == "animal:Chicken" && s.Days == 1);
        var large = all.Single(s => s.ItemId == "(O)174").Source;
        Assert.Contains(large.Setup, s => s.Name == "friendship:Chicken 200" && s.Days == 14);
        Assert.Equal(2, large.Lands.Lands(1));                  // the blind table assumes setup done (spec decision 3)
    }

    [Fact]
    public void A_pond_counts_population_growth_from_its_spawn_time()
    {
        var snapshot = Snapshot(("(O)142", SourceKind.Fish, DayTable.Always, Reliability.Dependable));
        var rows = new[] { new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 3, 1.0, null) }, SpawnTime: 4) };
        var buildings = new Dictionary<string, int> { ["Fish Pond"] = 2 };
        var roe = MadeSources.Ponds(rows, Objects, snapshot, NoFestivals, buildings).Single(s => s.ItemId == "(O)812").Source;
        Assert.Equal(9, roe.Lands.Lands(1));                    // fish day 1, two more fish at 4 days each = day 9
        Assert.Contains(roe.Setup, s => s.Name == "building:Fish Pond" && s.Days == 2);
    }
```

`Objects` in that test class must contain `(O)613` with tag `category_fruits`, `(O)24` with `category_vegetable`, `(O)142` with `fish_carp`; add them to the existing dictionary if missing.

- [ ] **Step 2: Run to verify they fail**

Expected: compile errors on the new parameters and `OutputMethodKind`.

- [ ] **Step 3: Implement**

`ObtainabilityInputs.cs`: add the enum and the new record parameters exactly as in Interfaces (defaults keep every existing constructor call compiling). Add `public IReadOnlyDictionary<string, int> Buildings { get; init; } = new Dictionary<string, int>();`.

`MadeSources.cs`:
- Delete `ShiftByDays`; keep `ProcessingDays`.
- `Machines(...)`: for each rule, `DayTable gate = ConditionSeasons.Availability(trigger, WeekMask.All)`; inputs' tables `dep = Earliest over inputs of snapshot.Table(input, DependableOnly)`, likewise `any`; `noInput` rules use `DayTable.Always`. For an output:
  - `Method == Cask`: `continue` (no source, no diagnostic).
  - `Method == SeedMaker`: for every input id, find the first `CropRow` whose `HarvestId == input`; emit `(crop.SeedId, Machine, dependable = inputDep.Then(gate).Delay(days), any = inputAny.Then(gate).Delay(days))` via `SourcePair`, detail `"(BC)25 seed maker from {input}"`; plus chance sources for `(O)770` and `(O)499` on `inputAny.Then(gate).Delay(days)` (detail "seed maker 2% mixed seeds" / "0.5% ancient seeds").
  - `Method == MushroomLog`: chance sources for `(O)404`, `(O)420`, `(O)422`, `(O)257`, `(O)281` on `DayTable.Always.Then(gate).Delay(days)`, requires `machine:(BC)MushroomLog`, `trees:mature trees within 3 tiles`.
  - `Method == Unknown` (or `None` with a non-empty `OutputMethod`): the unresolved diagnostic as before.
  - `DROP_IN`: per input, `SourcePair.Of(Machine, luck ? None : inputDepSingle.Then(gate).Delay(days), inputAnySingle.Then(gate).Delay(days), ...)` keyed under the input. With `Earliest` in the model this can never make the input earlier than itself.
  - Otherwise: `SourcePair.Of(Machine, luck ? None : dep.Then(gate).Delay(days), any.Then(gate).Delay(days), ...)` through `ItemQueries.Emit`.
  - Keep `UseFirstValidOutput` shadowing by intersecting the later outputs' gates with the complement, expressed as `gate.Except(shadowedGate)` is NOT right for tables; instead keep a `WeekMask shadowed` exactly as phase 1 and build each output's gate from `ConditionSeasons.Availability(outCond, trigger.Weeks.Except(shadowed))`.
- `Recipes(...)`: `dep = DayTable.Always; any = DayTable.Always;` then per ingredient `dep = dep.Latest(d); any = any.Latest(a);`; taught-by-shop: `dep = dep.Latest(DayTable.InWeeks(taught))` and same for any. Several outputs: `dep = DayTable.None`.
- `Animals(rows, festivals, buildings)`: table = `ConditionSeasons.Availability(reading, WeekMask.All).Delay(animal.DaysToProduce)`; `Setup = AnimalSetup(animal, buildings, friendship)`; reliability as before.
- `Ponds(rows, objects, snapshot, festivals, buildings)`: fish table per pond as before (Earliest over its fishes); for each product `growth = (product.RequiredPopulation - 1) * pond.SpawnTime` clamped at 0; `dep = fishDep.Delay(growth).Then(ConditionSeasons.Availability(reading, WeekMask.All))`; same for any; `Setup = { building:Fish Pond <buildings["Fish Pond"] or 0> }`.
- `Tappers`: `ConditionSeasons.Availability(reading, seasonMask).Delay(tap.DaysUntilReady)`; `Setup = { new SetupStep("tapper on a mature " + tap.TreeId, tap.DaysUntilReady) }` is NOT added (the delay is already in the table); keep only the requires note.
- `Geodes`: `snapshot.Table(geode, Any).Then(ConditionSeasons.Availability(reading, WeekMask.All))`.

`ObtainabilityBuilder.cs`: pass the new arguments; `Animals` moves from `direct` to `direct` still (it needs no snapshot) with `inputs.Buildings`.

Un-skip and rewrite the remaining Made and Builder tests: the pond chain in `ObtainabilityBuilderTests.A_pond_chain_settles` becomes `Assert.Equal(57, build.Model.Lands("(O)812", 1, ObtainFilter.Any with { Kinds = new[] { SourceKind.FishPond } }))` (Carp in Fall, day 57, population 1, spawn time default 1) and Aged Roe `(O)447` lands day 60 through the 3-day machine; `A_true_dependency_cycle_settles_without_looping` becomes `Assert.Equal(29, ...Lands("(O)1", 1, Any))` and the same for `(O)2` (zero-day machines).

- [ ] **Step 4: Run the full suite**

Expected: green, zero skips. State the count.

- [ ] **Step 5: Commit and push**

```bash
git add -A src/TheLongestYear.Core tests/TheLongestYear.Tests
git commit -m "obtainability: machines, recipes, animals and ponds by start day with setup steps; seed maker and mushroom log read; casks dropped"
git push origin story
```

---

### Task 6: CodeSources: mine fish, Moss, season seeds, guild rewards

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/CodeSources.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs` (`SlayerQuestRow`, `ObtainabilityInputs.SlayerQuests`)
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` (wire in)
- Modify: `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs` (add the file)
- Test: `tests/TheLongestYear.Tests/ObtainabilityCodeSourcesTests.cs`

**Interfaces:**
- `public sealed record SlayerQuestRow(string Id, IReadOnlyList<string> Targets, int Count, string RewardItemId);`
- `ObtainabilityInputs.SlayerQuests : IReadOnlyList<SlayerQuestRow>`
- ```csharp
  public static class CodeSources
  {
      public static IEnumerable<(string ItemId, ObtainSource Source)> MineFish();      // 158 floors 1-39, 161 floors 40-79, 162 floors 80-119
      public static IEnumerable<(string ItemId, ObtainSource Source)> Moss();          // (O)Moss, Spring to Fall outdoors; greenhouse all year
      public static IEnumerable<(string ItemId, ObtainSource Source)> SeasonSeeds();   // fishing treasure raccoon seed by catch day
      public static IEnumerable<(string ItemId, ObtainSource Source)> GuildRewards(IEnumerable<SlayerQuestRow> quests);
  }
  ```

Decompile facts: `MineShaft.getFish` 1132-1195 (area 0/10: `(O)158`; area 40: `(O)161`; area 80: `(O)162`; any season, any weather, a chance per cast that rises with fishing level, so Dependable with a note). `Tree.cs` 842-846 (moss grows on trees whose data says GrowsMoss, 50% per day update), 942-971 (not in Winter), 1274 (`(O)Moss` 1 to 2 when scraped). `Utility.getRaccoonSeedForCurrentTimeOfYear` 250-272: the season's seed, switching to the next season's after day 23 in Spring and day 20 in the others; Spring Carrot `(O)CarrotSeeds`, Summer `(O)SummerSquashSeeds`, Fall `(O)BroccoliSeeds`, Winter `(O)PowdermelonSeeds`; reached from `FishingRod.openTreasureMenuEndFunction` 2477 (golden chest) and the ordinary treasure roll. Guild rewards: `Data/MonsterSlayerQuests` (`MonsterSlayerQuestData.Targets`, `Count`, `RewardItemId`), handed over at the Adventure Guild.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityCodeSourcesTests
{
    [Fact]
    public void Mine_fish_are_dependable_any_day_with_their_floor_as_a_condition()
    {
        var fish = CodeSources.MineFish().ToDictionary(s => s.ItemId, s => s.Source);
        Assert.Equal(new[] { "(O)158", "(O)161", "(O)162" }, fish.Keys.OrderBy(k => k).ToArray());
        Assert.All(fish.Values, s => { Assert.Equal(SourceKind.Fish, s.Kind); Assert.Equal(Reliability.Dependable, s.Reliability); Assert.Equal(1, s.Lands.Lands(1)); });
        Assert.Contains("mines:floor 1", fish["(O)158"].Conditions.Requires);
        Assert.Contains("mines:floor 40", fish["(O)161"].Conditions.Requires);
        Assert.Contains("mines:floor 80", fish["(O)162"].Conditions.Requires);
    }

    [Fact]
    public void Moss_is_forage_outside_winter_and_all_year_in_the_greenhouse()
    {
        var moss = CodeSources.Moss().ToList();
        var outdoor = moss.Single(s => s.Source.Kind == SourceKind.Forage).Source;
        Assert.Equal("(O)Moss", moss[0].ItemId);
        Assert.Equal(1, outdoor.Lands.Lands(1));
        Assert.Null(outdoor.Lands.Lands(85));
        var indoor = moss.Single(s => s.Source.Kind == SourceKind.GreenhouseCrop).Source;
        Assert.Equal(85, indoor.Lands.Lands(85));
        Assert.Contains("mail:ccPantry", indoor.Conditions.Requires);
    }

    [Fact]
    public void Season_seeds_switch_to_the_next_season_late_in_the_month()
    {
        var seeds = CodeSources.SeasonSeeds().ToDictionary(s => s.ItemId, s => s.Source);
        Assert.Equal(1, seeds["(O)CarrotSeeds"].Lands.Lands(1));
        Assert.Equal(105, seeds["(O)CarrotSeeds"].Lands.Lands(24));    // Spring 24 onward gives Summer Squash; Carrot comes back from Winter 21
        Assert.Equal(24, seeds["(O)SummerSquashSeeds"].Lands.Lands(1));
        Assert.Equal(49, seeds["(O)BroccoliSeeds"].Lands.Lands(1));    // Summer 21
        Assert.Equal(77, seeds["(O)PowdermelonSeeds"].Lands.Lands(1)); // Fall 21
        Assert.Null(seeds["(O)PowdermelonSeeds"].Lands.Lands(105));    // Winter 21 onward gives Carrot
        Assert.Equal(105, seeds["(O)CarrotSeeds"].Lands.Lands(100));
        Assert.All(seeds.Values, s => { Assert.Equal(SourceKind.FishingTreasure, s.Kind); Assert.Equal(Reliability.Chance, s.Reliability); });
    }

    [Fact]
    public void Guild_rewards_come_from_the_slayer_rows()
    {
        var rows = new[] { new SlayerQuestRow("Duggies", new[] { "Duggy" }, 30, "(H)27") };
        var hat = CodeSources.GuildRewards(rows).Single().Source;
        Assert.Equal(SourceKind.Other, hat.Kind);
        Assert.Equal(Reliability.Dependable, hat.Reliability);
        Assert.Equal(1, hat.Lands.Lands(1));
        Assert.Contains("guild:Duggies 30 kills (Duggy)", hat.Conditions.Requires);
        Assert.Contains("mines:floor 1", hat.Conditions.Requires);   // MineSources.MonsterFloor for the first target
    }
}
```

Note for the guild test: the reward's kind. `SourceKind` has no "Quest" value; add `Guild` to the enum in `ObtainTypes.cs` and use it here instead of `Other` (update the assertion to `SourceKind.Guild`).

- [ ] **Step 2: Run to verify they fail**

Expected: `CodeSources`, `SlayerQuestRow`, `SourceKind.Guild` not defined.

- [ ] **Step 3: Implement CodeSources**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Facts that live in game CODE, typed from the PC 1.6 decompile (never from any table of this
/// mod): mine fish, Moss, the season seed fishing treasure rolls, and the guild rewards' shape.</summary>
public static class CodeSources
{
    private const string GreenhouseUnlock = "mail:ccPantry";
    private const int SpringSwitchDay = 23;   // Utility.cs 254: Spring switches after day 23
    private const int OtherSwitchDay = 20;    // Utility.cs 254: other seasons after day 20

    /// <summary>MineShaft.getFish 1148-1166: area 0 and 10 give Stonefish, 40 Ice Pip, 80 Lava Eel, on a
    /// per-cast chance that grows with fishing level and depth. Any season, any weather.</summary>
    private static readonly (string ItemId, string Floor, string Note)[] MineFishTable =
    {
        ("(O)158", "mines:floor 1", "Stonefish, floors 1 to 39, 2% + 1% per level point (MineShaft.cs 1153-1157)"),
        ("(O)161", "mines:floor 40", "Ice Pip, floors 40 to 79, 1.5% + 0.9% per level point (1160-1164)"),
        ("(O)162", "mines:floor 80", "Lava Eel, floors 80 to 119, 1% + 0.8% per level point (1167-1171)"),
    };

    /// <summary>Utility.getRaccoonSeedForCurrentTimeOfYear 250-272, by season of the catch.</summary>
    private static readonly IReadOnlyDictionary<Season, string> SeasonSeedIds = new Dictionary<Season, string>
    {
        [Season.Spring] = "(O)CarrotSeeds", [Season.Summer] = "(O)SummerSquashSeeds",
        [Season.Fall] = "(O)BroccoliSeeds", [Season.Winter] = "(O)PowdermelonSeeds",
    };

    public static IEnumerable<(string ItemId, ObtainSource Source)> MineFish()
    {
        foreach ((string id, string floor, string note) in MineFishTable)
            yield return (id, new ObtainSource(SourceKind.Fish, DayTable.Always, Reliability.Dependable,
                ObtainConditions.None with { Skill = "Fishing", SkillLevel = 0, Requires = new[] { floor } }, note));
    }

    /// <summary>Tree.cs 842-846 (moss grows on GrowsMoss trees), 942-971 (not in Winter), 1274 (scraped
    /// for 1 to 2 Moss).</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> Moss()
    {
        WeekMask notWinter = WeekMask.All.Except(WeekMask.ForSeason(Season.Winter));
        yield return ("(O)Moss", new ObtainSource(SourceKind.Forage, DayTable.InWeeks(notWinter), Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { "trees:mature trees with moss" } }, "moss scraped from trees (Tree.cs 1274)"));
        yield return ("(O)Moss", new ObtainSource(SourceKind.GreenhouseCrop, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { "trees:mature trees with moss", GreenhouseUnlock } }, "moss on greenhouse trees, all year"));
    }

    /// <summary>The seed a fishing treasure chest gives for the day it is opened (Utility.cs 250-272,
    /// reached from FishingRod.cs 2477 and the ordinary treasure roll).</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> SeasonSeeds()
    {
        foreach (Season season in Enum.GetValues<Season>())
        {
            string id = SeasonSeedIds[season];
            DayTable days = DayTable.Available(day => SeedSeasonOn(day) == season);
            yield return (id, new ObtainSource(SourceKind.FishingTreasure, days, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { "fishing:treasure chest" }, FewDays = true },
                $"season seed from a treasure chest ({season} window)"));
        }
    }

    /// <summary>Which season's seed a chest gives on a day of the year.</summary>
    public static Season SeedSeasonOn(int dayOfYear)
    {
        var season = (Season)((dayOfYear - 1) / Calendar.DaysPerMonth);
        int dayOfMonth = Calendar.DayOfMonthOf(dayOfYear);
        int switchDay = season == Season.Spring ? SpringSwitchDay : OtherSwitchDay;
        return dayOfMonth > switchDay ? (Season)(((int)season + 1) % Calendar.MonthsPerYear) : season;
    }

    /// <summary>Data/MonsterSlayerQuests rewards, handed over at the Adventure Guild once the count is
    /// reached. Dependable: killing is on purpose. The time the count takes is a condition for the
    /// consumer, not a number this model invents.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> GuildRewards(IEnumerable<SlayerQuestRow> quests)
    {
        foreach (SlayerQuestRow quest in quests)
        {
            if (string.IsNullOrWhiteSpace(quest.RewardItemId)) continue;
            var requires = new List<string> { $"guild:{quest.Id} {quest.Count} kills ({string.Join(", ", quest.Targets)})" };
            string? floor = quest.Targets.Select(MineSources.MonsterFloor).FirstOrDefault(f => f != null);
            if (floor != null) requires.Add(floor == "Skull Cavern" ? "location:SkullCave" : "mines:" + floor);
            yield return (BundleParsing.NormalizeItemId(quest.RewardItemId), new ObtainSource(SourceKind.Guild, DayTable.Always,
                Reliability.Dependable, ObtainConditions.None with { Requires = requires }, $"Adventure Guild reward for {quest.Id}"));
        }
    }
}
```

`Season` in Core: check `src/TheLongestYear.Core/Season.cs` (or wherever the enum lives) that Spring = 0 .. Winter = 3; `Enum.GetValues<Season>()` must enumerate exactly those four.

Wire into the builder's `direct` list: `CodeSources.MineFish()`, `Moss()`, `SeasonSeeds()`, `GuildRewards(inputs.SlayerQuests)`. Add `"TheLongestYear.Core/Obtainability/CodeSources.cs"` to the guard's `BlindFiles`. Add `Guild` to `SourceKind`.

- [ ] **Step 4: Run the full suite**

Expected: green.

- [ ] **Step 5: Commit and push**

```bash
git add -A src/TheLongestYear.Core/Obtainability tests/TheLongestYear.Tests
git commit -m "obtainability: code sources from the decompile (mine fish, moss, season seeds, guild rewards)"
git push origin story
```

---

### Task 7: Glue: new assets, LOCATION_FISH expansion, fishingGame, output methods, refuse to publish

**Files:**
- Modify: `src/TheLongestYear/Loop/GameObtainabilityData.cs`
- Create: `src/TheLongestYear.Core/Obtainability/ObtainabilityReadException.cs` (tiny; add to the guard list)
- Modify: `src/TheLongestYear/ModEntry.cs` (`BuildObtainabilityModel`, `CmdObtain` guard message)
- Test: `tests/TheLongestYear.Tests/ObtainabilityBuilderTests.cs` (LOCATION_FISH expansion is a Core helper; test it there), `ObtainabilityBlindGuardTests.cs`

**Interfaces:**
- `public sealed class ObtainabilityReadException : Exception { public IReadOnlyList<string> FailedSections { get; } }`
- `public static IReadOnlyList<LocationSpawn> SpawnSources.ExpandLocationFish(IReadOnlyList<LocationSpawn> fish)`: every row whose `ItemId` starts with `LOCATION_FISH <name>` is replaced by copies of `<name>`'s rows (recursively, with a visited set), re-homed to the delegating location, and dropped when the named location has none.
- Glue reads: `Data/Buildings` into `Buildings[Name] = BuildDays` (key by the data's `Name` field AND the dictionary key, so "Coop" and "Fish Pond" both resolve); `Data/MonsterSlayerQuests` into `SlayerQuests`; `FishPondData.SpawnTime` into `PondRow.SpawnTime`; `FarmAnimalData.DaysToProduce` into `AnimalRow.DaysToProduce`; `MachineItemOutput.OutputMethod` mapped to `OutputMethodKind` by whether the method name ends with `OutputSeedMaker`, `OutputMushroomLog`, `OutputCask` (else `Unknown`).
- Glue drops any location whose key is `fishingGame` (constant `MinigameLocations = { "fishingGame" }`).
- `Section(...)` records failures in a list; `Build()` throws `ObtainabilityReadException` after all sections ran if the list is non-empty (so every failure is logged in one go).

- [ ] **Step 1: Write the failing tests**

Add to `ObtainabilityBuilderTests.cs`:

```csharp
    [Fact]
    public void Location_fish_delegation_copies_the_named_locations_rows()
    {
        var rows = new List<LocationSpawn>
        {
            new("Forest", "(O)142", Season.Fall, null, 1.0, 0, false, 0),
            new("Forest", "LOCATION_FISH Town BOBBER_X BOBBER_Y WATER_DEPTH", null, null, 1.0, 0, false, 0),
            new("Town", "(O)138", Season.Summer, null, 1.0, 0, false, 0),
            new("Farm_Riverland", "LOCATION_FISH Forest BOBBER_X BOBBER_Y WATER_DEPTH", null, null, 1.0, 0, false, 0),
            new("Beach", "LOCATION_FISH Nowhere BOBBER_X BOBBER_Y WATER_DEPTH", null, null, 1.0, 0, false, 0),
        };
        var expanded = SpawnSources.ExpandLocationFish(rows);
        Assert.DoesNotContain(expanded, r => r.ItemId.StartsWith("LOCATION_FISH"));
        Assert.Contains(expanded, r => r.Location == "Farm_Riverland" && r.ItemId == "(O)142" && r.Season == Season.Fall);
        Assert.Contains(expanded, r => r.Location == "Farm_Riverland" && r.ItemId == "(O)138");   // through Forest's own delegation to Town
        Assert.Contains(expanded, r => r.Location == "Forest" && r.ItemId == "(O)138");
        Assert.DoesNotContain(expanded, r => r.Location == "Beach");
    }

    [Fact]
    public void A_read_failure_names_its_sections()
    {
        var ex = new ObtainabilityReadException(new[] { "Data/Crops", "Data/Shops" });
        Assert.Equal(2, ex.FailedSections.Count);
        Assert.Contains("Data/Crops", ex.Message);
    }
```

- [ ] **Step 2: Run to verify they fail**

Expected: `ExpandLocationFish`, `ObtainabilityReadException` not defined.

- [ ] **Step 3: Implement**

`ObtainabilityReadException.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>A data section failed to read, so no model is published: a model with a missing section
/// looks complete and would mislead any consumer (spec 2026-09-14-obtainability-phase2 section 2).</summary>
public sealed class ObtainabilityReadException : Exception
{
    public IReadOnlyList<string> FailedSections { get; }

    public ObtainabilityReadException(IReadOnlyList<string> failedSections)
        : base("Obtainability: no model published, these sections failed to read: " + string.Join(", ", failedSections))
        => FailedSections = failedSections;
}
```

`SpawnSources.ExpandLocationFish`:
```csharp
    private const string LocationFishQuery = "LOCATION_FISH";

    public static IReadOnlyList<LocationSpawn> ExpandLocationFish(IReadOnlyList<LocationSpawn> fish)
    {
        var byLocation = fish.GroupBy(r => r.Location).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        IEnumerable<LocationSpawn> RowsOf(string location, HashSet<string> visited)
        {
            if (!visited.Add(location) || !byLocation.TryGetValue(location, out List<LocationSpawn>? rows)) yield break;
            foreach (LocationSpawn row in rows)
            {
                string[] parts = row.ItemId.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] == LocationFishQuery)
                    foreach (LocationSpawn inherited in RowsOf(parts[1], visited))
                        yield return inherited with { Location = location };
                else
                    yield return row;
            }
        }
        var result = new List<LocationSpawn>();
        foreach (string location in byLocation.Keys)
            result.AddRange(RowsOf(location, new HashSet<string>(StringComparer.Ordinal)));
        return result;
    }
```
(`LocationSpawn` is a record, so `with` works.) The glue calls it on the `fish` list before building `ObtainabilityInputs`, and the Core builder does NOT call it (the glue owns asset shape).

Glue changes in `GameObtainabilityData.cs`:
- `private static readonly HashSet<string> MinigameLocations = new(StringComparer.Ordinal) { "fishingGame" };` and `if (MinigameLocations.Contains(kv.Key)) continue;` at the top of the Locations loop.
- New sections `Buildings` (`Game1.content.Load<Dictionary<string, BuildingData>>("Data/Buildings")`, `using StardewValley.GameData.Buildings;`, store under both `kv.Key` and `kv.Value.Name` when non-empty) and `MonsterSlayerQuests` (`Load<Dictionary<string, MonsterSlayerQuestData>>("Data/MonsterSlayerQuests")`, `using StardewValley.GameData;`).
- Ponds: pass `pond.SpawnTime`; animals: `a.DaysToProduce`.
- Machines: `OutputMethodKind Method(string? method)` helper: `null/empty` = `None`; ends with `OutputSeedMaker` = `SeedMaker`; `OutputMushroomLog` = `MushroomLog`; `OutputCask` = `Cask`; else `Unknown`. Pass it in the `MachineOutput` constructor.
- `Section`: append the asset name to `_failed`; after the last section, `if (_failed.Count > 0) throw new ObtainabilityReadException(_failed);` (log each failure as today, then throw).

`ModEntry.BuildObtainabilityModel`: catch `ObtainabilityReadException rex` first: `_obtainability = null; _obtainabilityFailure = rex.Message; Log(rex.Message, Warn)`. Keep the general catch. Add `private string _obtainabilityFailure;` cleared on success. `CmdObtain`: when `_obtainability == null` and `_obtainabilityFailure != null`, log `$"No obtainability model: {_obtainabilityFailure}"` instead of the "load a save" line.

Add `"TheLongestYear.Core/Obtainability/ObtainabilityReadException.cs"` to the guard's `BlindFiles`.

- [ ] **Step 4: Build the mod project and run the full suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` then the test command. Expected: both clean.

- [ ] **Step 5: Commit and push**

```bash
git add -A src tests/TheLongestYear.Tests
git commit -m "obtainability glue: buildings and slayer quests read, LOCATION_FISH expanded, fishingGame dropped, no model when a section fails"
git push origin story
```

---

### Task 8: Comparison report layout and tly_obtain <item> [day]

**Files:**
- Modify: `src/TheLongestYear.Core/ObtainabilityComparison.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdObtain` usage and day argument)
- Test: `tests/TheLongestYear.Tests/ObtainabilityComparisonTests.cs` (rewrite)

**Interfaces:**
- `CompareRow(string ItemId, int? ExistingPacing, int? ExistingHard, string ExistingBasis, int? NewDependable, int? NewAny, CompareVerdict Verdict)` unchanged in shape; `NewDependable` and `NewAny` are now LANDING WEEKS FROM DAY 1; the verdict compares `NewDependable` with `ExistingHard`. An item with dependable `null` but any non-null and an existing week is `OnlyExisting`? No: it stays a disagreement the reader must see. Add verdict `LuckOnly` (existing has a week, new has no dependable route but has a chance route) between `NewLater` and `OnlyExisting` in `SectionOrder`.
- `Render(...)` prints: intro, counts table (all verdicts plus unresolved), detail sections for `NewEarlier`, `NewLater`, `LuckOnly`, `OnlyExisting`; then `## Agree` table; then `## OnlyNew (names only)` one `- (O)id Name` per line; then the unresolved list.

- [ ] **Step 1: Rewrite the comparison tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityComparisonTests
{
    private static ObtainSource Source(DayTable lands, Reliability r = Reliability.Dependable, string detail = "test") =>
        new(SourceKind.Forage, lands, r, ObtainConditions.None with { Requires = new[] { "location:Town" } }, detail);

    private static DayTable FromWeek(int week) => DayTable.Available(d => WeekMask.WeekOfDay(d) >= week);

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)1"] = new[] { Source(FromWeek(2)) },
        ["(O)2"] = new[] { Source(FromWeek(9), detail: "Forage at Town"), Source(FromWeek(10), detail: "second"), Source(FromWeek(11), detail: "third"), Source(FromWeek(12), detail: "fourth") },
        ["(O)5"] = new[] { Source(FromWeek(4)) },
        ["(O)6"] = new[] { Source(FromWeek(3)) },
        ["(O)7"] = new[] { Source(DayTable.Always, Reliability.Chance, "shop Traveler") },
    });

    private static readonly Dictionary<string, (int Pacing, int Hard, string Basis)> Existing = new()
    {
        ["(O)1"] = (3, 2, "rule a"), ["(O)2"] = (5, 5, "rule b"), ["(O)3"] = (1, 1, "rule c"), ["(O)6"] = (8, 8, "rule d"), ["(O)7"] = (4, 4, "rule e"),
    };

    private static IReadOnlyList<CompareRow> Rows() => ObtainabilityComparison.Compare(
        new[] { "(O)1", "2", "(O)3", "(O)4", "(O)6", "(O)7" }, id => Existing.ContainsKey(id), id => Existing[id], Model);

    [Fact]
    public void Verdicts_compare_the_existing_hard_week_with_the_new_dependable_landing_week_from_day_1()
    {
        var byId = Rows().ToDictionary(r => r.ItemId, r => r.Verdict);
        Assert.Equal(CompareVerdict.Agree, byId["(O)1"]);
        Assert.Equal(CompareVerdict.NewLater, byId["(O)2"]);
        Assert.Equal(CompareVerdict.OnlyExisting, byId["(O)3"]);
        Assert.False(byId.ContainsKey("(O)4"));
        Assert.Equal(CompareVerdict.OnlyNew, byId["(O)5"]);
        Assert.Equal(CompareVerdict.NewEarlier, byId["(O)6"]);
        Assert.Equal(CompareVerdict.LuckOnly, byId["(O)7"]);
        CompareRow cart = Rows().Single(r => r.ItemId == "(O)7");
        Assert.Null(cart.NewDependable);
        Assert.Equal(1, cart.NewAny);
    }

    [Fact]
    public void The_report_details_only_the_verdicts_that_need_a_ruling_and_lists_only_new_by_name()
    {
        string text = ObtainabilityComparison.Render(Rows(), Model, new[] { "LOCATION_FISH Beach X | Forage at Beach" }, id => "Name " + id, "0.18.4");
        Assert.Contains("# Item obtainability comparison", text);
        Assert.Contains("| NewLater | 1 |", text);
        Assert.Contains("| LuckOnly | 1 |", text);
        Assert.Contains("| OnlyNew | 1 |", text);
        Assert.Contains("## NewEarlier", text);
        Assert.Contains("## LuckOnly", text);
        Assert.Contains("## OnlyExisting", text);
        Assert.Contains("Name (O)2", text);
        Assert.Contains("fourth", text);
        Assert.Contains("lands wk9/wk9/wk9/wk13", text);
        Assert.Contains("needs location:Town", text);
        Assert.Contains("existing basis: rule b", text);
        Assert.Contains("## OnlyNew (names only)", text);
        Assert.Contains("- (O)5 Name (O)5", text);
        int onlyNewAt = text.IndexOf("## OnlyNew (names only)");
        Assert.True(text.IndexOf("## Agree") < onlyNewAt);
        Assert.DoesNotContain("### (O)5", text);                 // no detail block for OnlyNew
        Assert.Contains("## Unresolved sources (1)", text);
        Assert.DoesNotContain("\u2014", text);
    }
}
```

`(O)2`'s table string: `FromWeek(9)` from Spring 1, Summer 1 and Fall 1 lands week 9; from Winter 1 (day 85) it lands week 13.

- [ ] **Step 2: Run to verify they fail**

Expected: `LuckOnly` not defined; layout assertions fail.

- [ ] **Step 3: Implement**

`ObtainabilityComparison.cs`:
- `public enum CompareVerdict { Agree, NewEarlier, NewLater, LuckOnly, OnlyExisting, OnlyNew }`
- `SectionOrder = { NewEarlier, NewLater, LuckOnly, OnlyExisting, OnlyNew, Agree }`; `DetailSections = { NewEarlier, NewLater, LuckOnly, OnlyExisting }`.
- In `Compare`: `int? newAny = model.LandingWeekFromDay1(id, ObtainFilter.Any); int? newDep = model.LandingWeekFromDay1(id, ObtainFilter.DependableOnly);` verdict:
  ```csharp
  CompareVerdict verdict = existing == null ? CompareVerdict.OnlyNew
      : newAny == null ? CompareVerdict.OnlyExisting
      : newDep == null ? CompareVerdict.LuckOnly
      : newDep == existing.Value.Hard ? CompareVerdict.Agree
      : newDep < existing.Value.Hard ? CompareVerdict.NewEarlier
      : CompareVerdict.NewLater;
  ```
- Intro sentence: "Existing weeks are the current item model's pacing and hard weeks. New weeks are the blind obtainability model's landing week starting from Spring 1: dependable sources only, then any source. The verdict compares the existing hard week with the new dependable week; LuckOnly means only a chance source (the cart, a drop) reaches it. Weeks mean: start from nothing on the day, when does it first land."
- Detail loop over `DetailSections` only; source lines via `ObtainabilityText.SourceLine`; then the Agree table (column "Week" = `NewDependable`); then `## OnlyNew (names only)` with `- {id} {name}` lines; then unresolved.

`ModEntry.CmdObtain`: usage `tly_obtain <itemId> [startDay 1-112] | tly_obtain compare [fileName]`; parse `args[1]` as an int when present and pass `startDay:` to `Describe`; reject out-of-range with a warn. Update the command's registration help text (line 346 area) to `"Item obtainability model (phase 2, not used by gameplay). Usage: tly_obtain <itemId> [startDay 1-112] | tly_obtain compare [fileName]"`.

- [ ] **Step 4: Run the full suite and build the mod**

Expected: green; mod builds.

- [ ] **Step 5: Commit and push**

```bash
git add -A src tests/TheLongestYear.Tests
git commit -m "obtainability: dependable-from-day-1 verdicts, LuckOnly, detail only where a ruling is needed; tly_obtain takes a start day"
git push origin story
```

---

### Task 9: Whole-branch review of the phase 2 code

**Files:** none new. Review commits from Task 1 to Task 8 (`git diff 55fafb2..HEAD -- src tests`).

- [ ] **Step 1: Run the review checklist**

Read the diff with the spec beside it and check, in order:
1. Every `DayTable` produced by a rule is monotone by construction (only `Available`, `InWeeks`, `Exact`, `Delay`, `Then`, `Earliest`, `Latest`, `Except` are used; no direct byte construction).
2. No rule uses a snapshot table's `LandingWeek(1)` as a shortcut for the whole table (the Task 2 stopgap is gone).
3. Every hand-typed fact in `CodeSources.cs` and the mine fish table cites a decompile file and line, and the line numbers match the decompile on disk (spot-check five with `sed -n`).
4. The guard test lists every `.cs` file in the blind folder (the `Nothing_else_hides_in_the_blind_folder_unlisted` test enforces it; confirm it passed).
5. `grep -rn $'\xe2\x80\x94' src/TheLongestYear.Core/Obtainability src/TheLongestYear/Loop/GameObtainabilityData.cs src/TheLongestYear.Core/ObtainabilityComparison.cs docs/superpowers/specs/2026-09-14-obtainability-phase2-design.md docs/superpowers/plans/2026-09-14-obtainability-phase2.md` returns nothing.
6. `ObtainabilityComparison.cs` is still the only file outside tests that names the existing model together with the new one.
7. Any file in the blind folder over 400 lines gets a split proposal in the report (not a split now).

- [ ] **Step 2: Fix what the checklist finds, with tests where behaviour changed**

- [ ] **Step 3: Run the full suite; commit and push**

```bash
git add -A src tests
git commit -m "obtainability: phase 2 review fixes (<one line per fix>)"
git push origin story
```

---

### Task 10: Live rerun, outliers, and the docs

**Files:**
- Modify: `STATUS.md` (new top section), `TODO.md` (replace the "Obtainability phase 2" entry's build notes with the rerun state; keep the ruling questions), `docs/superpowers/specs/2026-09-14-obtainability-phase2-design.md` (status line; ruling log stays for Jeff)

- [ ] **Step 1: Deploy and launch (automated run, not Jeff's)**

Follow `docs/HEADLESS_DRIVING.md`: build with deploy on (`dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`), launch the game headlessly, `tly_loadsave` a `None_*` throwaway (never `PuffPuff_*` or `Cheatside_*`), and wait for `Obtainability model: N items in M ms, P pass(es), U unresolved source(s).` in the SMAPI log. Record the line. If the log instead says `no model published`, stop and report which section failed.

- [ ] **Step 2: Spot checks with `tly_obtain`** (record each output in the STATUS section)

- `tly_obtain (O)24 1` and `tly_obtain (O)24 29`: Parsnip lands week 1 from Spring 1; from Summer 1 the dependable greenhouse route must be "never" (the seed is gone) and only Mixed Seeds or the cart remain.
- `tly_obtain (O)348 1`: Wine lands week 2 or 3 from Spring 1 (fruit plus 7 days).
- `tly_obtain (O)158`: Stonefish, Fish, Dependable, `mines:floor 1`.
- `tly_obtain (O)815 1`: Tea Leaves lands about week 4 from Spring 1 (recipe, then 20 days, then day 22).
- `tly_obtain (O)Moss`: Forage weeks Spring to Fall, greenhouse all year.
- `tly_obtain (H)27`: Hard Hat, Guild, Dependable.
- `tly_obtain (O)798 1`: Midnight Squid, Submarine lands week 15; the Magic Bait row now Ginger Island.
- `tly_obtain (O)139 1`: Salmon dependable lands week 9 (no more `fishingGame`).
- `tly_obtain (O)472 1`: Parsnip Seeds has a Seed Maker source.
- `tly_obtain (O)BroccoliSeeds 1`: FishingTreasure, Chance, Summer 21 window.

- [ ] **Step 3: Rerun the comparison**

`tly_obtain compare` then read `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\obtainability-compare.md`. Record the counts table. Then list, for Jeff, every item in NewEarlier, NewLater, LuckOnly and OnlyExisting with a one-line plain-English reason per item (or per group when the reason is shared, e.g. "12 fish the existing model gates on a rain window the new model treats as a condition"). Flag outliers: any dependable landing week that is 4 or more weeks off the existing hard week, any OnlyExisting that the gap list in the spec claimed to close, and any unresolved count above 25.

- [ ] **Step 4: Write the docs**

STATUS.md top section "2026-09-14 (later): item obtainability model, phase 2": commits, test count, the live build line, the ten spot checks with their real output, the counts table, and the outlier list. TODO.md: under "Obtainability phase 2", replace the "gaps to close" and "from the final review" bullets with "closed in phase 2 (commits ...)" and add "Jeff rules on the rerun report: <count> items listed in STATUS" as the open item; keep the Part B bullet. Spec status line: "phase 2 built (commits ...), rerun report awaiting Jeff's rulings".

- [ ] **Step 5: Commit and push**

```bash
git add STATUS.md TODO.md docs/superpowers/specs/2026-09-14-obtainability-phase2-design.md
git commit -m "docs: obtainability phase 2 live rerun, spot checks and the disagreement list for Jeff"
git push origin story
```

Then post to Jeff: the counts table, the disagreement list with reasons, and the outliers, and wait for rulings item by item. Each ruling goes into the spec's ruling log in its own small commit.

---

## Self-review

**Spec coverage.** Decision 1 and 2 (start day, days inside): Tasks 1, 2, 4, 5. Decision 3 (setup steps): Tasks 2, 4, 5. Decision 4 (dependable headline): Task 8. Decision 5 (report layout): Task 8. Section 2 gaps: mine fish, Moss, guild rewards, season seeds (Task 6); Tea Leaves (Task 4); Seed Maker, Mushroom Log, Cask dropped, self-feeding machine (Task 5); Magic Bait, island negation, day-level conditions (Task 3); LOCATION_FISH, fishingGame, refuse to publish, buildings and slayer assets (Task 7). Section 3 debug day argument (Task 8) and the rerun with rulings (Task 10). Section 4 guard list growth (Tasks 1, 6, 7), review (Task 9). Non-goal quality: casks dropped (Task 5).

**Type consistency.** `DayTable` API names are used identically in Tasks 2 to 8 (`Available`, `InWeeks`, `Exact`, `Lands`, `Delay`, `Then`, `Earliest`, `Latest`, `Except`, `LandingWeek`, `CanObtain`). `ObtainabilityModel.Table/Lands/CanObtain/LandingWeekFromDay1` are the only model queries after Task 2. `SetupStep(Name, Days)` everywhere. `OutputMethodKind` values `None, SeedMaker, MushroomLog, Cask, Unknown`. `SourceKind.Guild` added in Task 6 and used only there. `ConditionSeasons.Availability(reading, WeekMask)` introduced in Task 3, used in Tasks 4 and 5. `Calendar.DayOfMonthOf` added in Task 4, used in Task 6.

**Expectations.** Every number in the tests was worked by hand from the decompile rules and the DayTable invariant. If one fails to hold, the implementer stops and reports it (task, test, reasoning); nobody edits an assertion to match code.
