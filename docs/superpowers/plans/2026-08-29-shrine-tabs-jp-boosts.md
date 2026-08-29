# Shrine tabs + JP Boosts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The planning shrine sells the full JP Boost roster (15 rows across Instant / Week / Season / Loop) from a Boosts tab, shows what is running on an Active tab, and keeps the permanents preview (plus a Locked section) on a Plan tab.

**Architecture:** Core owns the catalog (`BoostCatalog`), the run record (`RunState.ActiveBoosts`), the purchase rules (`BoostPurchase` with a `BoostContext` of live facts), pricing (`BoostPricing`) and stacking (`ActiveEffectsProvider.BonusStacks`). The mod applies immediate effects in `BoostPurchaseService`, re-applies daily effects in a new `BoostEffectsService` on `DayStarted`, and the five reuse patches loop over the stack count. `ShrinePreviewMenu` gains a three-tab strip and builds a row list per tab.

**Tech Stack:** C# (net6), SMAPI 4 / Stardew 1.6, Harmony (per-class discovery in `ModEntry`), xunit (Core only), Newtonsoft via SMAPI's Data API for `RunState`.

**Spec:** `docs/superpowers/specs/2026-08-29-shrine-tabs-jp-boosts-design.md`

## Global Constraints

- No em dashes anywhere (code, comments, i18n, docs). Patch bump per commit, local commits only, stage only the task's files.
- Prices (opening bids, spec 1.1): Rain Dance 25, Storm Call 40, Fortune's Favor 30, Second Wind 20, Overgrowth 50, Feeding Frenzy 45, Growth Spurt 60, Rich Veins 55, Windfall 90, Quick Feet 40, Year-Two Seeds 75, Haggler 120, Fast Friends 150, Iron Lungs 90, Sneak Peek 100. Crash Course and Elevator Pass computed.
- Crash Course: `round(0.2 x SkillLevelCosts[target] x 3^(n-1))`, `n` = levels bought this loop across all skills + 1; cap 2 per skill per loop; target never 10; XP grant = full width of the target level.
- Elevator Pass: landing = next multiple of 10 above `LowestMineLevel`; price `round(0.2 x (75 + ((landing - 10) / 10) x 100))`; unavailable at 0 and at 120.
- Weather boosts refuse on festival-tomorrow and in Winter. Fortune's Favor sets `sharedDailyLuck = 0.10`.
- Stacking: one independent roll per stack; same id not re-buyable until expiry; reuse rows collide on `ModifierId`.
- Boosts tab host-only in multiplayer (`Context.IsMainPlayer`).
- Night Owl and Backpack Organizer are NOT in this plan (deferred, spec "Scope").
- i18n keys: `boost.<snake_id>.name/.desc` for every row; new `shrine.tab.active/boosts/plan`, `shrine.boosts.group.instant/week/season/loop`, `shrine.active.*`, `shrine.plan.locked`, `reach.*` keys. `I18nGuardTests` enforces all of them.
- Tests: `dotnet test tests/TheLongestYear.Tests` must stay green (1765 at start). Build + deploy: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`; live checks over the bridge per `docs/HEADLESS_DRIVING.md`.

---

### Task 1: Roster catalog, run record, expiry, purchase rules (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/Boosts.cs` (whole file rewritten)
- Modify: `src/TheLongestYear.Core/RunState.cs:125-132` (retire the two fields), `:266-295` (`BeginNewRun`)
- Modify: `src/TheLongestYear.Core/Calendar.cs` (add day-of-year helpers)
- Create: `src/TheLongestYear.Core/ActiveBoost.cs`, `src/TheLongestYear.Core/BoostContext.cs`
- Test: `tests/TheLongestYear.Tests/BoostsTests.cs` (rewrite), `tests/TheLongestYear.Tests/CalendarTests.cs` (add)

**Interfaces:**
- Produces: `enum BoostDuration { Instant, Week, Season, Loop }`; `enum BoostId` (17 values, order = catalog order); `record BoostDefinition(BoostId Id, long Cost, BoostDuration Duration, string NameKey, string DescKey, string? ModifierId = null)`; `BoostCatalog.All`, `BoostCatalog.Get(BoostId)`; `class ActiveBoost { string Id; int BoughtDay; int ExpiresAfterDay; int Skill = -1; bool IsActiveOn(int day) }`; `RunState.ActiveBoosts : List<ActiveBoost>`, `RunState.SkillLevelsBoughtThisLoop : Dictionary<int,int>`, `RunState.SkillLevelsBoughtTotal : int`, `RunState.WeatherOverrideDay : int = -1`, `RunState.WeatherOverride : string?`; `sealed record BoostContext(int DayOfYear, Season Season, bool TomorrowIsFestival, IReadOnlyList<int> SkillLevels, int MineFloor, int Skill = -1)`; `BoostPurchase.StateOf(meta, run, id, ctx)`, `BoostPurchase.TryBuy(meta, run, id, ctx)` returning `BoostPurchase.Result { Success, NotEnoughJp, AlreadyActive, NotAvailable }`; `BoostPurchase.ActiveEntries(run, day)`; `BoostState.IsActive(run, BoostId, day)`, `BoostState.YearTwoSeedsActive(run, day)`, `BoostState.SneakPeekActive(run, day)`, `BoostState.MigrateLegacy(run, day)`; `BoostExpiry.LastDayFor(BoostDuration, int dayOfYear)`; `Calendar.DayOfYear(int monthIndex, int dayOfMonth)`, `Calendar.SeasonOfDay(int dayOfYear)`, `Calendar.LastDayOfWeek(int dayOfYear)`, `Calendar.LastDayOfSeason(int dayOfYear)`, `Calendar.DaysPerYear = 112`.

- [ ] **Step 1: Calendar helpers and their failing tests**

Add to `tests/TheLongestYear.Tests/CalendarTests.cs` (create the file if it does not exist, else append inside the class):

```csharp
[Theory]
[InlineData(0, 1, 1)] [InlineData(0, 28, 28)] [InlineData(1, 1, 29)] [InlineData(3, 28, 112)]
public void DayOfYear_counts_from_spring_1(int month, int day, int expected)
    => Assert.Equal(expected, Calendar.DayOfYear(month, day));

[Theory]
[InlineData(1, 7)] [InlineData(7, 7)] [InlineData(8, 14)] [InlineData(28, 28)] [InlineData(29, 35)] [InlineData(112, 112)]
public void LastDayOfWeek_is_the_end_of_the_seven_day_block(int day, int expected)
    => Assert.Equal(expected, Calendar.LastDayOfWeek(day));

[Theory]
[InlineData(1, 28)] [InlineData(28, 28)] [InlineData(29, 56)] [InlineData(100, 112)]
public void LastDayOfSeason_is_day_28_of_that_season(int day, int expected)
    => Assert.Equal(expected, Calendar.LastDayOfSeason(day));

[Theory]
[InlineData(1, Season.Spring)] [InlineData(28, Season.Spring)] [InlineData(29, Season.Summer)] [InlineData(112, Season.Winter)]
public void SeasonOfDay_maps_the_four_blocks(int day, Season expected)
    => Assert.Equal(expected, Calendar.SeasonOfDay(day));
```

Add to `Calendar`:

```csharp
public const int DaysPerYear = DaysPerMonth * MonthsPerYear; // 112

/// <summary>1-based day of the loop year: Spring 1 = 1, Winter 28 = 112.</summary>
public static int DayOfYear(int monthIndex, int dayOfMonth) => monthIndex * DaysPerMonth + dayOfMonth;

public static Season SeasonOfDay(int dayOfYear) => (Season)((dayOfYear - 1) / DaysPerMonth);

/// <summary>Last day of the 7-day block containing <paramref name="dayOfYear"/>.</summary>
public static int LastDayOfWeek(int dayOfYear) => ((dayOfYear - 1) / DaysPerWeek + 1) * DaysPerWeek;

/// <summary>Day 28 of the season containing <paramref name="dayOfYear"/>.</summary>
public static int LastDayOfSeason(int dayOfYear) => ((dayOfYear - 1) / DaysPerMonth + 1) * DaysPerMonth;
```

Run: `dotnet test tests/TheLongestYear.Tests --filter CalendarTests` → PASS.

- [ ] **Step 2: `ActiveBoost` and `BoostContext`**

`src/TheLongestYear.Core/ActiveBoost.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>One bought boost on the run record. Persisted in RunState (Newtonsoft via SMAPI's
/// Data API), so plain settable properties. Days are day-of-year (Calendar.DayOfYear).</summary>
public sealed class ActiveBoost
{
    public string Id { get; set; } = "";
    public int BoughtDay { get; set; }
    /// <summary>Last day of year the boost is active, inclusive. 112 for Loop rows.</summary>
    public int ExpiresAfterDay { get; set; }
    /// <summary>Crash Course only: the skill index bought (Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4).</summary>
    public int Skill { get; set; } = -1;

    public bool IsActiveOn(int dayOfYear) => BoughtDay <= dayOfYear && dayOfYear <= ExpiresAfterDay;
}
```

`src/TheLongestYear.Core/BoostContext.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The live facts a boost purchase needs that Core cannot read itself. The mod fills
/// it from Game1 at the moment of the click; tests hand-build it.</summary>
/// <param name="DayOfYear">Today, 1..112.</param>
/// <param name="TomorrowIsFestival">Utility.isFestivalDay for tomorrow (weather rows refuse).</param>
/// <param name="SkillLevels">Current levels indexed Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4.</param>
/// <param name="MineFloor">netWorldState.LowestMineLevel (0 = mine not entered).</param>
/// <param name="Skill">Crash Course: the skill being bought; -1 otherwise.</param>
public sealed record BoostContext(
    int DayOfYear,
    bool TomorrowIsFestival,
    IReadOnlyList<int> SkillLevels,
    int MineFloor,
    int Skill = -1)
{
    public Season Season => Calendar.SeasonOfDay(DayOfYear);
    public static BoostContext Simple(int dayOfYear) =>
        new(dayOfYear, false, new[] { 0, 0, 0, 0, 0 }, 0);
}
```

- [ ] **Step 3: RunState fields**

