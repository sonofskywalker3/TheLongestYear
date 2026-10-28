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
        Func<AvailabilityInputs> AvailabilityInputs,
        TheLongestYear.Loop.LoadedLayers Layers, AnswerSet Vanilla, TheLongestYear.Loop.LexiconService Service);

    /// <summary><c>tly_lexicon &lt;sub-command&gt;</c>: tooling for the shipped path lexicon.
    /// <c>dumpinputs [fileName] [availabilityFileName]</c> writes the obtainability inputs the last save
    /// load read from the live game data as JSON in the mod folder, and the availability inputs
    /// (<see cref="AvailabilityInputs"/>, the projection the availability answers' stamp hashes) beside
    /// them; run on an unmodded game, the two files are the Core tests' vanilla inputs fixtures. <c>write vanilla</c> cold-builds the answers from the same
    /// inputs and writes (or merges) the save's variant into the shipped file
    /// <see cref="ShippedLexicon.VanillaRelativePath"/>: <c>pierre_year2_seeds</c> when the save owns
    /// that upgrade, else <c>base</c>. Run it on an unmodded game only.
    /// <c>write &lt;UniqueID&gt;</c> (or <c>write &lt;A&gt;+&lt;B&gt;</c>) cold-builds the live answers with
    /// the active layers' extra sources and writes that layer's answers delta (<see cref="LayerDelta.Write"/>)
    /// to <c>assets/lexicon/layers/</c>, keeping the file's other variant. Run it with only the named
    /// mods (and TLY) installed, so nothing else's rows or routes land in the delta.
    /// <c>status</c> logs where the current answers came from, the layers and the cache folder.
    /// <c>diff [file]</c> cold-builds the answers the way a cache miss would and compares them to the
    /// current ones (<see cref="LexiconDiff"/>), writing <c>lexicon-diff.md</c>; <c>gaps [file]</c> writes
    /// <c>lexicon-gaps.md</c> (<see cref="LexiconGaps"/>).</summary>
    internal static class LexiconCommand
    {
        public const string Name = "tly_lexicon";
        public const string Description =
            "Path lexicon tooling. Usage: tly_lexicon status (origin, layers, cache files) | tly_lexicon diff [file] (cold build vs the current answers; writes lexicon-diff.md) | tly_lexicon gaps [file] (items with no route, unreached maps, unresolved sources; writes lexicon-gaps.md) | tly_lexicon dumpinputs [fileName] [availabilityFileName] (writes the obtainability and availability inputs the last load read as JSON in the mod folder) | tly_lexicon write vanilla (builds this save's variant into assets/lexicon/vanilla.json; unmodded game only) | tly_lexicon write <UniqueID>[+<UniqueID>] (writes that supported-mod layer's answers delta into assets/lexicon/layers/; only those mods installed)";

        private const int KeyPrefixLength = 8;
        private const string StatusVerb = "status";
        private const string DiffVerb = "diff";
        private const string GapsVerb = "gaps";
        private const string DefaultDiffFileName = "lexicon-diff.md";
        private const string DefaultGapsFileName = "lexicon-gaps.md";
        private const string DumpInputs = "dumpinputs";
        private const string WriteVerb = "write";
        private const string VanillaTarget = "vanilla";
        private const string DefaultInputsFileName = "obtainability-inputs.json";
        private const string DefaultAvailabilityInputsFileName = "availability-inputs.json";
        private const string Usage = "Usage: tly_lexicon status | tly_lexicon diff [file] | tly_lexicon gaps [file] | tly_lexicon dumpinputs [fileName] [availabilityFileName] | tly_lexicon write vanilla | tly_lexicon write <UniqueID>[+<UniqueID>]";
        private static readonly string[] VanillaLayers = { RowHashes.Vanilla };

        public static void Run(
            IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, string[] args, Func<LexiconSaveContext> saveContext)
        {
            if (args.Length >= 1 && IsVerb(args[0], StatusVerb)) { Status(monitor, saveContext); return; }
            if (args.Length >= 1 && IsVerb(args[0], DiffVerb)) { Diff(monitor, inputs, modDirectory, args, saveContext); return; }
            if (args.Length >= 1 && IsVerb(args[0], GapsVerb)) { Gaps(monitor, inputs, modDirectory, args, saveContext); return; }
            if (args.Length >= 2 && string.Equals(args[0], WriteVerb, StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[1], VanillaTarget, StringComparison.OrdinalIgnoreCase))
            {
                WriteVanilla(monitor, inputs, modDirectory, saveContext);
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], WriteVerb, StringComparison.OrdinalIgnoreCase))
            {
                // "write A+B", "write A + B" and "write B+A" all name the same layer.
                WriteLayer(monitor, inputs, modDirectory, LayerFile.Name(string.Join("", args[1..])
                    .Split(LayerFile.Joiner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)), saveContext);
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

            AvailabilityInputs availability = saveContext()?.AvailabilityInputs();
            if (availability == null)
            {
                monitor.Log("tly_lexicon dumpinputs: no availability inputs yet (the engine pools are not read); wrote the obtainability inputs only.", LogLevel.Warn);
                return;
            }
            string availabilityFile = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]) ? args[2] : DefaultAvailabilityInputsFileName;
            string availabilityPath = Path.Combine(modDirectory, availabilityFile);
            string availabilityJson = JsonSerializer.Serialize(availability, LexiconJson.Options);
            File.WriteAllText(availabilityPath, availabilityJson);
            monitor.Log(
                $"tly_lexicon dumpinputs: wrote {availabilityPath} ({availabilityJson.Length:N0} chars; inputs {availability.Hash()}, year-two crops capped {availability.YearTwoCapped}).",
                LogLevel.Info);
        }

        private static bool IsVerb(string arg, string verb) => string.Equals(arg, verb, StringComparison.OrdinalIgnoreCase);

        private static string FileArg(string[] args, string fallback)
            => args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : fallback;

        private static string Short(string key) => key.Length > KeyPrefixLength ? key[..KeyPrefixLength] : key;

        private static string JoinOrNone(IEnumerable<string> items)
        {
            var list = items.OrderBy(k => k, StringComparer.Ordinal).ToList();
            return list.Count == 0 ? "none" : string.Join(", ", list);
        }

        private static void Status(IMonitor monitor, Func<LexiconSaveContext> saveContext)
        {
            TheLongestYear.Loop.LexiconService service = saveContext()?.Service;
            if (service == null)
            {
                monitor.Log("tly_lexicon status: no lexicon yet; load a save first.", LogLevel.Warn);
                return;
            }
            Resolved r = service.Current;
            if (r == null)
                monitor.Log("tly_lexicon status: no answers resolved (the last load could not read its inputs).", LogLevel.Warn);
            else
            {
                monitor.Log($"tly_lexicon status: {service.OriginLine}; key {Short(r.Key)}, resolved in {r.Ms} ms.", LogLevel.Info);
                monitor.Log($"tly_lexicon status: items {r.Variant.RoutedCount()} routed of {r.Variant.Obtain.Count}; {r.Variant.MapWeeks.Count} maps reached, {r.Variant.UnreachedMaps.Count} unreached, {r.Variant.ShopWeeks.Count} shops, {r.Variant.Unresolved.Count} unresolved.", LogLevel.Info);
                if (r.Origin == AnswerOrigin.Computed)
                    monitor.Log($"tly_lexicon status: {r.DirtyCount} data row(s) differ from the shipped answers ({JoinOrNone(r.DirtyOwners)}).", LogLevel.Info);
            }
            var layers = service.Loaded.Inputs;
            if (layers.Count == 0)
                monitor.Log("tly_lexicon status: layers: none applied (vanilla only).", LogLevel.Info);
            foreach (var layer in layers)
            {
                string installed = service.Loaded.InstalledVersionOf(layer.UniqueId) ?? "unknown";
                bool same = string.Equals(layer.CheckedVersion, installed, StringComparison.Ordinal);
                monitor.Log($"tly_lexicon status: layer {layer.UniqueId}: checked {layer.CheckedVersion}, installed {installed}, applied" +
                    (same ? "" : " (versions differ; changed rows are recomputed)") + ".", LogLevel.Info);
            }
            if (service.Loaded.Set.UnmappedPairs.Count > 0)
                monitor.Log($"tly_lexicon status: layer pairs not mapped: {string.Join(", ", service.Loaded.Set.UnmappedPairs)}.", LogLevel.Info);
            var present = service.Variant?.Availability.Keys ?? (IEnumerable<string>)Array.Empty<string>();
            monitor.Log($"tly_lexicon status: availability keys present: {JoinOrNone(present)}; computed this session: {JoinOrNone(service.ComputedAvailabilityKeys)}.", LogLevel.Info);
            var files = service.CacheFiles();
            monitor.Log($"tly_lexicon status: cache {TheLongestYear.Loop.LexiconService.CacheFolder}: {files.Count} file(s).", LogLevel.Info);
            foreach (CacheFileInfo f in files)
                monitor.Log($"tly_lexicon status: cache {f.KeyPrefix}: {f.Bytes:N0} bytes, written {f.LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm}.", LogLevel.Info);
        }

        private static void Diff(
            IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, string[] args, Func<LexiconSaveContext> saveContext)
        {
            if (inputs == null) { monitor.Log("tly_lexicon diff: no obtainability inputs yet; load a save first.", LogLevel.Warn); return; }
            LexiconSaveContext context = saveContext();
            TheLongestYear.Loop.LexiconService service = context?.Service;
            if (service?.Current == null || service.Variant == null)
            {
                monitor.Log("tly_lexicon diff: no resolved answers; load a save first.", LogLevel.Warn);
                return;
            }
            var timer = Stopwatch.StartNew();
            Resolved current = service.Current;
            // Exactly what a cache miss builds: the composed layers' extra sources, this save's weeks and shops.
            var extra = context.Layers?.Set.ExtraDirectSources;
            AnswerVariant cold;
            IReadOnlyList<string> lines;
            try
            {
                ObtainabilityInputs built = context.Layers?.Set.WithFishSpawns(inputs) ?? inputs;
                ObtainabilityBuild model = ObtainabilityBuilder.Build(built, extraDirect: extra);
                ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(built), extraDirect: extra);
                cold = AnswerVariantFactory.From(model, yearSort, inputs, context.Maps, context.AllMaps, context.Shops)
                    with { Availability = context.Availability() };
                lines = LexiconDiff.Compare(service.Variant, cold);
            }
            catch (Exception e)
            {
                monitor.Log($"tly_lexicon diff: the cold build failed ({e.GetType().Name}: {e.Message}).", LogLevel.Warn);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Lexicon diff").AppendLine();
            sb.AppendLine($"Current answers ('base') vs a cold build of this save's data ('live'). {service.OriginLine}; key {Short(current.Key)}; {lines.Count} difference(s).");
            if (current.Origin == AnswerOrigin.Computed)
                sb.AppendLine("The current answers were computed this session by the same build, so this comparison is trivially empty; run it on a load that served shipped or cached answers.");
            if (service.ComputedAvailabilityKeys.Count > 0)
                sb.AppendLine($"Availability key(s) computed this session ({JoinOrNone(service.ComputedAvailabilityKeys)}) come from the same build, so they are trivially equal.");
            sb.AppendLine($"Compared: obtain and year-sort sources per id (including their inputs), unresolved sources, map and shop weeks, unreached maps, and availability answers for {cold.Availability.Count} key(s) (the inputs stamp is ignored).").AppendLine();
            foreach (string line in lines) sb.AppendLine($"- {line}");
            string path = Path.Combine(modDirectory, FileArg(args, DefaultDiffFileName));
            File.WriteAllText(path, sb.ToString());
            monitor.Log($"tly_lexicon diff: {lines.Count} difference(s), wrote {path} ({service.OriginLine}; {timer.ElapsedMilliseconds} ms).", LogLevel.Info);
        }

        private static void Gaps(
            IMonitor monitor, ObtainabilityInputs inputs, string modDirectory, string[] args, Func<LexiconSaveContext> saveContext)
        {
            TheLongestYear.Loop.LexiconService service = saveContext()?.Service;
            if (inputs == null || service?.Variant == null)
            {
                monitor.Log("tly_lexicon gaps: no resolved answers; load a save first.", LogLevel.Warn);
                return;
            }
            AnswerVariant v = service.Variant;
            string path = Path.Combine(modDirectory, FileArg(args, DefaultGapsFileName));
            File.WriteAllText(path, LexiconGaps.Report(v, inputs));
            monitor.Log(
                $"tly_lexicon gaps: {v.Obtain.Count(p => p.Value.Count == 0)} item(s) with no route, {v.UnreachedMaps.Count} unreached map(s), {v.Unresolved.Count} unresolved source(s); wrote {path}.",
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

            IReadOnlyDictionary<string, ulong> rows = RowHashes.Of(inputs, context.PierreYear2Owned);
            foreach (var owner in rows.Keys.GroupBy(RowHashes.Owner, StringComparer.Ordinal)
                         .Where(g => g.Key != RowHashes.Vanilla).OrderBy(g => g.Key, StringComparer.Ordinal))
                monitor.Log($"tly_lexicon write vanilla: writing vanilla.json from a game with other content: {owner.Count()} rows owned by {owner.Key}.", LogLevel.Warn);

            var timer = Stopwatch.StartNew();
            ObtainabilityBuild model = ObtainabilityBuilder.Build(inputs);
            ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(inputs));
            AnswerVariant variant = AnswerVariantFactory.From(model, yearSort, inputs, context.Maps, context.AllMaps, context.Shops)
                with { Availability = context.Availability() };
            string name = context.PierreYear2Owned ? AnswerSet.PierreVariant : AnswerSet.BaseVariant;

            string path = ShippedLexicon.VanillaPath(modDirectory);
            AnswerSet existing = ShippedLexicon.LoadVanilla(modDirectory);
            AnswerSet merged = AnswerSetMerge.With(
                existing, name, variant, rows, context.GameVersion, context.ModVersion, VanillaLayers,
                m => monitor.Log($"tly_lexicon write vanilla: {m}", LogLevel.Warn));
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
            ObtainabilityInputs built = context.Layers.Set.WithFishSpawns(inputs);
            ObtainabilityBuild model = ObtainabilityBuilder.Build(built, extraDirect: extra);
            ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(built), extraDirect: extra);
            AnswerVariant live = AnswerVariantFactory.From(model, yearSort, inputs, context.Maps, context.AllMaps, context.Shops)
                with { Availability = context.Availability() };
            string name = context.PierreYear2Owned ? AnswerSet.PierreVariant : AnswerSet.BaseVariant;

            string path = Path.Combine(LayerFile.Folder(modDirectory), LayerFile.AnswersFileName(mods));
            LayerAnswers existing;
            try
            {
                existing = File.Exists(path) ? LayerFile.ReadAnswers(File.ReadAllText(path)) : null;
            }
            catch (JsonException e)
            {
                monitor.Log($"{verb}: the existing {path} could not be read ({e.Message}); fix or delete it, then run this again.", LogLevel.Warn);
                return;
            }
            LayerAnswers answers;
            try
            {
                answers = LayerDelta.Write(
                    target, context.Vanilla, context.Layers.Inputs, context.Layers.Answers, name, live,
                    RowHashes.Of(inputs, context.PierreYear2Owned), context.Layers.InstalledVersionOf, existing);
            }
            catch (InvalidOperationException e)
            {
                monitor.Log($"{verb}: no '{name}' variant to measure against ({e.Message}); write that variant of vanilla (and of each layer below) first.", LogLevel.Warn);
                return;
            }
            string json = LayerFile.WriteAnswers(answers);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            answers.Deltas.TryGetValue(name, out AnswerVariant delta);
            monitor.Log(
                $"{verb}: wrote variant '{name}' to {path} ({json.Length:N0} chars; variants [{string.Join(", ", answers.Deltas.Keys)}]; " +
                $"{delta?.Obtain.Count ?? 0} obtain ids differ, {(answers.VariantRowHashes.TryGetValue(name, out var rows) ? rows.Count : 0)} rows; {answers.CheckedVersions}; {timer.ElapsedMilliseconds} ms).",
                LogLevel.Info);
        }
    }
}
