using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Which derived-rule rows a pass evaluates (<see cref="ObtainabilityBuilder.DerivedSources(ObtainabilityInputs, ObtainabilityModel, IReadOnlyDictionary{string, WeekMask}, DerivedRowFilter?)"/>).
/// Rows are held by reference (two equal rows are still two rows); the default-table geode ids by value.</summary>
internal sealed class DerivedRowFilter
{
    private readonly HashSet<object> _rows = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _geodeDefaults = new(StringComparer.Ordinal);

    /// <summary>Whether the tea bush rule (which has no row of its own) runs.</summary>
    public bool TeaBush { get; private set; }

    public int Count => _rows.Count + _geodeDefaults.Count + (TeaBush ? 1 : 0);

    public void Add(object row)
    {
        if (ReferenceEquals(row, DerivedEdges.TeaBushRow)) TeaBush = true;
        else if (row is string geode) _geodeDefaults.Add(geode);
        else _rows.Add(row);
    }

    public bool Keeps(object row) => row is string geode ? _geodeDefaults.Contains(geode) : _rows.Contains(row);
}

/// <summary>Remembers what one machine or recipe row emitted and the source lists of every id it read
/// when it did, and hands the same sources back while those lists are unchanged. A row's sources are a
/// function of the rows themselves and its inputs' lists alone (the rule reads nothing else of the
/// model), and <see cref="DerivedEdges"/> lists every id each row reads, so a hit is exactly what
/// evaluating the row again would give. Machines dominate a pass's cost; recipes are next.</summary>
internal sealed class DerivedRowCache
{
    private readonly Dictionary<object, IReadOnlyCollection<string>> _reads = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, (List<IReadOnlyList<ObtainSource>> Seen, List<(string ItemId, ObtainSource Source)> Emitted)> _memo
        = new(ReferenceEqualityComparer.Instance);

    public DerivedRowCache(IEnumerable<RowEdge> edges)
    {
        foreach (RowEdge edge in edges) _reads[edge.Row] = edge.Inputs;
    }

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public IReadOnlyList<(string ItemId, ObtainSource Source)> Get(
        object row, ObtainabilityModel current, Func<IEnumerable<(string ItemId, ObtainSource Source)>> evaluate)
    {
        if (!_reads.TryGetValue(row, out IReadOnlyCollection<string>? reads))
        {
            Misses++;
            return evaluate().ToList();   // a row the edges do not know: never memoised
        }
        if (_memo.TryGetValue(row, out var hit) && Same(hit.Seen, reads, current))
        {
            Hits++;
            return hit.Emitted;
        }
        Misses++;
        var seen = reads.Select(id => current.Sources(id)).ToList();
        var emitted = evaluate().ToList();
        _memo[row] = (seen, emitted);
        return emitted;
    }

    private static bool Same(List<IReadOnlyList<ObtainSource>> seen, IReadOnlyCollection<string> reads, ObtainabilityModel current)
    {
        int i = 0;
        foreach (string id in reads)
        {
            IReadOnlyList<ObtainSource> now = current.Sources(id);
            if (!ReferenceEquals(now, seen[i]) && !now.SequenceEqual(seen[i])) return false;
            i++;
        }
        return true;
    }
}

/// <summary>One derived-rule row: the item ids it reads and every item id it can emit a source for,
/// read from the row and the objects alone (no tables), so the sets hold whatever a pass finds.</summary>
internal sealed record RowEdge(object Row, IReadOnlyCollection<string> Inputs, IReadOnlyCollection<string> Outputs);

/// <summary>The static shape of every derived rule (<see cref="ObtainabilityBuilder.DerivedSources(ObtainabilityInputs, ObtainabilityModel, IReadOnlyDictionary{string, WeekMask})"/>):
/// per row, the ids it reads and a SUPERSET of the ids it emits. Each case mirrors its rule's code; a
/// rule changed there must be changed here, or the incremental build may skip a row it needed
/// (<c>IncrementalBuilderTests</c> compare against the cold build to catch that).</summary>
internal static class DerivedEdges
{
    /// <summary>Stands for the tea bush rule, which has no input row.</summary>
    public static readonly object TeaBushRow = new();

    private static readonly IReadOnlyCollection<string> None = Array.Empty<string>();

