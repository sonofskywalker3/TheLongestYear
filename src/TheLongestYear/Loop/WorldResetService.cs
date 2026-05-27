using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using TheLongestYear.Core;

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

        public ProfessionPickerScheduler ProfessionPicker => _professionPicker;

        public WorldResetService(
            IMonitor monitor,
            TheLongestYear.Core.MetaState meta,
            TheLongestYear.Core.RunState run,
            TheLongestYear.Core.GameplayConfig config,
            CommunityCenterUnlock ccUnlock,
            string modDirectory,
            FarmerReset farmerReset,
            ProfessionPickerScheduler professionPicker)
        {
            _monitor = monitor;
            _meta = meta;
            _run = run;
            _config = config;
            _ccUnlock = ccUnlock;
            _modDirectory = modDirectory;
            _farmerReset = farmerReset;
            _professionPicker = professionPicker;
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

            string newSavePath = null;
            if (!string.IsNullOrEmpty(oldSavePath))
            {
                string oldFolder = Path.GetFileName(oldSavePath);
                int splitAt = oldFolder.LastIndexOf('_');
                if (splitAt > 0)
                {
                    string playerNamePart = oldFolder.Substring(0, splitAt);
                    string newFolder = $"{playerNamePart}_{Game1.uniqueIDForThisGame}";
                    newSavePath = Path.Combine(Path.GetDirectoryName(oldSavePath), newFolder);
                }
            }

            if (!string.IsNullOrEmpty(oldSavePath) && !string.IsNullOrEmpty(newSavePath)
                && oldSavePath != newSavePath && Directory.Exists(oldSavePath) && !Directory.Exists(newSavePath))
            {
                try
                {
                    Directory.Move(oldSavePath, newSavePath);
                    _monitor.Log(
                        $"In-place reset: renamed save folder to match new uniqueID ({Path.GetFileName(newSavePath)}).",
                        LogLevel.Trace);
                }
                catch (IOException ex)
                {
                    _monitor.Log(
                        $"In-place reset: could NOT rename save folder ({ex.Message}). " +
                        "Next save will create a new folder; the previous one will appear as a duplicate on the title screen.",
                        LogLevel.Warn);
                }
            }
            _monitor.Log(
                $"In-place reset: new uniqueIDForThisGame={Game1.uniqueIDForThisGame}.",
                LogLevel.Trace);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            Game1.game1.loadForNewGame(loadedGame: false);

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

            // 3. Capture the in-run peaks from the live player BEFORE the wipe — the cap
            //    side of cap-not-grant. The Farmer-side wipe happens inside
            //    _farmerReset.Apply, so peak-reading has to land here.
            PlayerSnapshot peaks = CapturePeaks(Game1.player);

            // 4. Build the reset baseline + apply Farmer-side state (gold, items, tool
            //    tiers, skill levels, kitchen flag).
            RunBaseline baseline = RunBaselineBuilder.Build(_meta, _run, peaks, _config.StartingMoney);
            _farmerReset.Apply(Game1.player, baseline);

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

            // 8. Pre-build kept buildings on the Farm. Coords are deterministic — we always
            //    use the same tiles so subsequent runs land buildings in the same spots.
            ApplyKeptBuildings(baseline.KeptBuildings);

            // 9. Early horse + stable.
            if (baseline.EarlyHorse)
                ApplyEarlyHorse();

            // 10. Place starting animals into matching housing.
            ApplyStartingAnimals(baseline.StartingAnimals);

            // 11. Bump CompletedResets — the single producer for the season:N meta-requirement.
            _meta.CompletedResets += 1;

            // 12. Place the player home, awake, in the rebuilt FarmHouse. resetForPlayerEntry
            //     also rebuilds the FarmHouse layout to match HouseUpgradeLevel — picking up
            //     the kitchen if the baseline set it.
            GameLocation home = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.currentLocation = home;
            Game1.currentLocation = home;
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            home.resetForPlayerEntry();

            // Re-apply the CC unlock so the loop preserves day-1 CC access (loadForNewGame + FarmerReset wiped it).
            _ccUnlock.Apply();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}. " +
                $"Reset #{_meta.CompletedResets}.",
                LogLevel.Info);
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

        private static readonly Vector2 StableTile = new(48f, 7f);

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
            }
        }

        private void ApplyEarlyHorse()
        {
            Farm farm = Game1.getFarm();
            // Skip if stable already there (idempotent across re-resets).
            if (farm.buildings.OfType<Stable>().Any())
                return;

            var stable = new Stable(StableTile);
            stable.daysOfConstructionLeft.Value = 0;
            stable.load();
            farm.buildings.Add(stable);
            stable.grabHorse();   // spawns the Horse NPC matched to the stable's HorseId
        }

        private void ApplyStartingAnimals(IReadOnlyList<StartingAnimal> animals)
        {
            if (animals.Count == 0) return;
            Farm farm = Game1.getFarm();

            foreach (var animal in animals)
            {
                Building housing = farm.buildings.FirstOrDefault(
                    b => b.buildingType.Value == animal.HousingType
                      || ChainTier(b.buildingType.Value) >= ChainTier(animal.HousingType));
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

        // Rank Coop=1, Big Coop=2, Deluxe Coop=3 (and same for Barn). "Or better" check
        // for animal placement — a Big Coop satisfies "needs Coop", a Deluxe Coop
        // satisfies "needs Big Coop", etc.
        private static int ChainTier(string blueprint) => blueprint switch
        {
            "Coop"         => 1,
            "Big Coop"     => 2,
            "Deluxe Coop"  => 3,
            "Barn"         => 1,
            "Big Barn"     => 2,
            "Deluxe Barn"  => 3,
            _ => 0
        };
    }
}
