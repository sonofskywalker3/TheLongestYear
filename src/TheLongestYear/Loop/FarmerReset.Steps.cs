using System.Linq;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using TheLongestYear.Core;
using TheLongestYear.Integration;

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

        // Tier-to-item ids (and the tool state copy) live in Integration.ToolTierItems, shared
        // with focus card delivery. A tier change REPLACES the tool with the correct-tier item.

        private static string ToolSlug(Item it) =>
            it is FishingRod ? "fishing_rod" :
            it is Hoe ? "hoe" :
            it is Pickaxe ? "pickaxe" :
            it is Axe ? "axe" :
            it is WateringCan ? "watering_can" : null;

        /// <summary>Snapshot the player's tools by slug before the inventory wipe: the highest-tier
        /// instance of each kind, wherever it sits (bag, Clint's counter, an owned chest), so the
        /// state transplant (bait, tackle, enchantments) comes from the tool that was actually
        /// upgraded, not a spare basic copy earlier in the bag.</summary>
        private static Dictionary<string, Tool> CaptureTools(Farmer p)
        {
            var tools = new Dictionary<string, Tool>();
            foreach ((string slug, Tool t, _) in ToolTierScan.OwnedTools(p))
            {
                if (!tools.TryGetValue(slug, out Tool known) || t.UpgradeLevel > known.UpgradeLevel)
                    tools[slug] = t;
            }
            return tools;
        }

        /// <summary>
        /// Rewind the one-shot trigger-action record. Vanilla stores every fired
        /// Data/TriggerActions row in <c>Farmer.triggerActionsRun</c> (TriggerActionManager.cs:499)
        /// and never fires it again; hearts and mail are wiped above, so without this the twelve
        /// heart-gated invite mails (Abigail 8, Penny 10, Elliott's letters, ...) fired once per
        /// save and the 8 and 10 heart events could not be reached in loop 2+. The money-earned
        /// mails (Mom/Dad/Tribune) clear too, since the rewind also zeroes money earned
        /// (<see cref="ResetMoneyEarned"/>); the one exception, the Better Start toggle, is in
        /// <see cref="TriggerActionResetRules"/>.
        /// </summary>
        private void ClearTriggerActionRecord(Farmer p)
        {
            if (p.triggerActionsRun.Count == 0)
                return;

            List<string> clear = TriggerActionResetRules.IdsToClear(
                p.triggerActionsRun.ToList(), ResendBetterStartGift());
            foreach (string id in clear)
                p.triggerActionsRun.Remove(id);

            _monitor.Log(
                $"FarmerReset: cleared {clear.Count} fired trigger action(s) so they can fire again this loop " +
                $"([{string.Join(", ", clear)}]); kept {p.triggerActionsRun.Count} " +
                $"([{string.Join(", ", p.triggerActionsRun)}]).",
                LogLevel.Info);
        }

        /// <summary>
        /// Zero the farm's money earned (Jeff, 2026-10-20: it counts toward Perfection, so it rewinds
        /// with the year). It lives on <c>FarmerTeam.totalMoneyEarned</c>, which loadForNewGame never
        /// rebuilds, and only the host may write it. Run this AFTER <see cref="Apply"/>: the Money
        /// setter adds any rise in gold to money earned (Farmer.cs:1989), so setting the starting gold
        /// would otherwise count as earnings. Written to the net field directly; the Farmer property's
        /// setter only adds milestone chat messages, and none apply going down to zero.
        /// It drives the Mom/Dad/Tribune money mails (cleared in <see cref="ClearTriggerActionRecord"/>),
        /// the "m" event precondition (Marnie's pet visit at 1000g, Demetrius' cave at 25000g), the
        /// money achievements, the Parrot Egg trinket's level and the statue-blessing butterfly's gold.
        /// </summary>
        public void ResetMoneyEarned()
        {
            if (!Game1.IsMasterGame || Game1.player?.team == null)
                return;
            uint before = Game1.player.totalMoneyEarned;
            Game1.player.team.totalMoneyEarned.Value = 0;
            _monitor.Log($"FarmerReset: money earned reset to 0 (was {before}g).", LogLevel.Info);
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

                ToolTierItems.CopyState(old, fresh, slug, moved);
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
                        string rid = ToolTierItems.RodItemId(rl);
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

                if (tiers.TryGetValue(slug, out int tier) && tier > 0 && tier <= ToolTierItems.MaxMetalLevel)
                {
                    string itemId = ToolTierItems.MetalItemId(slug, tier);
                    p.Items[i] = ItemRegistry.Create($"(T){itemId}");
                    applied.Add($"{slug}={itemId}");
                }
            }

            // Grant a fresh rod (at the kept tier) if none is held. Willy's rod scene is kept from
            // replaying on top of it in Apply (WillyRodEventId), after eventsSeen is rebuilt.
            if (!hasRod && tiers.TryGetValue("fishing_rod", out int rodLevel))
            {
                string rid = ToolTierItems.RodItemId(rodLevel);
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
