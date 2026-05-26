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

    /// <summary>
    /// Human-readable display name for a bonus/liability id. Used by the planning hub UI (Plan 05).
    /// The id strings themselves remain stable so the effect layer (Plan 06) can switch on them.
    /// Falls back to the raw id if unmapped (defensive — easy to spot in-game if a new id is missed).
    /// </summary>
    public static string DisplayNameFor(string modifierId) => modifierId switch
    {
        "forage_yield_up" => "Foraging Yield +",
        "forage_drops_off" => "No Forage Drops",
        "crop_growth_up" => "Crop Growth +",
        "crop_growth_down" => "Crop Growth −",
        "fish_bite_up" => "Fish Bite +",
        "mine_drops_up" => "Mine Drops +",
        "mine_drops_off" => "No Mine Drops",
        "shop_discount" => "Shop Discount",
        "stamina_drain_up" => "Stamina Drains Faster",
        _ => modifierId
    };
}
