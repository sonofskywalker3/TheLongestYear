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
        Theme.Foraging => ("forage_yield_up", "mines_closed"),
        Theme.Farming  => ("crop_growth_up", "fish_bite_down"),
        Theme.Fishing  => ("fish_bite_up", "crop_growth_down"),
        Theme.Mining   => ("mine_drops_up", "forage_off"),
        // Mixed: "+10% all drops, -50% all sell prices" — a generalist boost paired with a sharp
        // economic squeeze. (Replaces the prior shop-discount/stamina-drain pairing per playtest.)
        Theme.Mixed    => ("all_drops_up", "all_sell_prices_down"),
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
    };

    /// <summary>
    /// Human-readable description of what a bonus/liability id actually DOES in plain English,
    /// looked up from i18n (<c>modifier.&lt;id&gt;</c> keys in <c>i18n/default.json</c>). Falls
    /// back to the raw id if unmapped (defensive — easy to spot in-game if a new id is missed).
    /// The id strings themselves remain stable so the effect layer (Plan 06) can switch on them.
    /// </summary>
    public static string DisplayNameFor(string modifierId)
    {
        string result = Strings.Get($"modifier.{modifierId}");
        return result == $"modifier.{modifierId}" ? modifierId : result;
    }
}
