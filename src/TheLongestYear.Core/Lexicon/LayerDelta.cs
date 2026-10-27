using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Makes a layer's answers file (<c>tly_lexicon write &lt;UniqueID&gt;</c> or
/// <c>write &lt;A&gt;+&lt;B&gt;</c>) from the live answers of the save it runs in.</summary>
public static class LayerDelta
{
    /// <summary>The answers file for <paramref name="target"/> (one mod id, or several joined with
    /// <see cref="LayerFile.Joiner"/>), with <paramref name="variantName"/> set from <paramref name="live"/>
    /// and any other variant kept from <paramref name="existing"/>.
    /// <para>The baseline is the composed answers without this file: for a single mod, every other
    /// active layer; for an interaction, vanilla and its mods' single layers only. The delta holds the
    /// ids whose source list differs (compared as their <see cref="LexiconJson.Options"/> JSON, as
    /// <see cref="AnswerVariantDelta.Diff"/> does), the map and shop weeks, unresolved sources,
    /// unreached maps and availability answers that differ, and the live passes. The variant's row hashes
    /// are built fresh (nothing carried over from <paramref name="existing"/>, so a row the mod no longer
    /// has is gone): the live rows missing from or differing from the baseline's, plus (for a single mod)
    /// every row whose <see cref="RowHashes.Owner"/> (the id prefix before the first "." or "_") equals the
    /// mod's whole UniqueID, which only a UniqueID with no "." can match (a row of "Cornucopia.MoreCrops"
    /// never does); other rows enter only because they differ from the baseline. The other variants' rows are kept as they were. An empty delta with no other variant on file
    /// writes <c>"Deltas": {}</c>; an existing empty map is not carried over, since it cannot say which
    /// save state it was made in.</para></summary>
    public static LayerAnswers Write(
        string target, AnswerSet vanilla, IReadOnlyList<LayerInputs> activeInputs, IReadOnlyList<LayerAnswers> activeAnswers,
        string variantName, AnswerVariant live, IReadOnlyDictionary<string, ulong> liveRows,
        Func<string, string?> installedVersionOf, LayerAnswers? existing)
    {
        if (vanilla == null) throw new ArgumentNullException(nameof(vanilla));
        List<string> mods = target.Split(LayerFile.Joiner).Select(m => m.Trim()).Where(m => m.Length > 0)
            .Distinct(StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList();
        if (mods.Count == 0) throw new ArgumentException("No mod id given.", nameof(target));
        string name = LayerFile.Name(mods);
        bool single = mods.Count == 1;

        IReadOnlyList<LayerInputs> baseInputs = single
            ? activeInputs.Where(i => i.UniqueId != mods[0]).ToList()
            : activeInputs.Where(i => mods.Contains(i.UniqueId)).ToList();
        IReadOnlyList<LayerAnswers> baseAnswers = single
            ? activeAnswers.Where(a => !a.Mods.Contains(mods[0])).ToList()
            : activeAnswers.Where(a => a.Mods.Count == 1 && mods.Contains(a.Mods[0])).ToList();
        AnswerSet baseline = LayerSet.Compose(vanilla, baseInputs, baseAnswers, _ => { }).Overlay!;
        AnswerVariant below = baseline.Resolve(variantName)
            ?? throw new InvalidOperationException($"The answers below {name} have no '{variantName}' variant.");
        IReadOnlyDictionary<string, ulong> belowRows = baseline.RowHashesFor(variantName);

        var rows = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var (key, hash) in liveRows)
            if (!belowRows.TryGetValue(key, out ulong was) || was != hash || (single && RowHashes.Owner(key) == mods[0]))
                rows[key] = hash;

        AnswerVariant delta = Diff(below, live);
        var deltas = new Dictionary<string, AnswerVariant>(StringComparer.Ordinal);
        var variantRows = new Dictionary<string, IReadOnlyDictionary<string, ulong>>(StringComparer.Ordinal);
        if (existing != null)
            foreach (var (other, d) in existing.Deltas)
                if (other != variantName)
                {
                    deltas[other] = d;
                    if (existing.VariantRowHashes.TryGetValue(other, out var otherRows)) variantRows[other] = otherRows;
                }
        // A variant that changes rows or answers, or sits beside another mapped variant, is written out;
        // otherwise the file is the empty map that says "mapped, nothing changes".
        if (!IsEmpty(delta) || rows.Count > 0 || deltas.Count > 0) deltas[variantName] = delta;
        if (rows.Count > 0) variantRows[variantName] = rows;

        return new LayerAnswers(mods, string.Join(LayerFile.Joiner, mods.Select(m => $"{m}@{installedVersionOf(m) ?? "unknown"}")),
            variantRows, deltas);
    }

    /// <summary>What <see cref="LayerSet.LayDelta"/> must lay over <paramref name="below"/> to give
    /// <paramref name="live"/> (as near as its union rules for unresolved sources and unreached maps allow).</summary>
    public static AnswerVariant Diff(AnswerVariant below, AnswerVariant live)
    {
        AnswerVariant routes = AnswerVariantDelta.Diff(below, live);
        return new AnswerVariant(
            routes.Obtain, routes.YearSort,
            live.Unresolved.Except(below.Unresolved, StringComparer.Ordinal).OrderBy(u => u, StringComparer.Ordinal).ToList(),
            live.Passes,
            Changed(below.MapWeeks, live.MapWeeks, (a, b) => a == b),
            live.UnreachedMaps.Except(below.UnreachedMaps, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList(),
            Changed(below.ShopWeeks, live.ShopWeeks, (a, b) => a == b))
        {
            Availability = Changed(below.Availability, live.Availability, (a, b) => Text(a) == Text(b)),
        };
    }

    /// <summary>A delta that changes nothing a reader sees (the pass count aside).</summary>
    public static bool IsEmpty(AnswerVariant delta)
        => delta.Obtain.Count == 0 && delta.YearSort.Count == 0 && delta.Unresolved.Count == 0
           && delta.MapWeeks.Count == 0 && delta.UnreachedMaps.Count == 0 && delta.ShopWeeks.Count == 0
           && delta.Availability.Count == 0;

    private static Dictionary<string, T> Changed<T>(
        IReadOnlyDictionary<string, T> below, IReadOnlyDictionary<string, T> live, Func<T, T, bool> same)
        => live.Where(p => !below.TryGetValue(p.Key, out T? was) || !same(was, p.Value))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    private static string Text(AvailabilityAnswers answers) => JsonSerializer.Serialize(answers, LexiconJson.Options);
}