In `RunState.cs` replace the `YearTwoSeedsWeek` / `SneakPeekSeason` block (lines 125-132) with:

```csharp
    /// <summary>Every boost bought this run, active or expired-but-not-yet-pruned (the Active tab
    /// shows "expires tonight" from the entry). Pruned on DayStarted, cleared by BeginNewRun.</summary>
    public List<ActiveBoost> ActiveBoosts { get; set; } = new();

    /// <summary>Crash Course: levels bought this loop per skill index (cap 2). Earned level =
    /// current level minus this.</summary>
    public Dictionary<int, int> SkillLevelsBoughtThisLoop { get; set; } = new();

    /// <summary>Crash Course: levels bought this loop across all skills (the n in 3^(n-1)).</summary>
    public int SkillLevelsBoughtTotal { get; set; }

    /// <summary>Rain Dance / Storm Call: the day of year the override applies to, and the weather.</summary>
    public int WeatherOverrideDay { get; set; } = -1;
    public string? WeatherOverride { get; set; }

    /// <summary>Legacy (0.16.117 to 0.16.158): migrated into ActiveBoosts by BoostState.MigrateLegacy.</summary>
    public int YearTwoSeedsWeek { get; set; } = -1;
    public int SneakPeekSeason { get; set; } = -1;
```

In `BeginNewRun`, replace `YearTwoSeedsWeek = -1; SneakPeekSeason = -1;` with:

```csharp
        ActiveBoosts.Clear();
        SkillLevelsBoughtThisLoop.Clear();
        SkillLevelsBoughtTotal = 0;
        WeatherOverrideDay = -1;
        WeatherOverride = null;
        YearTwoSeedsWeek = -1;
        SneakPeekSeason = -1;
```

- [ ] **Step 4: Failing tests for the catalog and purchase rules**

Rewrite `tests/TheLongestYear.Tests/BoostsTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoostsTests
{
    private static MetaState Meta(long jp = 1000) => new() { JunimoPoints = jp };

    [Fact]
    public void Catalog_has_the_roster_in_spec_order_with_spec_prices()
    {
        string[] ids = BoostCatalog.All.Select(b => b.Id.ToString()).ToArray();
        Assert.Equal(new[]
        {
            "RainDance", "StormCall", "FortunesFavor", "SecondWind",
            "Overgrowth", "FeedingFrenzy", "GrowthSpurt", "RichVeins", "Windfall", "QuickFeet", "YearTwoSeeds",
            "Haggler", "FastFriends", "IronLungs", "SneakPeek",
            "CrashCourse", "ElevatorPass",
        }, ids);
        Assert.Equal(25, BoostCatalog.Get(BoostId.RainDance).Cost);
        Assert.Equal(150, BoostCatalog.Get(BoostId.FastFriends).Cost);
        Assert.Equal("forage_yield_up", BoostCatalog.Get(BoostId.Overgrowth).ModifierId);
        Assert.Equal(BoostDuration.Loop, BoostCatalog.Get(BoostId.ElevatorPass).Duration);
    }

    [Theory]
    [InlineData(BoostDuration.Instant, 10, 11)]
    [InlineData(BoostDuration.Week, 10, 14)]
    [InlineData(BoostDuration.Season, 10, 28)]
    [InlineData(BoostDuration.Loop, 10, 112)]
    public void Expiry_by_duration_class(BoostDuration d, int day, int expected)
        => Assert.Equal(expected, BoostExpiry.LastDayFor(d, day));

    [Fact]
    public void Second_wind_expires_the_same_day()
    {
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.SecondWind, BoostContext.Simple(5)));
        Assert.Equal(5, run.ActiveBoosts.Single().ExpiresAfterDay);
    }

    [Fact]
    public void Buying_a_week_row_spends_jp_and_is_active_through_the_week()
    {
        var meta = Meta(100); var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.Overgrowth, BoostContext.Simple(9)));
        Assert.Equal(50, meta.JunimoPoints);
        Assert.True(BoostState.IsActive(run, BoostId.Overgrowth, 14));
        Assert.False(BoostState.IsActive(run, BoostId.Overgrowth, 15));
        Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.TryBuy(meta, run, BoostId.Overgrowth, BoostContext.Simple(10)));
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.Overgrowth, BoostContext.Simple(15)));
    }

    [Fact]
    public void Not_enough_jp_is_reported_and_spends_nothing()
    {
        var meta = Meta(10); var run = new RunState();
        Assert.Equal(BoostPurchase.Result.NotEnoughJp, BoostPurchase.TryBuy(meta, run, BoostId.Windfall, BoostContext.Simple(1)));
        Assert.Equal(10, meta.JunimoPoints);
        Assert.Empty(run.ActiveBoosts);
    }

    [Fact]
    public void Weather_rows_refuse_in_winter_and_before_a_festival()
    {
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(90)));   // Winter
        var festival = new BoostContext(12, TomorrowIsFestival: true, new[] { 0, 0, 0, 0, 0 }, 0);
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(Meta(), run, BoostId.StormCall, festival));
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(12)));
        Assert.Equal(13, run.WeatherOverrideDay);
        Assert.Equal("Rain", run.WeatherOverride);
    }

    [Fact]
    public void Storm_call_after_rain_dance_replaces_the_override_and_expires_the_first_entry()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(12));
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.StormCall, BoostContext.Simple(12)));
        Assert.Equal("Storm", run.WeatherOverride);
        Assert.Single(run.ActiveBoosts, b => b.IsActiveOn(13));
    }

    [Fact]
    public void Year_two_seeds_cannot_be_bought_in_winter()
        => Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(Meta(), new RunState(), BoostId.YearTwoSeeds, BoostContext.Simple(100)));

    [Fact]
    public void Legacy_flags_migrate_once_into_the_list()
    {
        var run = new RunState { YearTwoSeedsWeek = 2, SneakPeekSeason = 0 };
        BoostState.MigrateLegacy(run, dayOfYear: 10);
        Assert.True(BoostState.YearTwoSeedsActive(run, 10));
        Assert.False(BoostState.YearTwoSeedsActive(run, 15));
        Assert.True(BoostState.SneakPeekActive(run, 28));
        Assert.False(BoostState.SneakPeekActive(run, 29));
        Assert.Equal(-1, run.YearTwoSeedsWeek);
        Assert.Equal(-1, run.SneakPeekSeason);
    }

    [Fact]
    public void A_new_run_clears_everything()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.Haggler, BoostContext.Simple(3));
        run.BeginNewRun(1);
        Assert.Empty(run.ActiveBoosts);
        Assert.Equal(0, run.SkillLevelsBoughtTotal);
    }

    [Fact]
    public void StateOf_agrees_with_TryBuy_and_mutates_nothing()
    {
        var meta = Meta(100); var run = new RunState(); var ctx = BoostContext.Simple(3);
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.StateOf(meta, run, BoostId.RichVeins, ctx));
        Assert.Equal(100, meta.JunimoPoints);
        Assert.Empty(run.ActiveBoosts);
    }

    [Fact]
    public void Run_state_with_boosts_round_trips_through_json()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.IronLungs, BoostContext.Simple(40));
        string json = System.Text.Json.JsonSerializer.Serialize(run);
        RunState back = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(56, back.ActiveBoosts.Single().ExpiresAfterDay);
        Assert.Empty(System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.ActiveBoosts);
    }

    [Theory]
    [InlineData(Season.Spring, "476")] [InlineData(Season.Summer, "485")] [InlineData(Season.Fall, "489")] [InlineData(Season.Winter, null)]
    public void Seed_per_season(Season season, string? expected) => Assert.Equal(expected, YearTwoSeeds.SeedIdFor(season));
}
```

Run: `dotnet test tests/TheLongestYear.Tests --filter BoostsTests` → FAIL (compile errors: missing types).

