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

        public void Apply(Farmer p, RunBaseline baseline)
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

            // Re-grant kept tool tiers. Player's toolList still has the vanilla baseline
            // tools (rusty); just bump their UpgradeLevel. Tool.UpgradeLevel is settable
            // directly (decompile: StardewValley\StardewValley\Tool.cs:167).
            ApplyToolTiers(p, baseline.ToolTiers);

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

            // House upgrade — set Kitchen on day 1 if kept_kitchen owned. The actual
            // FarmHouse layout switch happens in WorldResetService (it has to resetForPlayerEntry
            // after setting the level so the kitchen tiles appear).
            if (baseline.KitchenOnDay1)
                p.HouseUpgradeLevel = 1;

            _monitor.Log(
                $"FarmerReset: gold={baseline.StartingGold}, slots={baseline.MaxItems}, " +
                $"tools=[{string.Join(",", baseline.ToolTiers)}], " +
                $"skills=[{string.Join(",", baseline.SkillLevels)}], " +
                $"kitchen={baseline.KitchenOnDay1}.",
                LogLevel.Trace);
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

        private static void ApplyToolTiers(Farmer p, IReadOnlyDictionary<string, int> tiers)
        {
            // Find each basic tool in the player's toolList by type and bump UpgradeLevel.
            // loadForNewGame gives the player a Hoe, Pickaxe, Axe, WateringCan, and
            // Scythe (MeleeWeapon, no tier). NO FishingRod — vanilla Willy mails the
            // bamboo rod on day 2 — so we handle that one separately below.
            bool hasRodInInventory = false;
            foreach (var item in p.Items)
            {
                if (item is Hoe         h  && tiers.TryGetValue("hoe",          out int ht)) h.UpgradeLevel  = ht;
                if (item is Pickaxe     pk && tiers.TryGetValue("pickaxe",      out int pkt)) pk.UpgradeLevel = pkt;
                if (item is Axe         a  && tiers.TryGetValue("axe",          out int at)) a.UpgradeLevel  = at;
                if (item is WateringCan w  && tiers.TryGetValue("watering_can", out int wt)) w.UpgradeLevel  = wt;
                if (item is FishingRod  fr)
                {
                    hasRodInInventory = true;
                    if (tiers.TryGetValue("fishing_rod", out int frt))
                        fr.UpgradeLevel = frt;
                }
            }

            // Fishing rod: if we need to grant one and the player has no rod yet (the
            // common day-1 case), create a fresh rod at the requested UpgradeLevel and
            // slot it into the first empty inventory slot. Also pre-mail the "willyBackRoom"
            // flag so Willy's day-2 bamboo-rod event doesn't fire on top of this.
            if (!hasRodInInventory && tiers.TryGetValue("fishing_rod", out int rodLevel))
            {
                var rod = new FishingRod { UpgradeLevel = rodLevel };
                // Find the first null slot — p.Items has nulls between live items because
                // FarmerReset re-padded them.
                for (int i = 0; i < p.Items.Count; i++)
                {
                    if (p.Items[i] == null)
                    {
                        p.Items[i] = rod;
                        break;
                    }
                }
                p.mailReceived.Add("willyBackRoom");
            }
        }
    }
}
