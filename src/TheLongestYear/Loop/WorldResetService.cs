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
    /// <summary>
    /// Performs the in-place rewind to Spring 1. Reuses the game's own new-game initializer
    /// (Game1.loadForNewGame) for the heavy lifting — it clears Game1.locations, rebuilds the Farm and
    /// every location, regenerates the CC bundles (wiping donation progress), and re-adds NPCs — then
    /// applies targeted resets for the persistent Farmer and mine progress. "Fix the data, don't fight
    /// the system": no per-collection hacks.
    /// </summary>
    internal sealed partial class WorldResetService
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
        private readonly IReadOnlyDictionary<string, CoreSeason> _itemSeasonPins;
        private readonly IReadOnlyDictionary<string, int[]> _bundleQuotas;
        private readonly IGameContentHelper _gameContent;

        /// <summary>The pre-reset save folder recorded in <see cref="PerformReset"/>, deleted by
        /// <see cref="CleanupAbandonedSaveFolder"/> only after the post-reset full save confirms the
        /// new canonical folder. Null when there's nothing to clean up.</summary>
        private string _abandonedSaveFolder;

        public ProfessionPickerScheduler ProfessionPicker => _professionPicker;

        /// <summary>Derived item model, forwarded to the classifier when the reset regenerates a
        /// board so its PerItem bundles gate on computed deadlines instead of the curated pin
        /// table. Settable rather than a constructor argument because ModEntry builds this service
        /// before the engine pools the model is derived from exist; ModEntry sets it in the same
        /// pass, right after the pools are built. Null until then (and on any host that never sets
        /// it), which keeps the legacy pin-table path.</summary>
        public TheLongestYear.Core.ItemAvailabilityModel AvailabilityModel { get; set; }

        /// <summary>Qualified item ids with a catch limit (the five legendary fish), read once
        /// from Data/Locations at SaveLoaded by ModEntry and forwarded to <see cref="FarmerReset.Apply"/>
        /// on every reset so a legendary caught in an earlier loop can be caught again. Null/empty
        /// on any host that never sets it, which leaves player.fishCaught untouched.</summary>
        public IReadOnlyList<string> CatchLimitedFishIds { get; set; }

        /// <summary>Tech's Cross-Mod Bundles, for the Normal and Remixed reroll (spec 2026-10-08 addendum 2).
        /// Null skips it.</summary>
        public TheLongestYear.Core.ITechBundlesRerollTarget TechBundles { get; set; }

        /// <summary>Rebuilds <see cref="AvailabilityModel"/> for one difficulty step, called right
        /// after <see cref="PerformReset"/> re-resolves the new run's difficulty (spec
        /// 2026-08-28-obtainable-board, section 1: the week mode is a function of that step). Set by
        /// ModEntry to <c>BuildAvailabilityModelFor</c>, the same method that builds the model at
        /// SaveLoaded, so the two builds can never drift apart. Null on any host that never sets it,
        /// which keeps <see cref="AvailabilityModel"/> at whatever SaveLoaded built.</summary>
        public Func<TheLongestYear.Core.DifficultyStep, TheLongestYear.Core.ItemAvailabilityModel> RebuildAvailabilityModel { get; set; }

        /// <summary>The bundle-requirement manifest the most recent <see cref="PerformReset"/> call
        /// generated for the new loop (owned-bundle engine wiring) -- RunController.FinalizeReset
        /// re-injects this into the run via RunController.ReplaceRequirements right after PerformReset
        /// returns. Null until the first PerformReset call.</summary>
        public IReadOnlyList<BundleRequirement> LastGeneratedRequirements { get; private set; }

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
            TheLongestYear.UI.PlanningShrineService planningShrine,
            IReadOnlyDictionary<string, CoreSeason> itemSeasonPins,
            IReadOnlyDictionary<string, int[]> bundleQuotas,
            IGameContentHelper gameContent = null)
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
            _itemSeasonPins = itemSeasonPins;
            _bundleQuotas = bundleQuotas;
            _gameContent = gameContent;
        }

        /// <summary>The in-place rewind to Spring 1, as named phases in their fixed order (each phase
        /// lives in WorldResetService.ResetPhases.cs). The order matters: captures run before
        /// loadForNewGame wipes the world, the board is written after the farmer reset, and the farm is
        /// placed after the board.</summary>
        public void PerformReset()
        {
            // One-time safety backup before the first destructive reset (throws if it fails -> reset aborts).
            // Lands inside the mod folder (not in Stardew's Saves dir) so it doesn't appear as a second
            // save on the title screen.
            SaveBackup.BackupOnce(_meta, _monitor, _modDirectory);

            _monitor.Log("In-place reset: starting.", LogLevel.Info);

            var ctx = new ResetContext();
            ResolveDifficulty(ctx);
            ResolveBoardSource(ctx);
            SnapshotHeldVanillaBoard(ctx);
            ReseedWorld();
            CaptureCarryovers(ctx);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            Game1.game1.loadForNewGame(loadedGame: false);

            // 1-display. Put the zoom + UI scale back on the new Options instance. Game1.Update
            // notices the change on its next tick and calls refreshWindowSettings itself.
            RestoreStep("display options", () => DisplayOptionsCarryover.Restore(ctx.DisplayOptions, _monitor));

            // 1-seeds. First-loop-only starting seeds: loadForNewGame rebuilds the FarmHouse, whose
            // constructor (AddStarterGiftBox) drops a starter gift box of 15 parsnip seeds. FarmerReset
            // wipes the inventory but not this placed box, so it reappears every loop. PerformReset only
            // runs on resets (never the first new game), so removing it here means run 1 keeps the
            // vanilla nudge and every loop after gets none.
            RestoreStep("starter gift box", RemoveStarterGiftBox);

            ResetCommunityCenterState();
            ResetWorldStateCollections();
            RewindCalendar();
            ResetFarmer(ctx);
            RestoreKeptFarm(ctx);

            // 11. Bump CompletedResets — the single producer for the season:N meta-requirement.
            _meta.CompletedResets += 1;

            WriteBoard(ctx);
            RestoreGiftsAndIntros(ctx);
            RestoreFarmAndHouse(ctx);
            ReapplyWorldUnlocks();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}. " +
                $"Reset #{_meta.CompletedResets}.",
                LogLevel.Info);
        }

        /// <summary>One carry-over step after loadForNewGame. By then the old world is gone, so a
        /// throw must not abort the reset: that stranded the player on a fresh Spring 1 farm with
        /// the old run's state and the HUD hidden, and the next save kept that mix. Log the step
        /// and keep going; the rest of the reset still lands.</summary>
        private void RestoreStep(string name, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: the '{name}' step failed; continuing the reset without it.\n{ex}", LogLevel.Error);
            }
        }

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

        private const string GreenhouseType = "Greenhouse";

        /// <summary>Keep Greenhouse: vanilla spawns the (ruined) greenhouse at the map's default
        /// spot on every loadForNewGame. Put it back where the player had moved it (step-0c
        /// snapshot), clearing the fresh farm's debris off that footprint first (Jeff, 2026-08-29).</summary>
        private void RestoreGreenhouseSpot()
        {
            if (!_meta.HasUpgrade(GiftLadder.KeepGreenhouseId)) return;
            if (!_meta.KeptBuildingSpots.TryGetValue(GreenhouseType, out BuildingSpot spot)) return;
            Farm farm = Game1.getFarm();
            Building greenhouse = farm.buildings.FirstOrDefault(b => b.buildingType.Value == GreenhouseType);
            if (greenhouse == null)
            {
                _monitor.Log("Keep Greenhouse: no greenhouse building on the fresh farm; nothing moved.", LogLevel.Warn);
                return;
            }
            if (greenhouse.tileX.Value == spot.X && greenhouse.tileY.Value == spot.Y)
                return;

            ClearFootprint(farm, spot.X, spot.Y, greenhouse.tilesWide.Value, greenhouse.tilesHigh.Value);
            int fromX = greenhouse.tileX.Value, fromY = greenhouse.tileY.Value;
            greenhouse.tileX.Value = spot.X;
            greenhouse.tileY.Value = spot.Y;
            greenhouse.performActionOnBuildingPlacement();
            farm.OnBuildingMoved(greenhouse);
            _monitor.Log($"Keep Greenhouse: moved from ({fromX},{fromY}) to the player's spot ({spot.X},{spot.Y}).", LogLevel.Info);
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

        // FALLBACK tile coords for each kept-building blueprint, used only when
        // MetaState.KeptBuildingSpots has no snapshot for the family (fresh meta from before
        // v0.11.44). Normal path: the building goes back where the player had it (step 0c).
        //
        // The 1.6 FARMHOUSE is itself a Building at (59,12) whose footprint reaches at
        // least (67,16) and whose sprite draws over everything in x59-67 above it —
        // tiles must avoid BOTH. 2026-07-13 playtest: the silo's old tile (60,9) placed
        // it invisibly behind the farmhouse roof, and the old barn tile (62,12) sat
        // inside the farmhouse footprint outright.
        private static readonly Dictionary<string, Vector2> BuildingTiles = new()
        {
            ["Coop"]         = new Vector2(54f, 9f),
            ["Big Coop"]     = new Vector2(54f, 9f),
            ["Deluxe Coop"]  = new Vector2(54f, 9f),
            ["Barn"]         = new Vector2(46f, 12f),
            ["Big Barn"]     = new Vector2(46f, 12f),
            ["Deluxe Barn"]  = new Vector2(46f, 12f),
            // 3x3 silo just west of the coop (x54-59, y9-11), clear of the pet bowl (53,7).
            ["Silo"]         = new Vector2(51f, 9f),
        };

        // Vanilla's Traveling Cart year-1 guarantee window, transcribed from Game1.loadForNewGame
        // (Game1.cs:4212-4218): seed = uniqueIDForThisGame * 12, window = random.Next(2, ((16-2)*2)+3).
        private const double Y1GuaranteeSeedFactor = 12.0;
        private const int Y1GuaranteeMinVisits = 2;
        private const int Y1GuaranteeMaxVisits = 31;

        // The rest of the NetWorldState wipe-by-default pass (audit 2026-07-13). Rulings:
        // WIPE = run progression that must rewind with the year (island/walnut chain, raccoon
        // chain, perfection counters, Shrine-of-Challenge difficulty, in-flight Robin/Wizard
        // builds, world-state flags, daily ephemera). KEEP = save-level configuration or
        // session state (whichFarm/whichModFarm, ShuffleMineChests remix option, player
        // limits/privacy/farmhands, pause flags, LocationsWithBuildings — maintained by the
        // engine, DishOfTheDay — re-rolled by loadForNewGame). Calendar, weather, bundles,
        // museum, lost books, and mine levels are handled in their own steps above/below.
        private void ResetNetWorldStateLeftovers()
        {
            NetWorldState ws = Game1.netWorldState.Value;

            // Ginger Island / golden-walnut chain.
            ws.GoldenWalnuts = 0;
            ws.GoldenWalnutsFound = 0;
            ws.GoldenCoconutCracked = false;
            ws.FoundBuriedNuts.Clear();
            ws.IslandVisitors.Clear();
            ws.ParrotPlatformsUnlocked = false;
            ws.ActivatedGoldenParrot = false;
            ws.IsGoblinRemoved = false;
            ws.IsSubmarineLocked = false;

            // Perfection-adjacent + misc progression counters.
            ws.MiniShippingBinsObtained = 0;
            ws.PerfectionWaivers = 0;
            ws.TreasureTotemsUsed = 0;

            // Raccoon chain (giant stump requests).
            ws.TimesFedRaccoons = 0;
            ws.SeasonOfCurrentRacconBundle = -1;
            ws.DaysPlayedWhenLastRaccoonBundleWasFinished = 0;
            for (int i = 0; i < ws.raccoonBundles.Count; i++)
                ws.raccoonBundles[i] = false;

            // Shrine of Challenge / hard-mode toggles.
            ws.MinesDifficulty = 0;
            ws.SkullCavesDifficulty = 0;

            // In-flight Robin/Wizard constructions reference buildings the world wipe just
            // deleted — same class as Clint's toolBeingUpgraded in FarmerReset.
            ws.Builders.Clear();

            // World-state flags (trash bear, one-time map states, …) mirror the static
            // Game1.worldStateIDs set; clear both sides so nothing re-syncs back.
            foreach (string id in Game1.worldStateIDs.ToArray())
                ws.removeWorldStateID(id);
            Game1.worldStateIDs.Clear();

            // Daily ephemera the skipped vanilla day-start would have refreshed.
            ws.ActivePassiveFestivals.Clear();
            ws.CheckedGarbage.Clear();
            ws.canDriveYourselfToday.Value = false;
            ws.goldenClocksTurnedOff.Value = false;

            // Dish of the Day. Corrects the 2026-07-13 ruling, which kept it on the reasoning that
            // loadForNewGame re-rolls it — it does not. Game1.UpdateDishOfTheDay (Game1.cs:9432) is
            // reached only from the _newDayAfterFade night chain, which the rewind skips entirely, so
            // Gus opened Spring 1 still selling the dish from the day the reset fired. A real vanilla
            // Spring 1 has NO dish of the day for the same reason, and every consumer null-checks it
            // (ItemQueryResolver.cs:145, DefaultPhoneHandler.cs:394), so null is both the safe value
            // and the vanilla-accurate one. Day 2's night pass rolls the new run's first dish.
            ws.DishOfTheDay = null;

            // Traveling Cart year-1 red-cabbage guarantee. Corrects the 2026-07-13 ruling, which
            // set this to -1 as "the new-game sentinel" — it is not. -1 means the guarantee is
            // DISABLED: both consumers gate on `>= 0` before decrementing (Forest.cs:763,
            // Game1.cs:9020). Vanilla rolls the window once, at save creation, and only when the
            // YearOneCompletable new-game option is on (Game1.cs:4204-4219); loadForNewGame cannot
            // re-roll it during a reset because newGameSetupOptions is not populated outside the
            // new-game flow. So on a YearOneCompletable save the old line permanently killed the
            // guarantee at the first rewind. Re-roll the window with vanilla's own formula off the
            // freshly re-seeded uniqueIDForThisGame instead, and only when the save had it armed —
            // a save that never enabled the option sits at -1 and is left alone.
            if (ws.VisitsUntilY1Guarantee >= 0)
                ws.VisitsUntilY1Guarantee =
                    Utility.CreateRandom((double)Game1.uniqueIDForThisGame * Y1GuaranteeSeedFactor)
                        .Next(Y1GuaranteeMinVisits, Y1GuaranteeMaxVisits);

            _monitor.Log("In-place reset: netWorldState leftovers wiped (island/walnuts, raccoons, " +
                "perfection counters, mine difficulty, builders, world flags, daily ephemera, " +
                "dish of the day).",
                LogLevel.Trace);
        }

        // Refresh MetaState.KeptBuildingSpots from the live farm: for each known family
        // (coop/barn/silo, plus the greenhouse), remember the tile of the building the rewind
        // keeps (KeptBuildingSpotPicker: highest tier, then the last remembered spot, then the
        // first built). Families with no live building keep their previous entry (a demolished
        // building still remembers its spot). Before 0.19.22 the LAST building of a family won,
        // so a second coop pulled the kept one onto its spot (bug 2026-09-25).
        private void SnapshotKeptBuildingSpots()
        {
            var buildings = new List<FamilyBuilding>();
            foreach (Building b in Game1.getFarm().buildings)
            {
                var (family, tier) = ChainInfo(b.buildingType.Value);
                // The greenhouse is not a kept-building chain (vanilla spawns it), but Keep
                // Greenhouse (Gifts of the Junimos) puts it back where the player moved it.
                if (family.Length == 0 && b.buildingType.Value == GreenhouseType)
                    (family, tier) = (GreenhouseType, 1);
                if (family.Length == 0)
                    continue;
                buildings.Add(new FamilyBuilding(family, tier, new BuildingSpot(b.tileX.Value, b.tileY.Value)));
            }
            _meta.KeptBuildingSpots = KeptBuildingSpotPicker.Pick(buildings, _meta.KeptBuildingSpots);
        }

        private void ApplyKeptBuildings(IReadOnlyList<string> buildings)
        {
            Farm farm = Game1.getFarm();
            foreach (string blueprint in buildings)
            {
                // Keep Fish Pond has its own restore (step 9a, FishPondCarryoverService).
                if (blueprint == FishPondKeep.BuildingType)
                    continue;

                // Player's own spot first (step-0c snapshot); fixed tile only as legacy fallback.
                Vector2 tile;
                if (_meta.KeptBuildingSpots.TryGetValue(ChainInfo(blueprint).Family, out BuildingSpot spot))
                    tile = new Vector2(spot.X, spot.Y);
                else if (!BuildingTiles.TryGetValue(blueprint, out tile))
                {
                    _monitor.Log($"Reset: no tile mapped for kept building '{blueprint}', skipping.",
                        LogLevel.Warn);
                    continue;
                }

                // Already there? Some farm types spawn a starter building on every
                // loadForNewGame (Meadowlands: a Coop at its default tile). Never duplicate it;
                // instead walk it to the player's own spot, like Keep Greenhouse does
                // (2026-09-06, first Meadowlands rewind: the kept coop was skipped and the
                // player's placement lost).
                List<Building> fresh = farm.buildings.ToList();
                var (match, index) = AnimalHousing.FindOnFreshFarm(
                    blueprint, fresh.Select(f => f.buildingType.Value).ToList());
                Building existing = match == KeptBuildingMatch.Exact ? fresh[index] : null;
                // A LOWER tier of the kept chain is the farm type's starter (Meadowlands' Coop under
                // a kept Big or Deluxe Coop). Building the kept one beside the exact-type check left
                // the starter under it, overlapping. Take the starter down and move its animals
                // (Meadowlands' two chickens) into the kept building once it stands.
                List<FarmAnimal> starterAnimals = null;
                if (match == KeptBuildingMatch.LowerTier)
                {
                    Building starter = fresh[index];
                    starterAnimals = new List<FarmAnimal>();
                    if (starter.GetIndoors() is AnimalHouse starterHouse)
                    {
                        starterAnimals.AddRange(starterHouse.animals.Values);
                        starterHouse.animals.Clear();
                        starterHouse.animalsThatLiveHere.Clear();
                    }
                    farm.buildings.Remove(starter);
                    _monitor.Log($"Reset: starter '{starter.buildingType.Value}' at ({starter.tileX.Value},{starter.tileY.Value}) " +
                        $"replaced by the kept '{blueprint}' ({starterAnimals.Count} animal(s) move in).", LogLevel.Info);
                }
                if (existing != null)
                {
                    if (existing.tileX.Value != (int)tile.X || existing.tileY.Value != (int)tile.Y)
                    {
                        int fromX = existing.tileX.Value, fromY = existing.tileY.Value;
                        ClearFootprint(farm, (int)tile.X, (int)tile.Y, existing.tilesWide.Value, existing.tilesHigh.Value);
                        existing.tileX.Value = (int)tile.X;
                        existing.tileY.Value = (int)tile.Y;
                        existing.performActionOnBuildingPlacement();
                        farm.OnBuildingMoved(existing);
                        _monitor.Log($"Reset: kept building '{blueprint}' already on the fresh farm at ({fromX},{fromY}); moved to the player's spot ({tile.X},{tile.Y}).", LogLevel.Info);
                    }
                    continue;
                }

                // 1.6 factory: honours BuildingData.BuildingType (typed subclasses) where new Building()
                // would not — same result for Coop/Barn today, safer for Silo + mod-added buildings.
                var b = Building.CreateInstanceFromId(blueprint, tile);
                b.daysOfConstructionLeft.Value = 0;   // skip the construction animation
                b.load();                              // initialises interior
                farm.buildings.Add(b);
                // Building.load() -> LoadFromBuildingData(data) defaults forConstruction:false, so
                // InitializeIndoor bails before placing BuildingData.IndoorItems — the coop/barn hay
                // hopper. Vanilla only places them on construction (forConstruction:true) or upgrade,
                // which is why "upgrade the coop" fixed it for users (Nexus bug 1110130). Preferred
                // over performActionOnConstruction (sounds, construction timer, AddMailOnBuild).
                b.InitializeIndoor(b.GetData(), forConstruction: true, forUpgrade: false);

                // Bulldoze the footprint. The fresh farm regenerates random debris, trees, and
                // stump/boulder clumps anywhere — including on the player's chosen spot — so
                // objects alone (Robin's check) aren't enough: the building must win the tile
                // (2026-07-13 user ruling: "even if you have to clear out some stuff").
                ClearFootprint(farm, (int)tile.X, (int)tile.Y, b.tilesWide.Value, b.tilesHigh.Value);

                _monitor.Log($"Reset: kept building '{blueprint}' placed at ({tile.X},{tile.Y}).",
                    LogLevel.Info);

                if (starterAnimals is { Count: > 0 } && b.GetIndoors() is AnimalHouse keptHouse)
                {
                    foreach (FarmAnimal animal in starterAnimals)
                    {
                        animal.home = b;
                        keptHouse.adoptAnimal(animal);
                    }
                }
            }
        }


        private void ApplyStartingAnimals(IReadOnlyList<StartingAnimal> animals)
        {
            if (animals.Count == 0) return;
            Farm farm = Game1.getFarm();

            foreach (var animal in animals)
            {
                var requiredInfo = ChainInfo(animal.HousingType);
                // adoptAnimal never checks capacity, so skip full houses here. The Herd Book animals
                // are already in (they move in first, option C 2026-09-25), so isFull() counts them.
                Building housing = farm.buildings.FirstOrDefault(b =>
                {
                    var info = ChainInfo(b.buildingType.Value);
                    return info.Family == requiredInfo.Family && info.Tier >= requiredInfo.Tier
                        && b.GetIndoors() is AnimalHouse candidate && !candidate.isFull();
                });
                if (housing == null)
                {
                    _monitor.Log(
                        $"Reset: no '{animal.HousingType}'-or-better building with room for " +
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
                    AnimalSpecies.Record(_meta.AnimalSpeciesEverOwned, animal.VanillaType);
                    _monitor.Log($"Reset: starting animal '{animal.VanillaType}' placed in {housing.buildingType.Value}.", LogLevel.Info);
                }
            }
        }

        // See TheLongestYear.Core.AnimalHousing.
        private static (string Family, int Tier) ChainInfo(string blueprint) => AnimalHousing.Chain(blueprint);

        // Force-clear a building footprint on the fresh farm: spawned objects/forage
        // (removeObjectsAndSpawned), terrain features (trees, grass, hoed dirt), and
        // stump/boulder resource clumps. The kept building always wins its tiles.
        internal static void ClearFootprint(Farm farm, int tileX, int tileY, int width, int height)
        {
            farm.removeObjectsAndSpawned(tileX, tileY, width, height);

            for (int x = tileX; x < tileX + width; x++)
                for (int y = tileY; y < tileY + height; y++)
                    farm.terrainFeatures.Remove(new Vector2(x, y));

            for (int i = farm.resourceClumps.Count - 1; i >= 0; i--)
            {
                var clump = farm.resourceClumps[i];
                bool overlaps = false;
                for (int x = tileX; x < tileX + width && !overlaps; x++)
                    for (int y = tileY; y < tileY + height && !overlaps; y++)
                        overlaps = clump.occupiesTile(x, y);
                if (overlaps)
                    farm.resourceClumps.RemoveAt(i);
            }
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

        /// <summary>Gives a vanilla-generated board the three ask-side modifiers it can honour:
        /// stack size, quality asks, and required slots. Never changes which item a slot asks for,
        /// so a Standard or Remixed board keeps its identity and only its numbers move.
        ///
        /// When those three are all Normal the dials are skipped, and only a capped ask above one
        /// (Prismatic Shard, Mystery Box: Remixed's Helper's "5 Mystery Box") is lowered to one.
        /// A board without one gets zero writes and no extra log line, as before.
        ///
        /// Seeded from the same basis as the Engine path, so a replayed reset reproduces the same
        /// board and the anti-save-scum guarantee still holds.</summary>
        private void ApplyVanillaBoardDifficulty()
        {
            TheLongestYear.Core.DifficultyProfile difficulty = _meta.Difficulty;
            if (difficulty == null || difficulty.Steps.AsksAllNormal())
            {
                ClampVanillaCappedAsks();
                return;
            }

            Dictionary<string, string> live = Game1.netWorldState.Value.BundleData;
            if (live == null || live.Count == 0)
            {
                _monitor.Log(
                    "Reset: Vanilla difficulty pass skipped — no bundle data to adjust.",
                    LogLevel.Warn);
                return;
            }

            // Quality eligibility is derived from the game's own data (crop harvests, rod-caught
            // non-jelly fish, spawned forage), and it is what stops a gold star landing on Fiber
            // or on algae (Nexus 1122358). Only built when the quality modifier is actually above
            // Normal, because deriving the pools reads several data assets.
            IReadOnlySet<string> qualityEligibleIds = null;
            if (difficulty.QualityFactor > 1.0)
            {
                try
                {
                    qualityEligibleIds = new GameDataPools(_monitor)
                        .Build(_config.PoolTuning, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.HasUpgrade, difficulty.Steps.ItemRarity))
                        .QualityEligibleIds;
                }
                catch (Exception ex)
                {
                    // Without the derived set the built-in never-quality list still guards the
                    // known-impossible items, so degrade rather than abandon the reset.
                    _monitor.Log(
                        $"Reset: could not derive quality eligibility for the Vanilla difficulty pass ({ex.Message}); " +
                        "falling back to the built-in ineligible list only.",
                        LogLevel.Warn);
                }
            }

            int seed = BundleEngineSeed.For(
                unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);

            IDictionary<string, string> adjusted = TheLongestYear.Core.VanillaBoardDifficultyPass.Apply(
                new Dictionary<string, string>(live), difficulty, _config.PoolTuning, seed, qualityEligibleIds);

            Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(adjusted));

            _monitor.Log(
                $"Reset: Vanilla board adjusted for difficulty ({adjusted.Count} bundles; " +
                $"stacks {difficulty.Steps.StackSize}, quality {difficulty.Steps.QualityAsks}, " +
                $"required slots {difficulty.Steps.RequiredSlots}; seed {seed}). " +
                "Item ids are unchanged.",
                LogLevel.Info);
        }

        /// <summary>Randomizer "Random bundle rewards" on a freshly built Vanilla or Remixed board.
        /// Runs after the difficulty pass and never on a held board (the held snapshot already
        /// carries its rewards). Only field 1 of each bundle changes. Same seed basis as
        /// <see cref="ApplyVanillaBoardDifficulty"/> and the same reward pool the Engine draws from.</summary>
        private void ApplyVanillaBoardRewardShuffle()
        {
            if (!_meta.RandomBundleRewardsBoard)
                return;

            Dictionary<string, string> live = Game1.netWorldState.Value.BundleData;
            if (live == null || live.Count == 0)
            {
                _monitor.Log("Randomizer: bundle reward shuffle skipped, no bundle data on the board.", LogLevel.Warn);
                return;
            }

            IReadOnlyList<string> pool = BundleEngine.RewardPool(new VanillaBundlePool(_monitor).BuildRoomPools());
            int seed = BundleEngineSeed.For(
                unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
            IDictionary<string, string> shuffled = TheLongestYear.Core.BundleRewardShuffle.ApplyToData(
                new Dictionary<string, string>(live), seed, pool, BundleEngine.IsRewardShuffleSkippedRoom);
            Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(shuffled));

            int bundles = shuffled.Keys.Count(k => !BundleEngine.IsRewardShuffleSkippedRoom(k.Split('/')[0]));
            _monitor.Log($"Randomizer: bundle rewards shuffled ({bundles} bundles, pool {pool.Count}).", LogLevel.Info);
        }

        /// <summary>The all-Normal half of <see cref="ApplyVanillaBoardDifficulty"/>: writes only
        /// the bundles holding a capped ask above one, lowered to one. On a vanilla board the stack
        /// clamp is the whole capped rule; the per-board count is held on engine boards only, since
        /// this path never changes an item or removes a bundle.</summary>
        private void ClampVanillaCappedAsks()
        {
            Dictionary<string, string> live = Game1.netWorldState.Value.BundleData;
            if (live == null || live.Count == 0)
                return;

            var updates = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in live.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                string clamped = TheLongestYear.Core.CappedAsks.RepairBundleValue(live[key]);
                if (clamped != null)
                    updates[key] = clamped;
            }
            if (updates.Count == 0)
                return;

            Game1.netWorldState.Value.SetBundleData(updates);
            _monitor.Log(
                $"Reset: Vanilla board asks for one of each capped item per slot; lowered {updates.Count} bundle(s): " +
                string.Join(", ", updates.Keys) + ".",
                LogLevel.Info);
        }

    }
}
