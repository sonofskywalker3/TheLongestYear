using System;
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

    public bool Accepts(ObtainSource source)
        => (Reliabilities == null || Reliabilities.Contains(source.Reliability))
           && (Kinds == null || Kinds.Contains(source.Kind))
           && (IncludeYearTwo || !source.Conditions.YearTwo)
           && (IncludeGingerIsland || !source.Conditions.GingerIsland)
           && (IncludeUnresolved || !source.Conditions.Unresolved);
}

/// <summary>Every year-1 way to obtain every item, by start day: for each day the player could start
/// from nothing, the first day the item lands (spec 2026-09-14-obtainability-phase2, decisions 1 and 2).
/// Built at runtime from the installed game data; nothing reads it for gameplay yet.</summary>
public sealed class ObtainabilityModel
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> _sources;

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

    public IReadOnlyCollection<string> ItemIds => _sources.Keys.ToList();

    public int Count => _sources.Count;

    public IReadOnlyList<ObtainSource> Sources(string itemId)
        => _sources.TryGetValue(BundleParsing.NormalizeItemId(itemId), out IReadOnlyList<ObtainSource>? list)
            ? list
            : Array.Empty<ObtainSource>();

    /// <summary>The earliest landing per start day over every accepted source.</summary>
    public DayTable Table(string itemId, ObtainFilter filter)
    {
        DayTable table = DayTable.None;
        foreach (ObtainSource source in Sources(itemId))
            if (filter.Accepts(source)) table = table.Earliest(source.Lands);
        return table;
    }

    public int? Lands(string itemId, int startDay, ObtainFilter filter) => Table(itemId, filter).Lands(startDay);

    public bool CanObtain(string itemId, int startDay, int deadlineDay, ObtainFilter filter)
        => Table(itemId, filter).CanObtain(startDay, deadlineDay);

    public int? LandingWeekFromDay1(string itemId, ObtainFilter filter) => Table(itemId, filter).LandingWeek(1);
}
