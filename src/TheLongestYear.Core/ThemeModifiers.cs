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
        // Mixed: "+10% all drops, −50% all sell prices" — a generalist boost paired with a sharp
        // economic squeeze. (Replaces the prior shop-discount/stamina-drain pairing per playtest.)
        Theme.Mixed    => ("all_drops_up", "all_sell_prices_down"),
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
    };

    /// <summary>
    /// Human-readable display name with the numeric value for a bonus/liability id. Used by the
    /// planning hub UI (Plan 05). The id strings themselves remain stable so the effect layer
    /// (Plan 06) can switch on them. Numbers here are the v1 baselines; Plan 06 will read them
    /// from <see cref="GameplayConfig"/> so they can be tuned without redeploying.
    /// Falls back to the raw id if unmapped (defensive — easy to spot in-game if a new id is missed).
    /// Plain ASCII only (no U+2212 minus sign) — Stardew's smallFont doesn't include the
    /// typographic minus and renders it as tofu.
    /// </summary>
    public static string DisplayNameFor(string modifierId) => modifierId switch
    {
        "forage_yield_up"        => "+25% Foraging Yield",
        "forage_drops_off"       => "Foraging Disabled",
        "crop_growth_up"         => "+25% Crop Growth",
        "crop_growth_down"       => "-25% Crop Growth",
        "fish_bite_up"           => "+30% Fish Bite Rate",
        "mine_drops_up"          => "+30% Mine Drops",
        "mine_drops_off"         => "Mine Drops Disabled",
        "all_drops_up"           => "+10% All Drops",
        "all_sell_prices_down"   => "-50% All Sell Prices",
        // Legacy / unused-in-v1 — kept so old config files don't show raw ids if loaded.
        "shop_discount"          => "-15% Shop Prices",
        "stamina_drain_up"       => "+30% Stamina Drain",
        _ => modifierId
    };
}
