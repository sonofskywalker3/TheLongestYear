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
    private const string MapRowPrefix = "map|";

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
            Add(RowHashes.Owner(MapRowPrefix + map), $"unreached map: {map}");
        foreach (string source in variant.Unresolved.OrderBy(u => u, StringComparer.Ordinal))
            Add(RowHashes.Owner(MapRowPrefix + source.Replace('|', ' ')),$"unresolved source: {source}");

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