- [ ] **Step 5: Rewrite `Boosts.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>How long a boost runs. Spec 2026-08-29 section 1.2.</summary>
public enum BoostDuration { Instant, Week, Season, Loop }

/// <summary>Every purchasable in-loop boost, in catalog (display) order.</summary>
public enum BoostId
{
    RainDance, StormCall, FortunesFavor, SecondWind,
    Overgrowth, FeedingFrenzy, GrowthSpurt, RichVeins, Windfall, QuickFeet, YearTwoSeeds,
    Haggler, FastFriends, IronLungs, SneakPeek,
    CrashCourse, ElevatorPass,
}

/// <param name="Cost">Opening bid in JP; 0 for the computed rows (BoostPricing).</param>
/// <param name="ModifierId">Theme-modifier id this boost stacks onto (reuse rows); null for new effects.</param>
public sealed record BoostDefinition(
    BoostId Id, long Cost, BoostDuration Duration, string NameKey, string DescKey, string? ModifierId = null);

public static class BoostCatalog
{
    private static BoostDefinition Row(BoostId id, long cost, BoostDuration d, string snake, string? modifier = null)
        => new(id, cost, d, $"boost.{snake}.name", $"boost.{snake}.desc", modifier);

    public static readonly IReadOnlyList<BoostDefinition> All = new List<BoostDefinition>
    {
        Row(BoostId.RainDance,     25,  BoostDuration.Instant, "rain_dance"),
        Row(BoostId.StormCall,     40,  BoostDuration.Instant, "storm_call"),
        Row(BoostId.FortunesFavor, 30,  BoostDuration.Instant, "fortunes_favor"),
        Row(BoostId.SecondWind,    20,  BoostDuration.Instant, "second_wind"),
        Row(BoostId.Overgrowth,    50,  BoostDuration.Week,    "overgrowth",     "forage_yield_up"),
        Row(BoostId.FeedingFrenzy, 45,  BoostDuration.Week,    "feeding_frenzy", "fish_bite_up"),
        Row(BoostId.GrowthSpurt,   60,  BoostDuration.Week,    "growth_spurt",   "crop_growth_up"),
        Row(BoostId.RichVeins,     55,  BoostDuration.Week,    "rich_veins",     "mine_drops_up"),
        Row(BoostId.Windfall,      90,  BoostDuration.Week,    "windfall",       "all_drops_up"),
        Row(BoostId.QuickFeet,     40,  BoostDuration.Week,    "quick_feet"),
        Row(BoostId.YearTwoSeeds,  75,  BoostDuration.Week,    "year_two_seeds"),
        Row(BoostId.Haggler,       120, BoostDuration.Season,  "haggler"),
        Row(BoostId.FastFriends,   150, BoostDuration.Season,  "fast_friends"),
        Row(BoostId.IronLungs,     90,  BoostDuration.Season,  "iron_lungs"),
        Row(BoostId.SneakPeek,     100, BoostDuration.Season,  "sneak_peek"),
        Row(BoostId.CrashCourse,   0,   BoostDuration.Loop,    "crash_course"),
        Row(BoostId.ElevatorPass,  0,   BoostDuration.Loop,    "elevator_pass"),
    };

    public static BoostDefinition Get(BoostId id)
        => All.FirstOrDefault(b => b.Id == id) ?? throw new KeyNotFoundException($"Unknown boost '{id}'.");
}

public static class BoostExpiry
{
    /// <summary>Last active day (inclusive) for a boost bought on <paramref name="dayOfYear"/>.
    /// Instant lands tomorrow; Second Wind is the exception (tonight) and is handled by the caller.</summary>
    public static int LastDayFor(BoostDuration duration, int dayOfYear) => duration switch
    {
        BoostDuration.Instant => Math.Min(dayOfYear + 1, Calendar.DaysPerYear),
        BoostDuration.Week    => Calendar.LastDayOfWeek(dayOfYear),
        BoostDuration.Season  => Calendar.LastDayOfSeason(dayOfYear),
        _                     => Calendar.DaysPerYear,
    };
}

public static class BoostPurchase
{
    public enum Result { Success, NotEnoughJp, AlreadyActive, NotAvailable }

    public const string Rain = "Rain";
    public const string Storm = "Storm";

    /// <summary>Entries active on <paramref name="dayOfYear"/>.</summary>
    public static IEnumerable<ActiveBoost> ActiveEntries(RunState run, int dayOfYear)
        => run.ActiveBoosts.Where(b => b.IsActiveOn(dayOfYear));

    /// <summary>The purchase's outcome without buying: availability, then collision, then JP.</summary>
    public static Result StateOf(MetaState meta, RunState run, BoostId id, BoostContext ctx)
    {
        BoostDefinition def = BoostCatalog.Get(id);
        if (!Available(run, id, ctx)) return Result.NotAvailable;
        if (Collides(run, def, ctx.DayOfYear)) return Result.AlreadyActive;
        if (meta.JunimoPoints < BoostPricing.CostOf(def, run, ctx)) return Result.NotEnoughJp;
        return Result.Success;
    }

    /// <summary>Spend JP and append the entry. Never touches game state: the mod applies the
    /// immediate part (weather write, XP grant, floor write) after a Success.</summary>
    public static Result TryBuy(MetaState meta, RunState run, BoostId id, BoostContext ctx)
    {
        Result state = StateOf(meta, run, id, ctx);
        if (state != Result.Success) return state;

        BoostDefinition def = BoostCatalog.Get(id);
        meta.JunimoPoints -= BoostPricing.CostOf(def, run, ctx);

        int expires = id == BoostId.SecondWind ? ctx.DayOfYear : BoostExpiry.LastDayFor(def.Duration, ctx.DayOfYear);
        var entry = new ActiveBoost { Id = id.ToString(), BoughtDay = ctx.DayOfYear, ExpiresAfterDay = expires };

        switch (id)
        {
            case BoostId.RainDance:
            case BoostId.StormCall:
                // The second weather buy of a day replaces the first: expire the other entry now.
                foreach (ActiveBoost other in run.ActiveBoosts.Where(b => IsWeather(b.Id) && b.IsActiveOn(ctx.DayOfYear + 1)))
                    other.ExpiresAfterDay = ctx.DayOfYear - 1;
                run.WeatherOverrideDay = ctx.DayOfYear + 1;
                run.WeatherOverride = id == BoostId.RainDance ? Rain : Storm;
                break;
            case BoostId.CrashCourse:
                entry.Skill = ctx.Skill;
                run.SkillLevelsBoughtThisLoop[ctx.Skill] = run.SkillLevelsBoughtThisLoop.GetValueOrDefault(ctx.Skill) + 1;
                run.SkillLevelsBoughtTotal += 1;
                break;
        }
        run.ActiveBoosts.Add(entry);
        return Result.Success;
    }

    private static bool IsWeather(string id) => id == nameof(BoostId.RainDance) || id == nameof(BoostId.StormCall);

    private static bool Available(RunState run, BoostId id, BoostContext ctx) => id switch
    {
        BoostId.RainDance or BoostId.StormCall => ctx.Season != Season.Winter && !ctx.TomorrowIsFestival && ctx.DayOfYear < Calendar.DaysPerYear,
        BoostId.YearTwoSeeds => ctx.Season != Season.Winter,
        BoostId.CrashCourse => BoostPricing.CrashCourseAvailable(run, ctx),
        BoostId.ElevatorPass => BoostPricing.ElevatorPassAvailable(ctx.MineFloor),
        _ => true,
    };

    /// <summary>Same id active, or (reuse rows) another active boost on the same modifier. The two
    /// weather rows never collide with each other (the second replaces the first). Crash Course and
    /// Elevator Pass are repeatable.</summary>
    private static bool Collides(RunState run, BoostDefinition def, int day)
    {
        if (def.Id is BoostId.CrashCourse or BoostId.ElevatorPass) return false;
        if (IsWeather(def.Id.ToString())) return run.ActiveBoosts.Any(b => b.Id == def.Id.ToString() && b.IsActiveOn(day + 1));
        foreach (ActiveBoost b in ActiveEntries(run, day))
        {
            if (b.Id == def.Id.ToString()) return true;
            if (def.ModifierId != null && Enum.TryParse(b.Id, out BoostId otherId)
                && BoostCatalog.Get(otherId).ModifierId == def.ModifierId) return true;
        }
        return false;
    }
}

public static class BoostState
{
    public static bool IsActive(RunState run, BoostId id, int dayOfYear)
        => run.ActiveBoosts.Any(b => b.Id == id.ToString() && b.IsActiveOn(dayOfYear));

    public static bool YearTwoSeedsActive(RunState run, int dayOfYear) => IsActive(run, BoostId.YearTwoSeeds, dayOfYear);
    public static bool SneakPeekActive(RunState run, int dayOfYear) => IsActive(run, BoostId.SneakPeek, dayOfYear);

    /// <summary>Modifier ids with an active boost bound to them, one entry per active boost.</summary>
    public static IEnumerable<string> ActiveModifierIds(RunState run, int dayOfYear)
    {
        foreach (ActiveBoost b in BoostPurchase.ActiveEntries(run, dayOfYear))
            if (Enum.TryParse(b.Id, out BoostId id) && BoostCatalog.Get(id).ModifierId is string m)
                yield return m;
    }

    /// <summary>Drop entries whose last day is before today.</summary>
    public static int Prune(RunState run, int dayOfYear) => run.ActiveBoosts.RemoveAll(b => b.ExpiresAfterDay < dayOfYear);

    /// <summary>One-time migration of the 0.16.117 to 0.16.158 fields into the list.</summary>
    public static void MigrateLegacy(RunState run, int dayOfYear)
    {
        if (run.YearTwoSeedsWeek >= 0)
        {
            int weekStart = (run.YearTwoSeedsWeek - 1) * Calendar.DaysPerWeek + 1;
            run.ActiveBoosts.Add(new ActiveBoost { Id = nameof(BoostId.YearTwoSeeds), BoughtDay = weekStart, ExpiresAfterDay = weekStart + Calendar.DaysPerWeek - 1 });
            run.YearTwoSeedsWeek = -1;
        }
        if (run.SneakPeekSeason >= 0)
        {
            int seasonStart = run.SneakPeekSeason * Calendar.DaysPerMonth + 1;
            run.ActiveBoosts.Add(new ActiveBoost { Id = nameof(BoostId.SneakPeek), BoughtDay = seasonStart, ExpiresAfterDay = seasonStart + Calendar.DaysPerMonth - 1 });
            run.SneakPeekSeason = -1;
        }
    }
}

/// <summary>Year-Two Seeds facts (unchanged from plan 4).</summary>
public static class YearTwoSeeds
{
    public const double Chance = 0.05;
    public static string? SeedIdFor(Season season) => season switch
    {
        Season.Spring => "476", Season.Summer => "485", Season.Fall => "489", _ => null,
    };
}
```

`BoostPricing` is Task 2; add a minimal stub now so this compiles:

```csharp
public static class BoostPricing
{
    public static long CostOf(BoostDefinition def, RunState run, BoostContext ctx) => def.Cost;
    public static bool CrashCourseAvailable(RunState run, BoostContext ctx) => false;
    public static bool ElevatorPassAvailable(int floor) => false;
}
```

(put it in its own file `src/TheLongestYear.Core/BoostPricing.cs`; Task 2 fills it in.)

- [ ] **Step 6: Fix the mod-side compile**

The mod project references `BoostState.YearTwoSeedsActive(run, week)` and `SneakPeekActive(run, season)` (`ModEntry.cs:440-441`), `BoostPurchase.StateOf(_state, _run, id, _run.WeekOfYear)` (`ShrinePreviewMenu.cs:274`), and `BoostPurchase.TryBuy(..., week)` (`BoostPurchaseService.cs:36-42`). Make them compile with the new signatures (behaviour is finished in Tasks 4 and 5):

`ModEntry.cs:440-441`:
```csharp
            BoostChecker.YearTwoSeedsActive = () => BoostState.YearTwoSeedsActive(_meta.Run, TodayDayOfYear());
            BoostChecker.SneakPeekActive   = () => BoostState.SneakPeekActive(_meta.Run, TodayDayOfYear());
```
and add to `ModEntry`:
```csharp
        /// <summary>Today as a 1..112 day of year from the run's own calendar.</summary>
        private int TodayDayOfYear() => TheLongestYear.Core.Calendar.DayOfYear((int)_meta.Run.Season, _meta.Run.DayOfMonth);
```

