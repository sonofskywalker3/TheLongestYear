using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Keep Fish Pond glue (see <see cref="FishPondKeep"/>). <see cref="SnapshotSpot"/> runs with the
    /// step-0c kept-building snapshot, BEFORE loadForNewGame wipes the farm; <see cref="Restore"/>
    /// runs after every other building is back (kept buildings, greenhouse, stable), so the pond is
    /// the one that yields if its old spot is now taken.
    ///
    /// The pond always comes back EMPTY: a fresh FishPond from the building factory has no fish type,
    /// no occupants, no output, no request, no Golden Animal Cracker and gate 0, exactly as Robin
    /// leaves one. Nothing from the old pond is copied; only its top-left tile is remembered.
    /// </summary>
    internal static class FishPondCarryoverService
    {
        /// <summary>Remember the kept pond's tile: most fish wins, the first pond wins a tie. No pond
        /// on the farm leaves the previous entry alone (a filled-in pond still remembers its spot).
        /// Unconditional like the coop/barn/silo snapshot, so the first keep-owning rewind knows it.</summary>
        public static void SnapshotSpot(MetaState meta)
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return;
            List<Building> ponds = farm.buildings
                .Where(b => b.buildingType.Value == FishPondKeep.BuildingType)
                .ToList();
            int? pick = FishPondKeep.PickKeptPond(ponds.Select(p => p.currentOccupants.Value).ToList());
            if (pick == null) return;
            Building kept = ponds[pick.Value];
            meta.KeptBuildingSpots[FishPondKeep.SpotKey] = new BuildingSpot(kept.tileX.Value, kept.tileY.Value);
        }

        /// <summary>Put one empty Fish Pond back on the fresh farm, at the remembered spot if it is
        /// free, else the fallback search.</summary>
        public static void Restore(MetaState meta, IMonitor monitor)
        {
            if (meta == null || !meta.HasUpgrade(FishPondKeep.UpgradeId)) return;
            Farm farm = Game1.getFarm();
            if (farm == null) return;

            // Never duplicate. A pond the fresh farm already has (a modded farm map that starts with
            // one) is swapped for the fresh one at the player's spot, so there is still one pond.
            Building existing = farm.buildings.FirstOrDefault(b => b.buildingType.Value == FishPondKeep.BuildingType);
            if (existing != null)
            {
                farm.buildings.Remove(existing);
                monitor.Log($"Keep Fish Pond: the fresh farm already had a pond at ({existing.tileX.Value},{existing.tileY.Value}); replacing it with the kept one.", LogLevel.Info);
            }

            meta.KeptBuildingSpots.TryGetValue(FishPondKeep.SpotKey, out BuildingSpot remembered);
            BuildingSpot spot = FishPondKeep.ResolveSpot(remembered, (x, y) => TileFree(farm, x, y));
            if (spot == null)
            {
                monitor.Log("Keep Fish Pond: no free 5x5 spot on the fresh farm; no pond placed.", LogLevel.Warn);
                return;
            }
            if (remembered != null && spot != remembered)
                monitor.Log($"Keep Fish Pond: the player's spot ({remembered.X},{remembered.Y}) is taken; using ({spot.X},{spot.Y}).", LogLevel.Info);

            // 1.6 factory: BuildingData.BuildingType makes this a real FishPond, fresh and empty.
            Building pond = Building.CreateInstanceFromId(FishPondKeep.BuildingType, new Vector2(spot.X, spot.Y));
            pond.daysOfConstructionLeft.Value = 0;   // finished, no construction animation
            pond.load();
            farm.buildings.Add(pond);
            WorldResetService.ClearFootprint(farm, spot.X, spot.Y, pond.tilesWide.Value, pond.tilesHigh.Value);
            // Vanilla sets the netting style from the tile on placement (FishPond.performActionOnBuildingPlacement).
            pond.performActionOnBuildingPlacement();
            monitor.Log($"Keep Fish Pond: empty pond placed at ({spot.X},{spot.Y}).", LogLevel.Info);
        }

        // Mirrors the map side of vanilla GameLocation.isBuildable (buildable rectangle, Buildable /
        // Diggable Back properties, no blocking Buildings-layer tile, no water) plus "no other
        // building here". Debris, trees and clumps don't count: ClearFootprint removes them.
        private static bool TileFree(Farm farm, int x, int y)
        {
            if (!farm.isTileOnMap(x, y)) return false;
            Rectangle buildable = farm.GetBuildableRectangle();
            if (buildable != Rectangle.Empty && !buildable.Contains(x, y)) return false;
            var tile = new Vector2(x, y);
            if (farm.getBuildingAt(tile) != null) return false;
            if (farm.isWaterTile(x, y) || !farm.isTilePassable(tile)) return false;
            string buildableProp = farm.doesTileHavePropertyNoNull(x, y, BuildableProperty, BackLayer).ToLowerInvariant();
            if (NoValues.Contains(buildableProp)) return false;
            if (YesValues.Contains(buildableProp)) return true;
            return farm.doesTileHaveProperty(x, y, DiggableProperty, BackLayer) != null;
        }

        private const string BackLayer = "Back";
        private const string BuildableProperty = "Buildable";
        private const string DiggableProperty = "Diggable";
        private static readonly HashSet<string> NoValues = new() { "f", "false" };
        private static readonly HashSet<string> YesValues = new() { "t", "true" };
    }
}
