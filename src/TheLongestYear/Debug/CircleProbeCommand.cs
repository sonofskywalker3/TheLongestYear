using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Loop;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_circleprobe: why a chest will or will not go down on a Circle of Warding (Nexus
    /// report 2026-10-28). For every circle in the player's location it runs the game's own chest
    /// placement check on each of the nine tiles and logs the pieces that check is made of, then
    /// lists every mod with a Harmony patch on those methods. Vanilla 1.6.15 lets a chest stand on
    /// any passable rug, indoors or out, so a refused tile with every piece passing points at a
    /// patch from another mod. <c>place</c> also puts a chest on every tile that passes.</summary>
    internal static class CircleProbeCommand
    {
        public const string Name = "tly_circleprobe";
        public const string Usage = "Usage: tly_circleprobe [place]. Stand in the location with the circle.";

        private const string ChestId = "(BC)130";
        private const int TileSize = 64;
        private const int CircleTiles = 3;
        private const string PlaceVerb = "place";

        private static readonly (System.Type Type, string Method)[] PlacementMethods =
        {
            (typeof(Object), nameof(Object.canBePlacedHere)),
            (typeof(Object), nameof(Object.placementAction)),
            (typeof(Object), nameof(Object.isPlaceable)),
            (typeof(Furniture), nameof(Furniture.isPassable)),
            (typeof(GameLocation), nameof(GameLocation.CanItemBePlacedHere)),
            (typeof(GameLocation), nameof(GameLocation.isTilePlaceable)),
            (typeof(GameLocation), nameof(GameLocation.IsTileOccupiedBy)),
            (typeof(GameLocation), nameof(GameLocation.IsTileBlockedBy)),
            (typeof(GameLocation), nameof(GameLocation.GetFurnitureAt)),
            (typeof(Utility), nameof(Utility.playerCanPlaceItemHere)),
            (typeof(Utility), nameof(Utility.tryToPlaceItem)),
        };

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            GameLocation loc = Game1.player.currentLocation;
            bool place = args.Length > 0 && args[0] == PlaceVerb;
            List<Furniture> circles = loc.furniture.Where(f => f.ItemId == CircleOfWardingService.CircleId).ToList();
            monitor.Log($"{Name}: {circles.Count} circle(s) in {loc.NameOrUniqueName} (outdoors={loc.IsOutdoors}, decoratable={loc is StardewValley.Locations.DecoratableLocation}).", LogLevel.Info);
            foreach (Furniture circle in circles)
            {
                monitor.Log($"{Name}: circle at {circle.TileLocation} type={circle.furniture_type.Value} passable={circle.isPassable()} box={circle.GetBoundingBox()} restriction={circle.placementRestriction}.", LogLevel.Info);
                for (int y = 0; y < CircleTiles; y++)
                    for (int x = 0; x < CircleTiles; x++)
                        ProbeTile(monitor, loc, circle.TileLocation + new Vector2(x, y), place);
            }
            ReportPatches(monitor);
        }

        private static void ProbeTile(IMonitor monitor, GameLocation loc, Vector2 tile, bool place)
        {
            var chest = ItemRegistry.Create<Object>(ChestId);
            bool ok = chest.canBePlacedHere(loc, tile, CollisionMask.All, showError: false);
            // The same masks Object.canBePlacedHere hands CanItemBePlacedHere for a chest.
            CollisionMask ignore = ~CollisionMask.Objects & ~(CollisionMask.Characters | CollisionMask.Farmers);
            var blockers = new List<string>();
            foreach (CollisionMask layer in new[] { CollisionMask.Farmers, CollisionMask.Objects, CollisionMask.Furniture, CollisionMask.Characters,
                         CollisionMask.TerrainFeatures, CollisionMask.Flooring, CollisionMask.Buildings, CollisionMask.LocationSpecific })
                if (loc.IsTileOccupiedBy(tile, layer, ignore)) blockers.Add(layer.ToString());
            string line = $"{Name}:   {tile} chestOk={ok} placeable={loc.isTilePlaceable(tile)} passableMap={loc.isTilePassable(tile)} "
                          + $"furnitureAt={loc.GetFurnitureAt(tile)?.ItemId ?? "-"} object={(loc.objects.TryGetValue(tile, out Object o) ? o.QualifiedItemId : "-")} "
                          + $"occupiedBy=[{string.Join(",", blockers)}]";
            if (place && ok)
                line += chest.placementAction(loc, (int)tile.X * TileSize, (int)tile.Y * TileSize, Game1.player) ? " placed" : " placementAction refused";
            monitor.Log(line, LogLevel.Info);
        }

        private static void ReportPatches(IMonitor monitor)
        {
            foreach ((System.Type type, string name) in PlacementMethods)
            {
                // Every overload, declared on that type: a patch on any of them can refuse the chest.
                foreach (MethodBase method in AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name))
                    ReportPatches(monitor, method);
            }
        }

        private static void ReportPatches(IMonitor monitor, MethodBase method)
        {
            HarmonyLib.Patches info = Harmony.GetPatchInfo(method);
            if (info == null) return;
            IEnumerable<string> owners = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                .Select(p => p.owner).Distinct();
            monitor.Log($"{Name}: {method.DeclaringType?.Name}.{method.Name} patched by {string.Join(", ", owners)}", LogLevel.Info);
        }
    }
}