`BoostPurchaseService.cs`: replace `WeekOfYear` and `TryBuy` with
```csharp
        public BoostContext Context(int skill = -1) => BoostContextBuilder.Build(_store.Run, skill);

        public BoostPurchase.Result TryBuy(BoostId id, int skill = -1)
        {
            BoostContext ctx = Context(skill);
            BoostPurchase.Result result = BoostPurchase.TryBuy(_store.State, _store.Run, id, ctx);
            Report(id, result, ctx);
            return result;
        }
```
and fix `Report`'s cost lookup to `BoostPricing.CostOf(BoostCatalog.Get(id), _store.Run, ctx)`. Create `src/TheLongestYear/Donations/BoostContextBuilder.cs`:
```csharp
using System.Collections.Generic;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>Fills a BoostContext from live game state (the one place Core's boost rules touch Game1).</summary>
    internal static class BoostContextBuilder
    {
        public static BoostContext Build(RunState run, int skill = -1)
        {
            int day = Calendar.DayOfYear((int)run.Season, run.DayOfMonth);
            Farmer p = Game1.player;
            IReadOnlyList<int> levels = p == null
                ? new[] { 0, 0, 0, 0, 0 }
                : new[] { p.farmingLevel.Value, p.fishingLevel.Value, p.foragingLevel.Value, p.miningLevel.Value, p.combatLevel.Value };
            int floor = Game1.netWorldState?.Value?.LowestMineLevel ?? 0;
            return new BoostContext(day, TomorrowIsFestival(), levels, floor, skill);
        }

        private static bool TomorrowIsFestival()
        {
            WorldDate tomorrow = new WorldDate(Game1.Date); tomorrow.TotalDays += 1;
            return Utility.isFestivalDay(tomorrow.DayOfMonth, tomorrow.Season);
        }
    }
}
```
`ShrinePreviewMenu.cs:274`: `BoostPurchase.StateOf(_state, _run, boost.Id, BoostContextBuilder.Build(_run))` (add `using TheLongestYear.Donations;`). `CmdBoost` keeps compiling (its `TryBuy(id)` call still matches).

- [ ] **Step 7: Run all tests, build the mod**

Run: `dotnet test tests/TheLongestYear.Tests` → PASS (BoostsTests except the Crash Course / Elevator ones, which are in Task 2). Build: `pwsh -NoProfile -File tools/deploy.ps1 -NoLaunch` → 0 errors. `I18nGuardTests` will FAIL on the 15 new `boost.*` keys: add them now (Task 5 wording is final):

```json
    "boost.rain_dance.name": "Rain Dance",
    "boost.rain_dance.desc": "Tomorrow it rains. Not before a festival, and never in Winter.",
    "boost.storm_call.name": "Storm Call",
    "boost.storm_call.desc": "Tomorrow a storm rolls in. Not before a festival, and never in Winter.",
    "boost.fortunes_favor.name": "Fortune's Favor",
    "boost.fortunes_favor.desc": "Tomorrow is a guaranteed lucky day (the spirits' best mood).",
    "boost.second_wind.name": "Second Wind",
    "boost.second_wind.desc": "Tonight, sleeping late costs no energy and you wake without Exhaustion. Buy it before you go to bed.",
    "boost.overgrowth.name": "Overgrowth",
    "boost.overgrowth.desc": "This week, every forage you pick has an extra chance to double. Stacks with the Foraging theme.",
    "boost.feeding_frenzy.name": "Feeding Frenzy",
    "boost.feeding_frenzy.desc": "This week, fish bite 30% sooner. Stacks with the Fishing theme.",
    "boost.growth_spurt.name": "Growth Spurt",
    "boost.growth_spurt.desc": "This week, crops get an extra chance at a growth day. Stacks with the Farming theme.",
    "boost.rich_veins.name": "Rich Veins",
    "boost.rich_veins.desc": "This week, rocks and nodes have an extra chance to drop double. Stacks with the Mining theme.",
    "boost.windfall.name": "Windfall",
    "boost.windfall.desc": "This week, everything you gather has an extra chance at a bonus item. Stacks with the Mixed theme.",
    "boost.quick_feet.name": "Quick Feet",
    "boost.quick_feet.desc": "This week, you move one step faster.",
    "boost.haggler.name": "Haggler",
    "boost.haggler.desc": "This season, shops take another 10% off, on top of your Shop Discount. Buildings, animals and tool upgrades still cost full price.",
    "boost.fast_friends.name": "Fast Friends",
    "boost.fast_friends.desc": "This season, friendship you earn counts one and a half times.",
    "boost.iron_lungs.name": "Iron Lungs",
    "boost.iron_lungs.desc": "This season, +50 max energy.",
    "boost.crash_course.name": "Crash Course",
    "boost.crash_course.desc": "Gain one level in a skill right now (the level-up shows at bedtime). At most two per skill each loop, never to level 10, and a bought level cannot be kept. The price triples with each level bought this loop.",
    "boost.elevator_pass.name": "Elevator Pass",
    "boost.elevator_pass.desc": "The mine elevator reaches the next floor ending in 0, this loop only. Buy again for the next one.",
```

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/Boosts.cs src/TheLongestYear.Core/BoostPricing.cs src/TheLongestYear.Core/ActiveBoost.cs src/TheLongestYear.Core/BoostContext.cs src/TheLongestYear.Core/RunState.cs src/TheLongestYear.Core/Calendar.cs src/TheLongestYear/Donations/BoostContextBuilder.cs src/TheLongestYear/Donations/BoostPurchaseService.cs src/TheLongestYear/UI/ShrinePreviewMenu.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json tests/TheLongestYear.Tests/BoostsTests.cs tests/TheLongestYear.Tests/CalendarTests.cs
git commit -m "v0.16.159: boost roster catalog, ActiveBoost run record, expiry classes and purchase rules"
```

---

### Task 2: Pricing and availability for Crash Course and Elevator Pass (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/BoostPricing.cs` (replace the stub)
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs:66-73` (expose `SkillLevelCosts`), `:112` (expose the elevator cost formula)
- Test: `tests/TheLongestYear.Tests/BoostPricingTests.cs`

**Interfaces:**
- Produces: `BoostPricing.CostOf(BoostDefinition, RunState, BoostContext) : long`; `BoostPricing.CrashCourseCost(int targetLevel, int boughtSoFar) : long`; `BoostPricing.CrashCourseAvailable(RunState, BoostContext) : bool` (needs `ctx.Skill` 0..4); `BoostPricing.ElevatorLanding(int floor) : int`; `BoostPricing.ElevatorPassCost(int landing) : long`; `BoostPricing.ElevatorPassAvailable(int floor) : bool`; `UpgradeCatalogGenerators.SkillKeepCost(int level) : long`; `UpgradeCatalogGenerators.ElevatorKeepCost(int floor) : long`; `BoostPricing.MaxCrashCoursePerSkill = 2`.

- [ ] **Step 1: Failing tests**

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoostPricingTests
{
    [Theory]
    [InlineData(1, 0, 10)]   // first buy of level 1: 0.2 x 50
    [InlineData(2, 1, 60)]   // second buy at level 2: 0.2 x 100 x 3
    [InlineData(1, 2, 90)]   // third buy of level 1: 0.2 x 50 x 9
    [InlineData(9, 0, 250)]  // 0.2 x 1250
    public void Crash_course_price_table(int target, int boughtSoFar, long expected)
        => Assert.Equal(expected, BoostPricing.CrashCourseCost(target, boughtSoFar));

    [Fact]
    public void Crash_course_caps_at_two_per_skill_and_never_reaches_ten()
    {
        var run = new RunState();
        var ctx = new BoostContext(1, false, new[] { 3, 0, 0, 0, 0 }, 0, Skill: 0);
        Assert.True(BoostPricing.CrashCourseAvailable(run, ctx));
        run.SkillLevelsBoughtThisLoop[0] = 2;
        Assert.False(BoostPricing.CrashCourseAvailable(run, ctx));
        var nine = new BoostContext(1, false, new[] { 9, 0, 0, 0, 0 }, 0, Skill: 0);
        Assert.False(BoostPricing.CrashCourseAvailable(new RunState(), nine));
        Assert.False(BoostPricing.CrashCourseAvailable(new RunState(), ctx with { Skill = -1 }));
    }

    [Fact]
    public void Crash_course_purchase_through_TryBuy_prices_from_the_run_counter()
    {
        var meta = new MetaState { JunimoPoints = 1000 }; var run = new RunState();
        var farming = new BoostContext(1, false, new[] { 0, 0, 0, 0, 0 }, 0, Skill: 0);
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, farming));
        Assert.Equal(990, meta.JunimoPoints);
        var farming1 = farming with { SkillLevels = new[] { 1, 0, 0, 0, 0 } };
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, farming1));
        Assert.Equal(930, meta.JunimoPoints);          // 0.2 x 100 x 3 = 60
        Assert.Equal(2, run.SkillLevelsBoughtThisLoop[0]);
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, farming1));
        var mining = farming with { Skill = 3 };
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, mining));
        Assert.Equal(840, meta.JunimoPoints);          // 0.2 x 50 x 9 = 90
        Assert.Equal(0, run.ActiveBoosts[^1].Skill + 0 - 3 + 3 - 3 + 3);   // Skill recorded as 3
    }

    [Theory]
    [InlineData(35, 40)] [InlineData(40, 50)] [InlineData(1, 10)] [InlineData(119, 120)]
    public void Elevator_landing_is_the_next_multiple_of_ten(int floor, int landing)
        => Assert.Equal(landing, BoostPricing.ElevatorLanding(floor));

    [Theory]
    [InlineData(10, 15)] [InlineData(20, 35)] [InlineData(120, 235)]
    public void Elevator_pass_is_a_fifth_of_the_keep_row(int landing, long cost)
        => Assert.Equal(cost, BoostPricing.ElevatorPassCost(landing));

    [Fact]
    public void Elevator_pass_unavailable_before_the_mine_and_at_the_bottom()
    {
        Assert.False(BoostPricing.ElevatorPassAvailable(0));
        Assert.True(BoostPricing.ElevatorPassAvailable(5));
        Assert.False(BoostPricing.ElevatorPassAvailable(120));
    }

    [Fact]
    public void Elevator_pass_is_repeatable_and_priced_per_landing()
    {
        var meta = new MetaState { JunimoPoints = 1000 }; var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.ElevatorPass, new BoostContext(1, false, new[] { 0, 0, 0, 0, 0 }, 35)));
        Assert.Equal(1000 - 75, meta.JunimoPoints);    // landing 40: 0.2 x 375
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.ElevatorPass, new BoostContext(1, false, new[] { 0, 0, 0, 0, 0 }, 40)));
        Assert.Equal(2, run.ActiveBoosts.Count);
    }
}
```

