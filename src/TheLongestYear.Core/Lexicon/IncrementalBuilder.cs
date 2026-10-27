using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>The answers an incremental build starts from: a shipped variant and the row hashes it
/// was built from.</summary>
public sealed record IncrementalSeed(AnswerVariant Base, IReadOnlyDictionary<string, ulong> BaseRows);

/// <summary>How an incremental build went: the dirty items, the filtered passes it took, the derived
/// rows each pass evaluated, and how many full passes the closing check needed beyond the first (0
/// unless the dirty set missed something; the tests pin it at 0).</summary>
public sealed record IncrementalReport(
    IReadOnlySet<string> Dirty, int Passes, int RowsEvaluated, int RowsTotal, int RepairPasses,
    long DirtyMs, long PassMs, long CheckMs)
{
    public int DirtyCount => Dirty.Count;
}

/// <summary>Builds the obtainability model for live data over a shipped answer, recomputing only the
/// items <see cref="DirtyClosure"/> says a change can reach.
/// <para><b>The pass loop.</b> <c>current</c> holds the base's sources for every clean item and the
/// live direct sources for every dirty one. Each pass evaluates the derived rules over the live rows
/// and <c>current</c> (<see cref="ObtainabilityBuilder.DerivedSources(ObtainabilityInputs, ObtainabilityModel, IReadOnlyDictionary{string, WeekMask})"/>,
/// the cold build's own code), keeps the results for dirty items only, and repeats until no dirty
/// item's list changes. A row is skipped up front when none of the ids it can emit
/// (<see cref="DerivedEdges"/>, read from the live row, so a new row or a new query member is
/// covered) is dirty; fish ponds are always evaluated whole because a pond's fish depend on every
/// pond's precedence. Because a dirty item keeps every row that can emit it, in rule and row order,
/// its sources come out in the cold build's order.</para>
/// <para><b>The closing check.</b> One full pass of every rule over the result, the pass the cold build
/// ends on: its model is returned (so even Inputs are the live rules' own) and its diagnostics are
/// the build's <see cref="ObtainabilityBuild.Unresolved"/>. When the dirty set was sound that pass
/// changes nothing. Should it change something, the build keeps passing as the cold build does
/// until nothing changes, and <see cref="IncrementalReport.RepairPasses"/> counts those passes.</para></summary>
public static class IncrementalBuilder
{
    public static ObtainabilityBuild Build(ObtainabilityInputs live, IncrementalSeed seed, out int dirtyCount)
    {
        ObtainabilityBuild build = BuildWithReport(live, seed, out IncrementalReport report);
        dirtyCount = report.DirtyCount;
        return build;
    }

    public static ObtainabilityBuild BuildWithReport(ObtainabilityInputs live, IncrementalSeed seed, out IncrementalReport report)
    {
        if (live is null) throw new ArgumentNullException(nameof(live));
        if (seed is null) throw new ArgumentNullException(nameof(seed));
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var context = new Context(live);
        ObtainabilityModel baseModel = seed.Base.ObtainModel();
        HashSet<string> dirty = DirtyClosure.Of(live, baseModel, DirtyClosure.KnownIds(seed.Base.Obtain), seed.BaseRows,
            context.Direct, context.Edges, context.RecipeWeeks, context.Memo);
        return Run(context, baseModel, dirty, timer, out report);
    }

    /// <summary>The year-sort model (<see cref="YearTwoItems.WithoutCartRandomStock"/> inputs) over the
    /// shipped variant's <see cref="AnswerVariant.YearSort"/>, reusing the main build's dirty set.
    /// <para>No row hashes exist for the shipped cart-stripped inputs, so the year-sort dirty set is
    /// the main one (ruling for Task 7). That is sound: the two input sets differ from their bases by
    /// the same rows except the cart's query rows, which are absent on both year-sort sides, so a main
    /// seed is a superset of a year-sort one; a derived row that fed nothing over the main base feeds
    /// nothing over the year-sort base either (fewer direct sources only ever land later). The set is
    /// also closed again over the year-sort base's own edges, which can only add items.</para></summary>
    public static ObtainabilityBuild BuildYearSort(
        ObtainabilityInputs liveYearSort, IncrementalSeed seed, IReadOnlySet<string> mainDirty, out IncrementalReport report)
    {
        if (liveYearSort is null) throw new ArgumentNullException(nameof(liveYearSort));
        if (seed is null) throw new ArgumentNullException(nameof(seed));
        if (mainDirty is null) throw new ArgumentNullException(nameof(mainDirty));
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var context = new Context(liveYearSort);
        ObtainabilityModel baseModel = seed.Base.YearSortModel();
        HashSet<string> dirty = DirtyClosure.Close(mainDirty, baseModel, context.Edges);
        return Run(context, baseModel, dirty, timer, out report);
    }

