using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;
using Xunit.Abstractions;

namespace TheLongestYear.Tests;

/// <summary>The incremental build over a base answer gives exactly the cold build's answers for the
/// live data, item by item and source by source (Inputs included), while recomputing only the dirty
/// closure; the closing full pass must find nothing left to repair.</summary>
public sealed class IncrementalBuilderTests
{
    private static readonly string ShippedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "lexicon", "vanilla.json");
    private readonly ITestOutputHelper _output;

    public IncrementalBuilderTests(ITestOutputHelper output) => _output = output;

    // ---- small synthetic data ----

    private static ObjInfo Obj(string id, int category = 0, params string[] tags)
        => new(id, id, category, 10, tags, false);

    private static LocationSpawn Forage(string id, Season? season = null)
        => new("Forest", id, season, null, 1.0, 0, false, 0);

    private static ShopRow Shop(string id) => new("SeedShop", id, null, false);

    private static ObtainabilityInputs Inputs(
        IEnumerable<ObjInfo> objects, IEnumerable<LocationSpawn>? forage = null, IEnumerable<ShopRow>? shops = null,
        IEnumerable<CropRow>? crops = null, IEnumerable<MachineRow>? machines = null, IEnumerable<RecipeRow>? recipes = null)
        => new()
        {
            Objects = objects.ToDictionary(o => o.QualifiedId, o => o, StringComparer.Ordinal),
            Forage = (forage ?? Array.Empty<LocationSpawn>()).ToList(),
            Shops = (shops ?? Array.Empty<ShopRow>()).ToList(),
            Crops = (crops ?? Array.Empty<CropRow>()).ToList(),
            Machines = (machines ?? Array.Empty<MachineRow>()).ToList(),
            Recipes = (recipes ?? Array.Empty<RecipeRow>()).ToList(),
        };

    /// <summary>A base answer made the way the shipped one was: a cold build of the base data.</summary>
    private static IncrementalSeed SeedOf(ObtainabilityInputs baseInputs)
    {
        ObtainabilityBuild cold = ObtainabilityBuilder.Build(baseInputs);
        AnswerVariant variant = AnswerVariantFactory.From(cold, cold, baseInputs, null, Array.Empty<string>(), null);
        return new IncrementalSeed(variant, RowHashes.Of(baseInputs, false));
    }

    private static void AssertSameAsCold(ObtainabilityBuild incremental, ObtainabilityBuild cold)
    {
        Assert.False(cold.HitPassCap);
        Assert.False(incremental.HitPassCap);
        var ids = cold.Model.ItemIds.Concat(incremental.Model.ItemIds).Distinct().OrderBy(i => i, StringComparer.Ordinal).ToList();
        var wrong = new List<string>();
        foreach (string id in ids)
        {
            IReadOnlyList<ObtainSource> a = cold.Model.Sources(id), b = incremental.Model.Sources(id);
            bool same = a.SequenceEqual(b) && a.Zip(b).All(p => p.First.Inputs.Count == p.Second.Inputs.Count
                && p.First.Inputs.Zip(p.Second.Inputs).All(g => g.First.SequenceEqual(g.Second)));
            if (!same) wrong.Add($"{id}: cold [{string.Join("; ", a.Select(s => s.Detail))}] vs incremental [{string.Join("; ", b.Select(s => s.Detail))}]");
        }
        Assert.True(wrong.Count == 0, "Differs from cold:\n" + string.Join("\n", wrong.Take(20)));
        Assert.Equal(ObtainabilityGolden.Hash(cold.Model), ObtainabilityGolden.Hash(incremental.Model));
        Assert.Equal(cold.Unresolved, incremental.Unresolved);
    }

    private static ObtainabilityBuild Incremental(ObtainabilityInputs live, IncrementalSeed seed, out IncrementalReport report)
    {
        ObtainabilityBuild build = IncrementalBuilder.BuildWithReport(live, seed, out report);
        Assert.Equal(0, report.RepairPasses);   // the dirty set alone was right; the closing pass changed nothing
        return build;
    }

    [Fact]
    public void NewCropOnlyRecomputesItsClosure()
    {
        var baseObjects = new[] { Obj("(O)SeedA"), Obj("(O)CropA"), Obj("(O)C"), Obj("(O)D") };
        ObtainabilityInputs baseInputs = Inputs(baseObjects,
            forage: new[] { Forage("(O)C"), Forage("(O)D", Season.Summer) },
            shops: new[] { Shop("(O)SeedA") },
            crops: new[] { new CropRow("(O)SeedA", "(O)CropA", new[] { Season.Spring }, 4, 0) });
        ObtainabilityInputs live = baseInputs with
        {
            Objects = WithObjects(baseInputs, Obj("(O)SeedE"), Obj("(O)CropE")),
            Shops = baseInputs.Shops.Append(Shop("(O)SeedE")).ToList(),
            Crops = baseInputs.Crops.Append(new CropRow("(O)SeedE", "(O)CropE", new[] { Season.Summer }, 6, 0)).ToList(),
        };

        ObtainabilityBuild inc = Incremental(live, SeedOf(baseInputs), out IncrementalReport report);
        ObtainabilityBuild cold = ObtainabilityBuilder.Build(live);
        AssertSameAsCold(inc, cold);
        int total = cold.Model.ItemIds.Concat(live.Objects.Keys).Distinct().Count();
        Assert.True(report.DirtyCount < total, $"{report.DirtyCount} dirty of {total}");
        Assert.Contains("(O)CropE", report.Dirty);
        Assert.DoesNotContain("(O)CropA", report.Dirty);
    }

    private static IReadOnlyDictionary<string, ObjInfo> WithObjects(ObtainabilityInputs inputs, params ObjInfo[] extra)
    {
        var all = new Dictionary<string, ObjInfo>(inputs.Objects, StringComparer.Ordinal);
        foreach (ObjInfo o in extra) all[o.QualifiedId] = o;
        return all;
    }

    [Fact]
    public void RemovedRouteInCycleMatchesCold()
    {
        // A (a seed) from the Seed Maker fed B; B grown from A; a shop sells A. Without the shop, the
        // cycle has no way in, so neither lands: a build that kept A or B from the base would be wrong.
        var objects = new[] { Obj("(O)A"), Obj("(O)B"), Obj("(O)C") };
        var seedMaker = new MachineRow("(BC)25", "(O)B", Array.Empty<string>(), null,
            new[] { new MachineOutput(null, null, "OutputSeedMaker", false, OutputMethodKind.SeedMaker) }, 0, 1);
        ObtainabilityInputs baseInputs = Inputs(objects,
            forage: new[] { Forage("(O)C") },
            shops: new[] { Shop("(O)A") },
            crops: new[] { new CropRow("(O)A", "(O)B", Array.Empty<Season>(), 4, 0) },
            machines: new[] { seedMaker });
        IncrementalSeed seed = SeedOf(baseInputs);
        Assert.NotEmpty(seed.Base.Obtain["(O)B"]);
        ObtainabilityInputs live = baseInputs with { Shops = Array.Empty<ShopRow>() };

        ObtainabilityBuild inc = Incremental(live, seed, out IncrementalReport report);
        ObtainabilityBuild cold = ObtainabilityBuilder.Build(live);
        AssertSameAsCold(inc, cold);
        Assert.Empty(inc.Model.Sources("(O)A"));
        Assert.Empty(inc.Model.Sources("(O)B"));
        Assert.DoesNotContain("(O)C", report.Dirty);
    }

    [Fact]
    public void NewCategoryMemberMatchesCold()
    {
        // A recipe taking any -75 item; the base's only -75 item lands in summer, the new one on day 1.
        var objects = new[] { Obj("(O)V1", -75), Obj("(O)Dish"), Obj("(O)C") };
        var recipe = new RecipeRow("Veg Dish", new[] { "-75" }, "(O)Dish", "default", true);
        ObtainabilityInputs baseInputs = Inputs(objects,
            forage: new[] { Forage("(O)V1", Season.Summer), Forage("(O)C") },
            recipes: new[] { recipe });
        IncrementalSeed seed = SeedOf(baseInputs);
        ObtainabilityInputs live = baseInputs with
        {
            Objects = WithObjects(baseInputs, Obj("(O)V2", -75)),
            Forage = baseInputs.Forage.Append(Forage("(O)V2")).ToList(),
        };

        ObtainabilityBuild inc = Incremental(live, seed, out IncrementalReport report);
        ObtainabilityBuild cold = ObtainabilityBuilder.Build(live);
        AssertSameAsCold(inc, cold);
        Assert.Equal(cold.Model.Lands("(O)Dish", 1, ObtainFilter.Any), inc.Model.Lands("(O)Dish", 1, ObtainFilter.Any));
        Assert.NotEqual(seed.Base.ObtainModel().Lands("(O)Dish", 1, ObtainFilter.Any), inc.Model.Lands("(O)Dish", 1, ObtainFilter.Any));
        Assert.Contains("(O)Dish", report.Dirty);
    }

    [Fact]
    public void NewMachineTagMatchesCold()
    {
        // A machine taking anything tagged "mod_fruit": nothing in the base has the tag, so the base has
        // no route for its output and no Inputs edge to follow; the live data adds a tagged item.
        var objects = new[] { Obj("(O)Out"), Obj("(O)C", 0, "other") };
        var press = new MachineRow("(BC)Press", null, new[] { "mod_fruit" }, null,
            new[] { new MachineOutput("(O)Out", null, null) }, 0, 2);
        ObtainabilityInputs baseInputs = Inputs(objects, forage: new[] { Forage("(O)C") }, machines: new[] { press });
        IncrementalSeed seed = SeedOf(baseInputs);
        Assert.Empty(seed.Base.Obtain["(O)Out"]);
        ObtainabilityInputs live = baseInputs with
        {
            Objects = WithObjects(baseInputs, Obj("(O)Fruit", -79, "mod_fruit")),
            Forage = baseInputs.Forage.Append(Forage("(O)Fruit", Season.Fall)).ToList(),
        };

        ObtainabilityBuild inc = Incremental(live, seed, out _);
        ObtainabilityBuild cold = ObtainabilityBuilder.Build(live);
        AssertSameAsCold(inc, cold);
        Assert.NotEmpty(inc.Model.Sources("(O)Out"));
    }

    // ---- the vanilla fixture plus a synthetic mod ----

    private const string SalmonDinner = "(O)212";

    /// <summary>The fixture plus 30 rows a content pack might add: 10 objects (four seeds, four
    /// fruit, a fish and a pond product), four crops, four Pierre rows for the seeds, four summer
    /// forage rows and four artifact spot rows for the fruit, Salmon Dinner rewritten to need the first
    /// new fruit, a lake fish row with its Data/Fish row, and a pond for the new fish.</summary>
    private static ObtainabilityInputs WithSyntheticMod(ObtainabilityInputs vanilla)
    {
        var objects = new Dictionary<string, ObjInfo>(vanilla.Objects, StringComparer.Ordinal);
        var crops = vanilla.Crops.ToList();
        var shops = vanilla.Shops.ToList();
        var forage = vanilla.Forage.ToList();
        var spots = vanilla.ArtifactSpots.ToList();
        Season[] seasons = { Season.Spring, Season.Summer, Season.Fall, Season.Spring };
        for (int i = 0; i < 4; i++)
        {
            string seed = $"(O)SynthMod.Seed{i}", fruit = $"(O)SynthMod.Fruit{i}";
            objects[seed] = new ObjInfo(seed, $"Synth Seed {i}", -74, 20, new[] { "category_seeds" }, false);
            objects[fruit] = new ObjInfo(fruit, $"Synth Fruit {i}", -79, 80,
                new[] { "category_fruits", $"id_o_synthmod.fruit{i}" }, false);
            crops.Add(new CropRow(seed, fruit, new[] { seasons[i] }, 5 + i, i == 3 ? 3 : 0));
            shops.Add(new ShopRow("SeedShop", seed, null, false));
            forage.Add(new LocationSpawn("Forest", fruit, Season.Summer, null, 0.2, 0, false, 0));
            spots.Add(new ArtifactSpotRow("Town", fruit, null, 0.05));
        }
        const string fish = "(O)SynthMod.Fish", pearl = "(O)SynthMod.Pearl";
        objects[fish] = new ObjInfo(fish, "Synth Fish", -4, 60, new[] { "category_fish", "synthmod_fish" }, false);
        objects[pearl] = new ObjInfo(pearl, "Synth Pearl", 0, 300, Array.Empty<string>(), false);

        var recipes = vanilla.Recipes.ToList();
        int salmon = recipes.FindIndex(r => r.OutputId == SalmonDinner);
        Assert.True(salmon >= 0, "the fixture has no Salmon Dinner recipe");
        recipes[salmon] = recipes[salmon] with { Ingredients = recipes[salmon].Ingredients.Append("(O)SynthMod.Fruit0").ToList() };

        var fishRows = new Dictionary<string, FishRow>(vanilla.FishRows, StringComparer.Ordinal)
        {
            [fish] = new FishRow(fish, false, "both", 0, "600 2600"),
        };
        var locationFish = vanilla.LocationFish.Append(new LocationSpawn("Mountain", fish, Season.Summer, null, 0.3, 0, false, 0)).ToList();
        var ponds = vanilla.Ponds.Append(new PondRow("SynthMod.Pond", new[] { "synthmod_fish" }, 0,
            new[] { new PondProduct(pearl, 3, 0.5, null) })).ToList();

        return vanilla with
        {
            Objects = objects, Crops = crops, Shops = shops, Forage = forage, ArtifactSpots = spots,
            Recipes = recipes, FishRows = fishRows, LocationFish = locationFish, Ponds = ponds,
        };
    }

    [Fact]
    public void FixturePlusSyntheticModMatchesCold()
    {
        AnswerSet shipped = LexiconJson.Read(File.ReadAllText(ShippedPath));
        AnswerVariant baseline = shipped.Resolve(AnswerSet.BaseVariant)!;
        var seed = new IncrementalSeed(baseline, shipped.RowHashesFor(AnswerSet.BaseVariant));
        ObtainabilityInputs live = WithSyntheticMod(VanillaInputsFixture.Load());

        // One untimed run of each first, so the timings below compare warm code (the game has already
        // hashed the live rows by the time it builds, which warms the JSON serialiser).
        IncrementalBuilder.Build(live, seed, out int _);
        ObtainabilityBuilder.Build(live);
        var watch = Stopwatch.StartNew();
        ObtainabilityBuild inc = Incremental(live, seed, out IncrementalReport report);
        long incMs = watch.ElapsedMilliseconds;
        watch.Restart();
        ObtainabilityBuild cold = ObtainabilityBuilder.Build(live);
        long coldMs = watch.ElapsedMilliseconds;

        AssertSameAsCold(inc, cold);
        Assert.Equal(cold.Unresolved, inc.Unresolved);
        int total = cold.Model.ItemIds.Concat(live.Objects.Keys.Select(BundleParsing.NormalizeItemId)).Distinct().Count();
        _output.WriteLine($"dirty {report.DirtyCount} of {total} items ({100.0 * report.DirtyCount / total:F1}%), " +
            $"{report.RowsEvaluated} of {report.RowsTotal} derived rows per pass, {report.Passes} filtered passes; " +
            $"incremental {incMs} ms (dirty set {report.DirtyMs}, passes {report.PassMs}, check {report.CheckMs}), cold {coldMs} ms ({cold.Passes} passes)");
        Assert.True(report.DirtyCount < total * 0.4, $"{report.DirtyCount} dirty of {total}");
        Assert.Contains(SalmonDinner, report.Dirty);
        Assert.Contains("(O)SynthMod.Pearl", report.Dirty);
    }

    [Fact]
    public void FixtureWithNoChangeHasNothingDirty()
    {
        AnswerSet shipped = LexiconJson.Read(File.ReadAllText(ShippedPath));
        var seed = new IncrementalSeed(shipped.Resolve(AnswerSet.BaseVariant)!, shipped.RowHashesFor(AnswerSet.BaseVariant));
        ObtainabilityBuild inc = Incremental(VanillaInputsFixture.Load(), seed, out IncrementalReport report);
        Assert.Equal(0, report.DirtyCount);
        Assert.Equal(ObtainabilityGoldenTests.VanillaHash, ObtainabilityGolden.Hash(inc.Model));
    }

    [Fact]
    public void YearSortOverTheShippedYearSortMatchesCold()
    {
        AnswerSet shipped = LexiconJson.Read(File.ReadAllText(ShippedPath));
        var seed = new IncrementalSeed(shipped.Resolve(AnswerSet.BaseVariant)!, shipped.RowHashesFor(AnswerSet.BaseVariant));
        ObtainabilityInputs live = WithSyntheticMod(VanillaInputsFixture.Load());
        IncrementalBuilder.BuildWithReport(live, seed, out IncrementalReport main);
        ObtainabilityInputs stripped = YearTwoItems.WithoutCartRandomStock(live);

        ObtainabilityBuild inc = IncrementalBuilder.BuildYearSort(stripped, seed, main.Dirty, out IncrementalReport report);
        Assert.Equal(0, report.RepairPasses);
        AssertSameAsCold(inc, ObtainabilityBuilder.Build(stripped));
    }
}
