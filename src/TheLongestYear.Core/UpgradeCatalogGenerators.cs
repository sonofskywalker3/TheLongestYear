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
                    id, UpgradeCategory.Loadout, name, desc, ToolTierCosts[tier - 1], prereq,
                    metaRequirement: null, runReachRequirement: $"tool:{slug}:{tier}");
            }

        foreach (var (id, name, cost, prereq) in FishingRodTiers)
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Loadout, name,
                "Start each run with your Fishing Rod at this tier (capped at your in-run reach).",
                cost, prereq);
    }

    // Skill level keep costs, indexed [1..10]. Levels 5 and 10 jump because they
    // also re-trigger the profession picker (Phase A persistence design §B), so the
    // player is really buying both the level AND the profession-pick option.
    private static readonly long[] SkillLevelCosts =
    {
        0,         // index 0 unused
        50, 100, 175, 275,        // L1–L4
        500,                       // L5 — profession unlock
        650, 800, 1000, 1250,      // L6–L9
        2000                       // L10 — final profession unlock
    };

    private static readonly (string IdSlug, string DisplayName)[] SkillKinds =
    {
        ("farming",  "Farming"),
        ("mining",   "Mining"),
        ("foraging", "Foraging"),
        ("fishing",  "Fishing"),
        ("combat",   "Combat"),
    };

    /// <summary>Yield all 50 Carryover keep-skill-level entries.</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverSkillLevelKeeps()
    {
        foreach (var (slug, displayName) in SkillKinds)
            for (int level = 1; level <= 10; level++)
            {
                string id = $"keep_{slug}_level_{level}";
                string? prereq = level == 1 ? null : $"keep_{slug}_level_{level - 1}";
                string name = $"Keep {displayName} Level {level}";
                string desc = $"Start each run at {displayName} Level {level} (or whatever lower " +
                              "level you actually reached). XP is set to the level threshold — no " +
                              "half-progress preserved." +
                              (level == 5 || level == 10
                                ? $" Re-triggers the profession picker for Level {level}."
                                : "");
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Carryover, name, desc, SkillLevelCosts[level], prereq,
                    metaRequirement: null, runReachRequirement: $"skill:{slug}:{level}");
            }
    }

    /// <summary>Yield all 12 Carryover keep-mine-elevator-floor entries (10–120 step 10).</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverMineElevatorKeeps()
    {
        for (int floor = 10; floor <= 120; floor += 10)
        {
            string id = $"keep_mine_elevator_{floor}";
            string? prereq = floor == 10 ? null : $"keep_mine_elevator_{floor - 10}";
            // Cost ramp: 75 JP for floor 10, +100 per 10 floors → 1175 JP for floor 120.
            // Early floors are cheap (sub-Copper-tool territory); deep floors (90+) match or
            // exceed the Iridium-tool tier, reflecting that skipping most of the mines is a
            // major time save. Buy shallow elevator keeps early, deep ones after tool chains.
            long cost = 75 + ((floor - 10) / 10) * 100;
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Carryover,
                $"Keep Mine Elevator Floor {floor}",
                $"Start each run with the mine elevator accessible to floor {floor} (or your " +
                "in-run deepest floor, whichever is shallower).",
                cost, prereq, metaRequirement: null, runReachRequirement: $"mine:{floor}");
        }
    }
}
