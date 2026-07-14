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

    private static readonly string[] ToolSlugs = { "hoe", "pickaxe", "axe", "watering_can" };

    // Tier 1=Copper, 2=Steel, 3=Gold, 4=Iridium. Matches Tool.UpgradeLevel
    // (StardewValley\StardewValley\Tool.cs:167) which is 0=base/rusty, 1=copper, ...
    // Display names live in default.json as tier.1..tier.4 (see UpgradeDefinition ResolveTokens).

    // Fishing rod chain. 2026-06-01 (Spec A): added the Bamboo Pole root so the rod
    // chain mirrors the tool chains; reach gating (rod:N = FishingRod.UpgradeLevel, where
    // 0=bamboo, 2=fiberglass, 3=iridium — UpgradeLevel 1 is the Training Rod, which has no
    // keep) keeps un-earned tiers out of the shop. Names are hand-authored (upgrade.{id}.name);
    // the shared desc lives at upgrade-tpl.keep-rod.desc.
    private static readonly (string Id, long Cost, string? Prereq, string Reach)[] FishingRodTiers =
    {
        ("keep_fishing_rod_0", 25,  null,                 "rod:0"),
        ("keep_fishing_rod_1", 150, "keep_fishing_rod_0", "rod:2"),
        ("keep_fishing_rod_2", 425, "keep_fishing_rod_1", "rod:3"),
    };

    /// <summary>Yield all Loadout keep-tier entries (16 tools + 2 fishing rod = 18 rows).</summary>
    public static IEnumerable<UpgradeDefinition> LoadoutToolKeeps()
    {
        foreach (var slug in ToolSlugs)
            for (int tier = 1; tier <= 4; tier++)
            {
                string id = $"keep_{slug}_{tier}";
                string? prereq = tier == 1 ? null : $"keep_{slug}_{tier - 1}";
                var tokens = new Dictionary<string, string>
                {
                    ["tier"] = $"i18n:tier.{tier}",
                    ["tool"] = $"i18n:tool.{slug}",
                };
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Loadout,
                    "upgrade-tpl.keep-tool.name", "upgrade-tpl.keep-tool.desc", tokens,
                    ToolTierCosts[tier - 1], prereq,
                    metaRequirement: null, runReachRequirement: $"tool:{slug}:{tier}");
            }

        foreach (var (id, cost, prereq, reach) in FishingRodTiers)
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Loadout, $"upgrade.{id}.name", "upgrade-tpl.keep-rod.desc", tokens: null,
                cost, prereq, metaRequirement: null, runReachRequirement: reach);
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

    private static readonly string[] SkillSlugs = { "farming", "mining", "foraging", "fishing", "combat" };

    /// <summary>Yield all 50 Carryover keep-skill-level entries.</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverSkillLevelKeeps()
    {
        foreach (var slug in SkillSlugs)
            for (int level = 1; level <= 10; level++)
            {
                string id = $"keep_{slug}_level_{level}";
                string? prereq = level == 1 ? null : $"keep_{slug}_level_{level - 1}";
                string descKey = level == 5 || level == 10
                    ? "upgrade-tpl.keep-skill.desc-profession"
                    : "upgrade-tpl.keep-skill.desc";
                var tokens = new Dictionary<string, string>
                {
                    ["skill"] = $"i18n:skill.{slug}",
                    ["level"] = level.ToString(),
                };
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Carryover,
                    "upgrade-tpl.keep-skill.name", descKey, tokens,
                    SkillLevelCosts[level], prereq,
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
            var tokens = new Dictionary<string, string> { ["floor"] = floor.ToString() };
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Carryover,
                "upgrade-tpl.keep-elevator.name", "upgrade-tpl.keep-elevator.desc", tokens,
                cost, prereq, metaRequirement: null, runReachRequirement: $"mine:{floor}");
        }
    }

    // Mastery keep costs, indexed [1..5]. End-game progression (post all-skills-10), so a
    // steep ramp. Owning a tier is a PERMANENT floor (not in-run-peak capped like skill
    // keeps) — mastery is hard-won and persists across loops once kept.
    private static readonly long[] MasteryCosts = { 0, 1000, 1500, 2000, 2750, 3500 };

    /// <summary>Yield the 5 Carryover Keep-Mastery tiers.</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverMasteryKeeps()
    {
        for (int level = 1; level <= 5; level++)
        {
            string id = $"keep_mastery_{level}";
            string? prereq = level == 1 ? null : $"keep_mastery_{level - 1}";
            var tokens = new Dictionary<string, string> { ["level"] = level.ToString() };
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Carryover,
                "upgrade-tpl.keep-mastery.name", "upgrade-tpl.keep-mastery.desc", tokens,
                MasteryCosts[level], prereq, metaRequirement: null, runReachRequirement: $"mastery:{level}");
        }
    }

    // XP multiplier costs, indexed [1..4] (tier N = x(N+1) on one skill). Deliberately
    // cheaper than the keep-level chain: keeps set your starting floor, multipliers set
    // your re-leveling slope (spec 2026-07-14 economy Change 3). Full family = 5x1200
    // + 3000 capstone = 9000 JP for x10-everything — deep-meta territory.
    private static readonly long[] XpMultCosts = { 0, 100, 200, 350, 550 };

    private static readonly string[] XpMultSkillSlugs = { "farming", "fishing", "foraging", "mining", "combat" };

    /// <summary>Yield the 20 per-skill xp_mult tiers + the xp_mult_all capstone (21 rows).</summary>
    public static IEnumerable<UpgradeDefinition> EfficiencyXpMultipliers()
    {
        foreach (var slug in XpMultSkillSlugs)
            for (int tier = 1; tier <= 4; tier++)
            {
                string id = $"xp_mult_{slug}_{tier}";
                string? prereq = tier == 1 ? null : $"xp_mult_{slug}_{tier - 1}";
                var tokens = new Dictionary<string, string>
                {
                    ["skill"] = $"i18n:skill.{slug}",
                    ["factor"] = (tier + 1).ToString(),
                };
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Efficiency,
                    "upgrade-tpl.xp-mult.name", "upgrade-tpl.xp-mult.desc", tokens,
                    XpMultCosts[tier], prereq,
                    metaRequirement: null, runReachRequirement: null);
            }

        yield return new UpgradeDefinition(
            "xp_mult_all", UpgradeCategory.Efficiency,
            "upgrade.xp_mult_all.name", "upgrade.xp_mult_all.desc", tokens: null,
            3000, null,
            metaRequirement: "upgrades:xp_mult_farming_4,xp_mult_fishing_4,xp_mult_foraging_4,xp_mult_mining_4,xp_mult_combat_4",
            runReachRequirement: null);
    }
}
