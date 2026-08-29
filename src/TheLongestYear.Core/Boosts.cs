using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Purchasable one-off boosts, banked-JP funded, that grant a temporary edge inside the
/// current run rather than a permanent meta-upgrade. Plan 04 spec 2026-08-28-obtainable-board-4-boosts.</summary>
public enum BoostId
{
    /// <summary>Guarantees a bonus item roll can substitute the following-year seed for the
    /// current season, for the week it's bought (see <see cref="YearTwoSeeds"/>).</summary>
    YearTwoSeeds,

    /// <summary>Reveals the upcoming week's bonus items/theme early, for the rest of the
    /// current season.</summary>
    SneakPeek
}

/// <summary>Static catalog entry for one boost: cost in banked Junimo Points plus i18n keys for
/// its shop name/description (rendered mod-side; Core never touches display strings directly).</summary>
public sealed record BoostDefinition(BoostId Id, long Cost, string NameKey, string DescKey);

/// <summary>The full, fixed set of purchasable boosts. Costs and keys are Jeff's exact values
/// from the task brief; no other source of truth.</summary>
public static class BoostCatalog
{
    public static readonly IReadOnlyList<BoostDefinition> All = new List<BoostDefinition>
    {
        new(BoostId.YearTwoSeeds, 75, "boost.year_two_seeds.name", "boost.year_two_seeds.desc"),
        new(BoostId.SneakPeek, 100, "boost.sneak_peek.name", "boost.sneak_peek.desc")
    };
}

/// <summary>Validates and applies a boost purchase against banked JP and the per-run flags on
/// <see cref="RunState"/>. Core never reads live game state; callers supply the current
/// week-of-year explicitly.</summary>
public static class BoostPurchase
{
    public enum Result
    {
        Success,
        NotEnoughJp,
        AlreadyActive,
        NotAvailable
    }

    /// <summary>Attempt to buy <paramref name="id"/> for the given week-of-year (1-16). Checks
    /// availability (Year-Two Seeds cannot be bought in Winter), then whether the boost is
    /// already active for this week/season, then whether banked JP covers the cost; only on
    /// success does it spend JP and set the run-state flag.</summary>
    public static Result TryBuy(MetaState meta, RunState run, BoostId id, int weekOfYear)
    {
        BoostDefinition definition = Find(id);
        Season season = AvailabilityWeeks.SeasonOf(weekOfYear);

        if (id == BoostId.YearTwoSeeds && season == Season.Winter)
            return Result.NotAvailable;

        bool alreadyActive = id switch
        {
            BoostId.YearTwoSeeds => BoostState.YearTwoSeedsActive(run, weekOfYear),
            BoostId.SneakPeek => BoostState.SneakPeekActive(run, season),
            _ => false
        };
        if (alreadyActive)
            return Result.AlreadyActive;

        if (meta.JunimoPoints < definition.Cost)
            return Result.NotEnoughJp;

        meta.JunimoPoints -= definition.Cost;
        switch (id)
        {
            case BoostId.YearTwoSeeds:
                run.YearTwoSeedsWeek = weekOfYear;
                break;
            case BoostId.SneakPeek:
                run.SneakPeekSeason = (int)season;
                break;
        }
        return Result.Success;
    }

    private static BoostDefinition Find(BoostId id)
    {
        foreach (BoostDefinition definition in BoostCatalog.All)
        {
            if (definition.Id == id)
                return definition;
        }
        throw new KeyNotFoundException($"No boost definition for {id}.");
    }
}

/// <summary>Reads whether a purchased boost is currently in effect, from the flags
/// <see cref="BoostPurchase"/> set on <see cref="RunState"/>.</summary>
public static class BoostState
{
    /// <summary>True only for the exact week-of-year the boost was bought for.</summary>
    public static bool YearTwoSeedsActive(RunState run, int weekOfYear) => run.YearTwoSeedsWeek == weekOfYear;

    /// <summary>True for the rest of the season the boost was bought in.</summary>
    public static bool SneakPeekActive(RunState run, Season season) => run.SneakPeekSeason == (int)season;
}

/// <summary>The Year-Two Seeds boost's own rules: which seed id it can grant per season and the
/// roll chance a caller (mod-side bonus sampler) checks before granting it.</summary>
public static class YearTwoSeeds
{
    /// <summary>Chance, per eligible roll, that a bonus item is substituted with the
    /// following-year seed instead. Jeff's exact value from the task brief.</summary>
    public const double Chance = 0.05;

    /// <summary>The following-year seed item id for a season, or null in Winter (no seed).</summary>
    public static string? SeedIdFor(Season season) => season switch
    {
        Season.Spring => "476",
        Season.Summer => "485",
        Season.Fall => "489",
        _ => null
    };
}
