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
    }
}
