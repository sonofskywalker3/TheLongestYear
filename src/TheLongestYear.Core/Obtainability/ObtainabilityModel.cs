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

/// <summary>Every year-1 way to obtain every item, by week (spec 2026-09-14-item-obtainability).
/// Built at runtime from the installed game data; nothing reads it for gameplay in phase 1.</summary>
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

    public WeekMask Weeks(string itemId, ObtainFilter filter)
    {
        WeekMask mask = WeekMask.None;
        foreach (ObtainSource source in Sources(itemId))
            if (filter.Accepts(source)) mask |= source.Weeks;
        return mask;
    }

    public bool IsObtainable(string itemId, int week, ObtainFilter filter) => Weeks(itemId, filter).Contains(week);

    public int? EarliestWeek(string itemId, ObtainFilter filter) => Weeks(itemId, filter).Earliest;
}
