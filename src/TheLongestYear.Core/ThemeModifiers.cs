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
    /// Human-readable description of what a bonus/liability id actually DOES in plain English.
    /// 2026-05-28 playtest feedback: the prior "+30% Mine Drops" style was a number without a
    /// mechanic — the player had to hover the card for the tooltip to learn what it meant.
    /// New phrasing spells out the in-world effect ("30% chance for mined resources to drop
    /// +1", "All foraging items removed") so the planning-hub card stands on its own without
    /// a tooltip. Strings are sized to fit a 460px-wide card with smallFont after
    /// <c>Game1.parseText</c> word-wrapping; keep new entries concise enough to wrap to two
    /// lines max.
    ///
    /// The id strings themselves remain stable so the effect layer (Plan 06) can switch on
    /// them. Numbers here are the v1 baselines; Plan 06 will read them from
    /// <see cref="GameplayConfig"/> so they can be tuned without redeploying.
    /// Falls back to the raw id if unmapped (defensive — easy to spot in-game if a new id is
    /// missed). Plain ASCII only (no U+2212 minus sign) — Stardew's smallFont doesn't include
    /// the typographic minus and renders it as tofu.
    /// </summary>
    public static string DisplayNameFor(string modifierId) => modifierId switch
    {
        "forage_yield_up"        => "25% chance to find an extra foraged item",
        "forage_off"             => "All foraging items removed",
        // crop_growth_* is implemented deterministically via a 2-of-7-days rule in
        // CropGrowthPatch (~28.6% effective). Display rounds back to the spec'd 25% — the
        // exact integer count of skipped/extra days isn't player-facing.
        "crop_growth_up"         => "Crops grow 25% faster",
        "crop_growth_down"       => "Crops grow 25% slower",
        "fish_bite_up"           => "Fish bite 30% sooner",
        "fish_bite_down"         => "Fish bite 30% slower",
        "mine_drops_up"          => "30% chance for mined resources to drop +1",
        // 2026-05-28 playtest round 2: user requested a HARD entrance block — "floor 1 needs
        // to be blocked, floor 0 is accessible." MinesEntranceClosedPatch now intercepts the
        // performAction("Mine"/"NextMineLevel"/"MineElevator") verbs so the player can walk to
        // the mine entrance in Mountain but cannot enter the shaft at all.
        "mines_closed"           => "Mine entrance closed all week",
        "all_drops_up"           => "10% chance for any drop to be +1",
        "all_sell_prices_down"   => "All sell prices cut in half",
        // Legacy / unused-in-v1 -- kept so old config files don't show raw ids if loaded.
        "forage_drops_off"       => "Foraging disabled (legacy)",
        "mine_drops_off"         => "Mine drops disabled (legacy)",
        "shop_discount"          => "Shop prices 15% lower",
        "stamina_drain_up"       => "Tools drain 30% more stamina",
        _ => modifierId
    };
}
