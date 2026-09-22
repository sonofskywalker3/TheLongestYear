using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TheLongestYear.Scenes
{
    /// <summary>Where a figure in an overnight strike scene is allowed to put his feet, and how he
    /// gets in (spec 2026-09-21). The thief uses it, and Task 9 draws Shane with the same rule.
    ///
    /// It is DELIBERATELY stricter than the game's own passability. The engine will happily let a
    /// character walk over a crop, a placed keg or a rug, because the game has collision rules a
    /// scene does not want to reason about. A thief who clips a fence post or treads through the
    /// very crops the darkness spared reads as a bug, so the scene refuses everything except bare
    /// walkable ground, grass and laid flooring.</summary>
    internal static class SceneGround
    {
        /// <summary>Bare walkable ground: on the map, passable, dry, no object, no building, no
        /// furniture, and no terrain feature except grass and laid flooring.</summary>
        public static bool CanStandOn(GameLocation where, int x, int y)
        {
            if (where?.map == null) return false;
            var tile = new Vector2(x, y);
            if (!where.isTileOnMap(tile)) return false;
            if (where.terrainFeatures.TryGetValue(tile, out TerrainFeature feature)
                && !(feature is Grass)
                && !(feature is Flooring))
                return false;
            if (where.objects.ContainsKey(tile)) return false;
            if (where.isWaterTile(x, y)) return false;
            if (where.getBuildingAt(tile) != null) return false;
            if (where.GetFurnitureAt(tile) != null) return false;
            return where.isTilePassable(new xTile.Dimensions.Location(x, y), Game1.viewport);
        }

        /// <summary>The whole map as a grid of "he may stand here", indexed <c>[x, y]</c>, for
        /// <see cref="TheLongestYear.Core.Sabotage.ScenePath"/>.</summary>
        public static bool[,] PassableGrid(GameLocation where)
        {
            if (where == null) throw new ArgumentNullException(nameof(where));
            if (where.map == null) throw new ArgumentException($"{where.NameOrUniqueName} has no map loaded, so there is no ground to walk on.", nameof(where));
            int width = where.map.Layers[0].LayerWidth;
            int height = where.map.Layers[0].LayerHeight;
            var grid = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    grid[x, y] = CanStandOn(where, x, y);
            return grid;
        }

        /// <summary>The ways into this map, for the walk to come in by: every warp tile and the
        /// tiles beside it (a door tile itself is often not somewhere a figure may stand), plus,
        /// outdoors, every passable tile on the map's own border. Indoors that is the door the
        /// spec asks for. Outdoors it is the edge, and the farm's own gates and roads as well,
        /// which is where someone walking onto the farm at night would actually come from.</summary>
        public static IReadOnlyList<(int X, int Y)> WaysIn(GameLocation where)
        {
            var ways = new List<(int X, int Y)>();
            if (where?.map == null) return ways;
            int width = where.map.Layers[0].LayerWidth;
            int height = where.map.Layers[0].LayerHeight;
            var seen = new HashSet<(int X, int Y)>();

            void Offer(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                if (!seen.Add((x, y))) return;
                if (CanStandOn(where, x, y)) ways.Add((x, y));
            }

            if (where is StardewValley.Locations.FarmHouse house)
            {
                Point entry = house.getEntryLocation();
                Offer(entry.X, entry.Y);
                Offer(entry.X, entry.Y + 1);
                Offer(entry.X, entry.Y - 1);
            }
            foreach (Warp warp in where.warps)
            {
                Offer(warp.X, warp.Y);
                Offer(warp.X, warp.Y - 1);
                Offer(warp.X, warp.Y + 1);
                Offer(warp.X - 1, warp.Y);
                Offer(warp.X + 1, warp.Y);
            }
            if (!where.IsOutdoors) return ways;
            for (int x = 0; x < width; x++)
            {
                Offer(x, 0);
                Offer(x, height - 1);
            }
            for (int y = 0; y < height; y++)
            {
                Offer(0, y);
                Offer(width - 1, y);
            }
            return ways;
        }
    }
}
