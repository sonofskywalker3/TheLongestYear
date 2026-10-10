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
        /// <summary>Decide where this save's bundle requirement manifest comes from (owned-bundle
        /// engine wiring, Task 6; review-fixed v0.11.76). The three-way branch itself is the pure,
        /// tested <see cref="EngineModeDecider.Decide"/>:
        /// <list type="number">
        ///   <item>Engine mode -- <c>BundlesGeneratedForReset == CompletedResets</c>: the live
        ///   save's bundles were engine-written for THIS exact loop. Regenerate the manifest
        ///   deterministically from the seed (Generate() is pure given (UniqueMultiplayerID,
        ///   CompletedResets), so this reproduces the same set WriteToWorld wrote without a
        ///   second write) and defensively verify every generated entry matches the live
        ///   BundleData VALUE-for-value via <see cref="EngineManifestCheck.Matches"/> before
        ///   trusting it -- the engine's write-key space is invariant across generations, so
        ///   key-existence alone would pass a live board stuck on an OLDER generation (reachable:
        ///   MetaStore.Save persists the bumped counters before ForceFullSave writes the world; a
        ///   crash/skip in that window reloads with the new meta but the old generation's bundles
        ///   still on the board). A mismatch logs a WARN and falls through to case 3 instead of
        ///   silently serving a manifest that disagrees with what's actually on the CC board.</item>
        ///   <item>Fresh engine-era run-create -- no prior reset (<c>CompletedResets == 0</c>), no
        ///   legacy marker (<c>BundlesGeneratedForReset == -1</c>), and the CC is untouched (no
        ///   completed slot -- see <see cref="AnyBundleSlotComplete"/>): a brand-new save the
        ///   engine gets to author from day 1. Generates, WRITES the bundles into the world (the
        ///   only branch here that does), and stamps the marker.</item>
        ///   <item>Legacy -- everything else: an in-flight pre-engine loop finishing out on its
        ///   existing bundles. Read-and-classify off live BundleData, unchanged from before this
        ///   task.</item>
        /// </list>
        /// The CcItem catalog (<paramref name="builder"/>.Build(), called by the caller) always
        /// reads live BundleData regardless of which case fires here -- it reflects whatever
        /// bundles are actually live, engine-written or not.</summary>
        private IReadOnlyList<BundleRequirement> ResolveRequirements(
            BundleCatalogBuilder builder,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas)
        {
            MetaState state = _meta.State;
            // See WorldResetService.PerformReset step 11a for why this is the seed basis (not
            // Game1.uniqueIDForThisGame, which our own reset re-seeds every loop and is time-based
            // to begin with) -- it must match exactly what generated whatever is currently live.
            ulong seedBasis = unchecked((ulong)Game1.player.UniqueMultiplayerID);

            bool vanillaSource = BundleSourceNames.IsVanilla(state.BundleSource);
            RequirementsSource source = EngineModeDecider.Decide(
                state.BundlesGeneratedForReset, state.CompletedResets, AnyBundleSlotComplete(), vanillaSource);

            if (source == RequirementsSource.EngineManifest)
            {
                Dictionary<string, string> liveData = Game1.netWorldState.Value.BundleData;
                var seed = BundleEngineSeed.For(seedBasis, state.EffectiveBundleSeedLoop);

                // Generate with the CURRENT EnableNonObjectDonations first; if the live board
                // doesn't match, try the OPPOSITE flag — the only generation input that can
                // change between launches mid-loop. A flipped flag must not demote a healthy
                // engine board to the legacy read path (spec 2026-08-21): the board on disk was
                // composed with the old value and stays valid until the next reset regenerates.
                // Difficulty: re-derivation MUST use the STAMPED profile, never live config. The
                // board on disk was generated under the stamp, so resolving the current GMCM
                // values here would re-derive a different board and demote a healthy save to the
                // legacy read path on the next launch. A legacy save has no stamp and resolves
                // all-Normal, which is exactly what generated its board.
                TheLongestYear.Core.DifficultyProfile difficulty = state.BoardDifficulty(_config);
                BundleGenerationTuning difficultyTuning =
                    TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, difficulty);

                // Preferred path (0.16.158): the board the mod itself wrote, persisted at write time.
                // Verify the live board still IS that board, then rebuild requirements from it.
                // No re-derivation from the seed, so item pools that moved since the write (a
                // data mod's Content Patcher edits keyed on state the reset wiped: SVE audit) cannot
                // demote a healthy board. Saves written before this field existed fall through to
                // the seed re-derivation below.
                if (state.WrittenBoard != null && state.WrittenBoard.Count > 0)
                {
                    if (EngineManifestCheck.MatchesIgnoringDisplayName(state.WrittenBoard, liveData))
                    {
                        var pins = TheLongestYear.Core.BoardRequirements.PinsFromStored(state.WrittenBoardSeasonPins);
                        foreach (KeyValuePair<string, TheLongestYear.Core.Season> pin in itemSeasonPins)
                            pins[pin.Key] = pin.Value;   // base pins win over derived, as BundleEngine.BuildRequirements does
                        var stored = TheLongestYear.Core.BoardRequirements.Build(
                            state.WrittenBoard, pins, bundleQuotas, _availability);
                        this.Monitor.Log(
                            $"Requirements source: stored engine board (loop {state.CompletedResets}, seed loop {state.EffectiveBundleSeedLoop}, {stored.Count} bundles).",
                            LogLevel.Info);
                        return stored;
                    }
                    this.Monitor.Log(
                        $"ResolveRequirements: live board differs from the stored engine board at " +
                        $"{EngineManifestCheck.FirstDifference(state.WrittenBoard, liveData, ignoreDisplayName: true)}; trying seed re-derivation.",
                        LogLevel.Warn);
                }

                // "Allow mod items in custom bundles" is a generation input too (spec 2026-10-08
                // addendum 1). The board's own stamp goes first, never the live choice, so a mid-loop
                // toggle cannot demote the board; an unstamped board tries both values.
                bool[] modItemsOrder = TheLongestYear.Core.CustomBoardModItems.ManifestTryOrder(
                    state.BoardAllowsModItems, state.AllowModItemsInCustomBundles);
                foreach (bool allowModItems in modItemsOrder)
                foreach (bool nonObject in new[] { _config.EnableNonObjectDonations, !_config.EnableNonObjectDonations })
                {
                    var engine = new TheLongestYear.Loop.BundleEngine(this.Monitor, difficultyTuning, nonObject, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(state.HasUpgrade, difficulty.Steps.ItemRarity), difficulty);
                    engine.Availability = _availability;
                    engine.AllowModItems = allowModItems;
                    GeneratedBundleSet set = engine.Generate(seed, state.RandomBundleRewardsBoard);
                    IReadOnlyDictionary<string, string> generatedData = set.ToBundleData();
                    if (!EngineManifestCheck.Matches(generatedData, liveData))
                    {
                        // Say WHICH bundle drifted: a data mod whose Content Patcher edits change
                        // with season/day (SVE audit, TODO) re-derives a different board on the
                        // post-reset reload than the reset wrote, and without this line the
                        // mismatch is undiagnosable from a player log.
                        string drift = EngineManifestCheck.FirstDifference(generatedData, liveData);
                        this.Monitor.Log(
                            $"ResolveRequirements: manifest check (EnableNonObjectDonations={nonObject}, mod items {(allowModItems ? "allowed" : "vanilla only")}) differs at {drift ?? "(no difference found)"}.",
                            LogLevel.Debug);
                        continue;
                    }

                    var requirements = engine.BuildRequirements(
                        set, itemSeasonPins, bundleQuotas, _availability);
                    // An unstamped board now knows what it was built with, so later loads go
                    // straight to the right value.
                    if (state.BoardAllowsModItems == null)
                        state.BoardAllowsModItems = allowModItems;
                    string flagNote = nonObject == _config.EnableNonObjectDonations
                        ? ""
                        : $"; board was generated with EnableNonObjectDonations={nonObject} — honouring it this loop, the current setting applies from the next reset";
                    this.Monitor.Log(
                        $"Requirements source: engine manifest (loop {state.CompletedResets}, seed loop {state.EffectiveBundleSeedLoop}, {requirements.Count} bundles{flagNote}).",
                        LogLevel.Info);
                    return requirements;
                }

                this.Monitor.Log(
                    "ResolveRequirements: engine manifest mismatch (stale or foreign bundle data), " +
                    "falling back to read path.",
                    LogLevel.Warn);
                // fall through to the legacy read-and-classify path below.
            }
            else if (source == RequirementsSource.GenerateFreshRun)
            {
                // The first run has never been through a reset, so nothing has stamped a profile
                // yet. Stamp it here, before generating, so this board and the rest of loop 1 run
                // under the same values a later reset would re-stamp.
                state.Difficulty = TheLongestYear.Core.DifficultyResolver.Resolve(_config.Difficulty, _config);
                BundleGenerationTuning freshTuning =
                    TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, state.Difficulty);
                var engine = new TheLongestYear.Loop.BundleEngine(this.Monitor, freshTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, state.Difficulty.Steps.ItemRarity), state.Difficulty);
                engine.Availability = _availability;
                // A NEW board takes the save's "Allow mod items in custom bundles" choice (written
                // from the new-game default above) and stamps it with the board.
                state.BoardAllowsModItems = state.ModItemsChosen();
                engine.AllowModItems = state.BoardAllowsModItems.Value;
                // Randomizer: the fresh-run board is a NEW board, so it stamps the option here.
                state.RandomBundleRewardsBoard = _config.Randomizer?.RandomBundleRewards ?? false;
                GeneratedBundleSet set = engine.Generate(BundleEngineSeed.For(seedBasis, 0), state.RandomBundleRewardsBoard);
                engine.WriteToWorld(set, this.Monitor);
                state.BundlesGeneratedForReset = 0;
                state.WrittenBoard = new Dictionary<string, string>(set.ToBundleData());
                state.WrittenBoardSeasonPins = TheLongestYear.Core.BoardRequirements.PinsToStored(engine.LastDerivedSeasonPins);
                // See WorldResetService: the flavored slots' inputs belong to the board that was
                // just written, so they are stamped with it.
                state.WrittenBoardFlavors = new Dictionary<string, string>(set.Flavors);
                var requirements = engine.BuildRequirements(
                    set, itemSeasonPins, bundleQuotas, availability: _availability);
                this.Monitor.Log(
                    $"Requirements source: engine generation (fresh run, {requirements.Count} bundles written).",
                    LogLevel.Info);
                return requirements;
            }

            var legacyRequirements = builder.BuildRequirements();
            if (vanillaSource)
            {
                if (string.IsNullOrEmpty(state.VanillaBundleType))
                    state.VanillaBundleType = InferVanillaBundleType();
                this.Monitor.Log(
                    $"Requirements source: vanilla board (BundleSource=Vanilla, {state.VanillaBundleType}; read-and-classify, {legacyRequirements.Count} bundles; regenerated the same way at each reset).",
                    LogLevel.Info);
            }
            else
            {
                this.Monitor.Log(
                    "Requirements source: legacy read-and-classify (pre-engine save; regenerates at next reset).",
                    LogLevel.Info);
            }
            return legacyRequirements;
        }

        /// <summary>Standard vs Remixed for a Vanilla-mode save that predates the persisted
        /// choice: the live board IS the Data/Bundles asset (value-for-value) ⇒ Default,
        /// anything else ⇒ Remixed. A Content-Patcher-edited Data/Bundles compares equal too,
        /// which is right — GenerateBundles(Default) reproduces it.</summary>
        private string InferVanillaBundleType()
        {
            try
            {
                var standard = Game1.content.Load<Dictionary<string, string>>("Data\\Bundles");
                bool isStandard = BoardInspection.MatchesReference(Game1.netWorldState.Value.BundleData, standard);
                string type = isStandard ? Game1.BundleType.Default.ToString() : Game1.BundleType.Remixed.ToString();
                this.Monitor.Log($"VanillaBundleType inferred from the live board: {type}.", LogLevel.Info);
                return type;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"VanillaBundleType inference failed ({ex.GetType().Name}: {ex.Message}) — assuming Default.", LogLevel.Warn);
                return Game1.BundleType.Default.ToString();
            }
        }

        /// <summary>Vanilla mode only: if another mod rewrote BundleData since we classified
        /// (Challenging CC Bundles swaps values on DayStarted, AFTER our SaveLoaded), rebuild
        /// the catalog + requirements from the live board so season goals, weekly-theme pools
        /// and the donation patches follow what the CC actually shows.</summary>
        private void ReclassifyIfBoardChanged()
        {
            if (_boardBuilder == null || !BundleSourceNames.IsVanilla(_meta.State.BundleSource)) return;
            string fingerprint = BoardInspection.Fingerprint(Game1.netWorldState.Value.BundleData);
            if (fingerprint == _boardFingerprint) return;

            _boardFingerprint = fingerprint;
            _catalog = _boardBuilder.Build();
            _requirements = _boardBuilder.BuildRequirements();
            _runController?.ReplaceCatalog(_catalog);
            _runController?.ReplaceRequirements(_requirements);
            TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots =
                BoardInspection.HasNonObjectIngredients(Game1.netWorldState.Value.BundleData);
            this.Monitor.Log(
                $"Board changed by another mod since load — re-classified from the live data ({_requirements.Count} bundles, {_catalog.Count} catalog items).",
                LogLevel.Info);
        }

        /// <summary>True when any CC bundle completion slot is already marked complete. Same
        /// FieldDict-scan idiom as WorldResetService.PerformReset step 1a's defensive wipe, used
        /// read-only here to detect whether a fresh save's Community Center has been touched yet
        /// (gates the run-create branch of <see cref="ResolveRequirements"/>).</summary>
        private static bool AnyBundleSlotComplete()
        {
            foreach (KeyValuePair<int, Netcode.NetArray<bool, Netcode.NetBool>> kvp
                     in Game1.netWorldState.Value.Bundles.FieldDict)
            {
                Netcode.NetArray<bool, Netcode.NetBool> arr = kvp.Value;
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i])
                        return true;
            }
            return false;
        }

        /// <summary>Merge GameplayConfig.DefaultItemSeasonPins + user ItemSeasonPins. User wins on conflict.
        /// Invalid season strings in user config are logged and skipped (<see cref="ConfigOverrides"/>).</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> ParseItemSeasonPins()
            => ConfigOverrides.MergeEnum<TheLongestYear.Core.Season>(
                TheLongestYear.Core.GameplayConfig.DefaultItemSeasonPins, _config?.ItemSeasonPins,
                (key, value) => this.Monitor.Log(
                    $"ItemSeasonPins: '{value}' is not a valid season for id '{key}' — ignoring.",
                    LogLevel.Warn));

        /// <summary>Merge GameplayConfig.DefaultBundleQuotas + user BundleQuotas. User wins on conflict.
        /// Malformed user arrays (wrong length, negative values) are logged and skipped
        /// (<see cref="ConfigOverrides"/>).</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, int[]> ParseBundleQuotas()
            => ConfigOverrides.MergeQuotas(
                TheLongestYear.Core.GameplayConfig.DefaultBundleQuotas, _config?.BundleQuotas,
                TheLongestYear.Core.Calendar.MonthsPerYear,
                (key, value, problem) => this.Monitor.Log(
                    problem == ConfigOverrides.QuotaProblem.Negative
                        ? $"BundleQuotas: '{key}' has a negative count; ignoring."
                        : $"BundleQuotas: '{key}' needs a 4-int cumulative array; got length {value?.Length ?? 0} — ignoring.",
                    LogLevel.Warn));

        /// <summary>Merge GameplayConfig.DefaultThemeOverrides + user ThemeOverrides for the catalog builder.</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Theme> ParseThemeOverrides()
            => ConfigOverrides.MergeEnum<TheLongestYear.Core.Theme>(
                TheLongestYear.Core.GameplayConfig.DefaultThemeOverrides, _config?.ThemeOverrides,
                (key, value) => this.Monitor.Log(
                    $"ThemeOverrides: '{value}' is not a valid theme for id '{key}' — ignoring.",
                    LogLevel.Warn));
    }
}
