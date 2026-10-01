using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Items with nowhere else to go drop on the ground as ordinary pickups (spec Addendum 2): the
    /// mod never places a chest for them. One debris per stack (Game1.createItemDebris keeps the
    /// whole stack, and a container keeps its contents). Ground items are not saved overnight, so
    /// every drop happens on day 1 of a loop or at the moment of a refusal. A drop never throws:
    /// a failure is logged as an Error naming the item, and the caller is told it did not land.
    /// </summary>
    internal static class GroundDrop
    {
        /// <summary>How far, in tiles, the search for an open tile walks out from its start.</summary>
        internal const int SearchRadius = 12;

        private const float TileSize = 64f;
        private const float HalfTile = TileSize / 2f;
        private const int AnyDirection = -1;

        // A tile is open when nothing solid stands on it. Grass, flooring and passable objects or
        // furniture are fine: a pickup lands on them.
        private const CollisionMask Solid = CollisionMask.Buildings | CollisionMask.Objects | CollisionMask.Furniture
            | CollisionMask.TerrainFeatures | CollisionMask.Flooring | CollisionMask.LocationSpecific;
        private const CollisionMask PassableIsOpen = CollisionMask.Objects | CollisionMask.Furniture
            | CollisionMask.TerrainFeatures | CollisionMask.Flooring;

        /// <summary>A tile the player can walk onto to pick an item up.</summary>
        internal static bool IsOpen(GameLocation location, int x, int y)
        {
            var tile = new Vector2(x, y);
            return location.isTileOnMap(x, y)
                   && !location.isWaterTile(x, y)
                   && location.isTilePassable(tile)
                   && !location.IsTileOccupiedBy(tile, Solid, PassableIsOpen);
        }

        /// <summary>The nearest open tile to <paramref name="start"/> (itself included); the start
        /// tile itself when nothing within <see cref="SearchRadius"/> is open.</summary>
        internal static Vector2 NearestOpen(GameLocation location, Vector2 start)
        {
            DecorTile? spot = DropSpot.Nearest(new DecorTile((int)start.X, (int)start.Y),
                (x, y) => IsOpen(location, x, y), SearchRadius);
            return spot is DecorTile t ? new Vector2(t.X, t.Y) : start;
        }

        /// <summary>Drop one stack on a tile. True when it landed; false (logged as an Error) when
        /// it could not be dropped.</summary>
        internal static bool AtTile(GameLocation location, Vector2 tile, Item item, IMonitor monitor, string why)
        {
            if (item == null)
                return true;
            string what = $"'{item.QualifiedItemId}' x{item.Stack}";
            if (location == null)
            {
                monitor?.Log($"GroundDrop: {why}; no location to drop {what} in, it is lost.", LogLevel.Error);
                return false;
            }
            try
            {
                Game1.createItemDebris(item, tile * TileSize + new Vector2(HalfTile, HalfTile), AnyDirection, location);
                monitor?.Log($"GroundDrop: {why}; dropped {what} at ({tile.X}, {tile.Y}) in {location.NameOrUniqueName}.", LogLevel.Info);
                return true;
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: this is the last place an item can go, and it runs inside the
                // reset or a menu click. Never let it throw out; log the loss loudly instead.
                monitor?.Log($"GroundDrop: {why}; dropping {what} at ({tile.X}, {tile.Y}) threw, it is lost. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>Drop one stack on the nearest open tile to <paramref name="start"/>.</summary>
        internal static bool Near(GameLocation location, Vector2 start, Item item, IMonitor monitor, string why)
        {
            Vector2 tile = start;
            try
            {
                if (location != null)
                    tile = NearestOpen(location, start);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: a mod's location hook. The start tile still takes the drop.
                monitor?.Log($"GroundDrop: finding an open tile near ({start.X}, {start.Y}) threw; dropping there. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
            }
            return AtTile(location, tile, item, monitor, why);
        }

        /// <summary>Drop one stack at the player's feet, in the player's location.</summary>
        internal static bool AtFeet(Farmer who, Item item, IMonitor monitor, string why)
        {
            if (who == null)
            {
                monitor?.Log($"GroundDrop: {why}; no player to drop '{item?.QualifiedItemId}' at.", LogLevel.Error);
                return false;
            }
            return AtTile(who.currentLocation, who.Tile, item, monitor, why);
        }
    }
}