    public static List<RowEdge> Of(ObtainabilityInputs inputs)
    {
        var o = inputs.Objects;
        var edges = new List<RowEdge>();
        foreach (LocationSpawn row in inputs.LocationFish)
            edges.Add(new RowEdge(row, row.RequireMagicBait ? new[] { SpawnSources.MagicBaitId } : None, Ids(row.ItemId, o)));

        var poolSeeds = new HashSet<string>(GrowSources.MixedSeedPools.Values.SelectMany(s => s), StringComparer.Ordinal);
        foreach (CropRow crop in inputs.Crops)
        {
            if (crop.SeedId == GrowSources.MixedSeedsId) { edges.Add(new RowEdge(crop, None, None)); continue; }
            var read = new List<string> { Norm(crop.SeedId) };
            if (poolSeeds.Contains(crop.SeedId)) read.Add(GrowSources.MixedSeedsId);
            edges.Add(new RowEdge(crop, read, new[] { Norm(crop.HarvestId) }));
        }
        edges.Add(new RowEdge(TeaBushRow, new[] { GrowSources.TeaSaplingId }, new[] { GrowSources.TeaLeavesId }));
        foreach (FruitTreeRow tree in inputs.FruitTrees)
            edges.Add(new RowEdge(tree, new[] { Norm(tree.SaplingId) }, tree.Fruit.SelectMany(f => Ids(f.ItemId, o)).Distinct().ToList()));
        foreach (ShopRow row in inputs.Shops.Where(r => !r.IsRecipe && r.TradeItemId != null))
            edges.Add(new RowEdge(row, new[] { Norm(row.TradeItemId!) }, Ids(row.ItemId, o)));
        foreach (MachineRow rule in inputs.Machines)
            edges.Add(Machine(rule, o, inputs.Crops));
        foreach (RecipeRow recipe in inputs.Recipes)
            edges.Add(new RowEdge(recipe, recipe.Ingredients.SelectMany(i => Ingredient(i, o)).Distinct().ToList(), RecipeOutputs(recipe)));
        foreach (PondRow pond in inputs.Ponds)
            edges.Add(new RowEdge(pond,
                o.Values.Where(fish => MadeSources.MatchesTags(fish, pond.RequiredTags)).Select(fish => fish.QualifiedId).ToList(),
                pond.Products.SelectMany(p => Ids(p.ItemId, o)).Distinct().ToList()));
        foreach (GeodeDropRow row in inputs.GeodeDrops)
            edges.Add(new RowEdge(row, new[] { Norm(row.GeodeId) }, Ids(row.ItemId, o)));
        foreach (string geode in inputs.GeodesUsingDefaultTable)
            edges.Add(new RowEdge(geode, new[] { Norm(geode) }, DefaultGeodeItems(geode)));
        return edges;
    }

