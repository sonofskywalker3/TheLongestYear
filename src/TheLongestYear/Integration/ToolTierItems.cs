using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Tools;

namespace TheLongestYear.Integration
{
    /// <summary>Which item a tool kind is at each tier, and how a replacement tool takes over the
    /// old one's instance state.
    ///
    /// SDV 1.6 makes a tool's TIER its ItemId, not just its UpgradeLevel: a Copper Watering Can is
    /// the item "CopperWateringCan" (UpgradeLevel 1 comes with it from Data/Tools). Setting only
    /// UpgradeLevel bumps capacity/power but leaves the item (sprite, name, identity) basic
    /// (2026-06-01 playtest: "the game thinks it's copper but it's the basic can"). So a tier change
    /// REPLACES the tool with the correct-tier item from the registry. ItemIds verified against the
    /// decompile's MigrateLegacyItemId switches.</summary>
    internal static class ToolTierItems
    {
        public const int MaxMetalLevel = 4;

        private static readonly string[] MetalToolPrefixes = { "", "Copper", "Steel", "Gold", "Iridium" };

        private static readonly IReadOnlyDictionary<string, string> BasicToolBaseId =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ToolTierScan.HoeSlug] = "Hoe",
                [ToolTierScan.PickaxeSlug] = "Pickaxe",
                [ToolTierScan.AxeSlug] = "Axe",
                [ToolTierScan.WateringCanSlug] = "WateringCan",
            };

        /// <summary>Rod UpgradeLevel for each focus/keep rung: rung 1 Bamboo (0), rung 2
        /// Fiberglass (2), rung 3 Iridium (3). The Training Rod (1) has no rung.</summary>
        private static readonly int[] RodLevelByRung = { 0, 2, 3 };

        public static bool IsMetalSlug(string slug) => slug != null && BasicToolBaseId.ContainsKey(slug);

        /// <summary>Unqualified rod item id for an UpgradeLevel (0 bamboo / 1 training / 2
        /// fiberglass / 3 iridium); anything else is the bamboo pole.</summary>
        public static string RodItemId(int upgradeLevel) => upgradeLevel switch
        {
            0 => "BambooPole",
            1 => "TrainingRod",
            2 => "FiberglassRod",
            3 => "IridiumRod",
            _ => "BambooPole",
        };

        /// <summary>Unqualified item id of a metal tool at a level (0 = basic). Null for a slug
        /// that is not a metal tool or a level outside 0..4.</summary>
        public static string MetalItemId(string slug, int level)
            => IsMetalSlug(slug) && level >= 0 && level <= MaxMetalLevel
                ? MetalToolPrefixes[level] + BasicToolBaseId[slug]
                : null;

        /// <summary>The qualified <c>(T)</c> item id for a tool kind at an UpgradeLevel (for the
        /// rod, its rod level). Null for an unknown slug or a metal level out of range.</summary>
        public static string ItemIdFor(string slug, int levelOrRodLevel)
        {
            if (slug == ToolTierScan.RodSlug) return "(T)" + RodItemId(levelOrRodLevel);
            string id = MetalItemId(slug, levelOrRodLevel);
            return id == null ? null : "(T)" + id;
        }

        public static int RodLevelForRung(int rung)
            => RodLevelByRung[Math.Clamp(rung, 1, RodLevelByRung.Length) - 1];

        /// <summary>The UpgradeLevel (rod level for the rod) a chain's rung stands for.</summary>
        public static int LevelForRung(string slug, int rung)
            => slug == ToolTierScan.RodSlug ? RodLevelForRung(rung) : rung;

        /// <summary>Copy instance state from the outgoing tool onto its replacement: attachments
        /// (bait, tackle; clamped to the replacement's slot count), enchantments and the watering
        /// can's water (clamped to the new capacity). Appends a note per thing moved.</summary>
        public static void CopyState(Tool old, Tool fresh, string slug, List<string> moved)
        {
            int slots = Math.Min(old.attachments.Count, fresh.attachments.Count);
            for (int i = 0; i < slots; i++)
            {
                if (old.attachments[i] == null) continue;
                fresh.attachments[i] = old.attachments[i];
                moved?.Add($"{slug}:attachment[{i}]={old.attachments[i].Name}");
            }
            if (old.enchantments.Count > 0)
            {
                old.CopyEnchantments(old, fresh);
                moved?.Add($"{slug}:enchantments={old.enchantments.Count}");
            }
            if (old is WateringCan oldCan && fresh is WateringCan freshCan)
                freshCan.WaterLeft = Math.Min(oldCan.WaterLeft, freshCan.waterCanMax);
        }
    }
}
