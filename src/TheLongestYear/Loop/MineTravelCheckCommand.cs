using System;
using System.Diagnostics;
using System.Threading.Tasks;
using StardewModdingAPI;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary><c>tly_sabotage travelcheck [save]</c>: builds the obtainability model from the live game
    /// data twice, with and without the mine travel, and logs every fairness answer that differs
    /// (<see cref="MineTravelCheck"/>). Debug only; the game data and the save are read on the game
    /// thread, the two builds and the comparisons run in the background so the game keeps drawing.</summary>
    internal static class MineTravelCheckCommand
    {
        public const string Usage = "tly_sabotage travelcheck [save]";
        private const string SaveArg = "save";

        public static void Run(IMonitor monitor, string[] args)
        {
            bool fromSave = args.Length > 1 && string.Equals(args[1], SaveArg, StringComparison.OrdinalIgnoreCase);
            if (args.Length > 1 && !fromSave)
            {
                monitor.Log($"Usage: {Usage} ('save' starts every asked save from this farm's machines, recipes and animals instead of an empty one)", LogLevel.Warn);
                return;
            }

            ObtainabilityInputs inputs;
            try
            {
                inputs = new GameObtainabilityData(monitor).Build();
            }
            catch (ObtainabilityReadException rex)
            {
                monitor.Log($"travelcheck: the game data did not read ({rex.Message}).", LogLevel.Warn);
                return;
            }
            SaveSnapshot baseSave = fromSave ? SaveSnapshotReader.Read(msg => monitor.Log(msg, LogLevel.Trace)) : null;
            monitor.Log($"travelcheck: building the model with and without mine travel and comparing ({(fromSave ? "this farm" : "an empty farm")} at each depth); results follow in the log.", LogLevel.Info);

            Task.Run(() =>
            {
                try
                {
                    var timer = Stopwatch.StartNew();
                    ObtainabilityModel withTravel = ObtainabilityBuilder.Build(inputs).Model;
                    ObtainabilityModel withoutTravel = ObtainabilityBuilder.Build(inputs, mineTravel: false).Model;
                    long buildMs = timer.ElapsedMilliseconds;
                    TravelCheckResult result = MineTravelCheck.Compare(withTravel, withoutTravel, baseSave);
                    foreach (TravelMismatch m in result.Mismatches)
                        monitor.Log($"travelcheck mismatch: {m}", LogLevel.Warn);
                    monitor.Log(
                        $"travelcheck: {result.Comparisons} comparison(s) over {withTravel.Count} item(s), "
                        + $"{result.Mismatches.Count} mismatch(es); built in {buildMs} ms, done in {timer.ElapsedMilliseconds} ms.",
                        result.Mismatches.Count == 0 ? LogLevel.Info : LogLevel.Warn);
                }
                catch (Exception ex)
                {
                    monitor.Log($"travelcheck failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Error);
                }
            });
        }
    }
}
