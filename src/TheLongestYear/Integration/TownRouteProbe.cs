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
                "Print Town's Blacksmith door tile and its Farm warp tile (debug: the rewind pan's endpoints).",
                (_, _) =>
                {
                    GameLocation town = Game1.getLocationFromName("Town");
                    if (town == null) { monitor.Log("tly_townroute: Town is not loaded.", LogLevel.Warn); return; }

                    foreach (var door in town.doors.Pairs)
                        monitor.Log($"tly_townroute: door at ({door.Key.X},{door.Key.Y}) -> {door.Value}", LogLevel.Info);

                    foreach (Warp w in town.warps.Where(w => w.TargetName == "Farm"))
                        monitor.Log($"tly_townroute: warp to Farm at ({w.X},{w.Y})", LogLevel.Info);
                });
        }
    }
}