(Replace the odd arithmetic assertion in `Crash_course_purchase_through_TryBuy_prices_from_the_run_counter` with `Assert.Equal(3, run.ActiveBoosts[^1].Skill);`.)

Run: `dotnet test tests/TheLongestYear.Tests --filter BoostPricingTests` → FAIL.

- [ ] **Step 2: Expose the two price tables**

In `UpgradeCatalogGenerators.cs` add next to `SkillLevelCosts`:

```csharp
    /// <summary>Keep price for reaching <paramref name="level"/> (1..10); the boost twins read it.</summary>
    public static long SkillKeepCost(int level) => level is >= 1 and <= 10 ? SkillLevelCosts[level] : 0;

    /// <summary>Keep price for the elevator floor (10..120 step 10): 75 JP for floor 10, +100 per 10 floors.</summary>
    public static long ElevatorKeepCost(int floor) => 75 + ((floor - 10) / 10) * 100;
```
and use `ElevatorKeepCost(floor)` in `CarryoverMineElevatorKeeps` in place of the inline formula.

- [ ] **Step 3: `BoostPricing`**

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>Computed prices for the "20% twin" rows (spec 1.5, 1.6; ruling 10: earnable progress only).</summary>
public static class BoostPricing
{
    public const int MaxCrashCoursePerSkill = 2;
    public const int MaxSkillLevel = 10;
    public const int DeepestElevatorFloor = 120;
    private const double TwinFraction = 0.2;

    public static long CostOf(BoostDefinition def, RunState run, BoostContext ctx) => def.Id switch
    {
        BoostId.CrashCourse => ctx.Skill >= 0 && ctx.Skill < ctx.SkillLevels.Count
            ? CrashCourseCost(ctx.SkillLevels[ctx.Skill] + 1, run.SkillLevelsBoughtTotal)
            : 0,
        BoostId.ElevatorPass => ElevatorPassCost(ElevatorLanding(ctx.MineFloor)),
        _ => def.Cost,
    };

    /// <summary>0.2 x keepCost(target) x 3^(boughtSoFar).</summary>
    public static long CrashCourseCost(int targetLevel, int boughtSoFar)
        => (long)Math.Round(TwinFraction * UpgradeCatalogGenerators.SkillKeepCost(targetLevel) * Math.Pow(3, boughtSoFar), MidpointRounding.AwayFromZero);

    public static bool CrashCourseAvailable(RunState run, BoostContext ctx)
    {
        if (ctx.Skill < 0 || ctx.Skill >= ctx.SkillLevels.Count) return false;
        if (run.SkillLevelsBoughtThisLoop.GetValueOrDefault(ctx.Skill) >= MaxCrashCoursePerSkill) return false;
        return ctx.SkillLevels[ctx.Skill] + 1 < MaxSkillLevel;
    }

    public static int ElevatorLanding(int floor) => Math.Min(DeepestElevatorFloor, (floor / 10 + 1) * 10);

    public static long ElevatorPassCost(int landing)
        => (long)Math.Round(TwinFraction * UpgradeCatalogGenerators.ElevatorKeepCost(landing), MidpointRounding.AwayFromZero);

    public static bool ElevatorPassAvailable(int floor) => floor > 0 && floor < DeepestElevatorFloor;
}
```

- [ ] **Step 4: Run tests, commit**

Run: `dotnet test tests/TheLongestYear.Tests` → PASS.

```bash
git add src/TheLongestYear.Core/BoostPricing.cs src/TheLongestYear.Core/UpgradeCatalogGenerators.cs tests/TheLongestYear.Tests/BoostPricingTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.160: Crash Course and Elevator Pass pricing and availability (the 20% twins)"
```

---

### Task 3: Effect stacks, percent discount, reach text (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/ActiveEffectsProvider.cs`
- Modify: `src/TheLongestYear.Core/ShopDiscount.cs`
- Create: `src/TheLongestYear.Core/ReachText.cs`
- Test: `tests/TheLongestYear.Tests/ActiveEffectsProviderTests.cs` (append), `tests/TheLongestYear.Tests/ShopDiscountTests.cs` (append), `tests/TheLongestYear.Tests/ReachTextTests.cs` (create)

**Interfaces:**
- Produces: `ActiveEffectsProvider.AttachBoosts(Func<IEnumerable<string>> activeModifierIds)`, `ActiveEffectsProvider.DetachBoosts()`, `ActiveEffectsProvider.BonusStacks(string id) : int`; `ShopDiscount.ApplyPercent(int price, int percent) : int` (keeps `Apply(price, tier)` delegating to it), `ShopDiscount.PercentForTier(int tier)`, `ShopDiscount.HagglerPercent = 10`; `ReachText.Describe(string? requirement) : string` (i18n keys `reach.<metric>` with `{{key}}`/`{{value}}` tokens, falling back to the raw requirement).

- [ ] **Step 1: Failing tests**

Append to `ActiveEffectsProviderTests.cs` (mirror its existing `RunActivation` setup; every existing test in that file activates the run first, copy that line):

```csharp
    [Fact]
    public void BonusStacks_counts_the_theme_plus_every_bound_boost()
    {
        RunActivation.Activate();   // or whatever the file's existing activation call is
        ActiveEffectsProvider.Set("forage_yield_up", "mines_closed");
        ActiveEffectsProvider.AttachBoosts(() => new[] { "forage_yield_up", "fish_bite_up" });
        try
        {
            Assert.Equal(2, ActiveEffectsProvider.BonusStacks("forage_yield_up"));
            Assert.Equal(1, ActiveEffectsProvider.BonusStacks("fish_bite_up"));
            Assert.Equal(0, ActiveEffectsProvider.BonusStacks("crop_growth_up"));
            Assert.True(ActiveEffectsProvider.ActiveBonus("fish_bite_up"));
        }
        finally { ActiveEffectsProvider.DetachBoosts(); ActiveEffectsProvider.Clear(); }
    }
```

Append to `ShopDiscountTests.cs`:

```csharp
    [Fact]
    public void Percent_form_adds_haggler_on_top_of_the_chain_and_floors_at_one()
    {
        Assert.Equal(25, ShopDiscount.PercentForTier(5));
        Assert.Equal(65, ShopDiscount.ApplyPercent(100, 25 + ShopDiscount.HagglerPercent));
        Assert.Equal(1, ShopDiscount.ApplyPercent(1, 35));
        Assert.Equal(100, ShopDiscount.ApplyPercent(100, 0));
        Assert.Equal(ShopDiscount.Apply(200, 3), ShopDiscount.ApplyPercent(200, 15));
    }
```

`ReachTextTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ReachTextTests
{
    public ReachTextTests() { I18nFixture.Load(); }   // use the file's real fixture entry point

    [Theory]
    [InlineData("skill:farming:3", "Farming 3")]
    [InlineData("building:Stable", "a Stable")]
    [InlineData("mine:40", "floor 40")]
    [InlineData("tool:watering_can:2", "Steel Watering Can")]
    public void Describe_names_the_requirement_in_words(string requirement, string contains)
        => Assert.Contains(contains, ReachText.Describe(requirement));

    [Fact]
    public void Empty_requirement_is_empty_text() => Assert.Equal("", ReachText.Describe(null));
}
```

- [ ] **Step 2: Implement**

`ActiveEffectsProvider.cs`, add:

```csharp
    private static Func<IEnumerable<string>>? _boosts;

    /// <summary>Boost source: the modifier ids of every active boost, one per entry (spec 1.4).</summary>
    public static void AttachBoosts(Func<IEnumerable<string>> activeModifierIds) => _boosts = activeModifierIds;
    public static void DetachBoosts() => _boosts = null;

    /// <summary>Independent rolls a patch should make for <paramref name="id"/>: 1 for the week's
    /// theme bonus plus 1 per active boost bound to it. 0 when the run is inactive.</summary>
    public static int BonusStacks(string id)
    {
        if (!RunActivation.IsActive) return 0;
        int n = _bonusId != null && _bonusId == id ? 1 : 0;
        if (_boosts != null)
            foreach (string m in _boosts()) if (m == id) n++;
        return n;
    }

    public static bool ActiveBonus(string id) => BonusStacks(id) > 0;
```
(remove the old one-line `ActiveBonus`; add `using System; using System.Collections.Generic;`.)

`ShopDiscount.cs`:

```csharp
    public const int HagglerPercent = 10;
    public static int PercentForTier(int tier) => Math.Clamp(tier, 0, MaxTier) * 5;

    public static int Apply(int price, int tier) => ApplyPercent(price, PercentForTier(tier));

    /// <summary>Take <paramref name="percent"/> off; non-positive prices untouched; never below 1g.</summary>
    public static int ApplyPercent(int price, int percent)
    {
        if (price <= 0 || percent <= 0) return price;
        int discounted = (int)Math.Round(price * (1.0 - percent / 100.0), MidpointRounding.AwayFromZero);
        return discounted < 1 ? 1 : discounted;
    }
```
(delete `PerTier`.)

