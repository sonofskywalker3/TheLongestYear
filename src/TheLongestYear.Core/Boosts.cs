using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>How long a boost runs. Spec 2026-08-29 (shrine tabs + JP Boosts) section 1.2.</summary>
public enum BoostDuration { Instant, Week, Season, Loop }

/// <summary>Every purchasable in-loop boost, in catalog (display) order.</summary>
public enum BoostId
{
    RainDance, StormCall, FortunesFavor, SecondWind,
    Overgrowth, FeedingFrenzy, GrowthSpurt, RichVeins, Windfall, QuickFeet, YearTwoSeeds,
    Haggler, FastFriends, IronLungs, SneakPeek,
    CrashCourse, ElevatorPass,
}

/// <summary>Static catalog entry for one boost. Display strings are i18n keys rendered mod-side.</summary>
/// <param name="Cost">Opening bid in JP; 0 for the computed rows (see BoostPricing).</param>
/// <param name="ModifierId">Theme-modifier id this boost stacks onto (reuse rows); null for new effects.</param>
public sealed record BoostDefinition(
    BoostId Id, long Cost, BoostDuration Duration, string NameKey, string DescKey, string? ModifierId = null);

/// <summary>The full, fixed roster (spec 1.1). Prices are Jeff's opening bids.</summary>
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

/// <summary>Purchase rules (spec 1.3). Pure: JP and the run record only; the mod applies the
/// immediate part (weather write, XP grant, floor write) after a Success.</summary>
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

    public static bool IsWeather(string id) => id == nameof(BoostId.RainDance) || id == nameof(BoostId.StormCall);

    private static bool Available(RunState run, BoostId id, BoostContext ctx) => id switch
    {
        BoostId.RainDance or BoostId.StormCall
            => ctx.Season != Season.Winter && !ctx.TomorrowIsFestival && ctx.DayOfYear < Calendar.DaysPerYear,
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
        string idName = def.Id.ToString();
        if (IsWeather(idName)) return run.ActiveBoosts.Any(b => b.Id == idName && b.IsActiveOn(day + 1));
        foreach (ActiveBoost b in ActiveEntries(run, day))
        {
            if (b.Id == idName) return true;
            if (def.ModifierId != null && Enum.TryParse(b.Id, out BoostId otherId)
                && BoostCatalog.Get(otherId).ModifierId == def.ModifierId) return true;
        }
        return false;
    }
}

/// <summary>Reads of the run record: what is active today, the modifier ids to stack, pruning,
/// and the one-time migration of the plan-4 fields.</summary>
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
            run.ActiveBoosts.Add(new ActiveBoost
            {
                Id = nameof(BoostId.YearTwoSeeds), BoughtDay = weekStart, ExpiresAfterDay = weekStart + Calendar.DaysPerWeek - 1,
            });
            run.YearTwoSeedsWeek = -1;
        }
        if (run.SneakPeekSeason >= 0)
        {
            int seasonStart = run.SneakPeekSeason * Calendar.DaysPerMonth + 1;
            run.ActiveBoosts.Add(new ActiveBoost
            {
                Id = nameof(BoostId.SneakPeek), BoughtDay = seasonStart, ExpiresAfterDay = seasonStart + Calendar.DaysPerMonth - 1,
            });
            run.SneakPeekSeason = -1;
        }
    }
}

/// <summary>Year-Two Seeds facts (unchanged from plan 4): for the week it is bought in, every
/// Mixed Seeds roll has this chance of yielding the season's following-year seed instead.</summary>
public static class YearTwoSeeds
{
    public const double Chance = 0.05;

    public static string? SeedIdFor(Season season) => season switch
    {
        Season.Spring => "476",
        Season.Summer => "485",
        Season.Fall => "489",
        _ => null,
    };
}
