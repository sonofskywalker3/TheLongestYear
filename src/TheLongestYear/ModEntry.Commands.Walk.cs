using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Pathfinding;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>How long a walk may take before it is called off (60 ticks a second).</summary>
        private const int WalkTimeoutTicks = 60 * 90;
        private const int PathSearchLimit = 30000;
        /// <summary>Ticks to wait after the path ends for a warp at its end to take the farmer.</summary>
        private const int WarpSettleTicks = 90;

        private EventHandler<UpdateTickedEventArgs> _walkWatch;

        /// <summary>Walks the farmer to a tile with the game's own path search and movement, so a way
        /// between two maps is proved the way a player proves it: every step passes the game's collision
        /// test (walls, objects, logs, boulders) and a warp is taken by stepping on it, never by a
        /// teleport. The target may be a warp tile just off the map edge. Logs the path length or that
        /// no path exists, then where the farmer ended up.</summary>
        private void CmdWalkTo(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 2 || !int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
            {
                this.Monitor.Log("Usage: tly_walkto <x> <y>", LogLevel.Warn);
                return;
            }
            Farmer p = Game1.player;
            GameLocation here = Game1.currentLocation;
            if (here == null || Game1.eventUp || Game1.activeClickableMenu != null || Game1.dialogueUp)
            {
                this.Monitor.Log($"tly_walkto: cannot walk now (event={Game1.eventUp}, menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}, dialogue={Game1.dialogueUp}).", LogLevel.Warn);
                return;
            }
            var target = new Point(x, y);
            string from = here.Name;
            Point start = p.TilePoint;
            Stack<Point> path = PathFindController.findPath(start, target, PathFindController.isAtEndPoint, here, p, PathSearchLimit);
            if (path == null || path.Count == 0)
            {
                this.Monitor.Log($"tly_walkto: NO PATH from ({start.X},{start.Y}) to ({x},{y}) in '{from}': the way is blocked.", LogLevel.Info);
                return;
            }
            this.Monitor.Log($"tly_walkto: path of {path.Count} tile(s) from ({start.X},{start.Y}) to ({x},{y}) in '{from}'; walking.", LogLevel.Info);
            p.controller = new PathFindController(path, here, p, target);

            if (_walkWatch != null) this.Helper.Events.GameLoop.UpdateTicked -= _walkWatch;
            int ticks = 0, idle = 0;
            _walkWatch = (_, _) =>
            {
                ticks++;
                if (!Context.IsWorldReady || Game1.player == null || Game1.currentLocation == null)
                {
                    this.Helper.Events.GameLoop.UpdateTicked -= _walkWatch;
                    _walkWatch = null;
                    this.Monitor.Log("tly_walkto: the save was left before the walk ended.", LogLevel.Info);
                    return;
                }
                string now = Game1.currentLocation.Name ?? "?";
                bool moved = now != from;
                // The last step onto a warp ends the path a moment before the game changes maps, so
                // the walk is only over once the farmer has stood still for a while.
                if (!moved && Game1.player.controller == null) idle++;
                bool settled = !moved && idle > WarpSettleTicks;
                if (!moved && !settled && ticks < WalkTimeoutTicks) return;
                this.Helper.Events.GameLoop.UpdateTicked -= _walkWatch;
                _walkWatch = null;
                Point at = Game1.player.TilePoint;
                string how = moved ? $"WARPED from '{from}' to '{now}'"
                    : at == target ? "arrived"
                    : settled ? "STOPPED short" : "TIMED OUT";
                this.Monitor.Log($"tly_walkto: {how}; farmer at ({at.X},{at.Y}) in '{now}' after {ticks} tick(s).", LogLevel.Info);
            };
            this.Helper.Events.GameLoop.UpdateTicked += _walkWatch;
        }
    }
}
