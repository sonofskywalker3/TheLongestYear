using System.Linq;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class FarmerReset
    {
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
        private static void GrantHorseFlute(Farmer p, IMonitor monitor)
        {
            foreach (Item held in p.Items)
                if (held != null && held.QualifiedItemId == AnimalPowers.HorseFluteQid)
                    return;
            Item flute = StardewValley.ItemRegistry.Create(AnimalPowers.HorseFluteQid);
            bool added = p.addItemToInventoryBool(flute);
            monitor.Log(added
                    ? "Reset: Horse Flute power, a Horse Flute is in the backpack."
                    : "Reset: Horse Flute power, the backpack is full, no flute this loop.",
                added ? LogLevel.Info : LogLevel.Warn);
        }

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

        private static string ToolSlug(Item it) =>
            it is FishingRod ? "fishing_rod" :
            it is Hoe ? "hoe" :
            it is Pickaxe ? "pickaxe" :
            it is Axe ? "axe" :
            it is WateringCan ? "watering_can" : null;

        /// <summary>Snapshot the player's tools by slug before the inventory wipe.</summary>
        private static Dictionary<string, Tool> CaptureTools(Farmer p)
        {
            var tools = new Dictionary<string, Tool>();
            foreach (Item it in p.Items)
            {
                string slug = ToolSlug(it);
                if (slug != null && it is Tool t && !tools.ContainsKey(slug)) tools[slug] = t;
            }
            return tools;
        }

        /// <summary>
        /// Rewind the one-shot trigger-action record. Vanilla stores every fired
        /// Data/TriggerActions row in <c>Farmer.triggerActionsRun</c> (TriggerActionManager.cs:499)
        /// and never fires it again; hearts and mail are wiped above, so without this the twelve
        /// heart-gated invite mails (Abigail 8, Penny 10, Elliott's letters, ...) fired once per
        /// save and the 8 and 10 heart events could not be reached in loop 2+. Rules and the two
        /// exceptions (lifetime money mails, the Better Start toggle) in
        /// <see cref="TriggerActionResetRules"/>.
        /// </summary>
        private void ClearTriggerActionRecord(Farmer p)
        {
            if (p.triggerActionsRun.Count == 0)
                return;

            var conditionById = new Dictionary<string, string>();
            try
            {
                foreach (StardewValley.GameData.TriggerActionData row in DataLoader.TriggerActions(Game1.content))
                {
                    if (!string.IsNullOrEmpty(row.Id))
                        conditionById[row.Id] = row.Condition;
                }
            }
            catch (System.Exception ex)
            {
                // Without the rows we cannot tell a lifetime-money mail from a heart invite, and
                // clearing blind would re-send every Mom/Dad/Tribune letter on day 1. Keep the
                // record as it is this loop rather than guess.
                _monitor.Log($"FarmerReset: could not read Data/TriggerActions ({ex.Message}); leaving the trigger-action record untouched this loop.", LogLevel.Warn);
                return;
            }

            List<string> clear = TriggerActionResetRules.IdsToClear(
                p.triggerActionsRun.ToList(), conditionById, ResendBetterStartGift());
            foreach (string id in clear)
                p.triggerActionsRun.Remove(id);

            _monitor.Log(
                $"FarmerReset: cleared {clear.Count} fired trigger action(s) so they can fire again this loop " +
                $"([{string.Join(", ", clear)}]); kept {p.triggerActionsRun.Count} " +
                $"([{string.Join(", ", p.triggerActionsRun)}]).",
                LogLevel.Info);
        }

        /// <summary>Copy instance state (rod attachments, enchantments, water) from the outgoing tool
        /// onto the re-granted KEPT tool of the same kind. Only kept tiers qualify — a tool that
        /// wasn't kept is meant to be lost with the loop. Attachment copy is clamped to the new
        /// tool's slot count so a lower kept tier (fewer tackle slots) can't overflow.</summary>
        private static void TransplantToolState(Farmer p, IReadOnlyDictionary<string, int> keptTiers,
            IReadOnlyDictionary<string, Tool> outgoing, IMonitor monitor)
        {
            var moved = new List<string>();
            foreach (Item it in p.Items)
            {
                string slug = ToolSlug(it);
                if (slug == null || it is not Tool fresh) continue;
                if (!keptTiers.ContainsKey(slug)) continue;
                if (!outgoing.TryGetValue(slug, out Tool old) || ReferenceEquals(old, fresh)) continue;

                int slots = System.Math.Min(old.attachments.Count, fresh.attachments.Count);
                for (int i = 0; i < slots; i++)
                {
                    if (old.attachments[i] == null) continue;
                    fresh.attachments[i] = old.attachments[i];
                    moved.Add($"{slug}:attachment[{i}]={old.attachments[i].Name}");
                }
                if (old.enchantments.Count > 0)
                {
                    old.CopyEnchantments(old, fresh);
                    moved.Add($"{slug}:enchantments={old.enchantments.Count}");
                }
                if (old is WateringCan oldCan && fresh is WateringCan freshCan)
                    freshCan.WaterLeft = System.Math.Min(oldCan.WaterLeft, freshCan.waterCanMax);
            }
            if (moved.Count > 0)
                monitor.Log("Reset: transplanted tool state — " + string.Join(", ", moved), LogLevel.Trace);
        }

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

            // Grant a fresh rod (at the kept tier) if none is held. Willy's rod scene is kept from
            // replaying on top of it in Apply (WillyRodEventId), after eventsSeen is rebuilt.
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
                applied.Add($"fishing_rod={rid}(new)");
            }

            monitor.Log(
                $"ApplyToolTiers: requested=[{string.Join(",", tiers)}], applied=[{string.Join(",", applied)}], " +
                $"rodAlreadyInInventory={hasRod}.",
                LogLevel.Trace);
        }
    }
}
