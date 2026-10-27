using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>What the answers cannot place, for <c>tly_lexicon gaps</c>: items no route yields, maps
/// no route reaches and sources the builder could not resolve, grouped by the mod that owns the data
/// (<see cref="RowHashes.Owner"/>; "vanilla" when it is the base game's). Items are attributed by
/// their <c>obj|&lt;id&gt;</c> row key; a map or an unresolved source by its own text.</summary>
public static class LexiconGaps
{
    private const string ObjectRowPrefix = "obj|";
    public const string Unattributed = "unattributed";
    // An id in free text ends at whitespace or punctuation: "barter for (F)2176: trade item ..." names (F)2176.
    private static readonly System.Text.RegularExpressions.Regex QualifiedId = new(@"\([A-Za-z]+\)[^\s|,;:]+");
    private static readonly char[] PrefixEnd = { '.', '_' };

    /// <summary>A vanilla Data/Locations key is vanilla (even with an underscore); any other is owned by its prefix.</summary>
    private static string MapOwner(string map)
    {
        if (VanillaLocationKeys.All.Contains(map)) return RowHashes.Vanilla;
        int end = map.IndexOfAny(PrefixEnd);
        return end > 0 ? map[..end] : map;
    }

    /// <summary>An unresolved source is free text: attributed by the qualified item ids in it, else <see cref="Unattributed"/>.</summary>
    private static string SourceOwner(string source)
    {
        var ids = QualifiedId.Matches(source).Select(m => m.Value).ToList();
        return ids.Count == 0 ? Unattributed : RowHashes.Owner(ObjectRowPrefix + string.Join("|", ids));
    }

    public static string Report(AnswerVariant variant, ObtainabilityInputs inputs)
    {
        if (variant == null) throw new ArgumentNullException(nameof(variant));
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        var byOwner = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string owner, string line)
        {
            if (!byOwner.TryGetValue(owner, out var list)) byOwner[owner] = list = new List<string>();
            list.Add(line);
        }

        foreach (string id in variant.Obtain.Where(p => p.Value.Count == 0).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
            Add(RowHashes.Owner(ObjectRowPrefix + id), $"no route: {id}");
        foreach (string map in variant.UnreachedMaps.OrderBy(m => m, StringComparer.Ordinal))
            Add(MapOwner(map), $"unreached map: {map}");
        foreach (string source in variant.Unresolved.OrderBy(u => u, StringComparer.Ordinal))
            Add(SourceOwner(source), $"unresolved source: {source}");

        var sb = new StringBuilder();
        int total = byOwner.Values.Sum(l => l.Count);
        sb.AppendLine("# Lexicon gaps").AppendLine();
        sb.AppendLine($"{total} gap(s): {variant.Obtain.Count(p => p.Value.Count == 0)} item(s) with no route, {variant.UnreachedMaps.Count} unreached map(s), {variant.Unresolved.Count} unresolved source(s).");
        foreach (var (owner, lines) in byOwner)
        {
            sb.AppendLine().AppendLine($"## {owner} ({lines.Count})").AppendLine();
            foreach (string line in lines) sb.AppendLine($"- {line}");
        }
        return sb.ToString();
    }
}
