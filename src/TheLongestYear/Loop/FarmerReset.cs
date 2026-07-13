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
            IReadOnlyList<string> craftbookRecipes,
            IReadOnlyList<string> seenEventsEver)
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

            // A tool being upgraded at Clint's lives in Farmer.toolBeingUpgraded, NOT in p.Items, so
            // the inventory wipe above misses it. Left untouched, an in-flight (or finished-but-
            // uncollected) upgrade survives the loop reset for free — Clint hands back the upgraded
            // tool next visit, bypassing the revert-to-baseline rule (2026-06-08 playtest: a Copper
            // Hoe upgraded pre-reset reappeared after a tly_failreset). Cancel it: kept tool tiers
            // come from baseline.ToolTiers via ApplyToolTiers; EnsureBasicTools re-grants the basic
            // tool the player handed Clint, so clearing this leaves no orphaned/free upgrade.
            p.toolBeingUpgraded.Value = null;
            p.daysLeftForToolUpgrade.Value = 0;

            // Worn equipment — the STAT-BEARING slots (boots/rings/trinkets) live in their own
            // slots, not p.Items, so the inventory wipe above misses them (2026-07-09 reset-leak
            // audit, Dusklight7: worn rings survived every loop). Farmer.Equip(null, slot) routes
            // through vanilla's unequip hook (onUnequip + equipment-buff recompute) so ring/boot
            // effects actually drop with the item.
            // Hat/shirt/pants deliberately stay worn (user ruling 2026-07-13, revising the
            // 2026-07-09 all-slots wipe): they carry no stats, and stripping them can never be
            // undone "authentically" — the character-creation outfit is recorded nowhere, so a
            // wipe just leaves the farmer in underwear with no way back to their look.
            p.Equip<StardewValley.Objects.Boots>(null, p.boots);
            p.Equip<StardewValley.Objects.Ring>(null, p.leftRing);
            p.Equip<StardewValley.Objects.Ring>(null, p.rightRing);
            // Trinkets unequip by index assignment — that fires OnTrinketChange → Trinket.Unapply,
            // the same path the inventory page uses — then the emptied list is cleared.
            for (int i = 0; i < p.trinketItems.Count; i++)
                p.trinketItems[i] = null;
            p.trinketItems.Clear();

            // Run-scoped per-farmer progress the reset never covered (same audit):
            //  - slayer kill counts persist while the Gil_* reward mail is wiped below, so loop 2
            //    could walk into the guild and instantly re-claim every slayer ring;
            //  - consumed milestone-chest floors meant mine chests never respawned on later loops
            //    (descending with wiped weapons and no milestone gear);
            //  - power books / mastery exp + claims / prize-ticket ladder all persist in
            //    Stats.Values. Removal is WIPE-BY-DEFAULT with an explicit keep-list
            //    (StatResetRules, user ruling 2026-07-10): unknown future keys wipe; only
            //    engine-critical keys, RNG-sequence counters, and lifetime tallies survive.
            p.stats.specificMonstersKilled.Clear();
            p.chestConsumedMineLevels.Clear();
            foreach (string key in StatResetRules.SelectRunScoped(p.stats.Values.Keys))
                p.stats.Values.Remove(key);

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
            // The stat wipe above cleared masteryLevelsSpent + the mastery_* claim flags, so a Keep
            // Mastery owner re-claims their perks at the pedestal each loop — INTENTIONAL: the perk
            // recipes and reward items are themselves wiped every reset, so re-claiming is the only
            // way the keep functions (same pattern as kept skill levels re-picking professions).
            // Claims per loop are bounded by the kept level, and non-owners get no floor at all.
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

            // Cave gift — back to unchosen each loop. loadForNewGame rebuilds the FarmCave (mushroom
            // boxes gone) but keeps the persistent Farmer, so a stale caveChoice would otherwise
            // carry over. The Demetrius scene only plays once now (event-hygiene pass 2026-06-10);
            // CaveChoicePrompt re-offers mushrooms-vs-bats on cave entry whenever this is unchosen.
            p.caveChoice.Value = 0;

            // Event-gating Phase 1: re-seed eventsSeen from the cross-loop "seen ever" memory rather
            // than leaving it wiped, so a scene the player already watched stays suppressed by
            // vanilla's own seen-check (the unconditional Clear() was the root cause of vanilla early
            // scenes replaying every loop). Replayable ids (furnace teach, Demetrius cave, …) are
            // excluded so they stay eligible to re-fire under EventGatingPolicy's finer gating.
            int reseeded = 0;
            foreach (string id in seenEventsEver)
            {
                // Replayable scenes (furnace/cave) and relationship/heart events stay eligible: the
                // former are re-gated by EventGatingPolicy, the latter must re-fire as the player
                // rebuilds friendships from zero each loop.
                // Replayable = the hardcoded vanilla ids (furnace/cave) OR any unlock-granting cutscene
                // the load-time scan flagged (mod teach/unlock scenes). Either way, don't re-mark it
                // seen, so it stays eligible to re-fire this loop.
                if (EventGatingTables.Default.IsReplayable(id)
                    || ReplayableEventScan.IsReplayable(id)) continue;
                if (RelationshipEventIndex.Contains(id)) continue;
                if (p.eventsSeen.Contains(id)) continue;
                p.eventsSeen.Add(id);
                reseeded++;
            }

            // Suppress the vanilla intro cutscene from replaying every loop (matches TitleMenu's new-game path).
            p.eventsSeen.Add("60367");

            // Max health/stamina — rewind to the vanilla formula before refilling. NEVER reset
            // before (found live 2026-07-10: 500 max HP after 27 loops): maxHealth is a plain
            // field, so each loop's Fighter/Defender re-picks (+15/+25 via vanilla
            // LevelUpMenu.getImmediateProfessionPerk) and snake milk stacked forever. Mirrors
            // LevelUpMenu.RevalidateHealth's formula (100 base + 5 per combat level except 5
            // and 10 + professions + qiCave) — we can't call it directly because it only fixes
            // UPWARD. At this point professions are cleared (re-picks re-add their bonus at
            // pick time via the vanilla menu) and the qiCave snake-milk mail is wiped (+25
            // correctly drops until re-drunk this run), so only kept combat levels count.
            int expectedMaxHealth = 100;
            for (int i = 1; i <= p.combatLevel.Value; i++)
            {
                if (i != 5 && i != 10)
                    expectedMaxHealth += 5;
            }
            p.maxHealth = expectedMaxHealth;

            // Stardrops are tracked by CF_* mail (wiped above), making them re-collectable
            // each loop — without this their +34s would stack in maxStamina the same way.
            p.maxStamina.Value = 270;

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

            // Reset recipes to the vanilla new-game baseline, THEN re-grant banked on top. Without
            // the wipe, every recipe the player ever learned persisted across loops, so the cookbook/
            // craftbook (whose whole point is banking recipes to KEEP them across loops) did nothing
            // (2026-06-01 playtest: all crafting recipes retained with an empty craftbook; kept Fried
            // Egg without banking it). Clear → LearnDefaultRecipes() re-seeds exactly the data-driven
            // defaults (Data/CookingRecipes + CraftingRecipes entries whose unlock field == "default"),
            // so run 2+ matches a clean run 1. Banked entries are then added at value 0 ("learned but
            // never cooked"). Done AFTER clearing mail/events so no "you learned a recipe" pop-up fires
            // (the pop-up reads mailReceived for the "gotRecipe_X" flags). NetStringDictionary does not
            // implement IDictionary<string,int> — GrantBankedRecipes uses ContainsKey + indexer.
            p.cookingRecipes.Clear();
            p.craftingRecipes.Clear();
            p.LearnDefaultRecipes();
            GrantBankedRecipes(p.cookingRecipes, cookbookRecipes);
            GrantBankedRecipes(p.craftingRecipes, craftbookRecipes);

            _monitor.Log(
                $"FarmerReset: gold={baseline.StartingGold}, slots={baseline.MaxItems}, " +
                $"tools=[{string.Join(",", baseline.ToolTiers)}], " +
                $"skills=[{string.Join(",", baseline.SkillLevels)}], " +
                $"kitchen={baseline.KitchenOnDay1}, basement={baseline.BasementOnDay1}, " +
                $"shortcuts={baseline.ShortcutsUnlocked}, mastery={baseline.MasteryLevel}, " +
                $"goldenScythe={baseline.GrantGoldenScythe}, " +
                $"cookRecipes={cookbookRecipes.Count} banked (total {p.cookingRecipes.Count()}), " +
                $"craftRecipes={craftbookRecipes.Count} banked (total {p.craftingRecipes.Count()}), " +
                $"eventsReseeded={reseeded} (of {seenEventsEver.Count} seen-ever).",
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
