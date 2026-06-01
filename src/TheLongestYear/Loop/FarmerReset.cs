using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Applies a <see cref="RunBaseline"/> to the persistent <see cref="Farmer"/> at the top
    /// of a new run. Game1.loadForNewGame rebuilds the world but leaves the player's
    /// money/skills/inventory/relationships intact, so we clear them here, then re-apply the
    /// baseline (backpack, tool tiers, skill levels with XP flooring, starting gold).
    /// Plan 07 will carve the Junimo Stash out of the inventory wipe.
    /// </summary>
    internal sealed class FarmerReset
    {
        private readonly IMonitor _monitor;

        public FarmerReset(IMonitor monitor) => _monitor = monitor;

        public void Apply(Farmer p, RunBaseline baseline,
            IReadOnlyList<string> cookbookRecipes,
            IReadOnlyList<string> craftbookRecipes)
        {
            p.Money = baseline.StartingGold;

            // Inventory — wipe CONTENTS but set the slot count from the baseline (Stash
            // preservation is Plan 07). p.Items.Clear() removes the slot list itself, which
            // leaves MaxItems lookups returning 0 → addItemToInventory always fails (round-3
            // playtest bug); reset MaxItems then re-pad nulls.
            p.MaxItems = baseline.MaxItems;
            p.Items.Clear();
            for (int i = 0; i < p.MaxItems; i++)
                p.Items.Add(null);

            // Skills — clear everything first.
            for (int i = 0; i < p.experiencePoints.Count; i++)
                p.experiencePoints[i] = 0;
            p.farmingLevel.Value = 0;
            p.miningLevel.Value = 0;
            p.fishingLevel.Value = 0;
            p.foragingLevel.Value = 0;
            p.combatLevel.Value = 0;
            p.luckLevel.Value = 0;
            p.professions.Clear();

            // Re-grant kept skill levels + floor XP to the level's threshold.
            // Farmer.getBaseExperienceForLevel is the vanilla XP-for-level table
            // (decompile: StardewValley\StardewValley\Farmer.cs:3046, used at line 7233).
            foreach (var kvp in baseline.SkillLevels)
            {
                int skillIndex = kvp.Key;
                int level = kvp.Value;
                p.experiencePoints[skillIndex] = Farmer.getBaseExperienceForLevel(level);
                SetSkillLevel(p, skillIndex, level);
            }

            // Mastery — permanent floor from Keep Mastery. Set the global MasteryExp stat to the
            // threshold for the kept level so MasteryTrackerMenu.getCurrentMasteryLevel() reports it.
            if (baseline.MasteryLevel > 0)
            {
                int needed = StardewValley.Menus.MasteryTrackerMenu.getMasteryExpNeededForLevel(baseline.MasteryLevel);
                Game1.stats.Set("MasteryExp", needed);
            }

            // Every run starts with the 5 basic tools. The inventory wipe above removed them
            // and loadForNewGame does NOT re-grant them (it keeps the existing player), so we
            // re-add any that are missing here. Without this the player ends a reset toolless
            // (ApplyToolTiers only BUMPS existing tools, it doesn't create the basics).
            EnsureBasicTools(p, skipBasicScythe: baseline.GrantGoldenScythe);
            if (baseline.GrantGoldenScythe)
                GrantGoldenScythe(p);

            // Re-grant kept tool tiers: bump each basic tool's UpgradeLevel to the kept tier
            // (capped at the in-run peak by the baseline builder). Tools with no kept tier stay
            // basic. Tool.UpgradeLevel is settable directly (decompile: Tool.cs:167).
            ApplyToolTiers(p, baseline.ToolTiers, _monitor);

            // Relationships, mail, events, quests.
            p.friendshipData.Clear();
            p.mailReceived.Clear();
            p.eventsSeen.Clear();
            p.questLog.Clear();

            // Suppress the vanilla intro cutscene from replaying every loop (matches TitleMenu's new-game path).
            p.eventsSeen.Add("60367");

            // Vitals to full.
            p.stamina = p.maxStamina.Value;
            p.health = p.maxHealth;

            // House upgrade — pick the highest tier owned. The FarmHouse layout switch happens
            // in WorldResetService (it has to resetForPlayerEntry after setting the level so the
            // kitchen/kids-room/cellar-entrance tiles appear). L3 also triggers FarmHouse's
            // built-in AddCellarTiles + createCellarWarps + "Cask" recipe grant inside
            // setMapForUpgradeLevel — see decompile FarmHouse.cs:934-939.
            if (baseline.BasementOnDay1)
                p.HouseUpgradeLevel = 3;
            else if (baseline.KitchenOnDay1)
                p.HouseUpgradeLevel = 1;
            else
                // No house keep owned -> the farmhouse must revert to the starting cabin every
                // loop. loadForNewGame keeps the persistent Farmer, so HouseUpgradeLevel survives
                // unless we clear it; without this an upgraded house persisted across resets
                // (2026-06-01 playtest: "the farmhouse did not reset"). WorldResetService's
                // resetForPlayerEntry then rebuilds the small-house layout to match.
                p.HouseUpgradeLevel = 0;

            // Re-grant banked cooking recipes. Value 0 = vanilla "learned but never cooked".
            // Do this AFTER clearing mail/events so the "you learned a recipe" pop-up doesn't
            // fire for each one (the pop-up reads mailReceived for the "gotRecipe_X" flags;
            // clearing mail first means no duplicate notification on the first morning).
            // NetStringDictionary<int, NetInt> does not implement IDictionary<string,int> —
            // use its ContainsKey + indexer directly.
            GrantBankedRecipes(p.cookingRecipes, cookbookRecipes);
            GrantBankedRecipes(p.craftingRecipes, craftbookRecipes);

            _monitor.Log(
                $"FarmerReset: gold={baseline.StartingGold}, slots={baseline.MaxItems}, " +
                $"tools=[{string.Join(",", baseline.ToolTiers)}], " +
                $"skills=[{string.Join(",", baseline.SkillLevels)}], " +
                $"kitchen={baseline.KitchenOnDay1}, basement={baseline.BasementOnDay1}, " +
                $"shortcuts={baseline.ShortcutsUnlocked}, mastery={baseline.MasteryLevel}, " +
                $"goldenScythe={baseline.GrantGoldenScythe}, " +
                $"cookRecipes={cookbookRecipes.Count}, craftRecipes={craftbookRecipes.Count}.",
                LogLevel.Trace);
        }

        private static void GrantBankedRecipes(
            StardewValley.Network.NetStringDictionary<int, Netcode.NetInt> farmerDict,
            IReadOnlyList<string> banked)
        {
            foreach (string recipeId in banked)
            {
                // 0 = "learned but never cooked/crafted". Don't overwrite a higher count
                // if the player somehow already has it (idempotent add).
                if (!farmerDict.ContainsKey(recipeId))
                    farmerDict[recipeId] = 0;
            }
        }

        private static void SetSkillLevel(Farmer p, int skillIndex, int level)
        {
            switch (skillIndex)
            {
                case 0: p.farmingLevel.Value = level; break;
                case 1: p.fishingLevel.Value = level; break;
                case 2: p.foragingLevel.Value = level; break;
                case 3: p.miningLevel.Value = level; break;
                case 4: p.combatLevel.Value = level; break;
                // Luck (5) intentionally excluded — no level keeps for it per the design.
            }
        }

        /// <summary>Guarantee the player holds the 5 starting tools (Axe, Hoe, Watering Can,
        /// Pickaxe, Scythe). Adds any that are missing into the first empty slot, idempotent by
        /// qualified item id so it never duplicates a tool that survived. Uses the game's own
        /// <see cref="Farmer.initialTools"/> as the canonical list, so it tracks vanilla.</summary>
        private static void EnsureBasicTools(Farmer p, bool skipBasicScythe = false)
        {
            foreach (Item tool in Farmer.initialTools())
            {
                // The basic scythe is the MeleeWeapon in the initial-tools set; skip it when the
                // player has Keep Golden Scythe (GrantGoldenScythe grants (W)53 instead).
                if (skipBasicScythe && tool is StardewValley.Tools.MeleeWeapon)
                    continue;

                bool present = false;
                foreach (Item held in p.Items)
                {
                    if (held != null && held.QualifiedItemId == tool.QualifiedItemId)
                    {
                        present = true;
                        break;
                    }
                }
                if (present)
                    continue;

                for (int i = 0; i < p.Items.Count; i++)
                {
                    if (p.Items[i] == null)
                    {
                        p.Items[i] = tool;
                        break;
                    }
                }
            }
        }

        /// <summary>Add the Golden Scythe (W)53 into the first empty slot if not already held.</summary>
        private static void GrantGoldenScythe(Farmer p)
        {
            const string goldenScytheQid = "(W)53";
            foreach (Item held in p.Items)
                if (held != null && held.QualifiedItemId == goldenScytheQid)
                    return;
            Item scythe = StardewValley.ItemRegistry.Create(goldenScytheQid);
            for (int i = 0; i < p.Items.Count; i++)
                if (p.Items[i] == null) { p.Items[i] = scythe; return; }
        }

        // SDV 1.6 makes a tool's TIER its ItemId, not just its UpgradeLevel: a Copper Watering
        // Can is the item "CopperWateringCan" (UpgradeLevel 1 comes with it from Data/Tools).
        // Setting only UpgradeLevel bumps capacity/power but leaves the item — sprite, name,
        // identity — basic (2026-06-01 playtest: "the game thinks it's copper but it's the basic
        // can"). So we REPLACE the basic tool with the correct-tier item from the registry.
        // ItemIds verified against the decompile's MigrateLegacyItemId switches.
        private static readonly string[] MetalToolPrefixes = { "", "Copper", "Steel", "Gold", "Iridium" };
        private static readonly Dictionary<string, string> BasicToolBaseId = new()
        {
            ["hoe"] = "Hoe",
            ["pickaxe"] = "Pickaxe",
            ["axe"] = "Axe",
            ["watering_can"] = "WateringCan",
        };

        // FishingRod tiers by UpgradeLevel (0 bamboo / 2 fiberglass / 3 iridium are the keeps;
        // 1 training rod has no keep but is mapped for completeness).
        private static string RodItemId(int upgradeLevel) => upgradeLevel switch
        {
            0 => "BambooPole",
            1 => "TrainingRod",
            2 => "FiberglassRod",
            3 => "IridiumRod",
            _ => "BambooPole",
        };

        private static void ApplyToolTiers(Farmer p, IReadOnlyDictionary<string, int> tiers, IMonitor monitor)
        {
            // loadForNewGame + EnsureBasicTools leave the player holding basic Hoe/Pickaxe/Axe/
            // WateringCan/Scythe. NO FishingRod (vanilla Willy mails the bamboo rod on day 2), so
            // we add that one if a rod keep is owned. Replace any basic tool that has a kept tier
            // with the proper-tier ITEM (see note above).
            var applied = new List<string>();
            bool hasRod = false;

            for (int i = 0; i < p.Items.Count; i++)
            {
                Item it = p.Items[i];
                if (it == null) continue;

                if (it is FishingRod)
                {
                    hasRod = true;
                    if (tiers.TryGetValue("fishing_rod", out int rl))
                    {
                        string rid = RodItemId(rl);
                        p.Items[i] = ItemRegistry.Create($"(T){rid}");
                        applied.Add($"fishing_rod={rid}");
                    }
                    continue;
                }

                string slug =
                    it is Hoe ? "hoe" :
                    it is Pickaxe ? "pickaxe" :
                    it is Axe ? "axe" :
                    it is WateringCan ? "watering_can" : null;
                if (slug == null) continue;

                if (tiers.TryGetValue(slug, out int tier) && tier > 0 && tier < MetalToolPrefixes.Length)
                {
                    string itemId = MetalToolPrefixes[tier] + BasicToolBaseId[slug];
                    p.Items[i] = ItemRegistry.Create($"(T){itemId}");
                    applied.Add($"{slug}={itemId}");
                }
            }

            // Grant a fresh rod (at the kept tier) if none is held. Pre-mail "willyBackRoom" so
            // Willy's day-2 bamboo-rod event doesn't fire on top of it.
            if (!hasRod && tiers.TryGetValue("fishing_rod", out int rodLevel))
            {
                string rid = RodItemId(rodLevel);
                Item rod = ItemRegistry.Create($"(T){rid}");
                for (int i = 0; i < p.Items.Count; i++)
                {
                    if (p.Items[i] == null)
                    {
                        p.Items[i] = rod;
                        break;
                    }
                }
                p.mailReceived.Add("willyBackRoom");
                applied.Add($"fishing_rod={rid}(new)");
            }

            monitor.Log(
                $"ApplyToolTiers: requested=[{string.Join(",", tiers)}], applied=[{string.Join(",", applied)}], " +
                $"rodAlreadyInInventory={hasRod}.",
                LogLevel.Trace);
        }
    }
}
