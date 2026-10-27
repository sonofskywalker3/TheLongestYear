using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Which items a change to the input rows can reach, so the incremental build recomputes
/// only those (<see cref="IncrementalBuilder"/>). Everything outside the set keeps the base answers
/// verbatim, so the set must hold every item whose cold answer could differ from the base's.
/// <para><b>Seeds.</b> An item is dirty when any of these holds:
/// <list type="number">
/// <item>its object row was added, removed or changed;</item>
/// <item>it is an output of an added or changed row (<see cref="DerivedEdges.OutputsOfChangedRow"/>:
/// a crop's harvest, a tree's fruit, a recipe's outputs, a machine's literal and resolved outputs, a
/// pond's products, a geode's items, a fish row's fish, a shop row's item, a direct row's item);</item>
/// <item>its base source list is not what the live rules give when evaluated over the base answers:
/// the live direct sources plus one pass of every live derived rule over the base model, assembled
/// as the cold build assembles, compared in order and with <see cref="ObtainSource.Inputs"/>. The
/// base is the fixed point of the base rules, so for an item whose direct sources and rules did not
/// change this reproduces its base list exactly; a direct source added, removed or changed, a removed
/// derived row that fed it, or an input set that grew or shrank (a category or tag member) all show
/// here. This is the "direct source list differs" test made exact: a base answer does not say which
/// of its sources were direct (a no-input machine, a non-bait fish and Moss all have empty Inputs);</item>
/// <item>if any object row changed: every base item with a source whose Inputs hold a group of more
/// than one id (a category, tag or either-of expansion, whose membership may have moved);</item>
/// <item>it has no base answer at all (a new id).</item>
/// </list>
/// A festival row change dirties everything: festival dates feed condition readings in every rule.</para>
/// <para><b>Closure.</b> Then, to a fixed point: an item is dirty when anything it reads is dirty,
/// read along both the base's reverse-Inputs edges and the live rows' static edges
/// (<see cref="DerivedEdges.Of"/>; these catch a route the base never had, such as a machine whose
/// tag matched nothing before). An item outside the set therefore has the same direct sources, the
/// same rules and only clean inputs, all the way down, so the cold build's passes over it run exactly
/// as the base's did and end on the base's answer; that is the claim the tests check against cold
/// builds, including a route removed from inside a cycle and a new category member.</para></summary>
public static class DirtyClosure
{
    private const string FestivalType = "fest|";
    private const string ObjectType = "obj|";
    private const string MetaType = "meta|";

    public static IReadOnlySet<string> Of(
        ObtainabilityInputs live, IncrementalSeed seed, IReadOnlyList<(string ItemId, ObtainSource Source)> liveDirect)
    {
        if (live is null) throw new ArgumentNullException(nameof(live));
        if (seed is null) throw new ArgumentNullException(nameof(seed));
        if (liveDirect is null) throw new ArgumentNullException(nameof(liveDirect));
        List<RowEdge> edges = DerivedEdges.Of(live);
        return Of(live, seed.Base.ObtainModel(), KnownIds(seed.Base.Obtain), seed.BaseRows, liveDirect, edges,
            ShopSources.RecipeWeeks(live.Shops, live.Festivals), new DerivedRowCache(edges));
    }

    /// <summary>Every id a base answer map has an entry for (an empty entry included), normalised.</summary>
    internal static HashSet<string> KnownIds(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> answers)
        => new(answers.Keys.Select(BundleParsing.NormalizeItemId), StringComparer.Ordinal);

