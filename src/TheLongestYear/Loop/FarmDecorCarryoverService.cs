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
    /// and send displaced pieces to the stash. What the stash has no room for drops on the ground
    /// as an ordinary pickup next to whatever blocked the piece (spec Addendum 2). Nothing is
    /// deleted on purpose; a drop that itself fails is logged as an Error.</summary>
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

            // Every piece is placed or sent to the stash exactly once; this tracks which. If anything
            // in the plan or the clearing throws (a mod's hook), the pieces not yet handled go to the
            // stash (else the ground beside their own tile) so the rest of the reset goes on.
            // Catching Exception is deliberate here: the throw can come from any mod.
            var handled = new HashSet<int>();
            try
            {
                RestorePlanned(farm, snapshot, keptToolTiers, stash, monitor, handled);
            }
            catch (System.Exception ex)
            {
                List<FarmDecorSnapshot.Entry> left = snapshot.Entries.Where(e => !handled.Contains(e.Id)).ToList();
                monitor.Log($"Keep Farm Decor: the restore failed partway ({ex.GetType().Name}: {ex.Message}); " +
                            $"sending the {left.Count} piece(s) not yet placed to the stash.\n{ex}", LogLevel.Error);
                foreach (FarmDecorSnapshot.Entry e in left)
                    SendToStash(farm, e, stash, DropNear(farm, e.Tile, monitor), monitor);
            }
        }

        private static void RestorePlanned(Farm farm, FarmDecorSnapshot snapshot, IReadOnlyDictionary<string, int> keptToolTiers,
            JunimoStashService stash, IMonitor monitor, HashSet<int> handled)
        {
            int axe = keptToolTiers != null && keptToolTiers.TryGetValue(AxeKey, out int a) ? a : 0;
            int pick = keptToolTiers != null && keptToolTiers.TryGetValue(PickaxeKey, out int p) ? p : 0;

            List<ResourceClump> clumpRefs = farm.resourceClumps.ToList();
            List<DecorClump> clumps = clumpRefs
                .Select((c, i) => new DecorClump(i, c.parentSheetIndex.Value, ClumpTiles(c)))
                .ToList();
            List<DecorPiece> pieces = snapshot.Entries
                .Select(e => new DecorPiece(e.Id, e.Layer, e.Tiles, e.Obj?.QualifiedItemId))
                .ToList();

            DecorPlan plan = FarmDecorPlanner.Plan(pieces, clumps, (x, y) => Survey(farm, x, y), axe, pick,
                (x, y) => farm.objects.TryGetValue(new Vector2(x, y), out StardewValley.Object o) ? o?.QualifiedItemId : null);

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

            var swapTiles = new HashSet<Vector2>(plan.SameObjectTilesToSwap.Select(t => new Vector2(t.X, t.Y)));
            Dictionary<int, FarmDecorSnapshot.Entry> byId = snapshot.Entries.ToDictionary(e => e.Id);
            int failed = 0;
            int dropped = 0;
            foreach (int id in plan.Placed)
            {
                FarmDecorSnapshot.Entry e = byId[id];
                handled.Add(id);
                if (!TryPlace(farm, e, swapTiles, monitor))
                {
                    failed++;
                    dropped += SendToStash(farm, e, stash, DropNear(farm, e.Tile, monitor), monitor);
                }
            }

            foreach (int id in plan.Displaced)
            {
                handled.Add(id);
                FarmDecorSnapshot.Entry e = byId[id];
                dropped += SendToStash(farm, e, stash, BesideBlocker(farm, e, plan, monitor), monitor);
            }

            monitor.Log($"Keep Farm Decor: placed {plan.Placed.Count - failed} ({swapTiles.Count} in place of the fresh farm's own), " +
                        $"to the stash {plan.Displaced.Count + failed} ({dropped} item(s) dropped on the ground beside their blocker), " +
                        $"cleared {plan.ClearedClumps.Count} large debris and {plan.DebrisTilesToClear.Count} debris tile(s).", LogLevel.Info);
        }

        // Place one piece; a mod hook that throws sends the piece to the stash instead, unless the
        // instance already landed on the farm (then it stays, logged). Exception is caught on
        // purpose: the throw can come from any mod's collection hook.
        private static bool TryPlace(Farm farm, FarmDecorSnapshot.Entry e, HashSet<Vector2> swapTiles, IMonitor monitor)
        {
            try
            {
                // The fresh farm spawned the same object here (Meadowlands fences): the kept one,
                // with its gate state and torch, takes its place.
                if (e.Obj != null && swapTiles.Contains(e.Tile))
                    farm.objects.Remove(e.Tile);
                Place(farm, e);
                return true;
            }
            catch (System.Exception ex)
            {
                bool landed = IsOnFarm(farm, e);
                monitor.Log($"Keep Farm Decor: placing piece {e.Id} at ({e.Tile.X}, {e.Tile.Y}) threw; " +
                            (landed ? "it is on the farm anyway." : "sending it to the stash, else the ground beside it.") +
                            $" {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return landed;
            }
        }

        private static bool IsOnFarm(Farm farm, FarmDecorSnapshot.Entry e)
        {
            if (e.Floor != null)
                return farm.terrainFeatures.TryGetValue(e.Tile, out TerrainFeature tf) && tf == e.Floor;
            return e.Obj != null && farm.objects.TryGetValue(e.Tile, out StardewValley.Object o) && o == e.Obj;
        }

        // A piece that cannot go on its tile: its items go to the stash, what does not fit drops on
        // the ground at dropTile. Returns how many items were dropped.
        private static int SendToStash(Farm farm, FarmDecorSnapshot.Entry e, JunimoStashService stash, Vector2 dropTile, IMonitor monitor)
        {
            List<Item> items;
            try
            {
                items = ToItems(e, monitor).ToList();
            }
            catch (System.Exception ex)
            {
                // Last resort: the piece's own object (a path has no item to drop) goes on the
                // ground as it is, torch and all.
                monitor.Log($"Keep Farm Decor: could not turn piece {e.Id} at ({e.Tile.X}, {e.Tile.Y}) into stash items" +
                            (e.Obj != null ? "; dropping the piece itself on the ground. " : ". ") +
                            $"{ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                if (e.Obj == null)
                    return 0;
                GroundDrop.AtTile(farm, dropTile, e.Obj, monitor, $"Keep Farm Decor piece {e.Id} could not be stored");
                return 1;
            }
            int dropped = 0;
            foreach (Item item in items)
            {
                Item left;
                try
                {
                    left = stash != null ? stash.TryDeposit(item) : item;
                }
                catch (System.Exception ex)
                {
                    // Exception on purpose: a mod's inventory hook. The item drops instead.
                    monitor.Log($"Keep Farm Decor: could not put '{item.QualifiedItemId}' x{item.Stack} from piece {e.Id} in the stash; dropping it. " +
                                $"{ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                    left = item;
                }
                if (left == null) continue;
                dropped++;
                GroundDrop.AtTile(farm, dropTile, left, monitor, $"the stash had no room for Keep Farm Decor piece {e.Id}");
            }
            return dropped;
        }

        // Where a displaced piece drops: its own tile if that is open, else the nearest open tile
        // to whatever blocked it (the planner names the tile). Never throws.
        private static Vector2 BesideBlocker(Farm farm, FarmDecorSnapshot.Entry e, DecorPlan plan, IMonitor monitor)
        {
            try
            {
                DecorTile blocker = plan.BlockedAt.TryGetValue(e.Id, out DecorTile b) ? b : new DecorTile((int)e.Tile.X, (int)e.Tile.Y);
                DecorTile? spot = DropSpot.ForDisplaced(e.Tiles, blocker, (x, y) => GroundDrop.IsOpen(farm, x, y), GroundDrop.SearchRadius);
                return spot is DecorTile t ? new Vector2(t.X, t.Y) : new Vector2(blocker.X, blocker.Y);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's location hook. The piece's own tile still takes the drop.
                monitor.Log($"Keep Farm Decor: finding a drop spot for piece {e.Id} threw; using its own tile. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return e.Tile;
            }
        }

        // Where a piece that failed outside the plan drops: the nearest open tile to its own. Never throws.
        private static Vector2 DropNear(Farm farm, Vector2 tile, IMonitor monitor)
        {
            try
            {
                return GroundDrop.NearestOpen(farm, tile);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's location hook. The tile itself still takes the drop.
                monitor.Log($"Keep Farm Decor: finding a drop spot near ({tile.X}, {tile.Y}) threw; using that tile. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                return tile;
            }
        }

        // Fresh-farm state of one tile for the planner. Every terrain feature (grass, saplings, trees)
        // and every bush is small debris: it shares or blocks the tile and kept decor wins. Any other
        // object the fresh farm holds is FreshObject (the planner swaps it for a kept piece with the
        // same id); chests (the stash) and furniture are OtherObject.
        private static TileBlock Survey(Farm farm, int x, int y)
        {
            if (!farm.isTileOnMap(x, y)) return TileBlock.OffMap;
            var tile = new Vector2(x, y);
            Rectangle rect = TileRect(x, y);
            TileBlock block = TileBlock.None;
            if (farm.getBuildingAt(tile) != null) block |= TileBlock.Building;
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj))
                block |= IsSmallDebris(obj) ? TileBlock.SmallDebris
                    : obj is StardewValley.Objects.Chest ? TileBlock.OtherObject : TileBlock.FreshObject;
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
            else if (e.Obj != null)
            {
                farm.objects[e.Tile] = e.Obj;
                // A fence lights the torch it holds; everything else lights itself.
                StardewValley.Object lit = e.Obj is Fence ? e.Obj.heldObject.Value : e.Obj;
                lit?.initializeLightSource(e.Tile);
            }
        }

        // A displaced piece as stash items: the path's item, or a fresh fence/torch/sign/decoration
        // (plus a torch that sat on a fence).
        private static IEnumerable<Item> ToItems(FarmDecorSnapshot.Entry e, IMonitor monitor)
        {
            if (e.Floor != null)
            {
                string objectId = Flooring.GetFloorPathItemLookup().FirstOrDefault(kv => kv.Value == e.Floor.whichFloor.Value).Key;
                if (objectId != null)
                    yield return ItemRegistry.Create("(O)" + objectId);
                else
                    monitor.Log($"Keep Farm Decor: path '{e.Floor.whichFloor.Value}' at ({e.Tile.X}, {e.Tile.Y}) has no item to put in the stash, so it is not restored.", LogLevel.Warn);
            }
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
