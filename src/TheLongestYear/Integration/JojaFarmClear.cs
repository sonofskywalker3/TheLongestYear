using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's bulldozer: empties a tile rectangle of the farm so the drawn barn,
    /// coop and animal lines stand on bare ground whatever the player built or planted there.
    /// Objects, terrain features (crops, trees, grass, flooring), resource clumps, large terrain
    /// features (bushes) and any building overlapping it, except the main farmhouse (the scene hides
    /// that instead). Mod-tagged objects (modData keys starting "tly") are kept.
    ///
    /// IN MEMORY ONLY. This is safe only because the bad ending always ends at the title without
    /// saving: JojaBadEndingCommands fails closed to the title if the scene ends any other way, and
    /// the tlyClear command refuses to run outside the scene.</summary>
    internal static class JojaFarmClear
    {
        private const string ModDataPrefix = "tly";

        /// <summary>Clears the rectangle (tiles) and returns a count summary for the log.</summary>
        public static string Clear(Farm farm, Rectangle tiles)
        {
            Rectangle pixels = new(tiles.X * 64, tiles.Y * 64, tiles.Width * 64, tiles.Height * 64);

            int objects = 0;
            foreach (Vector2 tile in farm.objects.Keys.ToList())
            {
                if (!tiles.Contains((int)tile.X, (int)tile.Y)) continue;
                if (farm.objects.TryGetValue(tile, out SObject obj) && obj.modData.Keys.Any(k => k.StartsWith(ModDataPrefix))) continue;
                farm.objects.Remove(tile);
                objects++;
            }

            int features = 0;
            foreach (Vector2 tile in farm.terrainFeatures.Keys.ToList())
            {
                if (!tiles.Contains((int)tile.X, (int)tile.Y)) continue;
                farm.terrainFeatures.Remove(tile);
                features++;
            }

            int clumps = 0;
            foreach (ResourceClump clump in farm.resourceClumps.ToList())
            {
                if (!clump.getBoundingBox().Intersects(pixels)) continue;
                farm.resourceClumps.Remove(clump);
                clumps++;
            }

            int large = 0;
            foreach (LargeTerrainFeature feature in farm.largeTerrainFeatures.ToList())
            {
                if (!feature.getBoundingBox().Intersects(pixels)) continue;
                farm.largeTerrainFeatures.Remove(feature);
                large++;
            }

            int buildings = 0;
            Building house = farm.GetMainFarmHouse();
            var gone = new List<string>();
            foreach (Building building in farm.buildings.ToList())
            {
                if (building == house) continue;
                var footprint = new Rectangle(building.tileX.Value, building.tileY.Value, building.tilesWide.Value, building.tilesHigh.Value);
                if (!footprint.Intersects(tiles)) continue;
                farm.buildings.Remove(building);
                gone.Add(building.buildingType.Value);
                buildings++;
            }

            string names = gone.Count > 0 ? $" [{string.Join(", ", gone)}]" : "";
            return $"{objects} objects, {features} terrain features, {clumps} clumps, {large} large features, {buildings} buildings{names}";
        }
    }
}