    internal static HashSet<string> Of(
        ObtainabilityInputs live, ObtainabilityModel baseModel, IReadOnlySet<string> baseKnown, IReadOnlyDictionary<string, ulong> baseRows,
        IReadOnlyList<(string ItemId, ObtainSource Source)> liveDirect, IReadOnlyList<RowEdge> edges,
        IReadOnlyDictionary<string, WeekMask> recipeWeeks, DerivedRowCache memo)
    {
        var dirty = new HashSet<string>(StringComparer.Ordinal);
        void Mark(string id) => dirty.Add(BundleParsing.NormalizeItemId(id));

        // The Pierre flag is not an input of any rule: its effect is in the shop rows.
        IReadOnlyList<(string Key, object Row)> liveRows = RowHashes.Rows(live, false);
        var liveHashes = RowHashes.Of(liveRows);
        var (added, removed, changed) = RowHashes.Diff(
            baseRows.Where(p => !p.Key.StartsWith(MetaType, StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            liveHashes.Where(p => !p.Key.StartsWith(MetaType, StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));

        var allIds = new HashSet<string>(baseModel.ItemIds, StringComparer.Ordinal);
        foreach (string id in live.Objects.Keys) allIds.Add(BundleParsing.NormalizeItemId(id));
        foreach (var (id, _) in liveDirect)
            if (!IsDiagnostic(id)) allIds.Add(BundleParsing.NormalizeItemId(id));
        foreach (RowEdge edge in edges) { allIds.UnionWith(edge.Inputs); allIds.UnionWith(edge.Outputs); }

        if (added.Concat(removed).Concat(changed).Any(k => k.StartsWith(FestivalType, StringComparison.Ordinal)))
            return allIds;

        // 1. Object rows.
        bool anyObject = false;
        foreach (string key in added.Concat(removed).Concat(changed).Where(k => k.StartsWith(ObjectType, StringComparison.Ordinal)))
        {
            anyObject = true;
            Mark(StripDuplicateSuffix(key.Substring(ObjectType.Length)));
        }

        // 2. Outputs of added and changed rows.
        var byKey = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, row) in liveRows) byKey[key] = row;
        foreach (string key in added.Concat(changed))
        {
            if (!byKey.TryGetValue(key, out object? row)) continue;
            IReadOnlyCollection<string>? outputs = DerivedEdges.OutputsOfChangedRow(key, row, live);
            if (outputs == null) return allIds;
            foreach (string id in outputs) Mark(id);
        }

        // 3. Base answers the live rules do not reproduce over the base.
        var atBase = new List<(string ItemId, ObtainSource Source)>(liveDirect);
        atBase.AddRange(ObtainabilityBuilder.DerivedSources(live, baseModel, recipeWeeks, null, memo));
        ObtainabilityModel reproduced = ObtainabilityBuilder.Assemble(atBase, out _);
        foreach (string id in baseModel.ItemIds.Concat(reproduced.ItemIds))
            if (!SameList(baseModel.Sources(id), reproduced.Sources(id)))
                Mark(id);

        // 4. Category, tag and either-of groups, when any object changed.
        if (anyObject)
            foreach (string id in baseModel.ItemIds)
                if (baseModel.Sources(id).Any(s => s.Inputs.Any(g => g.Count > 1)))
                    Mark(id);

        // 5. New objects (also object rows added, so already in; kept for the rule's sake). Any other
        // id without a base answer has an implicit empty one, which test 3 checks like any other.
        foreach (string id in live.Objects.Keys.Select(BundleParsing.NormalizeItemId))
            if (!baseKnown.Contains(id)) dirty.Add(id);

        // Closure over base reverse-Inputs edges and the live rows' static edges.
        return Close(dirty, baseModel, edges);
    }

    /// <summary>Closes <paramref name="dirty"/> over <paramref name="model"/>'s reverse-Inputs edges and
    /// the live rows' static edges (the year-sort model reuses the main model's seeds).</summary>
    internal static HashSet<string> Close(IEnumerable<string> dirty, ObtainabilityModel model, IReadOnlyList<RowEdge> edges)
    {
        var readers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (string id in model.ItemIds)
            foreach (ObtainSource source in model.Sources(id))
                foreach (IReadOnlyList<string> group in source.Inputs)
                    foreach (string input in group) Reader(readers, input).Add(id);
        foreach (RowEdge edge in edges)
            foreach (string input in edge.Inputs)
                Reader(readers, input).UnionWith(edge.Outputs);
        var closed = new HashSet<string>(dirty, StringComparer.Ordinal);
        var queue = new Queue<string>(closed);
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!readers.TryGetValue(id, out HashSet<string>? next)) continue;
            foreach (string reader in next)
                if (closed.Add(reader)) queue.Enqueue(reader);
        }
        return closed;
    }

    private static HashSet<string> Reader(Dictionary<string, HashSet<string>> readers, string input)
    {
        string id = BundleParsing.NormalizeItemId(input);
        if (!readers.TryGetValue(id, out HashSet<string>? set)) readers[id] = set = new HashSet<string>(StringComparer.Ordinal);
        return set;
    }

    /// <summary>A diagnostic entry ("?query"), which the builder moves to the unresolved list.</summary>
    private static bool IsDiagnostic(string id) => id.StartsWith(ItemQueries.UnresolvedPrefix, StringComparison.Ordinal);

    /// <summary>A repeated object key carries a "|n" suffix (<see cref="RowHashes.Rows"/>); object ids
    /// never contain '|'.</summary>
    private static string StripDuplicateSuffix(string id)
    {
        int bar = id.IndexOf('|');
        return bar < 0 ? id : id.Substring(0, bar);
    }

    /// <summary>Same sources in the same order, and the same Inputs on each.</summary>
    internal static bool SameList(IReadOnlyList<ObtainSource> a, IReadOnlyList<ObtainSource> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i])) return false;
            if (!SameInputs(a[i].Inputs, b[i].Inputs)) return false;
        }
        return true;
    }

    private static bool SameInputs(IReadOnlyList<IReadOnlyList<string>> a, IReadOnlyList<IReadOnlyList<string>> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].SequenceEqual(b[i], StringComparer.Ordinal)) return false;
        return true;
    }
}
