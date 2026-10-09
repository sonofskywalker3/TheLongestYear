using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;
using SObject = StardewValley.Object;

namespace TheLongestYear.Integration
{
    /// <summary>The tiles round the porch where a Junimo would be hidden or crowded, for <see
    /// cref="PorchMarks"/>: anything standing on a tile (objects, big craftables, furniture, buildings,
    /// resource clumps, bushes, trees, a crop, a wall or water on the map) and every tile a taller
    /// sprite is drawn over from below (a big craftable's top half, a furniture sprite taller than
    /// its footprint, a building's roof, a tree's canopy, a map Front or AlwaysFront tile). The
    /// farmhouse itself is left out: the scene is staged in front of it and the marks start below
    /// its porch.</summary>
    internal static class PorchOcclusion
    {
        /// <summary>How far round the door the scan reaches (the marks and their fallbacks are inside).</summary>
        private const int ReachX = 14;
        private const int ReachUp = 1;
        private const int ReachDown = PorchMarks.LastRow + PorchMarks.FallbackRadius + 2;

        private const int TilePx = Game1.tileSize;
        private const int SpriteScale = 4;

        /// <summary>A grown wild tree's canopy: three tiles wide, five above the trunk.</summary>
        private const int TreeGrown = 5;
        private const int FruitTreeGrown = 4;
        private const int CanopyHalfWidth = 1;
        private const int CanopyRows = 5;

        private const string FarmhouseType = "Farmhouse";

        /// <summary>The blocked tiles round (<paramref name="doorX"/>, <paramref name="doorY"/>) on
        /// <paramref name="location"/>.</summary>
        public static HashSet<(int X, int Y)> Blocked(GameLocation location, int doorX, int doorY)
        {
            var blocked = new HashSet<(int X, int Y)>();
            if (location == null) return blocked;
            var area = new Rectangle(doorX - ReachX, doorY - ReachUp, ReachX * 2 + 1, ReachUp + ReachDown + 1);
            void Add(int x, int y) { if (area.Contains(x, y)) blocked.Add((x, y)); }
            void AddPixels(Rectangle px)
            {
                if (px.Width <= 0 || px.Height <= 0) return;
                int left = (int)Math.Floor(px.Left / (float)TilePx), right = (int)Math.Floor((px.Right - 1) / (float)TilePx);
                int top = (int)Math.Floor(px.Top / (float)TilePx), bottom = (int)Math.Floor((px.Bottom - 1) / (float)TilePx);
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++)
                        Add(x, y);
            }

            // The map: walls, water and anything drawn over a character.
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                {
                    if (!location.isTileOnMap(x, y)) { blocked.Add((x, y)); continue; }
                    if (location.getTileIndexAt(x, y, "Buildings") >= 0
                        || location.getTileIndexAt(x, y, "Front") >= 0
                        || location.getTileIndexAt(x, y, "AlwaysFront") >= 0
                        || location.isWaterTile(x, y))
                        blocked.Add((x, y));
                }

            // Objects; a big craftable is two tiles tall, drawn over the tile above its own.
            foreach (KeyValuePair<Vector2, SObject> pair in location.objects.Pairs)
            {
                int x = (int)pair.Key.X, y = (int)pair.Key.Y;
                Add(x, y);
                if (pair.Value != null && pair.Value.bigCraftable.Value) Add(x, y - 1);
            }

            // Furniture: its footprint, and its sprite drawn up from the footprint's bottom.
            foreach (Furniture f in location.furniture)
            {
                Rectangle box = f.boundingBox.Value;
                AddPixels(box);
                Rectangle source = f.sourceRect.Value;
                int drawnHeight = source.Height * SpriteScale;
                if (drawnHeight > box.Height)
                    AddPixels(new Rectangle(box.X, box.Bottom - drawnHeight, box.Width, drawnHeight));
            }

            // Buildings other than the farmhouse: the footprint and the roof drawn above it.
            foreach (Building b in location.buildings)
            {
                if (b == null || b.buildingType.Value == FarmhouseType) continue;
                var footprint = new Rectangle(b.tileX.Value * TilePx, b.tileY.Value * TilePx, b.tilesWide.Value * TilePx, b.tilesHigh.Value * TilePx);
                AddPixels(footprint);
                int drawnHeight;
                try { drawnHeight = b.getSourceRect().Height * SpriteScale; }
                catch (Exception) { drawnHeight = footprint.Height; }   // a building whose texture is missing
                if (drawnHeight > footprint.Height)
                    AddPixels(new Rectangle(footprint.X, footprint.Bottom - drawnHeight, footprint.Width, drawnHeight));
            }

            foreach (ResourceClump clump in location.resourceClumps)
                AddPixels(clump.getBoundingBox());

            // Bushes: the footprint and the row their leaves are drawn over.
            foreach (LargeTerrainFeature feature in location.largeTerrainFeatures)
            {
                Rectangle box = feature.getBoundingBox();
                AddPixels(box);
                AddPixels(new Rectangle(box.X, box.Y - TilePx, box.Width, TilePx));
            }

            // Trees (a canopy over the tiles above a grown one), and a crop standing in its soil.
            foreach (KeyValuePair<Vector2, TerrainFeature> pair in location.terrainFeatures.Pairs)
            {
                int x = (int)pair.Key.X, y = (int)pair.Key.Y;
                switch (pair.Value)
                {
                    case Tree tree:
                        Add(x, y);
                        if (tree.growthStage.Value >= TreeGrown) AddCanopy(Add, x, y);
                        else Add(x, y - 1);
                        break;
                    case FruitTree fruit:
                        Add(x, y);
                        if (fruit.growthStage.Value >= FruitTreeGrown) AddCanopy(Add, x, y);
                        else Add(x, y - 1);
                        break;
                    case HoeDirt dirt when dirt.crop != null:
                        Add(x, y);
                        break;
                }
            }
            return blocked;
        }

        private static void AddCanopy(Action<int, int> add, int x, int y)
        {
            for (int dy = 1; dy <= CanopyRows; dy++)
                for (int dx = -CanopyHalfWidth; dx <= CanopyHalfWidth; dx++)
                    add(x + dx, y - dy);
        }

        /// <summary>The Junimos' tiles for a porch scene staged at this door: <see cref="PorchMarks"/>
        /// over the Farm's blocked tiles.</summary>
        public static IReadOnlyList<(int X, int Y)> MarksFor(int doorX, int doorY, int count)
        {
            Farm farm = Game1.getFarm();
            return PorchMarks.Choose(doorX, doorY, count, Blocked(farm, doorX, doorY));
        }
    }
}
