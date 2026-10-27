using System;
using System.Collections.Generic;
using StardewModdingAPI;
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
    /// (<see cref="FlushPending"/>).</para></summary>
    internal sealed class LexiconService
    {
        public const string CacheFolder = "data/lexicon";
        public const int MaxCacheFiles = 3;
        private const int KeyPrefixLength = 8;
        private static readonly string[] Layers = { RowHashes.Vanilla };

        private readonly IMonitor _monitor;
        private readonly LexiconDiskCache _cache;
        private readonly LexiconResolver _resolver;
        /// <summary>The last resolve, and its variant with the availability answers added since.
        /// Null before the first resolve and after <see cref="Forget"/>.</summary>
        private Resolved _current;
        private AnswerVariant _variant;
        /// <summary>The computed entry the resolver asked to write and that is not on disk yet.</summary>
        private string _pendingKey;

        /// <param name="build">The answers for live data nothing has (the cold build, for now).</param>
        public LexiconService(IMonitor monitor, string modFolder, Func<ObtainabilityInputs, AnswerSet, AnswerVariant> build)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            Action<string> warn = m => _monitor.Log(m, LogLevel.Warn);
            Action<string> trace = m => _monitor.Log(m, LogLevel.Trace);
            _cache = new LexiconDiskCache(System.IO.Path.Combine(modFolder, CacheFolder), MaxCacheFiles, warn, trace);
            AnswerSet shipped = LexiconDiskCache.LoadShipped(() => ShippedLexicon.LoadVanilla(modFolder), warn, trace);
            _resolver = new LexiconResolver(shipped, _cache.Read, (key, _) => _pendingKey = key, build);
        }

        /// <summary>The answers for the live data, logged as one INFO line. A computed result is
        /// written by <see cref="StoreAvailability"/> or <see cref="FlushPending"/>.</summary>
        public Resolved Resolve(ObtainabilityInputs live, bool pierreOwned, string gameVersion, string modVersion)
        {
            Forget();
            Resolved r = _resolver.Resolve(live, pierreOwned, gameVersion, modVersion, Layers);
            _current = r;
            _variant = r.Variant;
            string line = $"Lexicon: {r.Origin} ({r.Key[..KeyPrefixLength]}), {r.Variant.RoutedCount()} items, {r.Ms} ms";
            if (r.Origin == AnswerOrigin.Computed)
                line += r.DirtyOwners.Count > 0
                    ? $" — {r.DirtyCount} data rows differ from the shipped answers ({string.Join(", ", r.DirtyOwners)})"
                    : " — no shipped answers";
            _monitor.Log(line + ".", LogLevel.Info);
            return r;
        }

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
            _pendingKey = null;
            AnswerSet set = _resolver.CurrentSet with
            {
                Variants = new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = _variant },
            };
            _cache.Write(_current.Key, set);
        }
    }
}
