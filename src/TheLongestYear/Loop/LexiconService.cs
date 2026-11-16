using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Loop
{
    /// <summary>The game side of the path lexicon: loads the shipped answers once, keeps the disk cache
    /// in <c>data/lexicon/&lt;key&gt;.json</c> under the mod folder (<see cref="LexiconDiskCache"/>:
    /// our own <see cref="LexiconJson"/> text, the <see cref="MaxCacheFiles"/> most recently written or
    /// used entries), and resolves the live data through <see cref="LexiconResolver"/>.
    /// <para>A computed entry is written once per load: the resolver's write is held until the load's
    /// availability answers are attached (<see cref="StoreAvailability"/>) or the load ends
    /// (<see cref="FlushPending"/>).</para>
    /// <para>Supported-mod layers (<see cref="LayerLoader"/>) are composed over the shipped answers
    /// once, here: the resolver is given the composed set as its shipped answers and its layer names
    /// for the cache key, the build gets the layers' extra direct sources, and the cart's year 2 slot
    /// gets their year rulings (<see cref="YearTwoCartPool.LayerRuling"/>).</para></summary>
    internal sealed class LexiconService
    {
        public const string CacheFolder = "data/lexicon";
        public const int MaxCacheFiles = 3;
        private const int KeyPrefixLength = 8;

        private readonly IMonitor _monitor;
        private readonly LexiconDiskCache _cache;
        private readonly LexiconResolver _resolver;
        /// <summary>The last resolve, and its variant with the availability answers added since.
        /// Null before the first resolve and after <see cref="Forget"/>.</summary>
        private Resolved _current;
        private AnswerVariant _variant;
        /// <summary>The computed entry the resolver asked to write and that is not on disk yet.</summary>
        private string _pendingKey;
        /// <summary>The availability keys computed (not served) since the last resolve.</summary>
        private readonly HashSet<string> _computedKeys = new(StringComparer.Ordinal);
        /// <summary>The shipped variant name the last resolve was for (base or Pierre's year-2 line).</summary>
        private string _variantName = AnswerSet.BaseVariant;

        /// <param name="build">The answers for live data nothing has (the cold build, for now), given the
        /// layers' extra direct sources to add and their year-2 maps and shops to flag.</param>
        /// <param name="onGameThread">False when constructed on a worker at the title screen: the
        /// constructor only reads files and composes them, and then draws no progress card.</param>
        public LexiconService(
            IMonitor monitor, string modFolder, IModRegistry registry,
            Func<ObtainabilityInputs, IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>>, YearTwoMarks, AnswerVariant> build,
            bool onGameThread = true)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            Action<string> warn = m => _monitor.Log(m, LogLevel.Warn);
            Action<string> trace = m => _monitor.Log(m, LogLevel.Trace);
            _cache = new LexiconDiskCache(System.IO.Path.Combine(modFolder, CacheFolder), MaxCacheFiles, warn, trace);
            // Before anything reads game data: the readers skip what is not vanilla's or an installed layer's.
            KnownData.Known = ReadKnownIds(modFolder, registry, warn);
            Vanilla = LexiconDiskCache.LoadShipped(() => ShippedLexicon.LoadVanilla(modFolder), warn, trace);
            // The support files are megabytes each: with any to read, say so on screen first.
            // Off the game thread (the title-screen warm-up) nothing may draw, and nothing needs to.
            if (onGameThread && LayerLoader.HasInstalledLayers(modFolder, registry)) LoadProgress.Reading();
            Loaded = LayerLoader.Load(modFolder, Vanilla, registry, warn);
            LayerSet layers = Loaded.Set;
            YearTwoCartPool.LayerRuling = layers.Ids.Count > 0 ? layers.YearRuling : null;
            if (layers.Ids.Count > 0)
                _monitor.Log(
                    $"Lexicon layers: {string.Join(", ", layers.Layers)}" +
                    (layers.UnmappedMods.Count > 0 ? $"; mods not mapped yet ({string.Join(", ", layers.UnmappedMods)})" : "") +
                    (layers.UnmappedPairs.Count > 0 ? $"; pairs not mapped yet ({string.Join(", ", layers.UnmappedPairs)})" : "") +
                    (layers.UnmappedMods.Count + layers.UnmappedPairs.Count > 0 ? ", so these answers are computed" : "") + ".",
                    LogLevel.Info);
            _resolver = new LexiconResolver(layers.Composed, _cache.Read, (key, _) => _pendingKey = key,
                live => build(layers.WithFishSpawns(live), layers.ExtraDirectSources, SaveMarks(layers.Marks)));
        }

        /// <summary>The known ids for this game, or null (nothing is filtered) when the vanilla ids
        /// file is missing or unreadable.</summary>
        private static KnownIds ReadKnownIds(string modFolder, IModRegistry registry, Action<string> warn)
        {
            try
            {
                IReadOnlyList<string> vanillaIds = ShippedLexicon.LoadVanillaIds(modFolder);
                if (vanillaIds == null) warn("Lexicon: no vanilla ids file; content from mods with no layer is not filtered out.");
                return LayerFile.ReadKnownIds(vanillaIds, LayerFile.Folder(modFolder), registry.IsLoaded, warn);
            }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                warn($"Lexicon: the vanilla ids file could not be read ({e.GetType().Name}: {e.Message}); content from mods with no layer is not filtered out.");
                return null;
            }
        }

        /// <summary>Whether the loaded save owns an upgrade (false with no save).</summary>
        internal static bool OwnsUpgrade(string id) => UpgradeChecker.HasUpgrade?.Invoke(id) == true;

        /// <summary>The layers' marks for the loaded save: the back doors its upgrades open are open
        /// (<see cref="TheLongestYear.Core.Obtainability.YearTwoMarks.For"/>).</summary>
        internal static TheLongestYear.Core.Obtainability.YearTwoMarks SaveMarks(TheLongestYear.Core.Obtainability.YearTwoMarks marks)
            => marks?.For(OwnsUpgrade);

        /// <summary>The shipped vanilla answers, or null when the file is absent or unreadable.</summary>
        public AnswerSet Vanilla { get; }

        /// <summary>The active supported-mod layers and the set composed from them.</summary>
        public LoadedLayers Loaded { get; }

        /// <summary>The answers for the live data, logged as one INFO line. A computed result is
        /// written by <see cref="StoreAvailability"/> or <see cref="FlushPending"/>.</summary>
        public Resolved Resolve(ObtainabilityInputs live, bool pierreOwned, string gameVersion, string modVersion)
        {
            Forget();
            _computedKeys.Clear();
            IReadOnlyList<string> doors = Loaded.Set.Marks.DoorUpgrades(OwnsUpgrade);
            _variantName = AnswerSet.VariantName(pierreOwned, doors, AnswerSet.DataTags(live));
            Resolved r = _resolver.Resolve(live, pierreOwned, gameVersion, modVersion, Loaded.Set.Layers, Loaded.Set.KeyLayers, doors);
            _current = r;
            _variant = r.Variant;
            string line = $"Lexicon: {r.Origin} ({r.Key[..KeyPrefixLength]}), {r.Variant.RoutedCount()} items, {r.Ms} ms";
            if (r.Origin == AnswerOrigin.Computed)
                line += ComputedReason(r, Loaded.Set.Composed, _variantName, gameVersion, modVersion);
            _monitor.Log(line + ".", LogLevel.Info);
            return r;
        }

        /// <summary>Why a computed answer was needed, as the tail of the INFO line.</summary>
        internal static string ComputedReason(Resolved r, AnswerSet shipped, string variantName, string gameVersion, string modVersion)
        {
            if (shipped != null && shipped.ModVersion != modVersion)
                return $" — shipped answers are for TLY {shipped.ModVersion}, running {modVersion}";
            if (r.DirtyOwners.Count > 0)
                return $" — {r.DirtyCount} data rows differ from the shipped answers ({string.Join(", ", r.DirtyOwners)}): "
                    + string.Join("; ", r.DirtyRows) + (r.DirtyCount > r.DirtyRows.Count ? "; ..." : "");
            if (shipped == null) return " — no shipped answers";
            if (shipped.GameVersion != gameVersion)
                return $" — 0 data rows differ; shipped game version {shipped.GameVersion} ≠ live {gameVersion}";
            if (!shipped.Variants.ContainsKey(variantName))
                return $" — variant '{variantName}' not in the shipped answers";
            return " — 0 data rows differ from the shipped answers";
        }

        /// <summary>The last resolve, or null before one (and after <see cref="Forget"/>).</summary>
        public Resolved Current => _current;

        /// <summary>The current answers with the availability answers added since the resolve; null with none.</summary>
        public AnswerVariant Variant => _variant;

        /// <summary>The year-2 answer for one item, or null when there is none. Served only here, never
        /// through the year-1 availability model; a computed load falls back to the shipped rulings
        /// (<see cref="YearTwoAnswers.TableFor"/>).</summary>
        public ItemAvailability YearTwoFor(string qualifiedId)
            => YearTwoTable.TryGetValue(qualifiedId, out ItemAvailability a) ? a : null;

        /// <summary>How many year-2 answers <see cref="YearTwoFor"/> serves.</summary>
        public int YearTwoCount => YearTwoTable.Count;

        /// <summary>Empty with no resolved answers (before a resolve, after <see cref="Forget"/>).</summary>
        private IReadOnlyDictionary<string, ItemAvailability> YearTwoTable
            => YearTwoAnswers.TableFor(_variant, _variant == null ? null : Loaded.Set.Composed, _variantName);

        /// <summary>The availability keys computed (not served from the answers) since the last resolve.</summary>
        public IReadOnlyCollection<string> ComputedAvailabilityKeys => _computedKeys;

        /// <summary>One line naming where the answers came from, for the top of a dump:
        /// <c>origin: shipped|cache|computed; layers: vanilla[, id...]</c>.</summary>
        public string OriginLine
            => $"origin: {(_current == null ? "none" : _current.Origin.ToString().ToLowerInvariant())}; layers: "
               + string.Join(", ", new[] { RowHashes.Vanilla }.Concat(Loaded.Set.Ids));

        /// <summary>The disk cache's entries, newest first.</summary>
        public IReadOnlyList<CacheFileInfo> CacheFiles() => _cache.Files();

        /// <summary>Drops the last resolved answers (a load whose inputs could not be read has none),
        /// writing a still-pending computed entry first.</summary>
        public void Forget()
        {
            FlushPending();
            _current = null;
            _variant = null;
        }

        /// <summary>Writes the computed entry the last resolve produced, if it is still pending.</summary>
        public void FlushPending()
        {
            if (_pendingKey == null || _variant == null) { _pendingKey = null; return; }
            WriteCurrent();
        }

        /// <summary>The stored availability answers for <paramref name="key"/> when they were computed
        /// from inputs hashing to <paramref name="inputsHash"/>; else null.</summary>
        public AvailabilityAnswers AvailabilityFor(string key, string inputsHash)
            => _variant != null && _variant.Availability.TryGetValue(key, out AvailabilityAnswers answers) && answers.Inputs == inputsHash
                ? answers
                : null;

        /// <summary>Whether the current answers were built or cached from data that differs from what
        /// shipped, so the weeks built from them are held to the shipped ones (<see cref="AvailabilityFloor"/>).</summary>
        public bool HoldsToShipped => _current != null && _current.Origin != AnswerOrigin.Shipped;

        /// <summary>The shipped availability answers for <paramref name="key"/> on this save's variant:
        /// vanilla's, and each installed layer's.</summary>
        public IReadOnlyList<AvailabilityAnswers> VettedAvailability(string key)
        {
            var vetted = new List<AvailabilityAnswers>();
            if (Vanilla?.Resolve(Vanilla.StoredNameFor(_variantName)) is AnswerVariant vanilla
                && vanilla.Availability.TryGetValue(key, out AvailabilityAnswers own))
                vetted.Add(own);
            foreach (LayerAnswers layer in Loaded.InstalledAnswers)
            {
                string stored = layer.Deltas.ContainsKey(_variantName) ? _variantName : AnswerSet.StemOf(_variantName);
                if (layer.Deltas.TryGetValue(stored, out AnswerVariant delta) && delta.Availability.TryGetValue(key, out AvailabilityAnswers theirs))
                    vetted.Add(theirs);
            }
            return vetted;
        }

        /// <summary>The inputs hash the current variant's answers for <paramref name="key"/> were made
        /// from, or null when it has none; for the load log.</summary>
        public string StoredInputs(string key)
            => _variant != null && _variant.Availability.TryGetValue(key, out AvailabilityAnswers answers) ? answers.Inputs : null;

        /// <summary>Adds freshly computed availability answers to the current variant. A cache-backed
        /// variant is (re)written to its cache entry; the shipped file is read-only, so for shipped
        /// answers they are kept in memory for this session only.</summary>
        public void StoreAvailability(string key, AvailabilityAnswers answers)
        {
            if (_variant == null) return;
            _computedKeys.Add(key);
            _variant = _variant with
            {
                Availability = new Dictionary<string, AvailabilityAnswers>(_variant.Availability, StringComparer.Ordinal) { [key] = answers },
            };
            if (_current.Origin == AnswerOrigin.Shipped)
            {
                _monitor.Log($"Lexicon: availability {key} is not in the shipped answers for these inputs; kept in memory for this session.", LogLevel.Trace);
                return;
            }
            // On a Cache origin (or a reset that rebuilt for a new step) this rewrites the whole entry
            // on the main thread: a few MB, once per availability miss.
            WriteCurrent();
        }

        private void WriteCurrent()
        {
            if (_current?.Origin == AnswerOrigin.Computed) LoadProgress.Saving();
            _pendingKey = null;
            AnswerSet set = _resolver.CurrentSet with
            {
                Variants = new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = _variant },
            };
            _cache.Write(_current.Key, set);
        }
    }
}
