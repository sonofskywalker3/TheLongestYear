using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TheLongestYear.Loop
{
    /// <summary>tly_decor: set up and inspect Keep Farm Decor cases from the debug bridge
    /// (spec 2026-10-01 live checks). Developer-only.</summary>
    internal static class FarmDecorDebug
    {
        private const string WoodPathObjectId = "405";
        private const string WoodFenceId = "322";
        private const string TorchId = "93";
        private const int DebrisRadiusTiles = 3;
        private const float TileSize = 64f;
        private const string Usage = "tly_decor <clumps|path x y|fence x y|check x y>";

        public static void Run(string[] args, IMonitor monitor)
        {
            Farm farm = Game1.getFarm();
            if (farm == null) { monitor.Log("tly_decor: no farm loaded.", LogLevel.Warn); return; }
            string mode = args.Length > 0 ? args[0] : "clumps";
            if (mode == "clumps")
            {
                foreach (ResourceClump c in farm.resourceClumps)
                    monitor.Log($"tly_decor: clump {c.parentSheetIndex.Value} at ({c.Tile.X}, {c.Tile.Y}) {c.width.Value}x{c.height.Value}", LogLevel.Info);
                monitor.Log($"tly_decor: {farm.resourceClumps.Count} clump(s).", LogLevel.Info);
                return;
            }
            if (args.Length < 3 || !int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y))
            {
                monitor.Log("tly_decor: usage " + Usage, LogLevel.Warn);
                return;
            }
            var tile = new Vector2(x, y);
            switch (mode)
            {
                case "path":
                    RemoveClumpsAt(farm, x, y, monitor);
                    farm.terrainFeatures[tile] = new Flooring(Flooring.GetFloorPathItemLookup()[WoodPathObjectId]);
                    monitor.Log($"tly_decor: wood path at ({x}, {y}).", LogLevel.Info);
                    break;
                case "fence":
                    var east = new Vector2(x + 1, y);
                    RemoveClumpsAt(farm, x, y, monitor);
                    RemoveClumpsAt(farm, x + 1, y, monitor);
                    farm.objects[tile] = new Fence(tile, WoodFenceId, false);
                    var torch = new Torch(1, TorchId);
                    farm.objects[east] = torch;
                    torch.initializeLightSource(east);
                    monitor.Log($"tly_decor: wood fence at ({x}, {y}), torch at ({x + 1}, {y}).", LogLevel.Info);
                    break;
                case "check":
                    Check(farm, x, y, monitor);
                    break;
                default:
                    monitor.Log("tly_decor: usage " + Usage, LogLevel.Warn);
                    break;
            }
        }

        private static void RemoveClumpsAt(Farm farm, int x, int y, IMonitor monitor)
        {
            foreach (ResourceClump c in farm.resourceClumps.Where(c => c.occupiesTile(x, y)).ToList())
            {
                farm.resourceClumps.Remove(c);
                monitor.Log($"tly_decor: removed clump {c.parentSheetIndex.Value} at ({c.Tile.X}, {c.Tile.Y}).", LogLevel.Info);
            }
        }

        private static void Check(Farm farm, int x, int y, IMonitor monitor)
        {
            var tile = new Vector2(x, y);
            string feature = farm.terrainFeatures.TryGetValue(tile, out TerrainFeature tf)
                ? tf is Flooring fl ? $"Flooring '{fl.whichFloor.Value}'" : tf.GetType().Name
                : "none";
            string obj = farm.objects.TryGetValue(tile, out StardewValley.Object o)
                ? $"{o.QualifiedItemId} ({o.GetType().Name}{(o.heldObject.Value != null ? ", holds " + o.heldObject.Value.QualifiedItemId : "")})"
                : "none";
            string clumps = string.Join(", ", farm.resourceClumps.Where(c => c.occupiesTile(x, y))
                .Select(c => $"{c.parentSheetIndex.Value}@({c.Tile.X}, {c.Tile.Y})"));
            monitor.Log($"tly_decor: ({x}, {y}) terrain {feature}; object {obj}; clumps [{clumps}]", LogLevel.Info);

            Vector2 center = tile * TileSize;
            float reach = DebrisRadiusTiles * TileSize;
            foreach (Debris d in farm.debris)
            {
                Vector2 at = d.Chunks.Count > 0 ? d.Chunks[0].position.Value : Vector2.Zero;
                if (Vector2.Distance(at, center) > reach) continue;
                string what = d.item != null ? $"{d.item.QualifiedItemId} x{d.item.Stack}" : d.itemId.Value;
                monitor.Log($"tly_decor: debris {what} at ({at.X / TileSize:0.0}, {at.Y / TileSize:0.0})", LogLevel.Info);
            }
        }
    }
}