    /// <summary>The live inputs' direct sources, static row edges and recipe shop weeks, built once.</summary>
    private sealed class Context
    {
        public Context(ObtainabilityInputs live)
        {
            Live = live;
            Direct = ObtainabilityBuilder.DirectSources(live);
            Edges = DerivedEdges.Of(live);
            RecipeWeeks = ShopSources.RecipeWeeks(live.Shops, live.Festivals);
            Memo = new DerivedRowCache(Edges);
        }

        /// <summary>Per-row memo shared by the dirty-set pass, the filtered passes and the closing pass.</summary>
        public DerivedRowCache Memo { get; }

        public ObtainabilityInputs Live { get; }
        public List<(string ItemId, ObtainSource Source)> Direct { get; }
        public List<RowEdge> Edges { get; }
        public IReadOnlyDictionary<string, WeekMask> RecipeWeeks { get; }
    }

    private static ObtainabilityBuild Run(Context context, ObtainabilityModel baseModel, HashSet<string> dirty,
        System.Diagnostics.Stopwatch timer, out IncrementalReport report)
    {
        long dirtyMs = timer.ElapsedMilliseconds;
        ObtainabilityInputs live = context.Live;
        DerivedRowFilter filter = RowsFor(live, context.Edges, dirty);

        // Clean items: the base's lists, verbatim. Dirty items: live direct sources to start.
        var clean = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (string id in baseModel.ItemIds)
            if (!dirty.Contains(id)) clean[id] = baseModel.Sources(id);
        var dirtyDirect = context.Direct.Where(s => dirty.Contains(BundleParsing.NormalizeItemId(s.ItemId))).ToList();
        ObtainabilityModel dirtyNow = ObtainabilityBuilder.Assemble(dirtyDirect, out _);

        int passes = 0;
        bool converged = false;
        while (passes < ObtainabilityBuilder.MaxPasses)
        {
            passes++;
            ObtainabilityModel current = Merge(clean, dirtyNow, baseModel, dirty);
            var all = new List<(string ItemId, ObtainSource Source)>(dirtyDirect);
            all.AddRange(ObtainabilityBuilder.DerivedSources(live, current, context.RecipeWeeks, filter, context.Memo)
                .Where(s => dirty.Contains(BundleParsing.NormalizeItemId(s.ItemId))));
            ObtainabilityModel next = ObtainabilityBuilder.Assemble(all, out _);
            bool same = ObtainabilityBuilder.SameTables(dirtyNow, next);
            dirtyNow = next;
            if (same) { converged = true; break; }
        }

        long passMs = timer.ElapsedMilliseconds - dirtyMs;

        // The closing full pass (see the class summary).
        ObtainabilityModel result = Merge(clean, dirtyNow, baseModel, dirty);
        int repairs = -1;
        List<string> unresolved;
        while (true)
        {
            var all = new List<(string ItemId, ObtainSource Source)>(context.Direct);
            all.AddRange(ObtainabilityBuilder.DerivedSources(live, result, context.RecipeWeeks, null, context.Memo));
            ObtainabilityModel next = ObtainabilityBuilder.Assemble(all, out unresolved);
            repairs++;
            bool same = ObtainabilityBuilder.SameTables(result, next);
            result = next;
            if (same || repairs >= ObtainabilityBuilder.MaxPasses) { converged &= same; break; }
        }
        report = new IncrementalReport(dirty, passes, filter.Count, context.Edges.Count, repairs,
            dirtyMs, passMs, timer.ElapsedMilliseconds - dirtyMs - passMs);
        return new ObtainabilityBuild(result, passes + 1 + repairs, !converged, unresolved);
    }

    /// <summary>Every derived row that can emit a dirty id; crop rows sharing a seed go together,
    /// because the Mixed Seeds rule reads the first row per seed.</summary>
    private static DerivedRowFilter RowsFor(ObtainabilityInputs live, IReadOnlyList<RowEdge> edges, IReadOnlySet<string> dirty)
    {
        var filter = new DerivedRowFilter();
        var seeds = new HashSet<string>(StringComparer.Ordinal);
        foreach (RowEdge edge in edges)
        {
            if (!edge.Outputs.Any(dirty.Contains)) continue;
            filter.Add(edge.Row);
            if (edge.Row is CropRow crop) seeds.Add(crop.SeedId);
        }
        foreach (CropRow crop in live.Crops)
            if (seeds.Contains(crop.SeedId)) filter.Add(crop);
        return filter;
    }

    /// <summary>The clean lists and the dirty ones as one model; clean ids read through to the base
    /// model, so their tables are folded once for the whole build.</summary>
    private static ObtainabilityModel Merge(
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> clean, ObtainabilityModel dirtyNow,
        ObtainabilityModel baseModel, IReadOnlySet<string> dirty)
    {
        var all = new Dictionary<string, IReadOnlyList<ObtainSource>>(clean, StringComparer.Ordinal);
        foreach (string id in dirtyNow.ItemIds) all[id] = dirtyNow.Sources(id);
        return new ObtainabilityModel(all, baseModel, dirty);
    }
}
