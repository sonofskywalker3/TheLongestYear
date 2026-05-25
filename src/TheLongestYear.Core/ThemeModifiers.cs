using System;

namespace TheLongestYear.Core;

/// <summary>
/// Maps each theme to the identifiers of its bonus (helps that playstyle) and liability
/// (throttles a different income stream). The gameplay effects keyed by these ids are
/// implemented in the obtainability/effects plan; here they are just stable identifiers.
/// </summary>
public static class ThemeModifiers
{
    public static (string BonusId, string LiabilityId) For(Theme theme) => theme switch
    {
        Theme.Foraging => ("forage_yield_up", "mine_drops_off"),
        Theme.Farming  => ("crop_growth_up", "forage_drops_off"),
        Theme.Fishing  => ("fish_bite_up", "crop_growth_down"),
        Theme.Mining   => ("mine_drops_up", "forage_drops_off"),
        Theme.Mixed    => ("shop_discount", "stamina_drain_up"),
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
    };
}
