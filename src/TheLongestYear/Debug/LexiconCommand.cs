using System;
using System.IO;
using System.Text.Json;
using StardewModdingAPI;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.DebugCommands
{
    /// <summary><c>tly_lexicon &lt;sub-command&gt;</c>: tooling for the shipped path lexicon.
    /// <c>dumpinputs [fileName]</c> writes the obtainability inputs the last save load read from the
    /// live game data as JSON in the mod folder; run on an unmodded game, that file is the Core
    /// tests' vanilla inputs fixture.</summary>
    internal static class LexiconCommand
    {
        public const string Name = "tly_lexicon";
        public const string Description =
            "Path lexicon tooling. Usage: tly_lexicon dumpinputs [fileName] (writes the obtainability inputs the last load read as JSON in the mod folder)";

        private const string DumpInputs = "dumpinputs";
        private const string DefaultInputsFileName = "obtainability-inputs.json";
        private const string Usage = "Usage: tly_lexicon dumpinputs [fileName]";

        public static void Run(IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, string[] args)
        {
            if (args.Length == 0 || !string.Equals(args[0], DumpInputs, StringComparison.OrdinalIgnoreCase))
            {
                monitor.Log(Usage, LogLevel.Info);
                return;
            }
            if (inputs == null)
            {
                monitor.Log("tly_lexicon dumpinputs: no obtainability inputs yet; load a save first.", LogLevel.Warn);
                return;
            }

            string fileName = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : DefaultInputsFileName;
            string path = Path.Combine(modDirectory, fileName);
            string json = JsonSerializer.Serialize(inputs, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(path, json);
            monitor.Log(
                $"tly_lexicon dumpinputs: wrote {path} ({json.Length:N0} chars; {inputs.Objects.Count} objects, {inputs.Shops.Count} shop rows).",
                LogLevel.Info);
        }
    }
}
