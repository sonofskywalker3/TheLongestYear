namespace TheLongestYear.Core;

/// <summary>
/// Categories of permanent upgrades sold at the Junimo Shrine (spec §11 + 2026-05-26 expansion).
/// Order matches the spec and is the natural tab order for the shop UI.
/// </summary>
public enum UpgradeCategory
{
    Loadout,        // backpack, starting tools, starting gold, starter seeds/sprinklers
    Carryover,      // % skill XP retained, hearts retained, pre-unlocked recipes
    Efficiency,     // bigger stamina, shop/seed discounts, early horse
    Obtainability,  // Cultivation (crops in Mixed Seeds), Fortune (rare drop boosts)
    Foresight,      // Weather Sage tiers, Cart Whisperer tiers
    Stash,          // Junimo Stash capacity tiers
    Buildings       // Keep Coop / Kitchen / Barn, Start with [animal] (added 2026-05-26)
}
