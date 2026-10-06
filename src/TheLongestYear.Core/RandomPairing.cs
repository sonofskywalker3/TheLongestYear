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

    public static string LiabilityFor(int seed, int weekOfYear, Theme theme, bool random)
    {
        string own = ThemeModifiers.For(theme).LiabilityId;
        if (!random) return own;
        IReadOnlySet<string> excluded = ExcludedFor(theme);
        var allowed = AllLiabilities.Where(id => !excluded.Contains(id)).ToList();
        // RollSeed, not a xor of related seeds: two cards of one week must draw independently (final review I1).
        var rng = RollSeed.Rng(seed, weekOfYear, Salt, (int)theme);
        return allowed[rng.Next(allowed.Count)];
    }

    public static (string BonusId, string LiabilityId) EffectsFor(RunState run, Theme theme)
    {
        var own = ThemeModifiers.For(theme);
        return (own.BonusId, run.CurrentLiabilityId ?? own.LiabilityId);
    }
}
