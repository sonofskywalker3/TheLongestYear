using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>One composed answer: the obtainability model's routes for every item, the year-sort
/// model's, and the map and shop weeks they were built against.</summary>
public sealed record AnswerVariant(
    IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> Obtain,     // every id in inputs.Objects has a key (empty list = no route)
    IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> YearSort,
    IReadOnlyList<string> Unresolved, int Passes,
    IReadOnlyDictionary<string, PlaceWeek> MapWeeks, IReadOnlyList<string> UnreachedMaps,
    IReadOnlyDictionary<string, PlaceWeek> ShopWeeks)
{
    /// <summary>The distinct day-table run strings, ordinally sorted; the file refers to them by index.
    /// Filled by <see cref="LexiconJson.Read"/> (and written by <see cref="LexiconJson.Write"/>, which
    /// builds its own pool); empty on a freshly built variant.</summary>
    public IReadOnlyList<string> Tables { get; init; } = Array.Empty<string>();
}

/// <summary>The shipped or cached answer file: which game, mod and layers it was made for, the
/// row hashes it was checked against, and its variants ("base", "pierre_year2_seeds").</summary>
public sealed record AnswerSet(
    string GameVersion, string ModVersion, IReadOnlyList<string> Layers,          // layer ids composed in, "vanilla" first
    IReadOnlyDictionary<string, ulong> RowHashes,
    IReadOnlyDictionary<string, AnswerVariant> Variants);

public static class AnswerVariantFactory
{
    /// <summary>Snapshots a built model. <c>Obtain</c> and <c>YearSort</c> carry a key for every object
    /// id in <paramref name="inputs"/> (an empty list when nothing yields it) plus every other id the
    /// model holds (hats, weapons, big craftables). Null <paramref name="maps"/> or
    /// <paramref name="shops"/> give empty weeks.</summary>
    public static AnswerVariant From(
        ObtainabilityBuild model, ObtainabilityBuild yearSort, ObtainabilityInputs inputs,
        LocationWeeks? maps, IReadOnlyCollection<string> allMaps, ShopWeeks? shops)
    {
        IReadOnlyDictionary<string, PlaceWeek> reached = maps?.Reached ?? new Dictionary<string, PlaceWeek>();
        return new AnswerVariant(
            Snapshot(model.Model, inputs), Snapshot(yearSort.Model, inputs),
            model.Unresolved.ToList(), model.Passes,
            new Dictionary<string, PlaceWeek>(reached),
            allMaps.Where(m => !reached.ContainsKey(m)).OrderBy(m => m, StringComparer.Ordinal).ToList(),
            new Dictionary<string, PlaceWeek>(shops?.Weeks ?? new Dictionary<string, PlaceWeek>()));
    }

    private static Dictionary<string, IReadOnlyList<ObtainSource>> Snapshot(ObtainabilityModel model, ObtainabilityInputs inputs)
    {
        var all = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (string id in inputs.Objects.Keys) all[id] = model.Sources(id);
        foreach (string id in model.ItemIds) all[id] = model.Sources(id);
        return all;
    }
}
