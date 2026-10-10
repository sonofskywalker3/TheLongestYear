using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public partial class ItemPoolBuilderTests
{
    private static readonly BundleGenerationTuning Tuning = new();

    private static Dictionary<string, RawObjectEntry> Objects(params (string id, RawObjectEntry e)[] entries)
        => entries.ToDictionary(x => x.id, x => x.e);

    private static RawObjectEntry Obj(int category = -75, int price = 50, string type = "Basic",
        bool excludeFromRandomSale = false, params string[] tags)
        => new(type, category, price, excludeFromRandomSale, tags);

    private static ItemPools Build(
        IReadOnlyList<RawCropEntry>? crops = null,
        IReadOnlyDictionary<string, RawObjectEntry>? objects = null,
        IReadOnlyList<RawSpawnEntry>? forage = null,
        IReadOnlyList<RawSpawnEntry>? fish = null,
        IReadOnlySet<string>? trap = null,
        IReadOnlyList<RawMonsterDropEntry>? drops = null,
        IReadOnlyList<RawFruitTreeEntry>? fruitTrees = null,
        IReadOnlyList<RawGeodeDropEntry>? geodeDrops = null)
        => ItemPoolBuilder.Build(
            crops ?? new List<RawCropEntry>(),
            objects ?? new Dictionary<string, RawObjectEntry>(),
            forage ?? new List<RawSpawnEntry>(),
            fish ?? new List<RawSpawnEntry>(),
            trap ?? new HashSet<string>(),
            drops ?? new List<RawMonsterDropEntry>(),
            fruitTrees ?? new List<RawFruitTreeEntry>(),
            geodeDrops ?? new List<RawGeodeDropEntry>(),
            Tuning);

    [Fact]
    public void Crops_QualifiedIds_SeasonsKept_OrderedByItemId()
    {
        var pools = Build(
            crops: new[]
            {
                new RawCropEntry("192", new[] { Season.Spring }),
                new RawCropEntry("24", new[] { Season.Spring }),
            },
            objects: Objects(("192", Obj()), ("24", Obj())));
        Assert.Equal(new[] { "(O)192", "(O)24" }, pools.Crops.Select(p => p.ItemId)); // ordinal: '1' < '2'
        Assert.All(pools.Crops, p => Assert.Equal(new[] { Season.Spring }, p.Seasons));
    }

    [Fact]
    public void Vetting_QuestType_ExcludeFromRandomSale_ConfigList_AllExcluded()
    {
        var tuning = new BundleGenerationTuning();
        tuning.ExcludedItemIds.Add("(O)3");
        var pools = ItemPoolBuilder.Build(
            new[]
            {
                new RawCropEntry("1", new[] { Season.Spring }),
                new RawCropEntry("2", new[] { Season.Spring }),
                new RawCropEntry("3", new[] { Season.Spring }),
                new RawCropEntry("4", new[] { Season.Spring }),
                new RawCropEntry("5", new[] { Season.Spring }),
            },
            Objects(
                ("1", Obj(type: "Quest")),
                ("2", Obj(excludeFromRandomSale: true)),
                ("3", Obj()),
                // fish_legendary is no longer a vet rule (legendaries are wanted on the board now).
                ("4", Obj(tags: "fish_legendary")),
                ("5", Obj())),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(), tuning);
        Assert.Equal(new[] { "(O)4", "(O)5" }, pools.Crops.Select(p => p.ItemId));
    }

    [Fact]
    public void MissingObjectEntry_ItemDropped()
    {
        var pools = Build(
            crops: new[] { new RawCropEntry("24", new[] { Season.Spring }) });
        Assert.Empty(pools.Crops); // no Data/Objects entry -> unobtainable/unknown, dropped
    }

    [Fact]
    public void Weights_NumericIdVanilla_NonNumericModded_RareOverrideWins()
    {
        var tuning = new BundleGenerationTuning(); // vanilla 3 / modded 1 / (O)337 override 1
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(
                ("334", Obj(category: -15)),
                ("Mod.CopperThing", Obj(category: -15)),
                ("337", Obj(category: -15))),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(), tuning);
        var metals = pools.Metals.ToDictionary(p => p.ItemId);
        Assert.Equal(3, metals["(O)334"].Weight);
        Assert.Equal(1, metals["(O)Mod.CopperThing"].Weight);
        Assert.Equal(1, metals["(O)337"].Weight); // RareRollWeights override
    }

    [Theory]
    [InlineData("(O)Goby", 3)] [InlineData("(O)SeaJelly", 3)] [InlineData("(O)24", 3)]
    [InlineData("(O)sonofskywalker3.CartCatalog_Book", 1)] [InlineData("(O)Author.Mod_Fish", 1)]
    public void Vanilla_is_any_id_without_a_dot(string id, int weight)
        => Assert.Equal(weight, ItemPoolBuilder.WeightFor(id, new BundleGenerationTuning()));

    [Fact]
    public void MonsterDrops_DedupedAcrossMonsters_PricedFromObjects()
    {
        var pools = Build(
            objects: Objects(("766", Obj(category: -28, price: 5)), ("768", Obj(category: -28, price: 40))),
            drops: new[]
            {
                new RawMonsterDropEntry("766"), new RawMonsterDropEntry("766"),
                new RawMonsterDropEntry("768"),
            });
        Assert.Equal(2, pools.MonsterDrops.Count);
        Assert.Equal(5, pools.MonsterDrops.First(p => p.ItemId == "(O)766").Price);
    }

    [Fact]
    public void Metals_And_ArtisanGoods_ComeFromObjectCategories()
    {
        var pools = Build(objects: Objects(
            ("334", Obj(category: -15)),   // metal
            ("426", Obj(category: -26)),   // artisan
            ("24", Obj(category: -75))));  // neither
        Assert.Equal(new[] { "(O)334" }, pools.Metals.Select(p => p.ItemId));
        Assert.Equal(new[] { "(O)426" }, pools.ArtisanGoods.Select(p => p.ItemId));
    }

    [Fact]
    public void IsExcludedLocation_MatchesMarkersCaseInsensitive()
    {
        var markers = new BundleGenerationTuning().ExcludedLocationMarkers;
        Assert.True(ItemPoolBuilder.IsExcludedLocation("IslandWest", markers));
        Assert.True(ItemPoolBuilder.IsExcludedLocation("Custom_FableReef", markers));
        Assert.True(ItemPoolBuilder.IsExcludedLocation("Custom_CrimsonBadlands", markers));
        Assert.False(ItemPoolBuilder.IsExcludedLocation("Custom_ForestWest", markers));
        Assert.False(ItemPoolBuilder.IsExcludedLocation("Beach", markers));
    }

    [Fact]
    public void Artifacts_Books_Cooking_TapperGoods_DeriveByTypeAndCategory()
    {
        var pools = Build(objects: Objects(
            ("100", Obj(type: "Arch", category: 0)),
            ("Book_PriceCatalogue", Obj(category: -102)),
            ("SkillBook_0", Obj(category: -103)),
            ("194", Obj(category: -7)),
            ("724", Obj(category: -27)),
            ("24", Obj(category: -75))));
        Assert.Equal(new[] { "(O)100" }, pools.Artifacts.Select(p => p.ItemId));
        Assert.Equal(new[] { "(O)Book_PriceCatalogue", "(O)SkillBook_0" }, pools.Books.Select(p => p.ItemId));
        Assert.Equal(new[] { "(O)194" }, pools.Cooking.Select(p => p.ItemId));
        Assert.Contains("(O)724", pools.TapperGoods.Select(p => p.ItemId));
    }

    [Fact]
    public void Books_KeptOnlyWhenInTheBookWeeksTable()
    {
        var pools = Build(objects: Objects(
            ("Book_Void", Obj(category: -102, tags: new[] { "book_item" })),
            ("Book_PriceCatalogue", Obj(category: -102, tags: new[] { "book_item" }))));
        Assert.Equal(new[] { "(O)Book_PriceCatalogue" }, pools.Books.Select(p => p.ItemId));
    }

    [Fact]
    public void Saplings_FromFruitTrees_BananaMangoExcludedByDefaultTuning()
    {
        var tuning = new BundleGenerationTuning();
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("628", Obj(category: -74)), ("69", Obj(category: -74)), ("835", Obj(category: -74))),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new[] { new RawFruitTreeEntry("628"), new RawFruitTreeEntry("69"), new RawFruitTreeEntry("835") },
            new List<RawGeodeDropEntry>(), tuning);
        Assert.Equal(new[] { "(O)628" }, pools.Saplings.Select(p => p.ItemId));
    }

    [Fact]
    public void GeodeMinerals_FromDrops_GemCategoryExcluded()
    {
        var pools = Build(
            objects: Objects(("86", Obj(category: -12)), ("60", Obj(category: -2))),
            geodeDrops: new[] { new RawGeodeDropEntry("86"), new RawGeodeDropEntry("60") });
        Assert.Contains("(O)86", pools.GeodeMinerals.Select(p => p.ItemId));
        Assert.DoesNotContain("(O)60", pools.GeodeMinerals.Select(p => p.ItemId));
    }

    [Fact]
    public void CropPoolAdditions_TeaLeavesJoinSpringSummerFall()
    {
        var pools = Build(objects: Objects(("815", Obj(category: -75))));
        var tea = pools.Crops.FirstOrDefault(p => p.ItemId == "(O)815");
        Assert.NotNull(tea);
        Assert.Equal(new[] { Season.Spring, Season.Summer, Season.Fall }, tea!.Seasons);
    }

    [Fact]
    public void QualityEligible_CropHarvests_RodFish_SpawnedForage_OnlyThose()
    {
        var pools = Build(
            crops: new[] { new RawCropEntry("24", new[] { Season.Spring }) },
            objects: Objects(
                ("24", Obj(category: -75)),                       // Parsnip (crop)
                ("128", Obj(type: "Fish", category: -4)),         // Pufferfish (rod)
                ("RiverJelly", Obj(type: "Fish", category: -4)),  // jelly (rod, never quality)
                ("715", Obj(type: "Fish", category: -4)),         // Lobster (trap)
                ("16", Obj(category: -81)),                       // Wild Horseradish (forage spawn, Greens)
                ("771", Obj(category: -16)),                       // Fiber spawn with a non-forage category
                ("430", Obj(category: 0)),                        // Truffle: special-cased by the game
                ("815", Obj(category: -81))),                     // Tea Leaves: curated addition only
            forage: new[]
            {
                new RawSpawnEntry("(O)16", Season.Spring, null, "Forest"),
                new RawSpawnEntry("(O)771", null, null, "Forest"),
                new RawSpawnEntry("(O)430", Season.Fall, null, "Forest"),
            },
            fish: new[]
            {
                new RawSpawnEntry("(O)128", Season.Summer, null, "Beach"),
                new RawSpawnEntry("(O)RiverJelly", null, null, "Town"),
                new RawSpawnEntry("(O)715", null, null, "Beach"),
            },
            trap: new HashSet<string> { "715" });

        var eligible = pools.QualityEligibleIds!;
        Assert.Contains("(O)24", eligible);
        Assert.Contains("(O)128", eligible);
        Assert.Contains("(O)16", eligible);
        Assert.Contains("(O)430", eligible);
        Assert.DoesNotContain("(O)RiverJelly", eligible);
        Assert.DoesNotContain("(O)715", eligible);
        Assert.DoesNotContain("(O)771", eligible);
        Assert.DoesNotContain("(O)815", eligible);   // in the Crops pool via the curated CropPoolAdditions list, still not eligible
        Assert.Contains(pools.Crops, p => p.ItemId == "(O)815");
    }

    [Fact]
    public void QualityEligible_CropWithHarvestMaxQualityZero_IsNotEligible()
    {
        var pools = Build(
            crops: new[]
            {
                new RawCropEntry("771", new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter },
                    HarvestMaxQuality: 0),                        // Fiber: quality clamped to base by CropData
                new RawCropEntry("24", new[] { Season.Spring }),  // Parsnip: uncapped (null), stays eligible
            },
            objects: Objects(
                ("771", Obj(category: -16)),
                ("24", Obj(category: -75))));

        Assert.Contains(pools.Crops, p => p.ItemId == "(O)771");
        Assert.DoesNotContain("(O)771", pools.QualityEligibleIds!);
        Assert.Contains("(O)24", pools.QualityEligibleIds!);
    }

    [Fact]
    public void QualityEligible_ForageTagCountsLikeCategory()
    {
        var pools = Build(
            objects: Objects(("999", Obj(category: 0, tags: "forage_item"))),
            forage: new[] { new RawSpawnEntry("(O)999", null, null, "Forest") });
        Assert.Contains("(O)999", pools.QualityEligibleIds!);
    }

    [Fact]
    public void IsJelly_MatchesTheThreeJellies()
    {
        Assert.True(ItemPoolBuilder.IsJelly("(O)RiverJelly"));
        Assert.True(ItemPoolBuilder.IsJelly("(O)SeaJelly"));
        Assert.True(ItemPoolBuilder.IsJelly("(O)CaveJelly"));
        Assert.False(ItemPoolBuilder.IsJelly("(O)128"));
    }

    [Fact]
    public void HandBuiltPools_HaveNullEligibility()
        => Assert.Null(new ItemPools().QualityEligibleIds);
}
