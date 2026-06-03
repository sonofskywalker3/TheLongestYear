using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Performs the in-place rewind to Spring 1. Reuses the game's own new-game initializer
    /// (Game1.loadForNewGame) for the heavy lifting — it clears Game1.locations, rebuilds the Farm and
    /// every location, regenerates the CC bundles (wiping donation progress), and re-adds NPCs — then
    /// applies targeted resets for the persistent Farmer and mine progress. "Fix the data, don't fight
    /// the system": no per-collection hacks.
    /// </summary>
    internal sealed class WorldResetService
    {
        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.MetaState _meta;
        private readonly CommunityCenterUnlock _ccUnlock;
        private readonly string _modDirectory;
        private readonly RunState _run;
        private readonly GameplayConfig _config;
        private readonly FarmerReset _farmerReset;
        private readonly ProfessionPickerScheduler _professionPicker;
        private readonly JunimoStashService _stashService;
        private readonly MountainUnlock _mountainUnlock;
        private readonly TheLongestYear.Integration.BookFurniture _bookFurniture;
        private readonly TheLongestYear.UI.PlanningShrineService _planningShrine;

        /// <summary>The pre-reset save folder recorded in <see cref="PerformReset"/>, deleted by
        /// <see cref="CleanupAbandonedSaveFolder"/> only after the post-reset full save confirms the
        /// new canonical folder. Null when there's nothing to clean up.</summary>
        private string _abandonedSaveFolder;

        public ProfessionPickerScheduler ProfessionPicker => _professionPicker;

        public WorldResetService(
            IMonitor monitor,
            TheLongestYear.Core.MetaState meta,
            TheLongestYear.Core.RunState run,
            TheLongestYear.Core.GameplayConfig config,
            CommunityCenterUnlock ccUnlock,
            string modDirectory,
            FarmerReset farmerReset,
            ProfessionPickerScheduler professionPicker,
            JunimoStashService stashService,
            MountainUnlock mountainUnlock,
            TheLongestYear.Integration.BookFurniture bookFurniture,
            TheLongestYear.UI.PlanningShrineService planningShrine)
        {
            _monitor = monitor;
            _meta = meta;
            _run = run;
            _config = config;
            _ccUnlock = ccUnlock;
            _modDirectory = modDirectory;
            _farmerReset = farmerReset;
            _professionPicker = professionPicker;
            _stashService = stashService;
            _mountainUnlock = mountainUnlock;
            _bookFurniture = bookFurniture;
            _planningShrine = planningShrine;
        }

        public void PerformReset()
        {
            // One-time safety backup before the first destructive reset (throws if it fails -> reset aborts).
            // Lands inside the mod folder (not in Stardew's Saves dir) so it doesn't appear as a second
            // save on the title screen.
            SaveBackup.BackupOnce(_meta, _monitor, _modDirectory);

            _monitor.Log("In-place reset: starting.", LogLevel.Info);

            // 0. Fresh world seed BEFORE loadForNewGame. Game1.uniqueIDForThisGame is the master
            // seed used by Utility.CreateDaySaveRandom and per-location forage-spawn randoms.
            // loadForNewGame does NOT touch it; without this, day-1 forage placement is
            // identical across runs (user playtest 2026-05-27: "always a dandelion in the same
            // place on day 1"). Reset weatherForTomorrow so the previous run's evening doesn't
            // bleed into Spring 1.
            //
            // SIDE-EFFECT: Stardew's save folder name is "<FarmerName>_<uniqueIDForThisGame>",
            // so changing the ID would create a new folder on next save (orphaning the existing
            // one). After changing the ID we rename the on-disk folder to match the new path so
            // the save stays a single folder.
            //
            // First attempt at this used Constants.CurrentSavePath both before AND after the ID
            // change, but SMAPI caches CurrentSavePath at SaveLoaded time — it does NOT recompute
            // when uniqueIDForThisGame changes mid-session. That made oldSavePath == newSavePath,
            // so the rename condition was false and we silently produced an orphan folder
            // (user playtest 2026-05-27: "I've still got 2 saves"). Compute the new folder name
            // ourselves from the old path: keep everything before the last underscore (the player
            // name component), append the new uniqueID.
            string oldSavePath = Constants.CurrentSavePath;
            Game1.uniqueIDForThisGame = Utility.NewUniqueIdForThisGame();
            Game1.weatherForTomorrow = "Sun";

            // Record the folder we're abandoning so it can be deleted AFTER the post-reset full save
            // writes the new canonical folder (RunController.ForceFullSave → CleanupAbandonedSaveFolder).
            // We deliberately DON'T rename it here: Stardew's SaveGame.Save writes to the CANONICAL
            // "<farmName>_<uniqueID>" folder, but the on-disk folder may carry a non-canonical prefix
            // (e.g. "None2_" from an earlier de-dup). Renaming by the old prefix produced a SECOND
            // folder that disagreed with what the save actually wrote — two "None" farms on the title
            // screen (2026-06-03 playtest). Leaving the old folder intact until the new save is
            // confirmed also means a kill mid-reset degrades to a loadable stale folder, not a brick.
            _abandonedSaveFolder = (!string.IsNullOrEmpty(oldSavePath) && Directory.Exists(oldSavePath))
                ? oldSavePath
                : null;

            _monitor.Log(
                $"In-place reset: new uniqueIDForThisGame={Game1.uniqueIDForThisGame}.",
                LogLevel.Trace);

            // 0a. Capture the player's pet (kind, breed, name, friendship) BEFORE loadForNewGame
            // wipes it. Gated on the keep_pet upgrade — owners get a sentimental pet-survives-
            // resets carryover; non-owners skip the snapshot and the pet is wiped normally.
            PetCarryoverService.SnapshotPet(_meta, _monitor);

            // 0b. Capture the player's stable tile + horse (name/hat) BEFORE loadForNewGame wipes the
            // buildings. Gated on early_horse ("Keep Horse"); restored after the rebuild at the same
            // tile so the stable persists where the player built it.
            HorseCarryoverService.SnapshotHorse(_meta, _monitor);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            Game1.game1.loadForNewGame(loadedGame: false);

            // 1-seeds. First-loop-only starting seeds: loadForNewGame rebuilds the FarmHouse, whose
            // constructor (AddStarterGiftBox) drops a starter gift box of 15 parsnip seeds. FarmerReset
            // wipes the inventory but not this placed box, so it reappears every loop. PerformReset only
            // runs on resets (never the first new game), so removing it here means run 1 keeps the
            // vanilla nudge and every loop after gets none.
            RemoveStarterGiftBox();

            // 1a. Defensive bundle / area-completion wipe. CommunityCenter.bundles is a PROPERTY
            // pointing at Game1.netWorldState.Value.Bundles (CommunityCenter.cs:104) — a NetCollection
            // at world-state level, not on the CC instance. The 2026-05-26 round-2 playtest showed
            // donations surviving tly_reset even after loadForNewGame (user: "the reset didn't
            // remove the foraging items I had donated, even though it switched which items were
            // required for it"). Re-clearing here regardless of whether loadForNewGame already
            // touched it — costs nothing and guarantees a clean slate.
            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc != null)
            {
                // Force-repopulate Bundles + BundleRewards FIRST. Necessary because (a) a prior
                // broken reset may have Clear()ed the dict (2026-05-26 round-3 crash) leaving
                // bundlesDict()[0] missing, and (b) NetWorldState.BundleData's lazy SetBundleData
                // only fires when netBundleData is empty — once it's populated, missing Bundles
                // entries aren't restored automatically. SetBundleData is idempotent for
                // existing keys (only ADDS missing), so calling it here is safe regardless of
                // current state.
                Game1.netWorldState.Value.SetBundleData(Game1.netWorldState.Value.BundleData);

                // Now zero each per-slot completion array WITHOUT clearing the keys — vanilla
                // does bundles[bundleIndex] lookups (CC.cs:585), KeyNotFoundException there is
                // what crashed JunimoNoteMenu.setUpMenu in round 3.
                // FieldDict exposes the underlying Dictionary<int, NetArray<bool, NetBool>> so we
                // can mutate the NetArray entries in-place (the .Pairs projection only gives a
                // bool[] snapshot, mutating that wouldn't sync).
                foreach (var kvp in Game1.netWorldState.Value.Bundles.FieldDict)
                {
                    var arr = kvp.Value;
                    for (int i = 0; i < arr.Length; i++)
                        arr[i] = false;
                }
                foreach (var kvp in Game1.netWorldState.Value.BundleRewards.FieldDict)
                    kvp.Value.Value = false;
                for (int i = 0; i < cc.areasComplete.Count; i++)
                    cc.areasComplete[i] = false;
                // Mail flags that vanilla sets on room completion + post-completion world
                // changes. These gate (a) hasCompletedCommunityCenter() + the "you completed
                // the Community Center" achievement, (b) the JojaMart-closed visual + door
                // (abandonedJojaMartAccessible — verified via Town.cs:580 +
                // WorldChangeEvent.cs:283, the lightning strike adds this mail for tomorrow),
                // (c) the Joja-path branch (JojaMember + jojaMember + the per-room ccXxx
                // Joja-path flags). User feedback 2026-05-26 round 2: "Joja is still closed
                // day 1" after reset confirmed these were sticking — the prior reset only
                // cleared in-memory bundle state.
                string[] mailToClear =
                {
                    "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank",
                    "ccVault", "ccBulletin", "ccIsComplete",
                    "abandonedJojaMartAccessible", "ccMovieTheater",
                    "JojaMember", "jojaMember",
                    "ccBoilerRoomJoja", "ccCraftsRoomJoja", "ccPantryJoja",
                    "ccFishTankJoja", "ccVaultJoja", "ccBulletinJoja",
                };
                foreach (string flag in mailToClear)
                    Game1.MasterPlayer.mailReceived.Remove(flag);
                Game1.MasterPlayer.mailForTomorrow.Remove("abandonedJojaMartAccessible");
                _monitor.Log(
                    $"In-place reset: cleared CC bundles + areasComplete + {mailToClear.Length} completion/joja mail flags.",
                    LogLevel.Trace);
            }

            // 2. Calendar -> Spring 1, year 1, morning. (loadForNewGame leaves dayOfMonth = 0 as a flag.)
            Game1.year = 1;
            Game1.season = StardewValley.Season.Spring;
            Game1.dayOfMonth = 1;
            Game1.timeOfDay = 600;
            Game1.netWorldState.Value.Date.Year = 1;
            Game1.netWorldState.Value.Date.Season = StardewValley.Season.Spring;
            Game1.netWorldState.Value.Date.DayOfMonth = 1;
            Game1.stats.DaysPlayed = 1;

            // The load-menu date label is read from the Farmer's *ForSaveGame display fields
            // (LoadGameMenu uses dayOfMonthForSaveGame/seasonForSaveGame/yearForSaveGame), which
            // vanilla refreshes during the normal sleep-save (Game1 sets player.*ForSaveGame). A
            // direct post-reset SaveGame.Save doesn't run that step, so without this the slot kept
            // the pre-reset date ("Day 5 of Spring" on a Spring-1 save — 2026-06-03 playtest). Set
            // them to match the rewound date so the title screen shows Spring 1.
            if (Game1.player != null)
            {
                Game1.player.dayOfMonthForSaveGame = Game1.dayOfMonth;
                Game1.player.seasonForSaveGame = (int)Game1.season;
                Game1.player.yearForSaveGame = Game1.year;
            }

            // 3. Capture the in-run peaks from the live player BEFORE the wipe — the cap
            //    side of cap-not-grant. The Farmer-side wipe happens inside
            //    _farmerReset.Apply, so peak-reading has to land here.
            PlayerSnapshot peaks = CapturePeaks(Game1.player);

            // 4. Build the reset baseline + apply Farmer-side state (gold, items, tool
            //    tiers, skill levels, kitchen flag).
            RunBaseline baseline = RunBaselineBuilder.Build(_meta, _run, peaks, _config.StartingMoney);
            _farmerReset.Apply(Game1.player, baseline,
                _meta.CookbookRecipes,
                _meta.CraftbookRecipes,
                _meta.SeenEventsEver);

            // 5. Profession picker re-trigger queue. Enqueued here; the actual menus
            //    surface on the next DayStarted (RunController drains after reset).
            foreach (int skill in baseline.ProfessionPickerSkillsToRequeue)
                _professionPicker.Enqueue(skill, baseline.SkillLevels[skill]);

            // 6. Mine progress. Restore elevator floor from baseline (cap-not-grant
            //    against in-run peak). MineShaft.lowestLevelReached property setter +
            //    LowestMineLevelForOrder field (NetWorldState.cs:362) drive the elevator
            //    panel options.
            Game1.netWorldState.Value.LowestMineLevelForOrder = -1;
            MineShaft.clearActiveMines();
            if (baseline.MineElevatorFloor > 0)
            {
                Game1.netWorldState.Value.LowestMineLevelForOrder = baseline.MineElevatorFloor;
                Game1.player.deepestMineLevel = System.Math.Max(
                    Game1.player.deepestMineLevel, baseline.MineElevatorFloor);
            }

            // 7. Vault gate pre-pay if bus is kept unlocked.
            if (baseline.BusUnlocked)
            {
                _run.VaultBundlesPaid.Clear();
                _run.VaultBundlesPaid.Add(VaultRules.Vault2500);
                _run.VaultBundlesPaid.Add(VaultRules.Vault5000);
                _run.VaultBundlesPaid.Add(VaultRules.Vault10000);
                _run.VaultBundlesPaid.Add(VaultRules.Vault25000);
            }

            // 7b. Robin's community-upgrade map shortcuts — single mail flag controls all five
            //     (Town fence, bus tunnel, forest stump bridge, Mountain quarry path, Mountain
            //     side route). Vanilla reads this flag in Forest.cs:421, Mountain.cs:177,
            //     Town.cs:589, Beach.cs:468, BeachNightMarket.cs:235 and on GameLocation map
            //     overrides — adding the flag here is sufficient; the per-location code re-applies
            //     map overrides on first entry of each location this run.
            if (baseline.ShortcutsUnlocked)
                Game1.MasterPlayer.mailReceived.Add("communityUpgradeShortcuts");

            // 7c. Cellar location for kept_basement. loadForNewGame doesn't include the Cellar
            //     location unless the player previously had an L3 house at save creation. With
            //     HouseUpgradeLevel = 3 forced by FarmerReset, the FarmHouse warp tile will try
            //     to teleport into a non-existent "Cellar" location. Create it on demand here
            //     before resetForPlayerEntry runs the warp setup. updateCellarAssignments
            //     binds the cellar to the master player.
            if (baseline.BasementOnDay1 && Game1.getLocationFromName("Cellar") == null)
            {
                Game1.locations.Add(new StardewValley.Locations.Cellar("Maps\\Cellar", "Cellar"));
                Game1.updateCellarAssignments();
            }

            // 8. Pre-build kept buildings on the Farm. Coords are deterministic — we always
            //    use the same tiles so subsequent runs land buildings in the same spots.
            ApplyKeptBuildings(baseline.KeptBuildings);

            // 9. Keep Horse — restore the player's stable + horse at its saved tile (pure carry-over;
            //    no auto-build, so a player who hasn't built a stable yet has no horse this loop).
            //    Gated on the upgrade + a prior snapshot inside the service.
            HorseCarryoverService.RestoreHorse(_meta, _monitor);

            // 10. Place starting animals into matching housing.
            ApplyStartingAnimals(baseline.StartingAnimals);

            // 10a. Restore the snapshotted pet on the Farm (keep_pet upgrade). Runs after
            // starting animals so the Farm.characters collection is already settled. No-op
            // when the upgrade isn't owned or no prior snapshot exists. Also sets the
            // MarniePetAdoption mail flag so vanilla's day-1 adoption offer is suppressed.
            PetCarryoverService.RestorePet(_meta, _monitor);

            // 11. Bump CompletedResets — the single producer for the season:N meta-requirement.
            _meta.CompletedResets += 1;

            // 12. Fire cookbook/craftbook quest intros on the first run after purchase.
            FireBookQuestIntros();

            // 13. Place the Junimo Stash chest on the Farm and populate from MetaState.
            _stashService?.PlaceChest();
            _stashService?.PopulateFromMeta();
            _planningShrine?.Place(_stashService?.LastPlacedTile);

            // 14. Place the player home, awake, in the rebuilt FarmHouse. resetForPlayerEntry
            //     also rebuilds the FarmHouse layout to match HouseUpgradeLevel — picking up
            //     the kitchen if the baseline set it.
            GameLocation home = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.currentLocation = home;
            Game1.currentLocation = home;
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            home.resetForPlayerEntry();

            // 14a. Rebuild the FarmHouse's built-in furniture. After the loop's house downgrade the
            //      cabin came back without its starter set (2026-06-01: fireplace MISSING, and earlier
            //      a stale bed blocked the door). Clear the furniture and re-run vanilla's own
            //      AddStarterFurniture so the FULL default set (bed, fireplace, rug, table+bowl, …) is
            //      laid down at the correct positions for the current HouseUpgradeLevel.
            RestoreFarmHouseFurniture(home);

            // Re-apply the CC unlock so the loop preserves day-1 CC access (loadForNewGame + FarmerReset wiped it).
            _ccUnlock.Apply();

            // Re-clear the Mountain landslide. loadForNewGame rebuilt every location, so the
            // Mountain ctor saw DaysPlayed = 1 and re-initialised landslide.Value = true.
            _mountainUnlock?.Apply();

            // Books are inventory items wiped by FarmerReset; re-grant exactly one of each.
            _bookFurniture?.ReconcileInventory();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}. " +
                $"Reset #{_meta.CompletedResets}.",
                LogLevel.Info);
        }

        // Vanilla's private FarmHouse.AddStarterFurniture(Farm) — lays down the full level-aware
        // default furniture set (bed, fireplace, rug, table+heldObject, chairs) for Game1.whichFarm.
        // Reflected because it's private; reused directly so the set always matches the game's.
        private static readonly System.Reflection.MethodInfo AddStarterFurnitureMethod =
            AccessTools.Method(typeof(FarmHouse), "AddStarterFurniture");

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

        /// <summary>Rename the main + "_old" save files inside a just-renamed save folder so their
        /// names match the new folder (Stardew names both folder and save file
        /// <c>&lt;FarmerName&gt;_&lt;uniqueID&gt;</c>). SaveGameInfo files are not id-named, so they're
        /// left alone. Best-effort: a failure here only re-opens the "save bricks on mid-window kill"
        /// gap, it never corrupts data.</summary>
        /// <summary>Delete the pre-reset save folder recorded in <see cref="PerformReset"/>. Called
        /// by RunController.ForceFullSave ONLY after the post-reset full save has written the new
        /// canonical folder — deleting after the confirmed save guarantees we never remove the
        /// player's only loadable copy (a kill before this point leaves the old folder intact).
        /// The reset always changes <c>uniqueIDForThisGame</c>, so the abandoned folder is always a
        /// different folder than the one just saved; a defensive id check skips it anyway.</summary>
        public void CleanupAbandonedSaveFolder()
        {
            string old = _abandonedSaveFolder;
            _abandonedSaveFolder = null;
            if (string.IsNullOrEmpty(old) || !Directory.Exists(old))
                return;

            // Defensive: never delete a folder named for the CURRENT (new) uniqueID.
            if (Path.GetFileName(old).EndsWith(Game1.uniqueIDForThisGame.ToString(), StringComparison.Ordinal))
                return;

            try
            {
                Directory.Delete(old, recursive: true);
                _monitor.Log(
                    $"In-place reset: deleted the abandoned pre-reset save folder ({Path.GetFileName(old)}) — no duplicate left behind.",
                    LogLevel.Info);
            }
            catch (IOException ex)
            {
                _monitor.Log(
                    $"In-place reset: could not delete abandoned save folder '{Path.GetFileName(old)}' ({ex.Message}). " +
                    "It may show as a duplicate on the title screen; it is safe to delete manually.",
                    LogLevel.Warn);
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

        // Read in-run peaks from the live player so the baseline builder can apply
        // cap-not-grant. Walks p.Items looking for each tool kind and reads its
        // UpgradeLevel; reads skill level fields directly.
        private static PlayerSnapshot CapturePeaks(Farmer p)
        {
            var toolTiers = new Dictionary<string, int>();
            foreach (var item in p.Items)
            {
                if (item is StardewValley.Tools.Hoe         h)  toolTiers["hoe"]          = System.Math.Max(toolTiers.TryGetValue("hoe", out var v0) ? v0 : 0, h.UpgradeLevel);
                if (item is StardewValley.Tools.Pickaxe     pk) toolTiers["pickaxe"]      = System.Math.Max(toolTiers.TryGetValue("pickaxe", out var v1) ? v1 : 0, pk.UpgradeLevel);
                if (item is StardewValley.Tools.Axe         a)  toolTiers["axe"]          = System.Math.Max(toolTiers.TryGetValue("axe", out var v2) ? v2 : 0, a.UpgradeLevel);
                if (item is StardewValley.Tools.WateringCan w)  toolTiers["watering_can"] = System.Math.Max(toolTiers.TryGetValue("watering_can", out var v3) ? v3 : 0, w.UpgradeLevel);
                if (item is StardewValley.Tools.FishingRod  fr) toolTiers["fishing_rod"]  = System.Math.Max(toolTiers.TryGetValue("fishing_rod", out var v4) ? v4 : 0, fr.UpgradeLevel);
            }

            var skillLevels = new Dictionary<int, int>
            {
                [0] = p.farmingLevel.Value,
                [1] = p.fishingLevel.Value,
                [2] = p.foragingLevel.Value,
                [3] = p.miningLevel.Value,
                [4] = p.combatLevel.Value,
            };

            return new PlayerSnapshot { ToolTiers = toolTiers, SkillLevels = skillLevels };
        }

        // Deterministic tile coords for each kept-building blueprint. Picked to fit
        // the default vanilla farm layout without overlapping the bus stop or the
        // starting clearing. If two chains both place (Coop + Barn) they don't
        // overlap each other.
        private static readonly Dictionary<string, Vector2> BuildingTiles = new()
        {
            ["Coop"]         = new Vector2(54f, 9f),
            ["Big Coop"]     = new Vector2(54f, 9f),
            ["Deluxe Coop"]  = new Vector2(54f, 9f),
            ["Barn"]         = new Vector2(62f, 12f),
            ["Big Barn"]     = new Vector2(62f, 12f),
            ["Deluxe Barn"]  = new Vector2(62f, 12f),
        };

        private void ApplyKeptBuildings(IReadOnlyList<string> buildings)
        {
            Farm farm = Game1.getFarm();
            foreach (string blueprint in buildings)
            {
                if (!BuildingTiles.TryGetValue(blueprint, out Vector2 tile))
                {
                    _monitor.Log($"Reset: no tile mapped for kept building '{blueprint}', skipping.",
                        LogLevel.Warn);
                    continue;
                }

                // Already there? (e.g. previous reset placed it and loadForNewGame somehow
                // preserved it.) Skip — never duplicate.
                if (farm.buildings.Any(b => b.buildingType.Value == blueprint))
                    continue;

                var b = new Building(blueprint, tile);
                b.daysOfConstructionLeft.Value = 0;   // skip the construction animation
                b.load();                              // initialises interior
                farm.buildings.Add(b);

                // Clear the footprint of any weeds/debris the farm regeneration spawned, the same
                // way Robin's build does. These baseline tiles usually sit in the cleared starting
                // area (a no-op), but this keeps the placement clean if the tile map ever changes.
                farm.removeObjectsAndSpawned((int)tile.X, (int)tile.Y, b.tilesWide.Value, b.tilesHigh.Value);
            }
        }


        private void ApplyStartingAnimals(IReadOnlyList<StartingAnimal> animals)
        {
            if (animals.Count == 0) return;
            Farm farm = Game1.getFarm();

            foreach (var animal in animals)
            {
                var requiredInfo = ChainInfo(animal.HousingType);
                Building housing = farm.buildings.FirstOrDefault(b =>
                {
                    var info = ChainInfo(b.buildingType.Value);
                    return info.Family == requiredInfo.Family && info.Tier >= requiredInfo.Tier;
                });
                if (housing == null)
                {
                    _monitor.Log(
                        $"Reset: no '{animal.HousingType}'-or-better building found for " +
                        $"starting animal '{animal.VanillaType}'; skipping.",
                        LogLevel.Warn);
                    continue;
                }

                long animalId = (long)Utility.RandomLong();
                var fa = new FarmAnimal(animal.VanillaType, animalId, Game1.player.UniqueMultiplayerID);

                // Add into the housing's animal collection via the vanilla adoptAnimal path.
                // AnimalHouse.adoptAnimal sets homeInterior + currentLocation + calls
                // setRandomPosition. We also set fa.home (the Building) which adoptAnimal
                // doesn't touch — it's set by Building.reload() when the save is loaded next;
                // setting it here avoids a null-ref if anything tries to read it before save.
                if (housing.indoors.Value is AnimalHouse house)
                {
                    fa.home = housing;
                    house.adoptAnimal(fa);

                    // Track only after a successful placement so a degenerate "indoors is null"
                    // case doesn't poison AnimalSpeciesEverOwned with a species the player doesn't
                    // actually have.
                    if (!_meta.AnimalSpeciesEverOwned.Contains(animal.VanillaType, StringComparer.OrdinalIgnoreCase))
                        _meta.AnimalSpeciesEverOwned.Add(animal.VanillaType);
                }
            }
        }

        // Coop chain (chickens, ducks, etc.) and Barn chain (cows, goats, etc.) both
        // have tier 1/2/3 housing — but a Coop must not satisfy a Cow placement and
        // vice versa. The family string segregates the two chains; tier comparison
        // only happens within a family.
        private static (string Family, int Tier) ChainInfo(string blueprint) => blueprint switch
        {
            "Coop"         => ("coop", 1),
            "Big Coop"     => ("coop", 2),
            "Deluxe Coop"  => ("coop", 3),
            "Barn"         => ("barn", 1),
            "Big Barn"     => ("barn", 2),
            "Deluxe Barn"  => ("barn", 3),
            _ => ("", 0)
        };

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
            if (!_meta.DismissedIndicators.Contains("tly.stash"))
            {
                AddIntroQuest(
                    id: "tly.-9003",
                    title: "A gift from the Junimos",
                    description: "The Junimos placed a special chest on your farm — it will survive the seasons. " +
                                 "Find it and use it wisely; it has very limited space.");
            }

            // Planning shrine — a view-only board just left of the farmhouse, present from loop 1.
            if (!_meta.DismissedIndicators.Contains("tly.shrine"))
            {
                AddIntroQuest(
                    id: "tly.-9005",
                    title: "The Junimo Shrine",
                    description: "There's a small Junimo shrine just left of your farmhouse. " +
                                 "Check it to see what you've unlocked and what you can plan for next loop.");
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
