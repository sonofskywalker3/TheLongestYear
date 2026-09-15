using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The built model, how many passes it took, and every source the model could not read.</summary>
public sealed record ObtainabilityBuild(ObtainabilityModel Model, int Passes, bool HitPassCap, IReadOnlyList<string> Unresolved);

/// <summary>Builds the model: direct sources once, then grown and made sources over the previous pass's
/// model until no item's weeks change (see the plan's Task 8 intro for why this settles).</summary>
public static class ObtainabilityBuilder
{
    public const int MaxPasses = 1000;

    public static ObtainabilityBuild Build(ObtainabilityInputs inputs)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var f = inputs.Festivals;
        var o = inputs.Objects;
        var direct = new List<(string ItemId, ObtainSource Source)>();
        direct.AddRange(SpawnSources.Forage(inputs.Forage, o, f));
        direct.AddRange(SpawnSources.LocationFish(inputs.LocationFish, inputs.FishRows, o, f));
        direct.AddRange(SpawnSources.CrabPot(inputs.FishRows.Values));
        direct.AddRange(SpawnSources.ArtifactSpots(inputs.ArtifactSpots, o, f));
        direct.AddRange(SpawnSources.GarbageCans(inputs.Garbage, o, f));
        direct.AddRange(SpawnSources.FishingTrash());
        direct.AddRange(ShopSources.Stock(inputs.Shops, o, f));
        direct.AddRange(ShopSources.FestivalRewards(f));
        direct.AddRange(MineSources.Nodes());
        direct.AddRange(MineSources.MonsterDrops(inputs.MonsterDrops, o));
        direct.AddRange(MineSources.FishingTreasure());
        direct.AddRange(MadeSources.Animals(inputs.Animals, f));
        direct.AddRange(MadeSources.Tappers(inputs.TapItems, o, f));
        IReadOnlyDictionary<string, WeekMask> recipeWeeks = ShopSources.RecipeWeeks(inputs.Shops, f);

        ObtainabilityModel current = Assemble(direct, out List<string> lastUnresolved);
        for (int pass = 1; pass <= MaxPasses; pass++)
        {
            var all = new List<(string ItemId, ObtainSource Source)>(direct);
            all.AddRange(GrowSources.Crops(inputs.Crops, current));
            all.AddRange(GrowSources.FruitTrees(inputs.FruitTrees, current, o, f));
            all.AddRange(ShopSources.Barter(inputs.Shops, o, f, current));
            all.AddRange(MadeSources.Machines(inputs.Machines, o, current, f));
            all.AddRange(MadeSources.Recipes(inputs.Recipes, o, recipeWeeks, current));
            all.AddRange(MadeSources.Ponds(inputs.Ponds, o, current, f));
            all.AddRange(MadeSources.Geodes(inputs.GeodeDrops, inputs.GeodesUsingDefaultTable, o, current, f));
            ObtainabilityModel next = Assemble(all, out List<string> unresolved);
            if (SameTables(current, next)) return new ObtainabilityBuild(next, pass, false, unresolved);
            current = next;
            lastUnresolved = unresolved;   // from the last full pass, derived sources included
        }
        return new ObtainabilityBuild(current, MaxPasses, true, lastUnresolved);
    }

    private static ObtainabilityModel Assemble(
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

    private static bool SameTables(ObtainabilityModel a, ObtainabilityModel b)
    {
        if (a.Count != b.Count) return false;
        foreach (string id in b.ItemIds)
            if (a.Sources(id).Count != b.Sources(id).Count
                || !a.Table(id, ObtainFilter.Any).Equals(b.Table(id, ObtainFilter.Any))
                || !a.Table(id, ObtainFilter.DependableOnly).Equals(b.Table(id, ObtainFilter.DependableOnly)))
                return false;
        return true;
    }
}
