using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Compares two answer variants field by field, for <c>tly_lexicon diff</c>: the shipped or
/// cached answers against a cold build of the same data should differ nowhere. One line per
/// difference, ordinally sorted. Compared: every id's source list in <see cref="AnswerVariant.Obtain"/>
/// and <see cref="AnswerVariant.YearSort"/> (presence, count, then the first source that differs by
/// <see cref="ObtainSource.Equals(ObtainSource?)"/> or by <see cref="ObtainSource.Inputs"/>, which Equals
/// leaves out but the fairness rule reads), <see cref="AnswerVariant.Unresolved"/> as a set, the map and
/// shop weeks, the unreached maps as a set, and per availability key the derived items, effort items, dish
/// bases and year-two crop dish units. The availability <see cref="AvailabilityAnswers.Inputs"/> stamp is a
/// hash of the inputs, not an answer, so it is not compared. <see cref="AnswerVariant.Passes"/> and
/// <see cref="AnswerVariant.Tables"/> are bookkeeping and not compared.</summary>
public static class LexiconDiff
{
    private const string BaseLabel = "base";
    private const string LiveLabel = "live";

    public static IReadOnlyList<string> Compare(AnswerVariant a, AnswerVariant b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));
        var lines = new List<string>();
        Sources("obtain", a.Obtain, b.Obtain, lines);
        Sources("yearsort", a.YearSort, b.YearSort, lines);
        Set("unresolved", a.Unresolved, b.Unresolved, lines);
        Set("unreached map", a.UnreachedMaps, b.UnreachedMaps, lines);
        Keyed("map week", a.MapWeeks, b.MapWeeks, (x, y) => Equals(x, y), lines);
        Keyed("shop week", a.ShopWeeks, b.ShopWeeks, (x, y) => Equals(x, y), lines);
        Keyed("catch rate", a.CatchRates, b.CatchRates, (x, y) => x.BestDay == y.BestDay && x.Weekly.SequenceEqual(y.Weekly), lines);
        AvailabilitySets(a.Availability, b.Availability, lines);
        Keyed("year 2", a.YearTwo, b.YearTwo, (x, y) => x == y, lines);
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    private static void Sources(
        string field, IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> a,
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> b, List<string> lines)
    {
        foreach (string id in a.Keys.Union(b.Keys, StringComparer.Ordinal))
        {
            bool inA = a.TryGetValue(id, out var sa), inB = b.TryGetValue(id, out var sb);
            if (!inA || !inB)
            {
                lines.Add($"{field} {id}: only in {(inA ? BaseLabel : LiveLabel)} ({(inA ? sa : sb)!.Count} sources)");
                continue;
            }
            string? first = FirstDifference(sa!, sb!);
            if (first != null)
                lines.Add($"{field} {id}: {BaseLabel} {Plural(sa!.Count)}, {LiveLabel} {sb!.Count}; first difference: {first}");
        }
    }

    private static string Plural(int n) => $"{n} source{(n == 1 ? "" : "s")}";

    private static string? FirstDifference(IReadOnlyList<ObtainSource> a, IReadOnlyList<ObtainSource> b)
    {
        int common = Math.Min(a.Count, b.Count);
        for (int i = 0; i < common; i++)
        {
            if (!a[i].Equals(b[i]))
                return $"#{i} {BaseLabel} [{ObtainabilityText.SourceLine(a[i])}] vs {LiveLabel} [{ObtainabilityText.SourceLine(b[i])}]";
            if (!SameInputs(a[i].Inputs, b[i].Inputs))
                return $"#{i} inputs {BaseLabel} {InputsText(a[i].Inputs)} vs {LiveLabel} {InputsText(b[i].Inputs)} [{ObtainabilityText.SourceLine(a[i])}]";
        }
        if (a.Count > common) return $"#{common} only in {BaseLabel} [{ObtainabilityText.SourceLine(a[common])}]";
        if (b.Count > common) return $"#{common} only in {LiveLabel} [{ObtainabilityText.SourceLine(b[common])}]";
        return null;
    }

    private static bool SameInputs(IReadOnlyList<IReadOnlyList<string>> a, IReadOnlyList<IReadOnlyList<string>> b)
        => a.Count == b.Count && a.Zip(b, (x, y) => x.SequenceEqual(y, StringComparer.Ordinal)).All(same => same);

    private static string InputsText(IReadOnlyList<IReadOnlyList<string>> inputs)
        => "{" + string.Join("; ", inputs.Select(g => string.Join("|", g))) + "}";

    private static void Set(string field, IReadOnlyList<string> a, IReadOnlyList<string> b, List<string> lines)
    {
        var sa = new HashSet<string>(a, StringComparer.Ordinal);
        var sb = new HashSet<string>(b, StringComparer.Ordinal);
        foreach (string s in sa.Where(s => !sb.Contains(s))) lines.Add($"{field}: only in {BaseLabel}: {s}");
        foreach (string s in sb.Where(s => !sa.Contains(s))) lines.Add($"{field}: only in {LiveLabel}: {s}");
    }

    private static void Keyed<T>(
        string field, IReadOnlyDictionary<string, T> a, IReadOnlyDictionary<string, T> b, Func<T, T, bool> same, List<string> lines)
    {
        foreach (string id in a.Keys.Union(b.Keys, StringComparer.Ordinal))
        {
            bool inA = a.TryGetValue(id, out T? x), inB = b.TryGetValue(id, out T? y);
            if (!inA || !inB) lines.Add($"{field} {id}: only in {(inA ? BaseLabel : LiveLabel)}");
            else if (!same(x!, y!)) lines.Add($"{field} {id}: {BaseLabel} {Show(x)}, {LiveLabel} {Show(y)}");
        }
    }

    private static string Show<T>(T value)
        => value is double[] d ? "[" + string.Join(",", d.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]" : value?.ToString() ?? "null";

    private static void AvailabilitySets(
        IReadOnlyDictionary<string, AvailabilityAnswers> a, IReadOnlyDictionary<string, AvailabilityAnswers> b, List<string> lines)
    {
        foreach (string key in a.Keys.Union(b.Keys, StringComparer.Ordinal))
        {
            bool inA = a.TryGetValue(key, out var x), inB = b.TryGetValue(key, out var y);
            if (!inA || !inB)
            {
                lines.Add($"availability {key}: only in {(inA ? BaseLabel : LiveLabel)}");
                continue;
            }
            Keyed($"availability {key} derived", x!.Derived, y!.Derived, (p, q) => p.Equals(q), lines);
            Keyed($"availability {key} effort", x.EffortDerived, y.EffortDerived, (p, q) => p.Equals(q), lines);
            Keyed($"availability {key} dishbases", x.DishBases, y.DishBases, (p, q) => p.SequenceEqual(q), lines);
            Keyed($"availability {key} yeartwo dish units", x.YearTwoCropDishUnits, y.YearTwoCropDishUnits, (p, q) => p == q, lines);
        }
    }
}
