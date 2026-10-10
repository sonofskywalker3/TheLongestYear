using System.Linq;
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
    /// Items the player keeps across the wipe live in the Junimo Stash chest (JunimoStashService).
    /// </summary>
    internal sealed partial class FarmerReset
    {
        private readonly IMonitor _monitor;

        public FarmerReset(IMonitor monitor) => _monitor = monitor;

        /// <summary>Config read-through for <see cref="TriggerActionResetRules"/>; defaults to
        /// re-sending so a test or an unwired caller behaves like a fresh save.</summary>
        public System.Func<bool> ResendBetterStartGift { get; set; } = () => true;

        /// <summary>Farmer.activeDialogueEvents key that makes every villager use their
        /// "Introduction" dialogue on first contact (vanilla Farmer ctor, Farmer.cs:2029).</summary>
        private const string IntroductionDialogueKey = "Introduction";

        /// <summary>Willy's beach scene that gives the Bamboo Pole (Event.cs case "739330").</summary>
        private const string WillyRodEventId = "739330";

        /// <summary>Days vanilla keeps that key alive on a brand-new farmer (Farmer.cs:2029).</summary>
        private const int IntroductionDialogueDays = 6;

        public void Apply(Farmer p, RunBaseline baseline,
            IReadOnlyList<string> cookbookRecipes,
            IReadOnlyList<string> craftbookRecipes,
            IReadOnlyList<string> seenEventsEver,
            IReadOnlyList<string> catchLimitedFishIds = null)
        {
            p.Money = baseline.StartingGold;

            // Inventory — wipe CONTENTS but set the slot count from the baseline (kept items
            // ride in the Junimo Stash chest). p.Items.Clear() removes the slot list itself, which
            // leaves MaxItems lookups returning 0 → addItemToInventory always fails (round-3
            // playtest bug); reset MaxItems then re-pad nulls.
            // Sub-state that lives on the tool INSTANCE, not in Data/Tools: rod bait/tackle,
            // enchantments, water level. Items.Clear() destroys the tool and ApplyToolTiers re-creates
            // a blank one from the registry, so capture here and transplant after (Nexus posts:
            // "kept rod came back without its bait").
            var outgoingTools = CaptureTools(p);
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

            // Marlon's item recovery reads Farmer.itemsLostLastDeath, which vanilla only clears on
            // the NEXT pass-out. Left alone, the Adventurer's Guild sells last loop's dropped loot
            // back in the new one, carrying an item across the reset (Mycatisinapiano1528, Nexus
            // posts, 0.18.38).
            p.itemsLostLastDeath.Clear();

            // Worn equipment — the STAT-BEARING slots (boots/rings/trinkets) live in their own
            // slots, not p.Items, so the inventory wipe above misses them (2026-07-09 reset-leak
            // audit, Dusklight7: worn rings survived every loop). Farmer.Equip(null, slot) routes
            // through vanilla's unequip hook (onUnequip + equipment-buff recompute) so ring/boot
            // effects actually drop with the item.
            // Hat/shirt/pants deliberately stay worn (user ruling 2026-07-13, revising the
            // 2026-07-09 all-slots wipe): they carry no stats, and stripping them can never be
            // undone "authentically" — the character-creation outfit is recorded nowhere, so a
            // wipe just leaves the farmer in underwear with no way back to their look.
            // Keep Worn Gear (spec 2026-10-01) skips this block: the owner keeps the worn instances.
            bool keptTrinket = baseline.KeepWornGear && p.trinketItems.Any(t => t != null);
            uint keptTrinketSlots = keptTrinket ? p.stats.Get(WornGearKeep.TrinketSlotsStat) : 0;
            if (!baseline.KeepWornGear)
            {
                p.Equip<StardewValley.Objects.Boots>(null, p.boots);
                p.Equip<StardewValley.Objects.Ring>(null, p.leftRing);
                p.Equip<StardewValley.Objects.Ring>(null, p.rightRing);
                // Trinkets unequip by index assignment: that fires OnTrinketChange → Trinket.Unapply,
                // the same path the inventory page uses. Then the emptied list is cleared.
                for (int i = 0; i < p.trinketItems.Count; i++)
                    p.trinketItems[i] = null;
                p.trinketItems.Clear();
            }

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
            // A kept trinket needs its slot visible, or it is worn and cannot be taken off.
            if (keptTrinketSlots > 0)
                p.stats.Set(WornGearKeep.TrinketSlotsStat, keptTrinketSlots);

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

            // Power books: Keep <book> rows (spec 2026-08-27). The wipe above removed every
            // Book_* flag (StatResetRules stays wipe-by-default); re-grant only the kept ones, the
            // same shape as the MasteryExp re-seed. Set, not Increment: the flag is binary and
            // Object.readBook treats any non-zero as "already read".
            foreach (string statKey in baseline.KeptBookStats)
                p.stats.Set(statKey, 1);

            // Every run starts with the 5 basic tools. The inventory wipe above removed them
            // and loadForNewGame does NOT re-grant them (it keeps the existing player), so we
            // re-add any that are missing here. Without this the player ends a reset toolless
            // (ApplyToolTiers only BUMPS existing tools, it doesn't create the basics).
            EnsureBasicTools(p, skipBasicScythe: baseline.GrantGoldenScythe);
            if (baseline.GrantGoldenScythe)
                GrantGoldenScythe(p);
            // Horse Flute power (spec 2026-10-09): granted here, before the stable is back (Keep
            // Horse's restore runs later in the reset, step 9 of WorldResetService.PerformReset). That
            // is fine: the flute looks for the player's horse when it is used, not when it is given.
            if (baseline.GrantHorseFlute)
                GrantHorseFlute(p, _monitor);

            // Re-grant kept tool tiers: bump each basic tool's UpgradeLevel to the kept tier
            // (capped at the in-run peak by the baseline builder). Tools with no kept tier stay
            // basic. Tool.UpgradeLevel is settable directly (decompile: Tool.cs:167).
            ApplyToolTiers(p, baseline.ToolTiers, _monitor);
            TransplantToolState(p, baseline.ToolTiers, outgoingTools, _monitor);

            // Relationships, mail, events, quests.
            p.friendshipData.Clear();
            p.mailReceived.Clear();

            // Kept wallet items / Stardrop source markers (spec 2026-08-27 keep-wallet-stardrops):
            // the wipe above took every flag; put back only the bought ones. A CF_* marker put back
            // here is what stops that Stardrop source paying out again next loop.
            foreach (string flag in baseline.KeptMailFlags)
                p.mailReceived.Add(flag);
            p.eventsSeen.Clear();
            p.questLog.Clear();
            ClearTriggerActionRecord(p);
            // Pending level-up menus (vanilla queues them for the next sleep). After the rewind
            // the skill is back at its kept level, so a queued menu would show the wrong level and
            // hand out a profession the player no longer has. Crash Course (a bought level on a
            // Fail day) makes this reachable in one day (found by the 2026-08-29 sim run).
            p.newLevels.Clear();

            // First-meeting dialogue. Vanilla does NOT key an NPC's "Introduction" line on
            // friendshipData: NPC.checkForNewCurrentDialogue (NPC.cs:4009) walks
            // Farmer.activeDialogueEvents and plays the dialogue named by each key, and the
            // "Introduction" key is added exactly once, in the Farmer constructor with a 6-day
            // window (Farmer.cs:2029), then counted down and dropped by dayUpdate (Farmer.cs:3550).
            // loadForNewGame keeps the persistent Farmer, so no later loop ever had the key and
            // villagers greeted a "stranger" with their ordinary daily line (Emmalution, 2026-08-27).
            // Re-seed vanilla's window, and drop the previous loop's active/remembered events so a
            // stale "_memory_oneweek" line can't fire in a year where the event never happened.
            p.activeDialogueEvents.Clear();
            p.previousActiveDialogueEvents.Clear();
            p.activeDialogueEvents.Add(IntroductionDialogueKey, IntroductionDialogueDays);
            // 1.6 gates every RewardItemIsSpecial museum reward (artifact statues, Singing Stone, the
            // Ancient Seeds item AND its recipe) on these two lists, not on mailReceived
            // (LibraryMuseum.CanCollectReward). They live on the persistent Farmer, so without this
            // the artifact-reward ladder was one-shot across the whole profile (Nexus bug 1107194).
            p.specialItems.Clear();
            p.specialBigCraftables.Clear();

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

            // Keep Rod: the player starts with the kept rod, so Willy's beach rod scene must not play
            // (it hands over a Bamboo Pole to anyone not holding one: an extra rod for a Fiberglass or
            // Iridium keep). The old guard pre-mailed "willyBackRoom" in ApplyToolTiers, which the
            // mail wipe above erased and which the game never reads for this scene anyway.
            if (baseline.ToolTiers.ContainsKey("fishing_rod"))
                p.eventsSeen.Add(WillyRodEventId);

            // Kept power events (Bear's Knowledge 2120303, Spring Onion Mastery 3910979): both are
            // replayable, so the re-seed above skipped them; a bought keep re-marks the scene seen,
            // which is exactly how Data/Powers grants the power.
            foreach (string id in baseline.KeptEventIds)
                p.eventsSeen.Add(id);

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
            // each loop; without this their +34s would stack in maxStamina the same way. Kept
            // Stardrops (keep_stardrop_* rows) add their +34 back here, and their CF_* marker was
            // re-added with the kept mail so the source stays shut.
            p.maxStamina.Value = WalletKeepTable.BaseStamina
                + WalletKeepTable.StardropStamina * baseline.KeptStardropCount;

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

            // Kept crafting recipes granted directly, outside the craftbook banking system
            // (e.g. Garden Pot keep): always present from day 1 once bought.
            foreach (string r in baseline.KeptCraftingRecipes)
                if (!p.craftingRecipes.ContainsKey(r))
                    p.craftingRecipes[r] = 0;

            // Legendary fish must be catchable again each loop. The game blocks a repeat catch
            // through SpawnFishData.CatchLimit checked against p.fishCaught (GameLocation.cs:13831),
            // and nothing else in this reset ever touches that record; without this, a legendary
            // caught in loop 1 stays permanently uncatchable in every later loop.
            int fishCleared = 0;
            if (catchLimitedFishIds != null && catchLimitedFishIds.Count > 0)
            {
                foreach (string id in CaughtFishReset.IdsToClear(catchLimitedFishIds, p.fishCaught.Keys.ToList()))
                {
                    p.fishCaught.Remove(id);
                    fishCleared++;
                }
            }
            if (fishCleared > 0)
                _monitor.Log($"Reset: cleared {fishCleared} catch-limited fish so they can be caught again.", LogLevel.Info);

            _monitor.Log(
                $"FarmerReset: gold={baseline.StartingGold}, slots={baseline.MaxItems}, " +
                $"tools=[{string.Join(",", baseline.ToolTiers)}], " +
                $"skills=[{string.Join(",", baseline.SkillLevels)}], " +
                $"kitchen={baseline.KitchenOnDay1}, basement={baseline.BasementOnDay1}, " +
                $"shortcuts={baseline.ShortcutsUnlocked}, mastery={baseline.MasteryLevel}, " +
                $"books=[{string.Join(",", baseline.KeptBookStats)}], " +
                $"wallet=[{string.Join(",", baseline.KeptMailFlags)}], " +
                $"events=[{string.Join(",", baseline.KeptEventIds)}], " +
                $"stardrops={baseline.KeptStardropCount}, " +
                $"goldenScythe={baseline.GrantGoldenScythe}, " +
                $"dialogueEvents=[{string.Join(",", p.activeDialogueEvents.Keys.Select(k => k + ":" + p.activeDialogueEvents[k]))}], " +
                $"cookRecipes={cookbookRecipes.Count} banked (total {p.cookingRecipes.Count()}), " +
                $"craftRecipes={craftbookRecipes.Count} banked (total {p.craftingRecipes.Count()}), " +
                $"eventsReseeded={reseeded} (of {seenEventsEver.Count} seen-ever).",
                LogLevel.Trace);
        }
    }
}
