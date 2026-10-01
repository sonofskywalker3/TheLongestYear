using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Puts kept decor back on the fresh farm (step 13a). Kept buildings, the stash chest and
    /// the planning shrine are already placed, so they count as blockers. FarmDecorPlanner decides;
    /// this applies: clear the large debris the kept tools can break (hardwood on the ground where it
    /// stood, boulders drop nothing), clear small debris under kept tiles (no drops), place the rest,
    /// and send displaced pieces to the stash, or to the overflow chest beside it when the stash is
    /// full. Nothing is ever deleted.</summary>
    internal static class FarmDecorCarryoverService
    {
        private const string AxeKey = "axe";
        private const string PickaxeKey = "pickaxe";
        private const float TileSize = 64f;
        private const int AnyDirection = -1;

        // Spots the fresh farm scatters that are not worth displacing decor over: artifact and seed spots.
        private static readonly HashSet<string> SpotIds = new() { "(O)590", "(O)SeedSpot" };

        public static void Restore(FarmDecorSnapshot snapshot, IReadOnlyDictionary<string, int> keptToolTiers,
            JunimoStashService stash, IMonitor monitor)
        {
            if (snapshot == null || snapshot.Entries.Count == 0) return;
            Farm farm = Game1.getFarm();
            if (farm == null)
            {
                monitor.Log($"Keep Farm Decor: no farm after the reset; {snapshot.Entries.Count} piece(s) not restored.", LogLevel.Warn);
                return;
            }

            int axe = keptToolTiers != null && keptToolTiers.TryGetValue(AxeKey, out int a) ? a : 0;
            int pick = keptToolTiers != null && keptToolTiers.TryGetValue(PickaxeKey, out int p) ? p : 0;

            List<ResourceClump> clumpRefs = farm.resourceClumps.ToList();
            List<DecorClump> clumps = clumpRefs
                .Select((c, i) => new DecorClump(i, c.parentSheetIndex.Value, ClumpTiles(c)))
                .ToList();
            List<DecorPiece> pieces = snapshot.Entries.Select(e => new DecorPiece(e.Id, e.Layer, e.Tiles)).ToList();

            DecorPlan plan = FarmDecorPlanner.Plan(pieces, clumps, (x, y) => Survey(farm, x, y), axe, pick);

            foreach (ClearedClump cleared in plan.ClearedClumps)
            {
                ResourceClump clump = clumpRefs[cleared.ClumpId];
                farm.resourceClumps.Remove(clump);
                if (cleared.HardwoodDrop > 0)
                    Game1.createMultipleItemDebris(ItemRegistry.Create(FarmDecorKeep.HardwoodId, cleared.HardwoodDrop),
                        clump.Tile * TileSize + new Vector2(TileSize, TileSize), AnyDirection, farm);
            }
            foreach (DecorTile tile in plan.DebrisTilesToClear)
                ClearSmallDebris(farm, tile);

            Dictionary<int, FarmDecorSnapshot.Entry> byId = snapshot.Entries.ToDictionary(e => e.Id);
            foreach (int id in plan.Placed)
                Place(farm, byId[id]);

            int overflowed = 0;
            foreach (int id in plan.Displaced)
                foreach (Item item in ToItems(byId[id]))
                {
                    Item left = stash != null ? stash.TryDeposit(item) : item;
                    if (left == null) continue;
                    overflowed++;
                    if (stash != null)
                        stash.StoreInOverflowChest(left);
                    else
                    {
                        // No stash service: still never delete. The ground is the last resort, logged.
                        Game1.createItemDebris(left, byId[id].Tile * TileSize + new Vector2(TileSize / 2, TileSize / 2), AnyDirection, farm);
                        monitor.Log($"Keep Farm Decor: no stash; dropped '{left.QualifiedItemId}' where it stood.", LogLevel.Warn);
                    }
                }

            monitor.Log($"Keep Farm Decor: placed {plan.Placed.Count}, to the stash {plan.Displaced.Count} ({overflowed} item(s) to the overflow chest), " +
                        $"cleared {plan.ClearedClumps.Count} large debris and {plan.DebrisTilesToClear.Count} debris tile(s).", LogLevel.Info);
        }

        // Fresh-farm state of one tile for the planner. Every terrain feature (grass, saplings, trees)
        // and every bush is small debris: it shares or blocks the tile and kept decor wins.
        private static TileBlock Survey(Farm farm, int x, int y)
        {
            if (!farm.isTileOnMap(x, y)) return TileBlock.OffMap;
            var tile = new Vector2(x, y);
            Rectangle rect = TileRect(x, y);
            TileBlock block = TileBlock.None;
            if (farm.getBuildingAt(tile) != null) block |= TileBlock.Building;
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj))
                block |= IsSmallDebris(obj) ? TileBlock.SmallDebris : TileBlock.OtherObject;
            if (farm.furniture.Any(f => f.boundingBox.Value.Intersects(rect))) block |= TileBlock.OtherObject;
            if (farm.terrainFeatures.ContainsKey(tile)) block |= TileBlock.SmallDebris;
            if (farm.largeTerrainFeatures.Any(l => l.getBoundingBox().Intersects(rect))) block |= TileBlock.SmallDebris;
            return block;
        }

        private static bool IsSmallDebris(StardewValley.Object obj)
            => obj.IsWeeds() || obj.IsTwig() || obj.IsBreakableStone() || obj.IsSpawnedObject || SpotIds.Contains(obj.QualifiedItemId);

        private static Rectangle TileRect(int x, int y)
            => new((int)(x * TileSize), (int)(y * TileSize), (int)TileSize, (int)TileSize);

        private static void ClearSmallDebris(Farm farm, DecorTile t)
        {
            var tile = new Vector2(t.X, t.Y);
            Rectangle rect = TileRect(t.X, t.Y);
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj) && IsSmallDebris(obj))
                farm.objects.Remove(tile);
            farm.terrainFeatures.Remove(tile);
            for (int i = farm.largeTerrainFeatures.Count - 1; i >= 0; i--)
                if (farm.largeTerrainFeatures[i].getBoundingBox().Intersects(rect))
                    farm.largeTerrainFeatures.RemoveAt(i);
        }

        private static void Place(Farm farm, FarmDecorSnapshot.Entry e)
        {
            if (e.Floor != null)
                farm.terrainFeatures[e.Tile] = e.Floor;
            else if (e.Furniture != null)
                farm.furniture.Add(e.Furniture);   // OnValueAdded runs Furniture.OnAdded (lights)
            else if (e.Obj != null)
            {
                farm.objects[e.Tile] = e.Obj;
                // A fence lights the torch it holds; everything else lights itself.
                StardewValley.Object lit = e.Obj is Fence ? e.Obj.heldObject.Value : e.Obj;
                lit?.initializeLightSource(e.Tile);
            }
        }

        // A displaced piece as stash items: the path's item, a fresh fence/torch/sign/decoration
        // (plus a torch that sat on a fence), or the furniture itself (its contents are already
        // trimmed to cosmetic, so the stash rule holds).
        private static IEnumerable<Item> ToItems(FarmDecorSnapshot.Entry e)
        {
            if (e.Floor != null)
            {
                string objectId = Flooring.GetFloorPathItemLookup().FirstOrDefault(kv => kv.Value == e.Floor.whichFloor.Value).Key;
                if (objectId != null)
                    yield return ItemRegistry.Create("(O)" + objectId);
            }
            else if (e.Furniture != null)
                yield return e.Furniture;
            else if (e.Obj != null)
            {
                yield return ItemRegistry.Create(e.Obj.QualifiedItemId);
                if (e.Obj is Fence && e.Obj.heldObject.Value is StardewValley.Object held)
                    yield return ItemRegistry.Create(held.QualifiedItemId);
            }
        }

        private static List<DecorTile> ClumpTiles(ResourceClump c)
        {
            var tiles = new List<DecorTile>();
            for (int dx = 0; dx < c.width.Value; dx++)
                for (int dy = 0; dy < c.height.Value; dy++)
                    tiles.Add(new DecorTile((int)c.Tile.X + dx, (int)c.Tile.Y + dy));
            return tiles;
        }
    }
}