`ReachText.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>A RunReachRequirement in the player's words for the Plan tab's Locked section.
/// Keys reach.<metric>; tokens key and value. Unknown metric: the raw requirement.</summary>
public static class ReachText
{
    private static readonly IReadOnlyDictionary<string, string> ToolNames = new Dictionary<string, string>
    {
        ["hoe"] = "Hoe", ["pickaxe"] = "Pickaxe", ["axe"] = "Axe", ["watering_can"] = "Watering Can",
    };
    private static readonly string[] ToolTiers = { "", "Copper", "Steel", "Gold", "Iridium" };

    public static string Describe(string? requirement)
    {
        RunReachRequirement? r = RunReachRequirement.Parse(requirement ?? "");
        if (r == null) return "";
        var tokens = new Dictionary<string, string> { ["key"] = r.Key ?? "", ["value"] = r.Threshold.ToString() };
        switch (r.Metric)
        {
            case "skill":
                tokens["key"] = Capitalise(r.Key);
                break;
            case "tool":
                tokens["key"] = (r.Threshold >= 1 && r.Threshold < ToolTiers.Length ? ToolTiers[r.Threshold] + " " : "")
                    + (ToolNames.TryGetValue(r.Key ?? "", out string? n) ? n : r.Key ?? "");
                break;
        }
        string key = "reach." + r.Metric;
        string text = Strings.Get(key, tokens);
        return text == key ? requirement ?? "" : text;
    }

    private static string Capitalise(string? s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
```

i18n (add to `default.json`, one per metric in `RunReachEvaluator.Meets`):

```json
    "reach.skill": "needs {{key}} {{value}} this loop",
    "reach.tool": "needs a {{key}} this loop",
    "reach.rod": "needs a better rod this loop",
    "reach.backpack": "needs the backpack upgrade this loop",
    "reach.mine": "needs floor {{value}} this loop",
    "reach.mastery": "needs {{value}} Mastery this loop",
    "reach.book": "needs that book read this loop",
    "reach.mail": "needs that unlock this loop",
    "reach.event": "needs that scene this loop",
    "reach.stardrop_mines": "needs the mines' Stardrop this loop",
    "reach.scythe": "needs the Golden Scythe this loop",
    "reach.building": "needs a {{key}} this loop",
    "reach.house": "needs house upgrade {{value}} this loop",
    "reach.pet": "needs a pet this loop",
    "reach.shortcuts": "needs Robin's shortcuts this loop",
    "reach.bus": "needs {{value}} vault bundles paid this loop",
```

- [ ] **Step 3: Run tests, commit**

Run: `dotnet test tests/TheLongestYear.Tests` → PASS.

```bash
git add src/TheLongestYear.Core/ActiveEffectsProvider.cs src/TheLongestYear.Core/ShopDiscount.cs src/TheLongestYear.Core/ReachText.cs src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/ActiveEffectsProviderTests.cs tests/TheLongestYear.Tests/ShopDiscountTests.cs tests/TheLongestYear.Tests/ReachTextTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.161: BonusStacks (theme + boosts), percent shop discount, ReachText"
```

---

### Task 4: Effects in the game (mod side)

**Files:**
- Modify: `src/TheLongestYear/Loop/ForageYieldPatch.cs:41,58`, `FishBiteRatePatch.cs:28-29`, `CropGrowthPatch.cs:79-80`, `MineDropsPatch.cs:63-74`, `AllDropsPatch.cs:41-45`, `TerrainBonusPatches.cs:214-215`
- Modify: `src/TheLongestYear/Loop/PassiveBonusPatches.cs:41-50` (Haggler)
- Create: `src/TheLongestYear/Loop/BoostPatches.cs` (Fast Friends, Second Wind)
- Create: `src/TheLongestYear/Loop/BoostEffectsService.cs` (DayStarted: prune, luck, buffs, weather forecast override)
- Modify: `src/TheLongestYear/Donations/BoostPurchaseService.cs` (apply immediate effects)
- Modify: `src/TheLongestYear/Integration/RunReachEvaluator.cs:120-128` (earned level)
- Modify: `src/TheLongestYear/ModEntry.cs` (wiring at 440-441 and 606-607, DayStarted, SaveLoaded migration, return-to-title, debug commands 1249-1257 and 3724-3741)
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs:116-131` (forecast override read)

**Interfaces:**
- Consumes: Task 1 to 3 types.
- Produces: `BoostEffectsService(IMonitor, MetaStore)` with `OnDayStarted()`, `ApplyDailyBuffs()`, `static bool SecondWindTonight`, `static bool FastFriendsActive`, `static bool HagglerActive` (read-through funcs set by `ModEntry`); `BoostPurchaseService.TryBuy(BoostId, int skill = -1)` applies weather / XP / floor on Success.

- [ ] **Step 1: Stack loops in the five reuse patches**

`ForageYieldPatch.cs` line 41 and 58: keep the early-return but on `BonusStacks(...) == 0`; where the single roll happens (the +1 roll per picked forage, further down the Postfix; find `Game1.random.NextDouble()` in that method) wrap it:
```csharp
int stacks = ActiveEffectsProvider.BonusStacks("forage_yield_up");
for (int s = 0; s < stacks; s++)
{
    if (Game1.random.NextDouble() >= RollChance) continue;   // the file's existing chance constant
    // existing +1 body
}
```
`FishBiteRatePatch.cs:28-29`:
```csharp
            for (int s = ActiveEffectsProvider.BonusStacks("fish_bite_up"); s > 0; s--)
                __result *= 0.70f;  // 30% sooner per stack (theme + Feeding Frenzy)
```
`CropGrowthPatch.cs:79-80`: replace the two lines with
```csharp
            int stacks = ActiveEffectsProvider.BonusStacks("crop_growth_up");
            if (stacks == 0) return;
            bool hit = false;
            for (int s = 0; s < stacks && !hit; s++) hit = Game1.random.NextDouble() < RollChance;
            if (!hit) return;
```
`MineDropsPatch.cs:63-74`: replace the `bool mineBonus / allBonus` pair and the single roll with
```csharp
            int mineStacks = ActiveEffectsProvider.BonusStacks("mine_drops_up");
            int allStacks  = ActiveEffectsProvider.BonusStacks("all_drops_up");
            if (mineStacks == 0 && allStacks == 0) return;
            string firingBonus = mineStacks > 0 ? "mine_drops_up" : "all_drops_up";
            double threshold = mineStacks > 0 ? 0.20 : BonusDropResolver.MixedAllDropsChance;
            int rolls = mineStacks > 0 ? mineStacks : allStacks;
            double roll = 1.0;
            for (int s = 0; s < rolls; s++) roll = System.Math.Min(roll, Game1.random.NextDouble());
            if (roll >= threshold)
```
(the rest of the method continues to use `roll`, `threshold`, `firingBonus`; update the debug log at lines 54-55 to print the two stack counts.)
`AllDropsPatch.cs:41-45` and `TerrainBonusPatches.cs:214-215`: same shape as CropGrowth (`stacks`, `hit` loop against `BonusDropResolver.MixedAllDropsChance`).

- [ ] **Step 2: Haggler in `ShopDiscountPatch`**

`PassiveBonusPatches.cs:41-50`:
```csharp
        int tier = UpgradeChecker.GetTier("shop_discount", ShopDiscount.MaxTier);
        int percent = ShopDiscount.PercentForTier(tier) + (BoostEffectsService.HagglerActive?.Invoke() == true ? ShopDiscount.HagglerPercent : 0);
        if (percent == 0) return;
        ...
            int discounted = ShopDiscount.ApplyPercent(info.Price, percent);
```

- [ ] **Step 3: `BoostPatches.cs`**

```csharp
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>Fast Friends (spec 2.8): friendship gains x1.5 while active. Runs before vanilla's
    /// own Book_Friendship 1.1, so the two compound.</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.changeFriendship))]
    internal static class FastFriendsPatch
    {
        private const double Factor = 1.5;
        private static void Prefix(ref int amount)
        {
            if (amount <= 0) return;
            if (BoostEffectsService.FastFriendsActive?.Invoke() != true) return;
            amount = (int)System.Math.Ceiling(amount * Factor);
        }
    }

    /// <summary>Second Wind (spec 2.5): the sleep that ends the day it was bought on costs no
    /// stamina and leaves no Exhausted status. Farmer.dayupdate(int timeWentToSleep) holds the
    /// penalty block (Farmer.cs:3520-3545): clear exhausted first, and make both bed-time reads
    /// say 2400 so the late-sleep deduction and the 2700 halving never fire. Nothing else in
    /// dayupdate reads those two values before the block.</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.dayupdate))]
    internal static class SecondWindPatch
    {
        private static void Prefix(Farmer __instance, ref int timeWentToSleep)
        {
            if (BoostEffectsService.SecondWindTonight?.Invoke() != true) return;
            __instance.exhausted.Value = false;
            __instance.timeWentToBed.Value = 0;
            if (timeWentToSleep > 2400) timeWentToSleep = 2400;
            PatchLog.Info("Second Wind: late-sleep penalty and Exhausted skipped for tonight.");
        }
    }
}
```
(Harmony discovery is per class in `ModEntry.cs:183-205`; add both classes to that list the way the other patches are registered.)

- [ ] **Step 4: `BoostEffectsService.cs`**

```csharp
using System;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Daily side of the boosts (spec 2.4, 2.6): prune expired entries, set the lucky day,
    /// re-apply the endless buffs vanilla clears at sleep. Static read-throughs feed the patches.</summary>
    internal sealed class BoostEffectsService
    {
        public const string QuickFeetBuffId = "sonofskywalker3.TheLongestYear/QuickFeet";
        public const string IronLungsBuffId = "sonofskywalker3.TheLongestYear/IronLungs";
        public const float QuickFeetSpeed = 1f;
        public const float IronLungsStamina = 50f;
        public const double LuckyDay = 0.10;

        public static Func<bool> SecondWindTonight;
        public static Func<bool> FastFriendsActive;
        public static Func<bool> HagglerActive;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;

        public BoostEffectsService(IMonitor monitor, MetaStore store) { _monitor = monitor; _store = store; }

        private int Today => Calendar.DayOfYear((int)_store.Run.Season, _store.Run.DayOfMonth);

        public bool Active(BoostId id) => BoostState.IsActive(_store.Run, id, Today);

        public void OnDayStarted()
        {
            if (!RunActivation.IsActive) return;
            int pruned = BoostState.Prune(_store.Run, Today);
            if (pruned > 0) _monitor.Log($"Boosts: {pruned} expired entr{(pruned == 1 ? "y" : "ies")} pruned.", LogLevel.Trace);

            if (Active(BoostId.FortunesFavor) && Game1.IsMasterGame)
            {
                Game1.player.team.sharedDailyLuck.Value = LuckyDay;
                _monitor.Log("Fortune's Favor: daily luck set to +0.10.", LogLevel.Info);
            }
            ApplyDailyBuffs();
        }

        /// <summary>Vanilla clears buffs at sleep; endless buffs with stable ids replace rather than stack.</summary>
        public void ApplyDailyBuffs()
        {
            Farmer p = Game1.player;
            if (p == null) return;
            if (Active(BoostId.QuickFeet))
                p.applyBuff(new Buff(QuickFeetBuffId, source: "The Longest Year", displaySource: Strings.Get("boost.quick_feet.name"),
                    duration: Buff.ENDLESS, effects: new BuffEffects { Speed = { QuickFeetSpeed } },
                    displayName: Strings.Get("boost.quick_feet.name")));
            else p.buffs.Remove(QuickFeetBuffId);
            if (Active(BoostId.IronLungs))
                p.applyBuff(new Buff(IronLungsBuffId, source: "The Longest Year", displaySource: Strings.Get("boost.iron_lungs.name"),
                    duration: Buff.ENDLESS, effects: new BuffEffects { MaxStamina = { IronLungsStamina } },
                    displayName: Strings.Get("boost.iron_lungs.name")));
            else p.buffs.Remove(IronLungsBuffId);
        }

        /// <summary>Rain Dance / Storm Call: write tomorrow's weather the way WeatherScheduleWriterPatch does.</summary>
        public static void WriteTomorrow(string weather, IMonitor monitor)
        {
            Game1.weatherForTomorrow = weather;
            Game1.netWorldState.Value.WeatherForTomorrow = weather;
            Game1.netWorldState.Value.GetWeatherForLocation("Default").WeatherForTomorrow = weather;
            monitor.Log($"Boost: tomorrow's weather set to {weather}.", LogLevel.Info);
        }
    }
}
```
(`p.buffs.Remove(id)` exists on `BuffManager` in 1.6; if the Android build lacks it, guard with `p.buffs.IsApplied(id)` first.)

- [ ] **Step 5: Apply immediate effects in `BoostPurchaseService`**

After a `Success` in `TryBuy`:
```csharp
            if (result == BoostPurchase.Result.Success)
                ApplyImmediate(id, ctx);
