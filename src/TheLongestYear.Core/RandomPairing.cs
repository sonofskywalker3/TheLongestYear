using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Spec section 3: a card keeps its theme's buff and draws a random drawback, never one that
/// blocks the theme's own goals.</summary>
public static class RandomPairing
{
    private const int Salt = 0x2B9D;

    public static readonly IReadOnlyList<string> AllLiabilities = new[]
    {
        "mines_closed", "fish_bite_down", "crop_growth_down", "forage_off",
        "all_sell_prices_down", "machines_slow", "cooked_food_weak", "monster_damage_up",
    };

    private static readonly IReadOnlySet<string> None = new HashSet<string>();
    private static readonly IReadOnlySet<string> NoForage = new HashSet<string> { "forage_off" };
    private static readonly IReadOnlySet<string> NoCropSlow = new HashSet<string> { "crop_growth_down" };
    private static readonly IReadOnlySet<string> NoFishSlow = new HashSet<string> { "fish_bite_down" };
    private static readonly IReadOnlySet<string> NoMinesClosed = new HashSet<string> { "mines_closed" };
    private static readonly IReadOnlySet<string> NoMachinesSlow = new HashSet<string> { "machines_slow" };

    public static IReadOnlySet<string> ExcludedFor(Theme t) => t switch
    {
        Theme.Foraging => NoForage,
        Theme.Farming => NoCropSlow,
        Theme.Fishing => NoFishSlow,
        Theme.Mining or Theme.Spelunking => NoMinesClosed,
        Theme.Artisan => NoMachinesSlow,
        _ => None,
    };

    /// <param name="otherCard">Double theme week only: the week's other card. The drawback that blocks
    /// its goals is excluded too, so neither card's drawback works against the other card's list.
    /// Null on a single week, which keeps the draw exactly as before.</param>
    public static string LiabilityFor(int seed, int weekOfYear, Theme theme, bool random, Theme? otherCard = null)
    {
        string own = ThemeModifiers.For(theme).LiabilityId;
        if (!random) return own;
        IReadOnlySet<string> excluded = ExcludedFor(theme);
        IReadOnlySet<string> excludedByOther = otherCard.HasValue ? ExcludedFor(otherCard.Value) : None;
        var allowed = AllLiabilities.Where(id => !excluded.Contains(id) && !excludedByOther.Contains(id)).ToList();
        // RollSeed, not a xor of related seeds: two cards of one week must draw independently (final review I1).
        var rng = RollSeed.Rng(seed, weekOfYear, Salt, (int)theme);
        return allowed[rng.Next(allowed.Count)];
    }

    /// <summary>The draw above, checked against the week's sampled goal lines: a drawback that
    /// hard-blocks any of them (<see cref="DrawbackConflicts"/>: mines_closed against a mine-only
    /// item, forage_off against a forage-only item) is redrawn from the drawbacks that block none,
    /// with the same seeded stream, so the same seed still gives the same card. If every allowed
    /// drawback blocks something, a soft drawback is drawn instead. A week whose first draw blocks
    /// nothing gets exactly the drawback it always did.</summary>
    /// <param name="goalItemIds">Every goal line the drawback would sit beside: this card's list,
    /// plus the other card's on a double week, since both drawbacks run all week.</param>
    /// <param name="profileOf">Source profile per item id (<see cref="ItemSourceIndex.For"/>).</param>
    public static string LiabilityFor(int seed, int weekOfYear, Theme theme, bool random, Theme? otherCard,
        IEnumerable<string>? goalItemIds, Func<string, ItemSourceProfile>? profileOf)
    {
        string own = ThemeModifiers.For(theme).LiabilityId;
        if (!random) return own;
        IReadOnlySet<string> excluded = ExcludedFor(theme);
        IReadOnlySet<string> excludedByOther = otherCard.HasValue ? ExcludedFor(otherCard.Value) : None;
        var allowed = AllLiabilities.Where(id => !excluded.Contains(id) && !excludedByOther.Contains(id)).ToList();
        var rng = RollSeed.Rng(seed, weekOfYear, Salt, (int)theme);
        string drawn = allowed[rng.Next(allowed.Count)];
        var goals = (goalItemIds ?? Array.Empty<string>()).ToList();
        if (!DrawbackConflicts.BlocksAny(drawn, goals, profileOf)) return drawn;

        var clear = allowed.Where(id => id != drawn && !DrawbackConflicts.BlocksAny(id, goals, profileOf)).ToList();
        if (clear.Count > 0) return clear[rng.Next(clear.Count)];
        var soft = allowed.Where(id => !DrawbackConflicts.IsHard(id)).ToList();
        return soft.Count > 0 ? soft[rng.Next(soft.Count)] : own;
    }

    public static (string BonusId, string LiabilityId) EffectsFor(RunState run, Theme theme)
    {
        var own = ThemeModifiers.For(theme);
        return (own.BonusId, run.CurrentLiabilityId ?? own.LiabilityId);
    }

    /// <summary>Double theme week: the second card's effects, from its own stored drawback.</summary>
    public static (string BonusId, string LiabilityId) SecondEffectsFor(RunState run, Theme theme)
    {
        var own = ThemeModifiers.For(theme);
        return (own.BonusId, run.SecondLiabilityId ?? own.LiabilityId);
    }
}
