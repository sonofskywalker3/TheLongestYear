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

            // Each piece is lifted on its own: one that throws (a mod's object or furniture hook)
            // stays on the farm, logged, and the rest of the reset goes on.
            int failed = 0;
            foreach (var pair in farm.terrainFeatures.Pairs.ToList())
            {
                if (pair.Value is not Flooring floor) continue;
                if (!TryLift(monitor, floor.GetType().Name, pair.Key,
                        remove: () => farm.terrainFeatures.Remove(pair.Key),
                        stillThere: () => farm.terrainFeatures.TryGetValue(pair.Key, out var tf) && tf == floor))
                    failed++;
                else
                    snap.Add(DecorLayer.Ground, pair.Key, OneTile(pair.Key), floor: floor);
            }

            foreach (var pair in farm.objects.Pairs.ToList())
            {
                StardewValley.Object obj = pair.Value;
                if (obj == null || !FarmDecorKeep.IsKeptDecor(KindOf(obj), obj.QualifiedItemId)) continue;
                if (!TryLift(monitor, obj.QualifiedItemId, pair.Key,
                        remove: () => farm.objects.Remove(pair.Key),
                        stillThere: () => farm.objects.TryGetValue(pair.Key, out var o) && o == obj))
                    failed++;
                else
                    snap.Add(DecorLayer.Object, pair.Key, OneTile(pair.Key), obj: obj);
            }

            foreach (Furniture f in farm.furniture.ToList())
            {
                if (!FarmDecorKeep.IsKeptDecor(FarmThingKind.Furniture, f.QualifiedItemId)) continue;
                if (!TryLift(monitor, f.QualifiedItemId, f.TileLocation,
                        remove: () =>
                        {
                            StashItemCodec.StripNonCosmetic(f);
                            farm.furniture.Remove(f);
                        },
                        stillThere: () => farm.furniture.Contains(f)))
                    failed++;
                else
                    snap.Add(DecorLayer.Object, f.TileLocation, TilesOf(f.boundingBox.Value), furniture: f);
            }

            monitor.Log($"Keep Farm Decor: lifted {snap.Entries.Count} piece(s) off the farm before the rewind" +
                        (failed > 0 ? $"; {failed} could not be lifted and stay with the old farm." : "."),
                        failed > 0 ? LogLevel.Warn : LogLevel.Info);
            return snap;
        }

        // Catching Exception is deliberate: a mod's hook on the farm's collections can throw anything,
        // and the reset must go on. True when the piece is off the farm (keep it in the snapshot,
        // even when a hook threw after the removal); false when it is still there (it stays).
        private static bool TryLift(IMonitor monitor, string what, Vector2 tile, System.Action remove, System.Func<bool> stillThere)
        {
            try
            {
                remove();
                return true;
            }
            catch (System.Exception ex)
            {
                bool lifted = !SafeStillThere(stillThere);
                monitor.Log($"Keep Farm Decor: lifting '{what}' at ({tile.X}, {tile.Y}) threw; " +
                            (lifted ? "it was already off the farm, so it is kept." : "it stays on the farm.") +
                            $" {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return lifted;
            }
        }

        private static bool SafeStillThere(System.Func<bool> stillThere)
        {
            try { return stillThere(); }
            catch (System.InvalidOperationException) { return true; }
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
