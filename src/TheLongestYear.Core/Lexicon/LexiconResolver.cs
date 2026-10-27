using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Where a resolved answer came from.</summary>
public enum AnswerOrigin { Shipped, Cache, Computed }

/// <summary>A resolved answer: the variant, where it came from, the cache key of the live data, how
/// long resolving took, and for <see cref="AnswerOrigin.Computed"/> which mods' rows differed from
/// the shipped answers and how many data rows differ from them (added, removed or changed; 0 with no
/// shipped set).</summary>
public sealed record Resolved(
    AnswerVariant Variant, AnswerOrigin Origin, string Key, long Ms, IReadOnlyList<string> DirtyOwners, int DirtyCount);

/// <summary>Finds the answers for the live data, in order:
/// <list type="number">
/// <item>the shipped set, when its game version is the live one, it holds the save's variant
/// (<c>pierre_year2_seeds</c> when owned, else <c>base</c>) and that variant's row hashes equal the
/// live rows exactly (same keys, same hashes);</item>
/// <item>the cache entry for <see cref="RowHashes.Key"/> of the live rows, layers, versions and
/// <see cref="CodeStamp"/> (an unreadable entry is a miss);</item>
/// <item><c>build(live)</c>, written to the cache under that key.</item>
/// </list>
/// A cache entry is for one exact data set, so it holds one variant, always named
/// <see cref="AnswerSet.BaseVariant"/> whatever the Pierre state was (that state is a row, so it is
/// already in the key).</summary>
public sealed class LexiconResolver
{
    private readonly AnswerSet? _shipped;
    private readonly Func<string, AnswerSet?> _readCache;
    private readonly Action<string, AnswerSet> _writeCache;
    private readonly Func<ObtainabilityInputs, AnswerVariant> _build;
    private readonly string _codeStamp;

    /// <summary>The Core assembly's module version id: it changes with every change to the code that
    /// computes the answers, so a cache entry written by other code is not reused even when the game
    /// and mod versions are unchanged (a branch that does not bump its version). Only the cache key
    /// names it; the shipped answers are checked by their rows and game version alone.</summary>
    public static string CodeStamp { get; } = typeof(LexiconResolver).Assembly.ManifestModule.ModuleVersionId.ToString("N");

    /// <param name="codeStamp">What the cache key names for the code; null uses <see cref="CodeStamp"/>.</param>
    public LexiconResolver(
        AnswerSet? shipped, Func<string, AnswerSet?> readCache, Action<string, AnswerSet> writeCache,
        Func<ObtainabilityInputs, AnswerVariant> build, string? codeStamp = null)
    {
        _shipped = shipped;
        _codeStamp = codeStamp ?? CodeStamp;
        _readCache = readCache ?? throw new ArgumentNullException(nameof(readCache));
        _writeCache = writeCache ?? throw new ArgumentNullException(nameof(writeCache));
        _build = build ?? throw new ArgumentNullException(nameof(build));
    }

    /// <summary>The set the last <see cref="Resolve"/> answered from: the shipped set, the cache entry
    /// read, or the one just written. Null before the first call.</summary>
    public AnswerSet? CurrentSet { get; private set; }

    /// <param name="layers">The layer ids the answers are composed from, stored in a computed set.</param>
    /// <param name="keyLayers">What the cache key names for the layers (<see cref="LayerSet.KeyLayers"/>:
    /// the ids with their files' hashes); null uses <paramref name="layers"/>.</param>
    public Resolved Resolve(
        ObtainabilityInputs live, bool pierreOwned, string gameVersion, string modVersion, IReadOnlyList<string> layers,
        IReadOnlyList<string>? keyLayers = null)
    {
        if (live == null) throw new ArgumentNullException(nameof(live));
        var timer = Stopwatch.StartNew();
        IReadOnlyDictionary<string, ulong> rows = RowHashes.Of(live, pierreOwned);
        string key = RowHashes.Key(rows, keyLayers ?? layers, gameVersion, modVersion, _codeStamp);
        string variantName = pierreOwned ? AnswerSet.PierreVariant : AnswerSet.BaseVariant;

        if (_shipped != null && _shipped.GameVersion == gameVersion && _shipped.Variants.ContainsKey(variantName)
            && SameRows(_shipped.RowHashesFor(variantName), rows)
            && _shipped.Resolve(variantName) is AnswerVariant shippedVariant)
        {
            CurrentSet = _shipped;
            return new Resolved(shippedVariant, AnswerOrigin.Shipped, key, timer.ElapsedMilliseconds, Array.Empty<string>(), 0);
        }

        AnswerSet? cached = null;
        try
        {
            cached = _readCache(key);
        }
        catch (Exception e) when (LexiconDiskCache.IsReadFailure(e))
        {
            cached = null;   // an unreadable entry is a miss; the reader logs it
        }
        if (cached?.Resolve(AnswerSet.BaseVariant) is AnswerVariant cachedVariant)
        {
            CurrentSet = cached;
            return new Resolved(cachedVariant, AnswerOrigin.Cache, key, timer.ElapsedMilliseconds, Array.Empty<string>(), 0);
        }

        AnswerVariant built = _build(live);
        var set = new AnswerSet(gameVersion, modVersion, layers.ToList(), rows,
            new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = built });
        _writeCache(key, set);
        CurrentSet = set;
        IReadOnlyList<string> differing = DifferingRows(_shipped?.RowHashesFor(variantName), rows);
        return new Resolved(built, AnswerOrigin.Computed, key, timer.ElapsedMilliseconds,
            differing.Select(RowHashes.Owner).Distinct(StringComparer.Ordinal).OrderBy(o => o, StringComparer.Ordinal).ToList(),
            differing.Count);
    }

    private static bool SameRows(IReadOnlyDictionary<string, ulong> a, IReadOnlyDictionary<string, ulong> b)
        => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out ulong h) && h == p.Value);

    /// <summary>Every row key added, removed or changed between the shipped and the live data; empty
    /// with no shipped set.</summary>
    private static IReadOnlyList<string> DifferingRows(IReadOnlyDictionary<string, ulong>? shippedRows, IReadOnlyDictionary<string, ulong> live)
    {
        if (shippedRows == null) return Array.Empty<string>();
        var (added, removed, changed) = RowHashes.Diff(shippedRows, live);
        return added.Concat(removed).Concat(changed).ToList();
    }
}
