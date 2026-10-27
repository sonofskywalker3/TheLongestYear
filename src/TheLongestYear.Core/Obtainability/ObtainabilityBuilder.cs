using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The built model, how many passes it took, and every source the model could not read.</summary>
public sealed record ObtainabilityBuild(ObtainabilityModel Model, int Passes, bool HitPassCap, IReadOnlyList<string> Unresolved);

/// <summary>Builds the model: direct sources once, then grown and made sources over the previous pass's
/// model, repeating until <see cref="SameTables"/> finds nothing changed (spec
/// 2026-09-14-obtainability-phase2, section 1, "Chains settle by repeated passes").
/// <para>What is proved: <see cref="SameTables"/> compares, per item, the whole source list (a source's
/// equality covers its table and its undelayed table), so a change seen only under an island, year 2
/// or owned-only filter keeps the passes going. The filter aggregates only ever move earlier, because
/// each pass rebuilds every derived source from a model whose inputs land no later than last pass's,
/// and an aggregate is the Earliest over them; with 112 day slots and finitely many items, they cannot
/// keep moving. That is a statement about the aggregates only. An individual Chance table is a
/// per-start difference (<see cref="DayTable.Except"/>) and can move either way as the dependable
/// half moves, and the source list can change with it, so it is not proved to settle.
/// <see cref="MaxPasses"/> bounds those; a build that hits the cap reports
/// <see cref="ObtainabilityBuild.HitPassCap"/> rather than claiming it converged.</para></summary>
public static class ObtainabilityBuilder
{
    public const int MaxPasses = 1000;

    /// <param name="mineTravel">Whether a direct mine route lands after the days it takes to reach its
    /// floor (the ruling, and the default). Off only for the debug check that the fairness rule gives the
    /// same answers either way.</param>
    public static ObtainabilityBuild Build(ObtainabilityInputs inputs, bool mineTravel = true)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        List<(string ItemId, ObtainSource Source)> direct = DirectSources(inputs, mineTravel);
        IReadOnlyDictionary<string, WeekMask> recipeWeeks = ShopSources.RecipeWeeks(inputs.Shops, inputs.Festivals);