```
```csharp
        private void ApplyImmediate(BoostId id, BoostContext ctx)
        {
            Farmer p = Game1.player;
            switch (id)
            {
                case BoostId.RainDance: BoostEffectsService.WriteTomorrow(BoostPurchase.Rain, _monitor); break;
                case BoostId.StormCall: BoostEffectsService.WriteTomorrow(BoostPurchase.Storm, _monitor); break;
                case BoostId.CrashCourse:
                {
                    int current = ctx.SkillLevels[ctx.Skill];
                    int width = Farmer.getBaseExperienceForLevel(current + 1) - Farmer.getBaseExperienceForLevel(current);
                    if (current == 0) width = Farmer.getBaseExperienceForLevel(1);
                    p.gainExperience(ctx.Skill, width);
                    _monitor.Log($"Crash Course: +{width} XP in skill {ctx.Skill} (level {current} to {current + 1} at bedtime).", LogLevel.Info);
                    break;
                }
                case BoostId.ElevatorPass:
                {
                    int landing = BoostPricing.ElevatorLanding(ctx.MineFloor);
                    Game1.netWorldState.Value.LowestMineLevel = landing;
                    Game1.netWorldState.Value.LowestMineLevelForOrder = landing;
                    p.deepestMineLevel = System.Math.Max(p.deepestMineLevel, landing);
                    _monitor.Log($"Elevator Pass: elevator now reaches floor {landing}.", LogLevel.Info);
                    break;
                }
                case BoostId.QuickFeet:
                case BoostId.IronLungs:
                    _effects?.ApplyDailyBuffs();
                    break;
            }
        }
```
Give the service a `BoostEffectsService _effects` set through a new ctor parameter. Note: `getBaseExperienceForLevel(0)` returns -1, hence the level-0 special case. Vanilla's skill index order for `gainExperience` is Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4, the same as `BoostContext.SkillLevels`.

- [ ] **Step 6: Earned level, wiring, migration, debug**

`RunReachEvaluator.cs` `SkillLevel`: subtract bought levels:
```csharp
        private static int SkillLevel(Farmer p, string name)
        {
            int index = name switch { "farming" => 0, "fishing" => 1, "foraging" => 2, "mining" => 3, "combat" => 4, _ => -1 };
            int current = name switch { "farming" => p.farmingLevel.Value, "mining" => p.miningLevel.Value, "foraging" => p.foragingLevel.Value, "fishing" => p.fishingLevel.Value, "combat" => p.combatLevel.Value, _ => 0 };
            int bought = index >= 0 ? (_runState?.Invoke()?.SkillLevelsBoughtThisLoop.GetValueOrDefault(index) ?? 0) : 0;
            return current - bought;   // a bought level is never keepable (spec 1.5)
        }
```
`ModEntry`:
- Field `private BoostEffectsService _boostEffects;`. At 606-607: `_boostEffects = new BoostEffectsService(this.Monitor, _meta); _boostPurchases = new BoostPurchaseService(this.Monitor, _meta, _boostEffects);` and
```csharp
            BoostEffectsService.SecondWindTonight = () => _boostEffects.Active(BoostId.SecondWind);
            BoostEffectsService.FastFriendsActive = () => _boostEffects.Active(BoostId.FastFriends);
            BoostEffectsService.HagglerActive = () => _boostEffects.Active(BoostId.Haggler);
            ActiveEffectsProvider.AttachBoosts(() => BoostState.ActiveModifierIds(_meta.Run, TodayDayOfYear()));
```
- Where the run is loaded (the SaveLoaded path that logs "Run N ready", `RunController.cs:163`): call `BoostState.MigrateLegacy(Run, Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth))` before that log and log "migrated legacy boost flags" when the list grew. Then `_boostEffects.ApplyDailyBuffs()` from `ModEntry`'s SaveLoaded after the run is ready.
- `OnDayStarted` (`ModEntry.cs:1750`): `_boostEffects?.OnDayStarted();` first thing after the world-ready guard.
- Return to title (654-655): null the three funcs and `ActiveEffectsProvider.DetachBoosts()`.
- `CmdBoost`: parse any `BoostId` name case-insensitively (`Enum.TryParse(args[0], true, out BoostId id)`), optional second arg skill name → index; `tly_boost list` prints every row with `BoostPurchase.StateOf` and `BoostPricing.CostOf`.
- `CmdActiveEffects`: append every `ActiveBoost` (`Id`, `BoughtDay`, `ExpiresAfterDay`, `Skill`) and `BonusStacks` for the eight bonus ids.
- New `tly_boostexpire`: `_boostEffects.OnDayStarted()`.
- `ShrinePreviewMenu.BuildForesight`: after `_weatherDays` is built, if `_run?.WeatherOverrideDay == tomorrowDayOfYear` replace slot 0's `Weather` with `_run.WeatherOverride` (`ForecastDay` is a record struct: `_weatherDays[0] = _weatherDays[0] with { Weather = _run.WeatherOverride }`).

- [ ] **Step 7: Build, deploy, live check, commit**

Build + deploy: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`. Over the bridge on the throwaway save: `tly_addjp 2000`, `tly_boost overgrowth`, `tly_boost haggler`, `tly_boost fortunesfavor`, `tly_boost crashcourse farming`, `tly_boost elevatorpass`, `tly_activeeffects` (expect 5 entries, forage stacks 1), `debug sleep`, `tly_activeeffects` (luck line "Fortune's Favor: daily luck set", Crash Course level-up queued), `tly_setday 8` + `debug sleep` (Overgrowth pruned, Haggler still there). No ERROR lines.

```bash
git add src/TheLongestYear/Loop/ForageYieldPatch.cs src/TheLongestYear/Loop/FishBiteRatePatch.cs src/TheLongestYear/Loop/CropGrowthPatch.cs src/TheLongestYear/Loop/MineDropsPatch.cs src/TheLongestYear/Loop/AllDropsPatch.cs src/TheLongestYear/Loop/TerrainBonusPatches.cs src/TheLongestYear/Loop/PassiveBonusPatches.cs src/TheLongestYear/Loop/BoostPatches.cs src/TheLongestYear/Loop/BoostEffectsService.cs src/TheLongestYear/Donations/BoostPurchaseService.cs src/TheLongestYear/Integration/RunReachEvaluator.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/UI/ShrinePreviewMenu.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.162: boost effects live: stacks, Haggler, Fast Friends, Second Wind, lucky day, buffs, weather, Crash Course, Elevator Pass"
```

---

### Task 5: The three-tab shrine

