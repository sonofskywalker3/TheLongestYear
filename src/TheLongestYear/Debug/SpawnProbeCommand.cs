using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_spawnprobe: headless check for the Spring/Summer/Fall Returns boosts.
    /// <c>fish &lt;rolls&gt; [waterDepth]</c> rolls the game's own fish picker
    /// (GameLocation.GetFishFromLocationData) at the player's tile and prints the tally;
    /// <c>forage</c> counts every spawned forage object on every map by name.</summary>
    internal static class SpawnProbeCommand
    {
        public const string Usage =
            "Debug: probe fish/forage spawns. Usage: tly_spawnprobe fish <rolls> [waterDepth] | tly_spawnprobe forage";

        private const int DefaultWaterDepth = 5;
        private const int TopRows = 25;

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string verb = args.Length > 0 ? args[0] : "";
            if (verb == "fish") Fish(monitor, args);
            else if (verb == "forage") Forage(monitor);
            else monitor.Log(Usage, LogLevel.Warn);
        }

        private static void Fish(IMonitor monitor, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int rolls) || rolls <= 0)
            {
                monitor.Log(Usage, LogLevel.Warn);
                return;
            }
            int depth = args.Length > 2 && int.TryParse(args[2], out int d) ? d : DefaultWaterDepth;
            Farmer player = Game1.player;
            GameLocation location = player.currentLocation;
            var tally = new Dictionary<string, int>();
            for (int i = 0; i < rolls; i++)
            {
                Item fish = GameLocation.GetFishFromLocationData(
                    location.Name, player.Tile, depth, player, isTutorialCatch: false, isInherited: false, location);
                string name = fish == null ? "(nothing)" : $"{fish.DisplayName} {fish.QualifiedItemId}";
                tally[name] = tally.TryGetValue(name, out int n) ? n + 1 : 1;
            }
            Print(monitor, $"fish x{rolls} at {location.Name} {player.TilePoint} {Game1.timeOfDay} {Game1.currentSeason} rain={location.IsRainingHere()}", tally);
        }

        private static void Forage(IMonitor monitor)
        {
            var tally = new Dictionary<string, int>();
            foreach (GameLocation location in Game1.locations)
                foreach (Object obj in location.objects.Values.Where(o => o.IsSpawnedObject))
                {
                    string name = $"{obj.DisplayName} {obj.QualifiedItemId}";
                    tally[name] = tally.TryGetValue(name, out int n) ? n + 1 : 1;
                }
            Print(monitor, $"forage on day {Game1.dayOfMonth} {Game1.currentSeason}", tally);
        }

        private static void Print(IMonitor monitor, string header, Dictionary<string, int> tally)
            => monitor.Log($"tly_spawnprobe: {header}: "
                + string.Join("; ", tally.OrderByDescending(kv => kv.Value).Take(TopRows).Select(kv => $"{kv.Key}={kv.Value}")),
                LogLevel.Info);
    }
}
