using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.DebugCommands
{
    /// <summary>What <c>tly_lexicon write</c> needs from the loaded save beyond the inputs: for a layer,
    /// also the active layers and the shipped vanilla answers (null before the lexicon is set up).</summary>
    internal sealed record LexiconSaveContext(
        bool PierreYear2Owned, LocationWeeks Maps, ShopWeeks Shops, IReadOnlyCollection<string> AllMaps,
        string GameVersion, string ModVersion,
        Func<IReadOnlyDictionary<string, AvailabilityAnswers>> Availability,
        TheLongestYear.Loop.LoadedLayers Layers, AnswerSet Vanilla);

    /// <summary><c>tly_lexicon &lt;sub-command&gt;</c>: tooling for the shipped path lexicon.
    /// <c>dumpinputs [fileName]</c> writes the obtainability inputs the last save load read from the
    /// live game data as JSON in the mod folder; run on an unmodded game, that file is the Core
    /// tests' vanilla inputs fixture. <c>write vanilla</c> cold-builds the answers from the same
    /// inputs and writes (or merges) the save's variant into the shipped file
    /// <see cref="ShippedLexicon.VanillaRelativePath"/>: <c>pierre_year2_seeds</c> when the save owns
    /// that upgrade, else <c>base</c>. Run it on an unmodded game only.
    /// <c>write &lt;UniqueID&gt;</c> (or <c>write &lt;A&gt;+&lt;B&gt;</c>) cold-builds the live answers with
    /// the active layers' extra sources and writes that layer's answers delta (<see cref="LayerDelta.Write"/>)
    /// to <c>assets/lexicon/layers/</c>, keeping the file's other variant. Run it with only the named
    /// mods (and TLY) installed, so nothing else's rows or routes land in the delta.</summary>
    internal static class LexiconCommand
    {
        public const string Name = "tly_lexicon";
        public const string Description =
            "Path lexicon tooling. Usage: tly_lexicon dumpinputs [fileName] (writes the obtainability inputs the last load read as JSON in the mod folder) | tly_lexicon write vanilla (builds this save's variant into assets/lexicon/vanilla.json; unmodded game only) | tly_lexicon write <UniqueID>[+<UniqueID>] (writes that supported-mod layer's answers delta into assets/lexicon/layers/; only those mods installed)";

        private const string DumpInputs = "dumpinputs";
        private const string WriteVerb = "write";
        private const string VanillaTarget = "vanilla";
        private const string DefaultInputsFileName = "obtainability-inputs.json";
        private const string Usage = "Usage: tly_lexicon dumpinputs [fileName] | tly_lexicon write vanilla | tly_lexicon write <UniqueID>[+<UniqueID>]";
        private static readonly string[] VanillaLayers = { RowHashes.Vanilla };

        public static void Run(
            IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, string[] args, Func<LexiconSaveContext> saveContext)
        {
            if (args.Length >= 2 && string.Equals(args[0], WriteVerb, StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[1], VanillaTarget, StringComparison.OrdinalIgnoreCase))
            {
                WriteVanilla(monitor, inputs, modDirectory, saveContext);
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], WriteVerb, StringComparison.OrdinalIgnoreCase))
            {
                WriteLayer(monitor, inputs, modDirectory, args[1], saveContext);
                return;
            }
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

        private static void WriteVanilla(IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, Func<LexiconSaveContext> saveContext)
        {
            if (inputs == null)
            {
                monitor.Log("tly_lexicon write vanilla: no obtainability inputs yet; load a save first.", LogLevel.Warn);
                return;
            }
            LexiconSaveContext context = saveContext();
            if (context == null)
            {
                monitor.Log("tly_lexicon write vanilla: no save loaded.", LogLevel.Warn);
                return;
            }

            var timer = Stopwatch.StartNew();
            ObtainabilityBuild model = ObtainabilityBuilder.Build(inputs);
            ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(inputs));
            AnswerVariant variant = AnswerVariantFactory.From(model, yearSort, inputs, context.Maps, context.AllMaps, context.Shops)
                with { Availability = context.Availability() };
            string name = context.PierreYear2Owned ? AnswerSet.PierreVariant : AnswerSet.BaseVariant;

            string path = ShippedLexicon.VanillaPath(modDirectory);
            AnswerSet existing = ShippedLexicon.LoadVanilla(modDirectory);
            AnswerSet merged = AnswerSetMerge.With(
                existing, name, variant, RowHashes.Of(inputs, context.PierreYear2Owned),
                context.GameVersion, context.ModVersion, VanillaLayers);
            string json = LexiconJson.Write(merged);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            monitor.Log(
                $"tly_lexicon write vanilla: wrote variant '{name}' to {path} ({json.Length:N0} chars; variants [{string.Join(", ", merged.Variants.Keys)}]; " +
                $"{variant.Obtain.Count} obtain ids, {variant.MapWeeks.Count} maps reached, {variant.UnreachedMaps.Count} unreached, " +
                $"{variant.ShopWeeks.Count} shops, {variant.Availability.Count} availability answer set(s), {model.Passes} pass(es), {model.Unresolved.Count} unresolved; game {context.GameVersion}; {timer.ElapsedMilliseconds} ms).",
                LogLevel.Info);
        }

        private static void WriteLayer(
            IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, string target, Func<LexiconSaveContext> saveContext)
        {
            string verb = $"tly_lexicon write {target}";
            if (inputs == null)
            {
                monitor.Log($"{verb}: no obtainability inputs yet; load a save first.", LogLevel.Warn);
                return;
            }
            LexiconSaveContext context = saveContext();
            if (context?.Layers == null || context.Vanilla == null)
            {
                monitor.Log($"{verb}: no save loaded, or no shipped vanilla answers to measure against.", LogLevel.Warn);
                return;
            }
            string[] mods = target.Split(LayerFile.Joiner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var activeIds = new HashSet<string>(context.Layers.Set.Ids, StringComparer.Ordinal);
            foreach (string mod in mods)
                if (!activeIds.Contains(mod))
                {
                    monitor.Log($"{verb}: {mod} has no active layer (it needs {LayerFile.FolderRelativePath}/{LayerFile.InputsFileName(mod)} and the mod installed).", LogLevel.Warn);
                    return;
                }
            var others = context.Layers.Set.Ids.Where(id => !mods.Contains(id)).ToList();
            if (others.Count > 0)
                monitor.Log($"{verb}: other supported mods are installed too ({string.Join(", ", others)}); their routes may land in this delta.", LogLevel.Warn);

            var timer = Stopwatch.StartNew();
            var extra = context.Layers.Set.ExtraDirectSources;
            ObtainabilityBuild model = ObtainabilityBuilder.Build(inputs, extraDirect: extra);
            ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(inputs), extraDirect: extra);
            AnswerVariant live = AnswerVariantFactory.From(model, yearSort, inputs, context.Maps, context.AllMaps, context.Shops)
                with { Availability = context.Availability() };
            string name = context.PierreYear2Owned ? AnswerSet.PierreVariant : AnswerSet.BaseVariant;

            string path = Path.Combine(LayerFile.Folder(modDirectory), LayerFile.AnswersFileName(mods));
            LayerAnswers existing = File.Exists(path) ? LayerFile.ReadAnswers(File.ReadAllText(path)) : null;
            LayerAnswers answers = LayerDelta.Write(
                target, context.Vanilla, context.Layers.Inputs, context.Layers.Answers, name, live,
                RowHashes.Of(inputs, context.PierreYear2Owned), context.Layers.InstalledVersionOf, existing);
            string json = LayerFile.WriteAnswers(answers);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            answers.Deltas.TryGetValue(name, out AnswerVariant delta);
            monitor.Log(
                $"{verb}: wrote variant '{name}' to {path} ({json.Length:N0} chars; variants [{string.Join(", ", answers.Deltas.Keys)}]; " +
                $"{delta?.Obtain.Count ?? 0} obtain ids differ, {answers.RowHashes.Count} rows; {answers.CheckedVersions}; {timer.ElapsedMilliseconds} ms).",
                LogLevel.Info);
        }
    }
}
