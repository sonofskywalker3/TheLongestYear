using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The Farm's kept decor, lifted off the old farm just before loadForNewGame (step 0g)
    /// and held in memory until FarmDecorCarryoverService.Restore (step 13a). Each instance is
    /// removed from the old farm so Netcode never sees one object in two collections, and the same
    /// instance goes onto the fresh farm, so gate state, sign text, rotation and a fence's torch
    /// all survive.</summary>
    internal sealed class FarmDecorSnapshot
    {
        internal sealed class Entry
        {
            public int Id;
            public DecorLayer Layer;
            public Vector2 Tile;
            public Flooring Floor;               // Ground layer
            public StardewValley.Object Obj;     // fence, torch, sign, decor big craftable
            public Furniture Furniture;          // outdoor furniture
            public List<DecorTile> Tiles;
        }

        private const int TileSize = 64;

        public readonly List<Entry> Entries = new();

        public static FarmDecorSnapshot Capture(Farm farm, IMonitor monitor)
        {
            var snap = new FarmDecorSnapshot();
            if (farm == null) return snap;

            foreach (var pair in farm.terrainFeatures.Pairs.ToList())
            {
                if (pair.Value is not Flooring floor) continue;
                farm.terrainFeatures.Remove(pair.Key);
                snap.Add(DecorLayer.Ground, pair.Key, OneTile(pair.Key), floor: floor);
            }

            foreach (var pair in farm.objects.Pairs.ToList())
            {
                StardewValley.Object obj = pair.Value;
                if (obj == null || !FarmDecorKeep.IsKeptDecor(KindOf(obj), obj.QualifiedItemId)) continue;
                farm.objects.Remove(pair.Key);
                snap.Add(DecorLayer.Object, pair.Key, OneTile(pair.Key), obj: obj);
            }

            foreach (Furniture f in farm.furniture.ToList())
            {
                if (!FarmDecorKeep.IsKeptDecor(FarmThingKind.Furniture, f.QualifiedItemId)) continue;
                StashItemCodec.StripNonCosmetic(f);
                farm.furniture.Remove(f);
                snap.Add(DecorLayer.Object, f.TileLocation, TilesOf(f.boundingBox.Value), furniture: f);
            }

            monitor.Log($"Keep Farm Decor: lifted {snap.Entries.Count} piece(s) off the farm before the rewind.", LogLevel.Info);
            return snap;
        }

        internal static FarmThingKind KindOf(StardewValley.Object obj) => obj switch
        {
            Chest => FarmThingKind.Object,          // chests are never kept (the stash included)
            Fence => FarmThingKind.Fence,
            Torch => FarmThingKind.Torch,
            Sign => FarmThingKind.Sign,
            _ when obj.bigCraftable.Value => FarmThingKind.BigCraftable,
            _ => FarmThingKind.Object,
        };

        internal static List<DecorTile> OneTile(Vector2 tile) => new() { new DecorTile((int)tile.X, (int)tile.Y) };

        internal static List<DecorTile> TilesOf(Rectangle pixels)
        {
            var tiles = new List<DecorTile>();
            for (int x = pixels.X / TileSize; x <= (pixels.Right - 1) / TileSize; x++)
                for (int y = pixels.Y / TileSize; y <= (pixels.Bottom - 1) / TileSize; y++)
                    tiles.Add(new DecorTile(x, y));
            return tiles;
        }

        private void Add(DecorLayer layer, Vector2 tile, List<DecorTile> tiles,
            Flooring floor = null, StardewValley.Object obj = null, Furniture furniture = null)
            => Entries.Add(new Entry { Id = Entries.Count, Layer = layer, Tile = tile, Tiles = tiles, Floor = floor, Obj = obj, Furniture = furniture });
    }
}
