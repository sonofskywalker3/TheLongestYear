using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Debug only: prints the two tiles the rewind pan travels between, read off the live
    /// Town map rather than estimated. Run once, write the numbers into RewindPanScene.</summary>
    internal static class TownRouteProbe
    {
        public static void Register(IModHelper helper, IMonitor monitor)
        {
            helper.ConsoleCommands.Add(
                "tly_townroute",
                "Print every door tile in Town and every warp tile out of Town, with its target location (debug: source data for the rewind pan's endpoints).",
                (_, _) =>
                {
                    GameLocation town = Game1.getLocationFromName("Town");
                    if (town == null) { monitor.Log("tly_townroute: Town is not loaded.", LogLevel.Warn); return; }

                    int doorCount = 0;
                    foreach (var door in town.doors.Pairs)
                    {
                        doorCount++;
                        monitor.Log($"tly_townroute: door at ({door.Key.X},{door.Key.Y}) -> {door.Value}", LogLevel.Info);
                    }
                    if (doorCount == 0)
                        monitor.Log("tly_townroute: no doors found in Town.", LogLevel.Warn);

                    int warpCount = 0;
                    foreach (Warp w in town.warps)
                    {
                        warpCount++;
                        monitor.Log($"tly_townroute: warp at ({w.X},{w.Y}) -> {w.TargetName}", LogLevel.Info);
                    }
                    if (warpCount == 0)
                        monitor.Log("tly_townroute: no warps found in Town.", LogLevel.Warn);
                });
        }
    }
}