    /// <summary>The ids a changed input row of any kind can give a source to, for the dirty set: a
    /// derived row's outputs, a direct row's item. Null means "every item" (a festival's dates feed
    /// condition readings everywhere).</summary>
    public static IReadOnlyCollection<string>? OutputsOfChangedRow(string key, object row, ObtainabilityInputs inputs)
    {
        var o = inputs.Objects;
        string type = key.Split('|')[0];
        switch (type)
        {
            case "fest": return null;
            case "obj": return row is ObjInfo info ? new[] { info.QualifiedId, Norm(key.Substring(type.Length + 1)) } : None;
            case "fishrow": return row is FishRow fish ? new[] { Norm(fish.ItemId) } : None;
            case "geodedefault": return row is string g ? DefaultGeodeItems(g) : None;
            case "tv":
                string name = key.Substring(type.Length + 1);
                return inputs.Recipes.Where(r => r.Name == name).SelectMany(RecipeOutputs).ToList();
            case "building":
                // Build days are setup steps on animal produce (direct) and pond products (derived).
                return inputs.Ponds.SelectMany(p => p.Products).SelectMany(p => Ids(p.ItemId, o))
                    .Concat(inputs.Animals.SelectMany(a => a.Produce.Concat(a.DeluxeProduce)).Select(p => Norm(p.ItemId)))
                    .Distinct().ToList();
            case "machine":
                // Keys are list positions per machine, so one change can shift every key of that
                // machine: every live row of the machine counts.
                string machineId = ((MachineRow)row).MachineId;
                return inputs.Machines.Where(m => m.MachineId == machineId)
                    .SelectMany(m => Machine(m, o, inputs.Crops).Outputs).Distinct().ToList();
            case "geode":
                string geodeId = ((GeodeDropRow)row).GeodeId;
                return inputs.GeodeDrops.Where(r => r.GeodeId == geodeId).SelectMany(r => Ids(r.ItemId, o)).Distinct().ToList();
        }
        return row switch
        {
            LocationSpawn spawn => Ids(spawn.ItemId, o),
            CropRow crop => new[] { Norm(crop.HarvestId) },
            FruitTreeRow tree => tree.Fruit.SelectMany(f => Ids(f.ItemId, o)).Distinct().ToList(),
            RecipeRow recipe => RecipeOutputs(recipe),
            PondRow pond => pond.Products.SelectMany(p => Ids(p.ItemId, o)).Distinct().ToList(),
            ShopRow shop => Ids(shop.ItemId, o),
            ArtifactSpotRow spot => Ids(spot.ItemId, o),
            GarbageRow garbage => Ids(garbage.ItemId, o),
            MonsterDropRow drop => Ids(drop.ItemId, o),
            AnimalRow animal => animal.Produce.Concat(animal.DeluxeProduce).Select(p => Norm(p.ItemId)).Distinct().ToList(),
            TapRow tap => Ids(tap.ItemId, o),
            SlayerQuestRow quest => new[] { Norm(quest.RewardItemId) },
            _ => None,
        };
    }

    private static RowEdge Machine(MachineRow rule, IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyList<CropRow> crops)
    {
        bool noInput = rule.RequiredItemId == null && rule.RequiredTags.Count == 0;
        List<string> inputs = noInput ? new List<string>() : MadeSources.Inputs(rule, objects).ToList();
        if (!noInput && inputs.Count == 0) return new RowEdge(rule, inputs, None);
        var read = new HashSet<string>(inputs, StringComparer.Ordinal);
        var outputs = new List<string>();
        foreach (MachineOutput output in rule.Outputs)
        {
            if (output.Method == OutputMethodKind.Cask) continue;
            if (output.Method == OutputMethodKind.SeedMaker)
            {
                outputs.AddRange(crops.Where(c => read.Contains(c.HarvestId)).Select(c => Norm(c.SeedId)));
                outputs.Add(MadeSources.MixedSeedsId);
                outputs.Add(MadeSources.AncientSeedsId);
                continue;
            }
            if (output.Method == OutputMethodKind.MushroomLog) { outputs.AddRange(MadeSources.MushroomLogItems); continue; }
            if (output.Method == OutputMethodKind.Unknown || output.OutputMethod != null) continue;
            if (output.ItemId == MadeSources.DropIn) { outputs.AddRange(inputs); continue; }
            if (string.IsNullOrWhiteSpace(output.ItemId)) continue;
            outputs.AddRange(Ids(output.ItemId, objects));
        }
        return new RowEdge(rule, inputs, outputs.Distinct().ToList());
    }

    private static IEnumerable<string> Ingredient(string ingredient, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        if (ingredient == MadeSources.AnyWildSeed) return MadeSources.WildSeedIds;
        if (!int.TryParse(ingredient, out int category) || category >= 0) return new[] { Norm(ingredient) };
        return objects.Values.Where(o => o.Category == category).Select(o => o.QualifiedId);
    }

    private static IReadOnlyCollection<string> RecipeOutputs(RecipeRow recipe)
        => new[] { recipe.OutputId }.Concat(recipe.AlternateOutputIds ?? Array.Empty<string>()).Select(Norm).Distinct().ToList();

    private static IReadOnlyCollection<string> DefaultGeodeItems(string geode)
        => MadeSources.DefaultGeodeTable.TryGetValue(geode, out string[]? table) ? table : MadeSources.OtherGeodeDefault;

    private static IReadOnlyCollection<string> Ids(string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects)
        => ItemQueries.Resolve(itemIdOrQuery, objects).ItemIds.Select(Norm).Distinct().ToList();

    private static string Norm(string id) => BundleParsing.NormalizeItemId(id);
}
