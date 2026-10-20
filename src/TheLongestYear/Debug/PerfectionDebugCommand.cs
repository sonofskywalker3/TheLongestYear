using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug: log the farmer-side Perfection records the rewind clears (fish caught,
    /// dishes cooked, the Monster Slayer flag, shipped items) and the live Perfection total, or
    /// record one cooked dish through the game's own cookedRecipe so a headless run can stage it.</summary>
    internal static class PerfectionDebugCommand
    {
        public const string Name = "tly_perfection";
        public const string Description =
            "Debug: log fishCaught / recipesCooked / slayer flag / basicShipped counts and percentGameComplete, or record a cooked dish. Usage: tly_perfection [cook <itemId>]";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            if (args.Length >= 2 && args[0].ToLowerInvariant() == "cook")
            {
                p.cookedRecipe(args[1]);
                monitor.Log($"tly_perfection: cooked '{args[1]}'.", LogLevel.Info);
            }
            else if (args.Length > 0)
            {
                monitor.Log(Description, LogLevel.Warn);
                return;
            }

            monitor.Log(
                $"tly_perfection: fishCaught={p.fishCaught.Length} recipesCooked={p.recipesCooked.Length} " +
                $"slayerDone={p.hasCompletedAllMonsterSlayerQuests.Value} slayerKills={p.stats.specificMonstersKilled.Count} " +
                $"basicShipped={p.basicShipped.Length} percent={Utility.percentGameComplete() * 100f:0.0}",
                LogLevel.Info);
        }
    }
}
