using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.UI;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    internal sealed partial class WorldResetService
    {
        /// <summary>
        /// Restores the bus for a player who owns <see cref="VaultRules.KeepBusUnlockedId"/>
        /// (Nexus bug, gazumbrado, 2026-08-29: 1,500 JP bought only the mod's gate counter and the
        /// desert stayed locked). Vanilla keys the bus on the <c>ccVault</c> mail alone (BusStop.cs:78,
        /// Game1.isLocationAccessible "Desert", Pam's bus schedule NPC.cs:1191), and step 1a strips
        /// it with the other completion flags; put it back. NOTHING else: the four vault bundles stay
        /// on the board and must be paid like any other bundle (Jeff, 2026-08-29: completing the
        /// bundles is the point of the game), so the Vault area stays incomplete, the slots stay
        /// empty and the JP for paying them is still earned. Vanilla adds the completion mail only
        /// when it is not already received (CommunityCenter.cs:757), so paying the vault later does
        /// not replay the bus-repair scene.
        /// </summary>
        private void RestoreKeptGifts(RunBaseline baseline)
        {
            // Every Gift is a vanilla completion mail: ccPantry (greenhouse, GreenhouseBuilding.cs:47),
            // ccCraftsRoom (quarry bridge, Mountain.cs:168), ccFishTank (glittering boulder,
            // Mountain.cs:139), ccBoilerRoom (minecarts, Mountain/Town/BusStop), ccVault (bus, above).
            // Step 1a stripped them all; put the owned ones back. RefreshMutatedVanillaMaps (later)
            // re-applies the map overrides. Completing that room again later does NOT replay the
            // repair scene: vanilla only queues the letter when it is not already received
            // (CommunityCenter.cs:757), and the letter is what fires the morning event.
            foreach (string mail in baseline.KeptGiftMails)
            {
                if (!Game1.MasterPlayer.mailReceived.Contains(mail))
                    Game1.MasterPlayer.mailReceived.Add(mail);
            }
            _monitor.Log(
                $"Gifts of the Junimos: restored [{string.Join(", ", baseline.KeptGiftMails)}]; the bundles stay on the board.",
                LogLevel.Info);
        }

        /// <summary>Locations whose vanilla progression code edits the loaded map in place instead
        /// of layering a flag-guarded override: the beach bridge repair (Beach + its night-market
        /// variant) and the community-upgrade shortcuts (Beach, Forest, Mountain, Town).</summary>
        private static readonly string[] MutatedVanillaMapLocations =
            { "Beach", "BeachNightMarket", "Forest", "Mountain", "Town" };

        private void RefreshMutatedVanillaMaps()
        {
            foreach (string name in MutatedVanillaMapLocations)
            {
                GameLocation loc = Game1.getLocationFromName(name);
                if (loc?.mapPath?.Value == null) continue;
                try
                {
                    // Invalidate first so the reload below misses the cache and re-reads the asset
                    // (still through SMAPI, so Content Patcher edits apply). Without a content
                    // helper (tests) the reload alone still hits the cache, so skip in that case.
                    if (_gameContent == null) return;
                    _gameContent.InvalidateCache(PathUtilities.NormalizeAssetName(loc.mapPath.Value));
                    loc.reloadMap();
                    loc.updateLayout();
                    _monitor.Log($"Reset: reloaded '{name}' map from clean data.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Reset: could not reload '{name}' map: {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
            }
        }

        // Vanilla's private FarmHouse.AddStarterFurniture(Farm) — lays down the full level-aware
        // default furniture set (bed, fireplace, rug, table+heldObject, chairs) for Game1.whichFarm.
        // Reflected because it's private; reused directly so the set always matches the game's.
        private static readonly System.Reflection.MethodInfo AddStarterFurnitureMethod =
            AccessTools.Method(typeof(FarmHouse), "AddStarterFurniture");

        /// <summary>Tiles where vanilla's starter set places an OBJECT (not furniture) for a farm
        /// type; only Riverland (1) does, with its Fish Smoker at (4,4).</summary>
        private static IEnumerable<Vector2> StarterObjectTiles(int whichFarm)
            => whichFarm == 1 ? new[] { new Vector2(4f, 4f) } : System.Array.Empty<Vector2>();

        /// <summary>Rebuild the FarmHouse's built-in furniture to vanilla's default starter set.
        /// loadForNewGame + the house downgrade leave the cabin's furniture stale/missing (the
        /// fireplace vanished; a bed once blocked the door). Clearing + re-invoking the game's own
        /// AddStarterFurniture restores the complete set at the right tiles for the current upgrade
        /// level. Best-effort: a reflection/placement failure is logged, never fatal to the reset.</summary>
        private void RestoreFarmHouseFurniture(GameLocation home)
        {
            if (home is not FarmHouse fh)
                return;
            if (AddStarterFurnitureMethod == null)
            {
                _monitor.Log("RestoreFarmHouseFurniture: AddStarterFurniture not found via reflection; " +
                    "skipping (cabin furniture may be stale).", LogLevel.Warn);
                return;
            }

            try
            {
                int before = fh.furniture.Count;
                fh.furniture.Clear();
                // Riverland's starter set also drops a Fish Smoker OBJECT at (4,4) (FarmHouse.
                // AddStarterFurniture case 1), and loadForNewGame has just laid that down; a second
                // Add on the same tile throws "same key". Lift the fresh starter object first so the
                // re-run recreates it in place (2026-09-06, first non-Standard rewind).
                foreach (Vector2 tile in StarterObjectTiles(Game1.whichFarm))
                {
                    if (fh.objects.TryGetValue(tile, out StardewValley.Object starter) && starter.bigCraftable.Value)
                        fh.objects.Remove(tile);
                }
                AddStarterFurnitureMethod.Invoke(fh, new object[] { Game1.getFarm() });
                _monitor.Log(
                    $"RestoreFarmHouseFurniture: rebuilt starter furniture (house level {fh.upgradeLevel}); " +
                    $"{before} → {fh.furniture.Count} pieces.",
                    LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"RestoreFarmHouseFurniture: failed: {(ex.InnerException ?? ex).Message}", LogLevel.Warn);
            }
        }

        /// <summary>Remove the vanilla starter gift box (15 parsnip seeds) that the rebuilt
        /// FarmHouse drops on every loadForNewGame. Identified by Chest.giftboxIsStarterGift so
        /// we never touch other gift boxes (e.g. the Adventurer's Guild Marlon book).</summary>
        private void RemoveStarterGiftBox()
        {
            GameLocation farmHouse = Game1.getLocationFromName("FarmHouse");
            if (farmHouse == null) return;

            var toRemove = new List<Vector2>();
            foreach (var kv in farmHouse.objects.Pairs)
            {
                if (kv.Value is StardewValley.Objects.Chest c && c.giftboxIsStarterGift.Value)
                    toRemove.Add(kv.Key);
            }
            foreach (var tile in toRemove)
                farmHouse.objects.Remove(tile);

            if (toRemove.Count > 0)
                _monitor.Log($"In-place reset: removed {toRemove.Count} starter gift box(es) from the FarmHouse (first-loop-only seeds).", LogLevel.Info);
            else
                _monitor.Log("In-place reset: no starter gift box found in the FarmHouse to remove.", LogLevel.Trace);
        }

        /// <summary>
        /// Adds vanilla Quests to the player's questLog for each TLY interactable the first
        /// time it appears for them (Cookbook, Craftbook, Stash, Season Goals fireplace board).
        /// "First time" = the dismissal flag in <see cref="MetaState.DismissedIndicators"/>
        /// has not been set yet (which happens when the player opens the matching menu).
        /// AddIntroQuest is idempotent against the questLog so calling this on every save
        /// load + every reset is safe — no duplicates land.
        ///
        /// Made <c>internal</c> 2026-05-29 so ModEntry can also fire it on save load — that
        /// way the quests appear on existing playthroughs that pre-date a given intro
        /// (e.g. the fireplace board added in this round) rather than waiting for the next
        /// loop reset to surface them.
        /// </summary>
        internal void FireBookQuestIntros()
        {
            // The Cookbook, Craftbook, and Bundle-log are carried book items now (see
            // BookFurniture) — they arrive in the inventory each loop, so no "go find it" quest.

            // Stash quest always fires — the chest is placed unconditionally (auto-pick when
            // config is (0,0)). The DismissedIndicators guard suppresses it once interacted with.
            if (!_meta.DismissedIndicators.Contains(IntroQuestIds.StashDismissed))
            {
                AddIntroQuest(
                    id: IntroQuestIds.StashQuest,
                    title: Strings.Get("quest.stash.title"),
                    description: Strings.Get("quest.stash.desc"));
            }

            // Planning shrine — a view-only board just left of the farmhouse, present from loop 1.
            if (!_meta.DismissedIndicators.Contains(IntroQuestIds.ShrineDismissed))
            {
                AddIntroQuest(
                    id: IntroQuestIds.ShrineQuest,
                    title: Strings.Get("quest.shrine.title"),
                    description: Strings.Get("quest.shrine.desc"));
            }
        }

        private void AddIntroQuest(string id, string title, string description)
        {
            // Idempotent across same-day resets: if the quest already exists, don't add a
            // duplicate — but DO refresh its text. A quest created on an earlier playthrough
            // (before a wording fix shipped) keeps its old text baked into the save; this
            // rewrites it to the current copy so deployed text fixes reach existing runs.
            foreach (var existing in Game1.player.questLog)
            {
                if (existing.id.Value != id) continue;

                if (existing.questTitle != title
                    || existing.questDescription != description
                    || existing.currentObjective != description)
                {
                    existing.questTitle = title;
                    existing.currentObjective = description;
                    existing.questDescription = description;
                    _monitor.Log($"WorldResetService: refreshed text for existing quest (id {id}).", LogLevel.Trace);
                }
                return;
            }

            var q = new Quest();
            q.questType.Value = Quest.type_basic;
            q.questTitle = title;
            q.currentObjective = description;
            q.questDescription = description;
            q.dayQuestAccepted.Value = Game1.Date.TotalDays;
            q.daysLeft.Value = -1;          // no time limit
            q.id.Value = id;
            Game1.player.questLog.Add(q);

            _monitor.Log($"WorldResetService: added quest intro '{title}' (id {id}).", LogLevel.Trace);
        }
    }
}
