using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewModdingAPI;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Loop
{
    /// <summary>The game side of the path lexicon: loads the shipped answers once, keeps the disk cache
    /// in <c>data/lexicon/&lt;key&gt;.json</c> under the mod folder (our own <see cref="LexiconJson"/>
    /// text, not SMAPI's JSON helper, which would re-serialise it in another format), and resolves the
    /// live data through <see cref="LexiconResolver"/>. The cache keeps the
    /// <see cref="MaxCacheFiles"/> most recently written or used entries.</summary>
    internal sealed class LexiconService
    {
        public const string CacheFolder = "data/lexicon";
        public const int MaxCacheFiles = 3;
        private const int KeyPrefixLength = 8;
        private const string CacheExtension = ".json";
        private static readonly string[] Layers = { RowHashes.Vanilla };

        private readonly IMonitor _monitor;
        private readonly string _cacheDirectory;
        private readonly LexiconResolver _resolver;

        /// <param name="build">The answers for live data nothing has (the cold build, for now).</param>
        public LexiconService(IMonitor monitor, string modFolder, Func<ObtainabilityInputs, AnswerSet, AnswerVariant> build)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _cacheDirectory = Path.Combine(modFolder, CacheFolder);
            _resolver = new LexiconResolver(LoadShipped(modFolder), ReadCache, WriteCache, build);
        }

        /// <summary>The answer set the last <see cref="Resolve"/> answered from, and its variant name.</summary>
        public AnswerSet CurrentSet => _resolver.CurrentSet;
        public string CurrentVariantName => _resolver.CurrentVariantName;

        /// <summary>The answers for the live data, logged as one INFO line.</summary>
        public Resolved Resolve(ObtainabilityInputs live, bool pierreOwned, string gameVersion, string modVersion)
        {
            Resolved r = _resolver.Resolve(live, pierreOwned, gameVersion, modVersion, Layers);
            string line = $"Lexicon: {r.Origin} ({r.Key[..KeyPrefixLength]}), {r.Variant.Obtain.Count} items, {r.Ms} ms";
            if (r.Origin == AnswerOrigin.Computed)
                line += $" — {r.DirtyCount} items recomputed for "
                    + (r.DirtyOwners.Count > 0 ? string.Join(", ", r.DirtyOwners) : "no shipped answers");
            _monitor.Log(line + ".", LogLevel.Info);
            return r;
        }

        /// <summary>Writes <paramref name="set"/> as the cache entry for <paramref name="key"/> and prunes
        /// the folder; a failed write is logged and otherwise ignored (the answers are still served).</summary>
        public void WriteCache(string key, AnswerSet set)
        {
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                File.WriteAllText(PathFor(key), LexiconJson.Write(set));
                Prune();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _monitor.Log($"Lexicon: could not write the cache entry {key[..KeyPrefixLength]} ({e.GetType().Name}: {e.Message}).", LogLevel.Warn);
            }
        }

        private AnswerSet LoadShipped(string modFolder)
        {
            try
            {
                AnswerSet shipped = ShippedLexicon.LoadVanilla(modFolder);
                if (shipped == null)
                    _monitor.Log($"Lexicon: no shipped answers at {ShippedLexicon.VanillaRelativePath}; every load builds or reads the cache.", LogLevel.Warn);
                return shipped;
            }
            catch (Exception e) when (e is JsonException or IOException or FormatException)
            {
                _monitor.Log($"Lexicon: the shipped answers could not be read ({e.GetType().Name}: {e.Message}); ignoring them.", LogLevel.Warn);
                return null;
            }
        }

        /// <summary>The cache entry for <paramref name="key"/>, or null when there is none or it cannot be
        /// read (one WARN, then treated as a miss). A hit is touched so pruning keeps it.</summary>
        private AnswerSet ReadCache(string key)
        {
            string path = PathFor(key);
            if (!File.Exists(path)) return null;
            try
            {
                AnswerSet set = LexiconJson.Read(File.ReadAllText(path));
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                return set;
            }
            catch (Exception e) when (e is JsonException or IOException or FormatException)
            {
                _monitor.Log($"Lexicon: the cache entry {key[..KeyPrefixLength]} could not be read ({e.GetType().Name}: {e.Message}); rebuilding.", LogLevel.Warn);
                return null;
            }
        }

        private void Prune()
        {
            IEnumerable<FileInfo> stale = new DirectoryInfo(_cacheDirectory).GetFiles("*" + CacheExtension)
                .OrderByDescending(f => f.LastWriteTimeUtc).Skip(MaxCacheFiles);
            foreach (FileInfo file in stale) file.Delete();
        }

        private string PathFor(string key) => Path.Combine(_cacheDirectory, key + CacheExtension);
    }
}