**Files:**
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs` (tabs, per-tab row builders, Locked section, Crash Course picker, host-only Boosts)
- Modify: `src/TheLongestYear/i18n/default.json`

**Interfaces:**
- Consumes: `BoostCatalog.All`, `BoostDefinition.Duration`, `BoostPurchase.StateOf`, `BoostPricing.CostOf`, `BoostPricing.ElevatorLanding`, `BoostState`, `ReachText.Describe`, `KeepShopFilter`, `ThemeModifiers.DisplayNameFor`, `ActiveEffectsProvider.BonusId/LiabilityId/LiabilitySuppressed`, `BoostContextBuilder.Build(run, skill)`, `_buyBoost` widened to `Func<BoostId, int, BoostPurchase.Result>` (id, skill).

- [ ] **Step 1: Tab strip**

Constants: `TabIdBase = 6200`, `TabWidth = 220`, `TabHeight = 52`, `TabsTop = 104` (the foresight/list top moves down by `TabHeight + 12` on every tab). `enum ShrineTab { Active, Boosts, Plan }`, `_tab = ShrineTab.Active`. Build three `ClickableTextureComponent`s in `RecomputeBoundsAndLayout` at `xPositionOnScreen + 40 + i * (TabWidth + 8)`, `yPositionOnScreen + TabsTop`, ids `TabIdBase + i`, left/right neighbours chained, `downNeighborID = RowIdBase`. Draw them exactly as `JunimoShrineMenu.draw` draws its tabs (`Game1.menuTexture` box `(0,256,60,60)`, active `Color.White`, others `* 0.7f`, label `Strings.Get("shrine.tab.active|boosts|plan")`). `receiveLeftClick`: a tab hit sets `_tab`, `_scrollIndex = 0`, `BuildRows()`, `Game1.playSound("smallSelect")`. Gamepad: mirror `JunimoShrineMenu.receiveGamePadButton` for the tab ids.

- [ ] **Step 2: Row model and per-tab builders**

Extend `Row`: `public BoostDuration? Group;` (unused), `public string Note;` (right-hand text for Active rows), `public bool IsLockedToggle; public bool IsLockedRow; public string Requirement;`, `public int Skill = -1;` (Crash Course picker sub-rows). `BuildRows()` switches on `_tab`:

Active:
```csharp
_rows.Add(Header(Strings.Get("shrine.active.running")));
int today = TodayDayOfYear();
var running = BoostPurchase.ActiveEntries(_run, today).ToList();
if (running.Count == 0) _rows.Add(Note(Strings.Get("shrine.active.none")));
foreach (ActiveBoost b in running)
{
    BoostId id = Enum.Parse<BoostId>(b.Id);
    BoostDefinition def = BoostCatalog.Get(id);
    string when = b.ExpiresAfterDay >= Calendar.DaysPerYear ? Strings.Get("shrine.active.this-loop")
        : b.ExpiresAfterDay == today ? Strings.Get("shrine.active.tonight")
        : b.ExpiresAfterDay == today + 1 && def.Duration == BoostDuration.Instant ? Strings.Get("shrine.active.tomorrow")
        : Strings.Get("shrine.active.through", new() { ["season"] = SeasonName(Calendar.SeasonOfDay(b.ExpiresAfterDay)), ["day"] = ((b.ExpiresAfterDay - 1) % 28 + 1).ToString() });
    string name = id == BoostId.CrashCourse ? $"{Strings.Get(def.NameKey)} ({SkillName(b.Skill)})" : Strings.Get(def.NameKey);
    _rows.Add(new Row { Text = name, Note = when, Tooltip = Strings.Get(def.DescKey), IsOwned = true });
}
_rows.Add(Header(Strings.Get("shrine.active.this-week")));
string bonus = ActiveEffectsProvider.BonusId, liability = ActiveEffectsProvider.LiabilityId;
_rows.Add(Note(bonus == null ? Strings.Get("shrine.active.no-theme")
    : Strings.Get("shrine.active.theme", new() { ["bonus"] = ThemeModifiers.DisplayNameFor(bonus), ["liability"] = ThemeModifiers.DisplayNameFor(liability) + (ActiveEffectsProvider.LiabilitySuppressed ? " " + Strings.Get("shrine.active.lifted") : "") })));
foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
{
    var owned = KeepShopFilter.OwnedLeavesInCategory(cat, _state, RunReachEvaluator.Meets);
    if (owned.Count == 0) continue;
    _rows.Add(Header(ThemeDisplay.CategoryName(cat)));
    foreach (var def in owned) _rows.Add(new Row { Def = def, IsOwned = true, Tooltip = ... as today ... });
}
```
(`Header(text)` and `Note(text)` are tiny factories; a Note row draws as plain small text and is not hoverable. `SeasonName` uses the existing `ThemeDisplay`/`Strings` season keys; `SkillName(i)` maps 0..4 to `Strings.Get("skill.<slug>")` if those keys exist, else the English names.)

Boosts:
```csharp
if (!Context.IsMainPlayer) { _rows.Add(Note(Strings.Get("shrine.boosts.host-only"))); return; }
foreach (BoostDuration d in new[] { BoostDuration.Instant, BoostDuration.Week, BoostDuration.Season, BoostDuration.Loop })
{
    _rows.Add(Header(Strings.Get("shrine.boosts.group." + d.ToString().ToLowerInvariant())));
    foreach (BoostDefinition boost in BoostCatalog.All.Where(b => b.Duration == d))
    {
        _rows.Add(new Row { Boost = boost, Tooltip = Strings.Get(boost.DescKey) });
        if (boost.Id == BoostId.CrashCourse)
            for (int skill = 0; skill < 5; skill++)
                _rows.Add(new Row { Boost = boost, Skill = skill, Tooltip = Strings.Get(boost.DescKey) });
    }
}
```
A Crash Course sub-row draws indented: "Farming 3 to 4" with the price from `BoostPricing.CostOf(def, _run, BoostContextBuilder.Build(_run, skill))` and its own Buy button (state from `BoostPurchase.StateOf(..., ctx with Skill)`); the parent Crash Course row draws name only, no button. The Elevator Pass row draws "Floor 35 to 40" from `BoostContextBuilder.Build(_run).MineFloor` and `BoostPricing.ElevatorLanding`. `DrawBoostRow` takes the row (not just the definition) and reads `row.Skill`; `BoostButtonBounds` unchanged; the click loop calls `_buyBoost(row.Boost.Id, row.Skill)`.

Plan: today's `BuildRows` loop without the owned rows, and after each category's buyable rows:
```csharp
var locked = UpgradeCatalog.ByCategory(cat)
    .Where(d => !_state.HasUpgrade(d.Id) && (d.PrerequisiteId == null || _state.HasUpgrade(d.PrerequisiteId))
             && _state.MeetsMetaRequirement(d.MetaRequirement)
             && d.RunReachRequirement != null && !RunReachEvaluator.Meets(d.RunReachRequirement)).ToList();
if (locked.Count > 0)
{
    _rows.Add(new Row { IsLockedToggle = true, Text = Strings.Get("shrine.plan.locked", new() { ["count"] = locked.Count.ToString() }), Category = cat });
    if (_expandedLocked.Contains(cat))
        foreach (var d in locked)
            _rows.Add(new Row { Def = d, IsLockedRow = true, Requirement = ReachText.Describe(d.RunReachRequirement), Tooltip = d.Description });
}
```
`_expandedLocked : HashSet<UpgradeCategory>`; clicking a toggle row flips membership and rebuilds. Locked rows draw the name in `Color.Gray` and the requirement right-aligned in brown. The foresight panel is drawn only on Plan (`DrawForesight` and `LayoutForesight` guarded by `_tab == ShrineTab.Plan`; the list top uses `ForesightPanelHeight()` only on Plan).

- [ ] **Step 3: i18n**

```json
    "shrine.tab.active": "Active",
    "shrine.tab.boosts": "Boosts",
    "shrine.tab.plan": "Plan",
    "shrine.active.running": "Running boosts",
    "shrine.active.none": "Nothing running. The Boosts tab sells this-loop edges for JP.",
    "shrine.active.tonight": "expires tonight",
    "shrine.active.tomorrow": "tomorrow",
    "shrine.active.through": "through {{season}} {{day}}",
    "shrine.active.this-loop": "this loop",
    "shrine.active.this-week": "This week",
    "shrine.active.theme": "Bonus: {{bonus}}. Liability: {{liability}}.",
    "shrine.active.lifted": "(lifted)",
    "shrine.active.no-theme": "No theme picked yet this week.",
    "shrine.boosts.group.instant": "Instant (tonight or tomorrow)",
    "shrine.boosts.group.week": "This week",
    "shrine.boosts.group.season": "This season",
    "shrine.boosts.group.loop": "This loop",
    "shrine.boosts.host-only": "The host buys boosts for the farm.",
    "shrine.boosts.crash-course-row": "{{skill}} {{from}} to {{to}}",
    "shrine.boosts.elevator-row": "Floor {{from}} to {{to}}",
    "shrine.plan.locked": "Locked ({{count}})",
```
Update `shrine.boosts.header` usage (it is no longer drawn; delete the key so `NoOrphanKeys` stays green, or keep it if still referenced).

- [ ] **Step 4: Build, deploy, live check, commit**

`ShrinePreviewMenu` is opened by the `tly_openshop`-style command? No: it opens from the shrine furniture. For a headless check use `tly_boost list` for the row states and the log; the draw path needs one human look (STATUS already owes Jeff that). Build + deploy, open the shrine with `tools/game.ps1` ONLY if Jeff has said yes this session; otherwise leave the visual check to him and say so.

```bash
git add src/TheLongestYear/UI/ShrinePreviewMenu.cs src/TheLongestYear/UI/PlanningShrineService.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json
git commit -m "v0.16.163: planning shrine gets Active / Boosts / Plan tabs, Locked section, Crash Course picker"
```

---

### Task 6: Docs

**Files:**
- Modify: `TODO.md` (mark the "APPROVED FOR SPEC" entry built, note the two deferred rows), `STATUS.md` (header + a section), `CHANGELOG.md` (Unreleased: Added), `README.md` + `release-notes/` Nexus description (a Boosts feature paragraph, both in step per the workspace rule)

- [ ] **Step 1: Write the entries**

CHANGELOG Unreleased / Added: "JP Boosts: fifteen this-loop buys at the planning shrine (weather, luck, sleep, stacking bonuses, shop discount, friendship, stamina, speed, skill levels, elevator floors), on a new Boosts tab; the shrine also gains Active and Plan tabs and a Locked section that says what each keep needs." README + Nexus description: one feature bullet with the same sentence, plus the What's New line.

- [ ] **Step 2: Commit**

```bash
git add TODO.md STATUS.md CHANGELOG.md README.md release-notes/
git commit -m "docs: JP Boosts and shrine tabs built; Night Owl and Backpack Organizer deferred"
```

## Self-review notes

Spec coverage: 1.1 catalog (T1), 1.2 record and expiry (T1), 1.3 purchase (T1), 1.4 stacks (T3, T4), 1.5 Crash Course (T2 price, T4 XP, T4 earned level), 1.6 Elevator Pass (T2, T4), 2.1 to 2.10 effects (T4), 3 menu (T5), 4 diagnostics (T4), 5 tests (T1 to T3), multiplayer (T5). Ruling 9's "verify the profession requeue at reset" is a live check in T4 step 7: after `tly_boost crashcourse farming` at level 4 (lands 5, pick a profession at bedtime), `tly_reset` with no `keep_farming_level_5` owned must log a farming level below 5 and no profession requeue for farming.