        ObtainabilityModel current = Assemble(direct, out List<string> lastUnresolved);
        for (int pass = 1; pass <= MaxPasses; pass++)
        {
            var all = new List<(string ItemId, ObtainSource Source)>(direct);
            all.AddRange(DerivedSources(inputs, current, recipeWeeks));
            ObtainabilityModel next = Assemble(all, out List<string> unresolved);
            if (SameTables(current, next)) return new ObtainabilityBuild(next, pass, false, unresolved);
            current = next;
            lastUnresolved = unresolved;   // from the last full pass, derived sources included
        }
        return new ObtainabilityBuild(current, MaxPasses, true, lastUnresolved);
    }

    /// <summary>The incremental build over shipped answers (<see cref="Lexicon.IncrementalBuilder"/>).</summary>
    public static ObtainabilityBuild Build(ObtainabilityInputs inputs, Lexicon.IncrementalSeed seed)
        => Lexicon.IncrementalBuilder.Build(inputs, seed, out _);

    /// <summary>Every source read straight out of the data, with no other item's table behind it:
    /// built once per build, in this order (the order is part of every item's source list).</summary>
    public static List<(string ItemId, ObtainSource Source)> DirectSources(ObtainabilityInputs inputs, bool mineTravel = true)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var f = inputs.Festivals;
        var o = inputs.Objects;
        var direct = new List<(string ItemId, ObtainSource Source)>();
        direct.AddRange(SpawnSources.Forage(inputs.Forage, o, f));
        direct.AddRange(SpawnSources.CrabPot(inputs.FishRows.Values));
        direct.AddRange(SpawnSources.ArtifactSpots(inputs.ArtifactSpots, o, f));
        direct.AddRange(SpawnSources.GarbageCans(inputs.Garbage, o, f));
        direct.AddRange(SpawnSources.FishingTrash());
        direct.AddRange(ShopSources.Stock(inputs.Shops, o, f));
        direct.AddRange(ShopSources.FestivalRewards(f));
        direct.AddRange(MineSources.Nodes());
        direct.AddRange(MineSources.MonsterDrops(inputs.MonsterDrops, o));
        direct.AddRange(MineSources.FishingTreasure());
        direct.AddRange(LivestockSources.Animals(inputs.Animals, f, inputs.Buildings));
        direct.AddRange(LivestockSources.Tappers(inputs.TapItems, o, f));
        direct.AddRange(CodeSources.MineFish());
        direct.AddRange(CodeSources.Moss());
        direct.AddRange(CodeSources.SeasonSeeds());
        direct.AddRange(CodeSources.GuildRewards(inputs.SlayerQuests));
        direct.AddRange(CodeSources.MagicBait());
        direct.AddRange(WorldSources.All());
        // Mine depth goes into the landing day here, once, and only here: a direct route needing floor N
        // lands the days it takes to get there from nothing later (ruling 2026-09-16), and everything
        // made from it inherits that through its input's table, so no derived pass adds it again.
        if (mineTravel)
            for (int i = 0; i < direct.Count; i++)
                direct[i] = (direct[i].ItemId, MineDepth.WithTravel(direct[i].Source));
        return direct;
    }

    /// <summary>One pass of every derived rule (grown, made, bartered, pond and bait-gated fish
    /// sources) over <paramref name="current"/>, in the order the build appends them.</summary>
    public static List<(string ItemId, ObtainSource Source)> DerivedSources(
        ObtainabilityInputs inputs, ObtainabilityModel current, IReadOnlyDictionary<string, WeekMask> recipeWeeks)
        => DerivedSources(inputs, current, recipeWeeks, null);

    /// <summary><see cref="DerivedSources(ObtainabilityInputs, ObtainabilityModel, IReadOnlyDictionary{string, WeekMask})"/>
    /// over only the rows <paramref name="rows"/> keeps (null keeps every row). A rule whose rows read
    /// each other (fish pond precedence) is always given every row.</summary>
    internal static List<(string ItemId, ObtainSource Source)> DerivedSources(
        ObtainabilityInputs inputs, ObtainabilityModel current, IReadOnlyDictionary<string, WeekMask> recipeWeeks,
        DerivedRowFilter? rows, DerivedRowCache? memo = null)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var f = inputs.Festivals;
        var o = inputs.Objects;
        var all = new List<(string ItemId, ObtainSource Source)>();
        all.AddRange(SpawnSources.LocationFish(Keep(inputs.LocationFish, rows), inputs.FishRows, o, f, current));
        all.AddRange(GrowSources.Crops(Keep(inputs.Crops, rows), current));
        if (rows == null || rows.TeaBush) all.AddRange(GrowSources.TeaBush(current));
        all.AddRange(GrowSources.FruitTrees(Keep(inputs.FruitTrees, rows), current, o, f));
        all.AddRange(ShopSources.Barter(Keep(inputs.Shops, rows), o, f, current));
        if (memo == null)
        {
            all.AddRange(MadeSources.Machines(Keep(inputs.Machines, rows), o, current, f, inputs.Crops));
            all.AddRange(MadeSources.Recipes(Keep(inputs.Recipes, rows), o, recipeWeeks, current, inputs.CookingChannel));
        }
        else
        {
            // Both rules emit row by row in row order, so evaluating one row at a time and
            // concatenating gives the same list.
            foreach (MachineRow rule in Keep(inputs.Machines, rows))
                all.AddRange(memo.Get(rule, current, () => MadeSources.Machines(new[] { rule }, o, current, f, inputs.Crops)));
            foreach (RecipeRow recipe in Keep(inputs.Recipes, rows))
                all.AddRange(memo.Get(recipe, current, () => MadeSources.Recipes(new[] { recipe }, o, recipeWeeks, current, inputs.CookingChannel)));
        }
        all.AddRange(LivestockSources.Ponds(inputs.Ponds, o, current, f, inputs.Buildings));
        all.AddRange(MadeSources.Geodes(Keep(inputs.GeodeDrops, rows), Keep(inputs.GeodesUsingDefaultTable, rows), o, current, f));
        return all;
    }

    private static IReadOnlyCollection<T> Keep<T>(IReadOnlyCollection<T> rows, DerivedRowFilter? filter) where T : notnull
        => filter == null ? rows : rows.Where(r => filter.Keeps(r)).ToList();

    internal static ObtainabilityModel Assemble(
        IEnumerable<(string ItemId, ObtainSource Source)> sources, out List<string> unresolved)
    {
        var real = new List<(string ItemId, ObtainSource Source)>();
        unresolved = new List<string>();
        foreach (var s in sources)
        {
            if (s.ItemId.StartsWith(ItemQueries.UnresolvedPrefix, StringComparison.Ordinal))
                unresolved.Add($"{s.ItemId.Substring(ItemQueries.UnresolvedPrefix.Length)} | {s.Source.Detail}");
            else
                real.Add(s);
        }
        unresolved = unresolved.Distinct().OrderBy(u => u, StringComparer.Ordinal).ToList();
        return new ObtainabilityModel(real
            .GroupBy(s => BundleParsing.NormalizeItemId(s.ItemId), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ObtainSource>)g.Select(s => s.Source).ToList(), StringComparer.Ordinal));
    }

    internal static bool SameTables(ObtainabilityModel a, ObtainabilityModel b)
    {
        if (a.Count != b.Count) return false;
        foreach (string id in b.ItemIds)
            if (!a.Sources(id).SequenceEqual(b.Sources(id)))
                return false;
        return true;
    }
}
