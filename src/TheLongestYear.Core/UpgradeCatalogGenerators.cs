using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Programmatic generators for the long chained "keep_*" entries in UpgradeCatalog
/// (Phase A persistence design §B). Hand-authoring 80+ entries would be brittle;
/// these generators emit them deterministically. Cost tables baked in here keep the
/// pricing curve documented in one place per chain.
/// </summary>
internal static class UpgradeCatalogGenerators
{
    // Tool tier costs (Copper → Steel → Gold → Iridium). Loose mirror of vanilla
    // gold costs (2k/5k/10k/25k) scaled into the JP economy. Same table used for
    // hoe / pickaxe / axe / watering can — the player pays the same JP for any
    // tool's tier-up keep.
    private static readonly long[] ToolTierCosts = { 150, 300, 525, 875 };

    private static readonly (string IdSlug, string DisplayName)[] ToolKinds =
    {
        ("hoe",          "Hoe"),
        ("pickaxe",      "Pickaxe"),
        ("axe",          "Axe"),
        ("watering_can", "Watering Can"),
    };

    // Tier 1=Copper, 2=Steel, 3=Gold, 4=Iridium. Matches Tool.UpgradeLevel
    // (StardewValley\StardewValley\Tool.cs:167) which is 0=base/rusty, 1=copper, ...
    private static readonly string[] TierNames = { "Copper", "Steel", "Gold", "Iridium" };

    // Fishing rod has just two upgrade tiers worth keeping (training rod is L0,
    // bamboo rod is L1 — the player gets a bamboo rod from Willy day 2 of every
    // run so there's no value in a "keep bamboo" entry).
    private static readonly (string Id, string DisplayName, long Cost, string? Prereq)[] FishingRodTiers =
    {
        ("keep_fishing_rod_1", "Keep Fiberglass Rod", 150, null),
        ("keep_fishing_rod_2", "Keep Iridium Rod",    425, "keep_fishing_rod_1"),
    };

    /// <summary>Yield all Loadout keep-tier entries (16 tools + 2 fishing rod = 18 rows).</summary>
    public static IEnumerable<UpgradeDefinition> LoadoutToolKeeps()
    {
        foreach (var (slug, displayName) in ToolKinds)
            for (int tier = 1; tier <= 4; tier++)
            {
                string id = $"keep_{slug}_{tier}";
                string? prereq = tier == 1 ? null : $"keep_{slug}_{tier - 1}";
                string name = $"Keep {TierNames[tier - 1]} {displayName}";
                string desc = $"Start each run with your {displayName} at the {TierNames[tier - 1]} tier " +
                              "or whatever lower tier you actually reached this run, whichever is lower.";
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Loadout, name, desc, ToolTierCosts[tier - 1], prereq);
            }

        foreach (var (id, name, cost, prereq) in FishingRodTiers)
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Loadout, name,
                "Start each run with your Fishing Rod at this tier (capped at your in-run reach).",
                cost, prereq);
    }
}
