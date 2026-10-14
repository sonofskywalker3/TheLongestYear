using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Diagnostics-only dry run of the owned-bundle engine for a given loop number:
        /// generates the set (nothing written or persisted — never touches live BundleData or
        /// MetaState), logs each room's picked bundle names + slot counts, the manifest
        /// classification summary, and a determinism self-check (regenerates off the SAME seed
        /// and diffs the two sets byte-for-byte). Mirrors <see cref="CmdClassify"/>'s design —
        /// locals only. Guarded exactly like tly_classify (requires a loaded save) because the
        /// seed basis is Game1.player.UniqueMultiplayerID, which doesn't exist at the title
        /// screen.
        ///
        /// The optional mode argument (<c>custom</c> default, <c>standard</c>, <c>remixed</c>)
        /// picks WHICH board is audited: the engine's own set, or the board vanilla would build
        /// for the matching Advanced Options dropdown choice
        /// (<see cref="TheLongestYear.Loop.BundleOptionPatch.Choice"/>). Both vanilla modes run
        /// the exact same listing, classification, gate audit and determinism self-check, and
        /// write nothing either.</summary>
        private void CmdGenBundles(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int seedLoop = _meta.State.EffectiveBundleSeedLoop;
            var mode = TheLongestYear.Loop.BundleOptionPatch.Choice.TlyCustom;
            foreach (string arg in args)
            {
                if (int.TryParse(arg, out int parsedLoop))
                {
                    // Same rejection tly_reset makes: a seed loop is a count of completed resets,
                    // so a negative one is a typo, not a board.
                    if (parsedLoop < 0)
                    {
                        this.Monitor.Log($"tly_genbundles: '{arg}' is not a seed loop. Usage: tly_genbundles [seedLoop] [custom|standard|remixed]", LogLevel.Warn);
                        return;
                    }
                    seedLoop = parsedLoop;
                }
                else if (TheLongestYear.Loop.BundleOptionPatch.TryParseChoice(arg, out var parsedMode))
                    mode = parsedMode;
                else
                {
                    this.Monitor.Log($"tly_genbundles: unknown argument '{arg}'. Usage: tly_genbundles [seedLoop] [custom|standard|remixed]", LogLevel.Warn);
                    return;
                }
            }

            // Same seed basis as ResolveRequirements/WorldResetService.PerformReset — see
            // ResolveRequirements' comment for why (Game1.uniqueIDForThisGame is time-based and
            // re-seeded by our own reset every loop, so it can't be the basis).
            ulong seedBasis = unchecked((ulong)Game1.player.UniqueMultiplayerID);
            int seed = BundleEngineSeed.For(seedBasis, seedLoop);

            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins = ParseItemSeasonPins();
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas = ParseBundleQuotas();

            if (mode != TheLongestYear.Loop.BundleOptionPatch.Choice.TlyCustom)
            {
                GenBundlesVanilla(mode, seedLoop, seed, itemSeasonPins, bundleQuotas);
                return;
            }

            // Diagnostics have to show what the loop actually runs under, so this uses the STAMPED
            // profile like every other generation path. A preview resolved from live config would
            // report a board the save is not playing.
            TheLongestYear.Core.DifficultyProfile genDifficulty = _meta.State.BoardDifficulty(_config);
            BundleGenerationTuning genTuning =
                TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, genDifficulty);
            var firstEngine = new TheLongestYear.Loop.BundleEngine(this.Monitor, genTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, genDifficulty.Steps.ItemRarity), genDifficulty);
            firstEngine.Availability = _availability;
            firstEngine.AllowModItems = _meta.State.ModItemsOnBoard();
            GeneratedBundleSet first = firstEngine.Generate(seed, _meta.State.RandomBundleRewardsBoard);
            this.Monitor.Log(
                $"tly_genbundles: generated for loop {seedLoop} (seed {seed}, mode custom), diagnostics only, nothing written.",
                LogLevel.Info);
            LogGeneratedBundleSet(firstEngine, first, itemSeasonPins, bundleQuotas);

            var secondEngine = new TheLongestYear.Loop.BundleEngine(this.Monitor, genTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, genDifficulty.Steps.ItemRarity), genDifficulty);
            secondEngine.Availability = _availability;
            secondEngine.AllowModItems = _meta.State.ModItemsOnBoard();
            GeneratedBundleSet second = secondEngine.Generate(seed, _meta.State.RandomBundleRewardsBoard);
            string difference = FirstBundleSetDifference(first, second);
            if (difference == null)
                this.Monitor.Log("tly_genbundles: determinism OK (second generation matched the first byte-for-byte).", LogLevel.Info);
            else
                this.Monitor.Log($"tly_genbundles: determinism ERROR: {difference}", LogLevel.Error);
        }

        /// <summary>The vanilla half of <see cref="CmdGenBundles"/>: build the board the GAME
        /// would build for the given Advanced Options dropdown choice, then run it through the
        /// same listing, classification, gate audit and determinism self-check the engine board
        /// gets. Nothing is written: neither path touches BundleData or MetaState.
        ///
        /// Standard is just <c>Data/Bundles</c> (what <c>Game1.GenerateBundles(Default)</c>
        /// hands to SetBundleData). Remixed runs the game's own <c>BundleGenerator</c> over
        /// <c>Data/RandomBundles</c>, seeded with vanilla's own formula
        /// (<c>CreateRandom(seed * 9.0)</c>) but off OUR per-loop seed rather than
        /// <c>Game1.uniqueIDForThisGame</c>: a TLY reset re-seeds uniqueIDForThisGame from the
        /// clock, so the game's own per-loop remix is not reproducible, while this is (and it
        /// varies per seed loop, which is the point of the diagnostic).</summary>
        private void GenBundlesVanilla(
            TheLongestYear.Loop.BundleOptionPatch.Choice mode,
            int seedLoop,
            int seed,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas)
        {
            bool remixed = mode == TheLongestYear.Loop.BundleOptionPatch.Choice.VanillaRemixed;
            string label = remixed ? "remixed" : "standard";
            // Standard reads Data/Bundles verbatim, so the seed never enters the board at all: every
            // seed loop yields the same board. Say that in the header line rather than printing a
            // seed the mode ignores.
            string modeLabel = remixed ? "mode remixed" : "mode standard (seed ignored: Data/Bundles is fixed)";

            System.Collections.Generic.IReadOnlyDictionary<string, string> firstData;
            System.Collections.Generic.IReadOnlyDictionary<string, string> secondData = null;
            try
            {
                firstData = remixed
                    ? TheLongestYear.Loop.VanillaBundlePool.GenerateRemixedBundleData(seed)
                    : TheLongestYear.Loop.VanillaBundlePool.LoadStandardBundleData();
                if (remixed)
                    secondData = TheLongestYear.Loop.VanillaBundlePool.GenerateRemixedBundleData(seed);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_genbundles: could not build the vanilla {label} board ({ex.GetType().Name}: {ex.Message}).", LogLevel.Error);
                return;
            }

            GeneratedBundleSet first = TheLongestYear.Loop.VanillaBundlePool.SetFromBundleData(firstData);
            this.Monitor.Log(
                $"tly_genbundles: generated for loop {seedLoop} (seed {seed}, {modeLabel}), diagnostics only, nothing written.",
                LogLevel.Info);
            LogGeneratedBundleSet(null, first, itemSeasonPins, bundleQuotas, $"vanilla slots ({label})");

            // The self-check regenerates off the same seed and diffs. For standard that compares
            // Data/Bundles to itself, which can never fail and so proves nothing: skip it and say so.
            if (!remixed)
            {
                this.Monitor.Log(
                    "tly_genbundles: determinism self-check skipped for standard (Data/Bundles is fixed and the seed is ignored, so there is nothing to vary).",
                    LogLevel.Info);
                return;
            }

            GeneratedBundleSet second = TheLongestYear.Loop.VanillaBundlePool.SetFromBundleData(secondData);
            string difference = FirstBundleSetDifference(first, second);
            if (difference == null)
                this.Monitor.Log("tly_genbundles: determinism OK (second generation matched the first byte-for-byte).", LogLevel.Info);
            else
                this.Monitor.Log($"tly_genbundles: determinism ERROR: {difference}", LogLevel.Error);
        }

        /// <summary>Logs each room's picked bundle names + slot counts, then the manifest
        /// classification summary ("N generated, M classified, K skipped"). K counts every
        /// bundle <see cref="GeneratedBundleSet.BuildRequirements"/> drops (Vault/non-themed
        /// rooms are expected to drop — RoomThemeMap has no entry for them — so that's not
        /// logged as a problem); any drop INSIDE a themed room is unexpected (the engine
        /// authored every bundle, so nothing themed should ever fail classification) and is
        /// called out at WARN with a per-room breakdown.</summary>
        /// <param name="engine">The engine that produced the set, for its per-slot provenance.
        /// Null for a vanilla board (no engine ran); then <paramref name="slotSourceOverride"/>
        /// labels every slot line instead.</param>
        /// <param name="slotSourceOverride">Fixed slot-source label, e.g.
        /// "vanilla slots (remixed)". Null keeps the engine's per-bundle provenance.</param>
        private void LogGeneratedBundleSet(
            TheLongestYear.Loop.BundleEngine engine,
            GeneratedBundleSet set,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas,
            string slotSourceOverride = null)
        {
            var derivedSeasonPins = engine?.LastDerivedSeasonPins
                ?? (System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season>)
                   new Dictionary<string, TheLongestYear.Core.Season>(StringComparer.Ordinal);

            foreach (var roomGroup in set.Bundles.GroupBy(b => b.Room).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                this.Monitor.Log($"  {roomGroup.Key}:", LogLevel.Info);
                foreach (BundleSpec spec in roomGroup.OrderBy(b => b.Index))
                {
                    // Authored names are unique by construction (AuthoredBundleCatalog), so an
                    // exact Name match is sufficient here -- unlike Uniquify's " II"/" III"
                    // collision suffixes (which only ever apply to vanilla RandomBundles name
                    // collisions), an authored def's Name never gets suffixed.
                    string authoredTag = engine != null && TheLongestYear.Core.AuthoredBundleCatalog.All.Any(d => d.Name == spec.Name)
                        ? " [authored]"
                        : "";
                    this.Monitor.Log(
                        $"    [{spec.Index}] {spec.DisplayName} (pick {spec.NumberOfSlots} of {spec.Slots.Count}){authoredTag}",
                        LogLevel.Info);

                    // engine.LastDomains keyed by absolute index; missing key/None = vanilla slots
                    // (money bundles only since spec 2026-08-28-obtainable-board-3-pools). A Recipe
                    // pick names its recipe and its parts, so the log shows WHICH pool it drew from.
                    // An authored bundle is composed by AuthoredBundleComposer and is FINAL, so the
                    // engine files it under None as well; it does NOT keep vanilla slots, and
                    // saying so made the "no non-money bundle keeps vanilla slots" audit unreadable.
                    string source;
                    if (slotSourceOverride != null)
                        source = slotSourceOverride;
                    else if (authoredTag.Length > 0)
                        source = "authored slots";
                    else if (!engine.LastDomains.TryGetValue(spec.Index, out TheLongestYear.Core.DomainMatch m)
                        || m.Domain == TheLongestYear.Core.PoolDomain.None)
                        source = "vanilla slots";
                    else if (m.Domain == TheLongestYear.Core.PoolDomain.Recipe)
                        source = $"re-rolled from recipe {(engine.LastRecipes.TryGetValue(spec.Index, out string recipe) ? recipe : spec.Name)}";
                    else
                        source = $"re-rolled from {m.Domain}{(m.Season != null ? $"({m.Season})" : "")}";
                    this.Monitor.Log(
                        $"      {spec.Room}/{spec.Index} '{spec.Name}' [{spec.Slots.Count} slots, need {spec.NumberOfSlots}] — {source}",
                        LogLevel.Info);

                    // Vault money slots carry the GOLD amount in Quality with item id -1, so they
                    // showed up here as "-1 q25000" and read like an impossible quality ask. They are
                    // not asks at all: skip them (Jeff, 0.13.0 smoke).
                    var qualityAsks = spec.Slots
                        .Where(s => s.Quality > 0 && s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{s.ItemId} q{s.Quality}").ToList();
                    if (qualityAsks.Count > 0)
                        this.Monitor.Log($"        quality asks: {string.Join(", ", qualityAsks)}", LogLevel.Info);

                    // Stack asks above 1, so a balance report can show what the stack-size
                    // difficulty modifier actually did. Money slots are excluded for the same
                    // reason as above: a Vault "stack" is a gold amount, not an ask.
                    var stackAsks = spec.Slots
                        .Where(s => s.Stack > 1 && s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{s.ItemId} x{s.Stack}").ToList();
                    if (stackAsks.Count > 0)
                        this.Monitor.Log($"        stack asks: {string.Join(", ", stackAsks)}", LogLevel.Info);

                    // Every slot by name, so a log alone is enough to audit what the board asks
                    // for (bundle-loop audit, 2026-08-29). Money slots are gold amounts, not asks.
                    var slotNames = spec.Slots
                        .Where(s => s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{DisplayName(s.ItemId)} ({s.ItemId}) x{s.Stack}{(s.Quality > 0 ? $" q{s.Quality}" : "")}")
                        .ToList();
                    if (slotNames.Count > 0)
                        this.Monitor.Log($"        slots: {string.Join(", ", slotNames)}", LogLevel.Info);
                }
            }
            this.Monitor.Log($"  derived season pins in effect: {derivedSeasonPins.Count}", LogLevel.Info);

            IReadOnlyList<BundleRequirement> requirements = engine != null
                ? engine.BuildRequirements(set, itemSeasonPins, bundleQuotas, availability: _availability)
                : set.BuildRequirements(itemSeasonPins, bundleQuotas, availability: _availability);
            int generated = set.Bundles.Count;
            int classified = requirements.Count;
            int skipped = generated - classified;

            // The gates this board would run under, then the same audit tly_gatecheck runs on the
            // live board, so a diagnostic loop can be checked for IMPOSSIBLE gates without a reset.
            foreach (BundleRequirement req in requirements.OrderBy(r => r.Theme).ThenBy(r => r.Name, StringComparer.Ordinal))
            {
                string gates = req.Kind switch
                {
                    BundleKind.Seasonal => $"all by {req.SeasonalSeason}",
                    BundleKind.PerItem => string.Join(", ", req.ItemSeasonPins
                        .OrderBy(kv => (int)kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => $"{DisplayName(kv.Key)} by {kv.Value}")),
                    BundleKind.Percentage => $"ramp [{string.Join(",", req.CumulativeRequiredBySeason)}] of X={req.NumberOfSlots}",
                    _ => "",
                };
                this.Monitor.Log($"      gates {req.Name} ({req.Kind}): {gates}", LogLevel.Info);
            }
            var auditPins = new Dictionary<string, TheLongestYear.Core.Season>(derivedSeasonPins, StringComparer.Ordinal);
            foreach (var kv in itemSeasonPins)
                auditPins[kv.Key] = kv.Value;
            LogGateAudit(requirements, auditPins, "tly_genbundles", engine?.LastVanillaOnlyRecipes);

            var themedSkipsByRoom = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BundleSpec spec in set.Bundles)
            {
                if (!RoomThemeMap.TryGetTheme(spec.Room, out TheLongestYear.Core.Theme theme))
                    continue; // Vault / non-themed room — always classified out, not a problem.

                var parsed = BundleParsing.Parse(BundleDataWriter.Key(spec), BundleDataWriter.Value(spec));
                if (BundleClassifier.Classify(parsed, theme, itemSeasonPins, bundleQuotas, _availability) == null)
                    themedSkipsByRoom[spec.Room] = themedSkipsByRoom.TryGetValue(spec.Room, out int n) ? n + 1 : 1;
            }
            int themedSkipped = themedSkipsByRoom.Values.Sum();

            this.Monitor.Log(
                $"tly_genbundles: {generated} generated, {classified} classified, {skipped} skipped.",
                LogLevel.Info);
            if (themedSkipped > 0)
            {
                string breakdown = string.Join(", ", themedSkipsByRoom.Select(kv => $"{kv.Key}: {kv.Value}"));
                // A vanilla board can legitimately drop a themed bundle (category-only asks such
                // as "any fish"), so that is reported, not flagged as a defect; only the engine's
                // own board is expected to classify every themed bundle it authored.
                this.Monitor.Log(
                    engine != null
                        ? $"tly_genbundles: {themedSkipped} skipped bundle(s) fell inside themed rooms (unexpected: {breakdown})."
                        : $"tly_genbundles: {themedSkipped} skipped bundle(s) fell inside themed rooms ({breakdown}).",
                    engine != null ? LogLevel.Warn : LogLevel.Info);
            }
        }

        /// <summary>Compares two engine-generated sets by their written BundleData key/value
        /// pairs (the canonical form the game itself would see) and returns a description of the
        /// first difference found, or null if they're identical.</summary>
        private static string FirstBundleSetDifference(GeneratedBundleSet a, GeneratedBundleSet b)
        {
            IReadOnlyDictionary<string, string> dataA = a.ToBundleData();
            IReadOnlyDictionary<string, string> dataB = b.ToBundleData();

            foreach (string key in dataA.Keys.Union(dataB.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool inA = dataA.TryGetValue(key, out string valueA);
                bool inB = dataB.TryGetValue(key, out string valueB);
                if (!inA)
                    return $"key '{key}' is present in the second generation but missing from the first.";
                if (!inB)
                    return $"key '{key}' is present in the first generation but missing from the second.";
                if (valueA != valueB)
                    return $"key '{key}' differs: '{valueA}' vs '{valueB}'.";
            }
            return null;
        }
    }
}
