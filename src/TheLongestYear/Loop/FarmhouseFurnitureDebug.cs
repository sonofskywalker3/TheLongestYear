using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>tly_housefurn: set up and inspect Keep Farmhouse Furniture cases from the debug bridge
    /// (spec 2026-10-01 live checks). Developer-only.</summary>
    internal static class FarmhouseFurnitureDebug
    {
        private const string Usage = "tly_housefurn <list|place id x y [rotations]|fill x y|check>";
        private const float TileSize = 64f;
        private const string HatId = "(H)0";
        private const string RingId = "(O)517";
        private const string FishId = "(O)128";

        public static void Run(string[] args, TheLongestYear.Core.MetaState meta, IMonitor monitor)
        {
            if (Utility.getHomeOfFarmer(Game1.player) is not FarmHouse house)
            {
                monitor.Log("tly_housefurn: no farmhouse loaded.", LogLevel.Warn);
                return;
            }
            string mode = args.Length > 0 ? args[0] : "list";
            switch (mode)
            {
                case "list":
                    List(house, "house", monitor);
                    if (FarmhouseFurnitureCarryover.FindCellar(house, monitor) is GameLocation cellar)
                        List(cellar, "cellar", monitor);
                    break;
                case "place":
                    Place(house, args, monitor);
                    break;
                case "fill":
                    Fill(house, args, monitor);
                    break;
                case "check":
                    Check(house, meta, monitor);
                    break;
                default:
                    monitor.Log("tly_housefurn: usage " + Usage, LogLevel.Warn);
                    break;
            }
        }

        private static void List(GameLocation location, string label, IMonitor monitor)
        {
            foreach (Furniture f in location.furniture)
            {
                string held = f.heldObject.Value != null ? $", holds {f.heldObject.Value.QualifiedItemId}" : "";
                string slots = f is StorageFurniture s
                    ? $", contents [{string.Join(", ", s.heldItems.Where(i => i != null).Select(i => i.QualifiedItemId))}]"
                    : "";
                string kept = FarmhouseFurnitureKeep.IsKeptPiece(f.QualifiedItemId) ? "" : " (not kept)";
                monitor.Log($"tly_housefurn: {label} {f.QualifiedItemId} '{f.DisplayName}' at ({f.TileLocation.X}, {f.TileLocation.Y}) " +
                            $"rotation {f.currentRotation.Value}{held}{slots}{kept}", LogLevel.Info);
            }
            monitor.Log($"tly_housefurn: {label} has {location.furniture.Count} piece(s).", LogLevel.Info);
        }

        private static void Place(FarmHouse house, string[] args, IMonitor monitor)
        {
            if (args.Length < 4 || !int.TryParse(args[2], out int x) || !int.TryParse(args[3], out int y))
            {
                monitor.Log("tly_housefurn: usage " + Usage, LogLevel.Warn);
                return;
            }
            int rotations = args.Length > 4 && int.TryParse(args[4], out int r) ? r : 0;
            string id = args[1].StartsWith("(") ? args[1] : "(F)" + args[1];
            if (ItemRegistry.Create(id) is not Furniture piece)
            {
                monitor.Log($"tly_housefurn: '{id}' is not furniture.", LogLevel.Warn);
                return;
            }
            piece.SetPlacement(x, y, rotations);
            var entry = new FarmhouseFurnitureCarryover.Entry { Piece = piece, Tile = new Vector2(x, y) };
            bool fits = FarmhouseFurnitureCarryover.Fits(house, entry);
            house.furniture.Add(piece);
            monitor.Log($"tly_housefurn: placed {id} at ({x}, {y}) rotation {piece.currentRotation.Value}; " +
                        $"the reset's placement check says it {(fits ? "fits" : "does not fit")} here.", LogLevel.Info);
        }

        // A dresser or table at (x, y) gets a hat, a ring and (for a tank) a fish, so the wipe can be seen.
        private static void Fill(FarmHouse house, string[] args, IMonitor monitor)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y))
            {
                monitor.Log("tly_housefurn: usage " + Usage, LogLevel.Warn);
                return;
            }
            Furniture f = house.GetFurnitureAt(new Vector2(x, y));
            if (f == null)
            {
                monitor.Log($"tly_housefurn: no furniture at ({x}, {y}).", LogLevel.Warn);
                return;
            }
            if (f is FishTankFurniture tank)
                tank.heldItems.Add(ItemRegistry.Create(FishId));
            else if (f is StorageFurniture storage)
            {
                storage.heldItems.Add(ItemRegistry.Create(HatId));
                storage.heldItems.Add(ItemRegistry.Create(RingId));
            }
            else if (f.IsTable())
                f.heldObject.Value = ItemRegistry.Create<StardewValley.Object>(RingId);
            else
            {
                monitor.Log($"tly_housefurn: {f.QualifiedItemId} holds nothing.", LogLevel.Warn);
                return;
            }
            monitor.Log($"tly_housefurn: filled {f.QualifiedItemId} at ({x}, {y}).", LogLevel.Info);
        }

        private static void Check(FarmHouse house, TheLongestYear.Core.MetaState meta, IMonitor monitor)
        {
            Vector2 door = FarmhouseFurnitureCarryover.DoorTile(house, monitor);
            monitor.Log($"tly_housefurn: upgrade owned {meta?.HasUpgrade(FarmhouseFurnitureKeep.UpgradeId)}; " +
                        $"house level {house.upgradeLevel}; front-door entry ({door.X}, {door.Y}); " +
                        $"beds {house.furniture.OfType<BedFurniture>().Count()}.", LogLevel.Info);
            foreach (Debris d in house.debris)
            {
                Vector2 at = d.Chunks.Count > 0 ? d.Chunks[0].position.Value : Vector2.Zero;
                string what = d.item != null ? $"{d.item.QualifiedItemId} x{d.item.Stack}" : d.itemId.Value;
                monitor.Log($"tly_housefurn: pickup {what} at ({at.X / TileSize:0.0}, {at.Y / TileSize:0.0})", LogLevel.Info);
            }
            monitor.Log($"tly_housefurn: {house.debris.Count} pickup(s) in the house.", LogLevel.Info);
        }
    }
}
