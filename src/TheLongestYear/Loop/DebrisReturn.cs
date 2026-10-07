using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard debris_return (spec section 8): the farm map's Paths-layer markers 19/20/21 are where
    /// vanilla puts a hollow log, boulder and stump when the farm is first built
    /// (<c>GameLocation.loadPathsLayerObjectsInArea</c>). On the reveal each marker whose whole 2x2
    /// footprint is free gets its clump back, through the same
    /// <c>addResourceClumpAndRemoveUnderlyingTerrain</c> call. "Free" is the Forest farm's own stump
    /// respawn test (<c>Farm.DayUpdate</c>): <c>CanItemBePlacedHere</c> on every collision layer with
    /// nothing passable ignored, so a building, crop, tree, grass, object, furniture, flooring or
    /// path, an existing clump, or anyone standing there keeps the spot clear. Runs once, on the
    /// reveal; a reload never re-adds what the player has already cleared.
    /// </summary>
    internal static class DebrisReturn
    {
        private const string PathsLayer = "Paths";

        public static int Apply(IMonitor monitor)
        {
            if (!RunActivation.IsActive || !DayEffects.Has(WildcardSchedule.DebrisReturn)) return 0;
            if (!Game1.IsMasterGame) return 0;
            Farm farm = Game1.getFarm();
            if (farm?.Map?.GetLayer(PathsLayer) == null)
            {
                monitor.Log("Wildcard debris: the farm has no Paths layer; nothing returned.", LogLevel.Info);
                return 0;
            }
            int width = farm.Map.Layers[0].LayerWidth;
            int height = farm.Map.Layers[0].LayerHeight;
            int placed = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int? clump = DebrisPlacement.ClumpFor(farm.getTileIndexAt(x, y, PathsLayer));
                    if (clump == null) continue;
                    if (!DebrisPlacement.FreeFootprint((tx, ty) => IsFree(farm, tx, ty), x, y)) continue;
                    farm.addResourceClumpAndRemoveUnderlyingTerrain(
                        clump.Value, DebrisPlacement.Size, DebrisPlacement.Size, new Vector2(x, y));
                    placed++;
                }
            }
            monitor.Log($"Wildcard debris: {placed} stump(s), boulder(s) and log(s) returned to the farm.", LogLevel.Info);
            return placed;
        }

        private static bool IsFree(Farm farm, int x, int y)
            => farm.CanItemBePlacedHere(new Vector2(x, y), itemIsPassable: false, CollisionMask.All, CollisionMask.None);
    }
}
