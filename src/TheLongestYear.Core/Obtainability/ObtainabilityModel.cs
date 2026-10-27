using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Which sources a question counts. Null collections mean "all".</summary>
public sealed record ObtainFilter
{
    public static readonly ObtainFilter Any = new();
    public static readonly ObtainFilter DependableOnly = new() { Reliabilities = new[] { Reliability.Dependable } };

    public IReadOnlyCollection<Reliability>? Reliabilities { get; init; }
    public IReadOnlyCollection<SourceKind>? Kinds { get; init; }
    public bool IncludeYearTwo { get; init; }
    public bool IncludeGingerIsland { get; init; }
    public bool IncludeUnresolved { get; init; } = true;
    public bool IncludeOwnedOnly { get; init; }

    public bool Accepts(ObtainSource source)
        => (Reliabilities == null || Reliabilities.Contains(source.Reliability))
           && (Kinds == null || Kinds.Contains(source.Kind))
           && (IncludeYearTwo || !source.Conditions.YearTwo)
           && (IncludeGingerIsland || !source.Conditions.GingerIsland)
           && (IncludeUnresolved || !source.Conditions.Unresolved)
           && (IncludeOwnedOnly || !source.Conditions.OwnedOnly);

    /// <summary>The filter by content, for memo keys: the record's own equality compares the two
    /// collections by reference, so two filters naming the same reliabilities would key apart.</summary>
    internal FilterKey Key => new(
        Reliabilities == null ? AllMembers : Bits(Reliabilities.Select(r => (int)r)),
        Kinds == null ? AllMembers : Bits(Kinds.Select(k => (int)k)),
        IncludeYearTwo, IncludeGingerIsland, IncludeUnresolved, IncludeOwnedOnly);

    private const long AllMembers = -1;

    private static long Bits(IEnumerable<int> members)
    {
        long bits = 0;
        foreach (int m in members) bits |= 1L << m;
        return bits;
    }

    internal readonly record struct FilterKey(
        long Reliabilities, long Kinds, bool YearTwo, bool GingerIsland, bool Unresolved, bool OwnedOnly);
}

/// <summary>Every year-1 way to obtain every item, by start day: for each day the player could start
/// from nothing, the first day the item lands (spec 2026-09-14-obtainability-phase2, decisions 1 and 2).
/// Built at runtime from the installed game data; nothing reads it for gameplay yet.</summary>
public sealed class ObtainabilityModel
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> _sources;

    /// <summary>Every table this model has folded, by item, filter and side: the model never changes
    /// after construction, so each fold is done once. Concurrent because a built model is read from
    /// more than one thread.</summary>
    private readonly ConcurrentDictionary<(string ItemId, ObtainFilter.FilterKey Filter, bool Undelayed), DayTable> _tables = new();

    /// <summary>Every <see cref="Derived.Of"/> read of this model, by the id as asked.</summary>
    private readonly ConcurrentDictionary<string, Derived.Input> _inputs = new(StringComparer.Ordinal);

    public ObtainabilityModel(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> sources)
    {
        if (sources is null) throw new ArgumentNullException(nameof(sources));
        _sources = sources
            .GroupBy(kv => BundleParsing.NormalizeItemId(kv.Key), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ObtainSource>)g.SelectMany(kv => kv.Value).Distinct().ToList(),
                StringComparer.Ordinal);
    }

    /// <summary>A model whose every id outside <paramref name="dirty"/> has exactly
    /// <paramref name="cleanFrom"/>'s sources (the incremental build's passes): its folds and input
    /// reads for those ids are <paramref name="cleanFrom"/>'s own, memoised once across passes.</summary>
    internal ObtainabilityModel(
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> sources, ObtainabilityModel cleanFrom, IReadOnlySet<string> dirty)
        : this(sources)
    {
        _cleanFrom = cleanFrom ?? throw new ArgumentNullException(nameof(cleanFrom));
        _dirty = dirty ?? throw new ArgumentNullException(nameof(dirty));
    }

    private readonly ObtainabilityModel? _cleanFrom;
    private readonly IReadOnlySet<string>? _dirty;

    private bool IsShared(string normalizedId) => _cleanFrom != null && !_dirty!.Contains(normalizedId);

    public IReadOnlyCollection<string> ItemIds => _sources.Keys.ToList();

    public int Count => _sources.Count;

    public IReadOnlyList<ObtainSource> Sources(string itemId)
        => _sources.TryGetValue(BundleParsing.NormalizeItemId(itemId), out IReadOnlyList<ObtainSource>? list)
            ? list
            : Array.Empty<ObtainSource>();

    /// <summary>The earliest landing per start day over every accepted source.</summary>
    public DayTable Table(string itemId, ObtainFilter filter) => Folded(itemId, filter, undelayed: false);

    /// <summary>The same Earliest over each accepted source's table with no mine travel
    /// (<see cref="ObtainSource.Undelayed"/>).</summary>
    public DayTable UndelayedTable(string itemId, ObtainFilter filter) => Folded(itemId, filter, undelayed: true);

    private DayTable Folded(string itemId, ObtainFilter filter, bool undelayed)
    {
        string id = BundleParsing.NormalizeItemId(itemId);
        if (IsShared(id)) return _cleanFrom!.Folded(id, filter, undelayed);
        var key = (id, filter.Key, undelayed);
        if (_tables.TryGetValue(key, out DayTable? cached)) return cached;
        var accepted = new List<DayTable>();
        foreach (ObtainSource source in Sources(id))
            if (filter.Accepts(source)) accepted.Add(undelayed ? source.Undelayed : source.Lands);
        return _tables.GetOrAdd(key, DayTable.EarliestOf(accepted));
    }

    /// <summary><see cref="Derived.Of"/>'s read of <paramref name="itemId"/>, made once per model.</summary>
    internal Derived.Input InputOf(string itemId, Func<ObtainabilityModel, string, Derived.Input> read)
    {
        if (_inputs.TryGetValue(itemId, out Derived.Input? cached)) return cached;
        if (IsShared(BundleParsing.NormalizeItemId(itemId))) return _cleanFrom!.InputOf(itemId, read);
        return _inputs.GetOrAdd(itemId, read(this, itemId));
    }

    public int? Lands(string itemId, int startDay, ObtainFilter filter) => Table(itemId, filter).Lands(startDay);

    public bool CanObtain(string itemId, int startDay, int deadlineDay, ObtainFilter filter)
        => Table(itemId, filter).CanObtain(startDay, deadlineDay);

    public int? LandingWeekFromDay1(string itemId, ObtainFilter filter) => Table(itemId, filter).LandingWeek(1);
}
