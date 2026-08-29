using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemPoolBuilderTests
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

    /// <summary>Jeff, 2026-08-28: Midnight Squid, Spook Fish and Blobfish "should be valid
    /// options" for Night Fishing. They carry ExcludeFromRandomSale in Data/Objects (the game keeps
    /// them out of random shop stock), and the vet dropped them with everything else so flagged.
    /// A fish with a Night Market (Submarine) spawn row is a market fish: it stays in the pool,
    /// and only its festival rows count, because its Beach rows are gated in code, not data, and
    /// would otherwise read as an all-year beach catch.</summary>
    [Fact]
    public void Fish_NightMarketFish_KeptDespiteExcludeFromRandomSale_SeasonWinter_SubmarineOnly()
    {
        var pools = Build(
            fish: new[]
            {
                new RawSpawnEntry("(O)800", null, null, "Beach"),
                new RawSpawnEntry("(O)800", null, null, "Submarine"),
                new RawSpawnEntry("(O)898", null, "PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", "Beach"),
            },
            objects: Objects(
                ("800", Obj(category: -4, type: "Fish", excludeFromRandomSale: true)),   // Blobfish
                ("898", Obj(category: -4, type: "Fish", excludeFromRandomSale: true))));  // Son of Crimsonfish: no market row, stays out
        var blob = Assert.Single(pools.Fish);
        Assert.Equal("(O)800", blob.ItemId);
        Assert.Equal(new[] { Season.Winter }, blob.Seasons);
        Assert.Equal(new[] { "Submarine" }, blob.Locations);
        Assert.Equal(Season.Winter, pools.DerivedSeasonPins["(O)800"]);
    }

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
    public void FishPool_RequiresObjectTypeFish_JunkSpawnsExcluded()
    {
        var pools = Build(
            objects: Objects(("128", Obj(category: -4, type: "Fish")), ("388", Obj(category: -16))),
            fish: new[]
            {
                new RawSpawnEntry("(O)128", null, null, "Beach"),
                new RawSpawnEntry("(O)388", null, null, "Town"), // wood in a fish table — junk
            });
        Assert.Equal(new[] { "(O)128" }, pools.Fish.Select(p => p.ItemId));
    }

    [Fact]
    public void MonsterPool_RequiresMonsterLootCategory_OtherDropsExcluded()
    {
        var pools = Build(
            objects: Objects(("768", Obj(category: -28)), ("80", Obj(category: -2))),
            drops: new[] { new RawMonsterDropEntry("768"), new RawMonsterDropEntry("80") });
        Assert.Equal(new[] { "(O)768" }, pools.MonsterDrops.Select(p => p.ItemId));
    }

    [Fact]
    public void Fish_SeasonsUnionAcrossSpawns_LocationsCollected_TrapSeparated()
    {
        var pools = Build(
            objects: Objects(("128", Obj(category: -4, type: "Fish")), ("715", Obj(category: -4, type: "Fish"))),
            fish: new[]
            {
                new RawSpawnEntry("(O)128", Season.Summer, null, "Beach"),
                new RawSpawnEntry("(O)128", Season.Fall, null, "Forest"),
                new RawSpawnEntry("(O)715", null, null, "Beach"),
            },
            trap: new HashSet<string> { "715" });
        var fish = Assert.Single(pools.Fish);
        Assert.Equal("(O)128", fish.ItemId);
        Assert.Equal(new[] { Season.Summer, Season.Fall }, fish.Seasons);
        Assert.Equal(new[] { "Beach", "Forest" }, fish.Locations);
        Assert.Equal("(O)715", Assert.Single(pools.CrabPot).ItemId);
    }

    [Fact]
    public void SeasonsFromSpawn_ConditionTokensParsed_NoSignalMeansAllSeasons()
    {
        Assert.Equal(new[] { Season.Winter }, ItemPoolBuilder.SeasonsFromSpawn(Season.Winter, null));
        Assert.Equal(new[] { Season.Spring, Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "LOCATION_SEASON Here spring summer"));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, null));               // empty = any
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "PLAYER_HAS_MAIL x")); // no season signal
    }

    /// <summary>Player report 2026-08-28: a Sea Cucumber (Fall/Winter at the beach) was demanded
    /// before Summer 1. Its Submarine row carries no season because the game gates the Night
    /// Market by date in code, so the pool read it as all-year. Rows for a passive festival's
    /// own maps (Submarine, BeachNightMarket) and rows conditioned on
    /// IS_PASSIVE_FESTIVAL_OPEN take that festival's season from Data/PassiveFestivals.</summary>
    [Fact]
    public void SeasonsFromSpawn_PassiveFestivalRows_TakeTheFestivalSeason()
    {
        var festivals = new Dictionary<string, Season> { ["NightMarket"] = Season.Winter, ["SquidFest"] = Season.Winter, ["TroutDerby"] = Season.Summer };
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, null, "Submarine", festivals));
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, null, "BeachNightMarket", festivals));
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, "IS_PASSIVE_FESTIVAL_OPEN SquidFest, TIME 0600 1800", "Beach", festivals));
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "IS_PASSIVE_FESTIVAL_OPEN TroutDerby", "Forest", festivals));
        // An explicit season or season tokens still win; an unknown festival is no signal.
        Assert.Equal(new[] { Season.Fall },
            ItemPoolBuilder.SeasonsFromSpawn(Season.Fall, "IS_PASSIVE_FESTIVAL_OPEN SquidFest", "Beach", festivals));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "IS_PASSIVE_FESTIVAL_OPEN ModFest", "Beach", festivals));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, null, "Beach", festivals));
        // Without festival data the Night Market maps still read as Winter (built-in fallback).
        Assert.Equal(new[] { Season.Winter }, ItemPoolBuilder.SeasonsFromSpawn(null, null, "Submarine"));
    }

    [Fact]
    public void FishPool_NightMarketFish_IsWinterNotAnySeason_AndPinnedFall()
    {
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("154", Obj(category: -4, price: 75, type: "Fish")), ("800", Obj(category: -4, price: 500, type: "Fish"))),
            new List<RawSpawnEntry>(),
            new[]
            {
                new RawSpawnEntry("154", null, "LOCATION_SEASON Here fall winter", "Beach"),
                new RawSpawnEntry("154", null, null, "Submarine"),
                new RawSpawnEntry("800", null, null, "Submarine"),
            },
            new HashSet<string>(), new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(),
            new List<RawGeodeDropEntry>(), Tuning,
            festivalSeasons: new Dictionary<string, Season> { ["NightMarket"] = Season.Winter });
        Assert.Equal(new[] { Season.Fall, Season.Winter }, pools.Fish.Single(p => p.ItemId == "(O)154").Seasons); // Sea Cucumber
        Assert.Equal(new[] { Season.Winter }, pools.Fish.Single(p => p.ItemId == "(O)800").Seasons);              // Blobfish
        Assert.Equal(Season.Fall, pools.DerivedSeasonPins["(O)154"]);
        Assert.Equal(Season.Winter, pools.DerivedSeasonPins["(O)800"]);
    }

    [Fact]
    public void SeasonsFromSpawn_NegatedCondition_TreatedAsNoSignal()
    {
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!LOCATION_SEASON Here winter"));
    }

    /// <summary>Winter Root and Snow Yam are dug up, not picked, so they have no Data/Locations
    /// forage row and never reached the Winter pool; vanilla's own Winter Foraging bundle asks
    /// for both. Built-in (not a tuning default) because a saved config.json replaces the
    /// SeasonalForageAdditions dictionary wholesale.</summary>
    [Fact]
    public void BuiltInWinterAdditions_WinterRootAndSnowYam_JoinWinterPool_EvenWithEmptiedTuning()
    {
        var emptied = new BundleGenerationTuning { SeasonalForageAdditions = new() };
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("412", Obj(category: -81, price: 70)), ("416", Obj(category: -81, price: 100))),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(), new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            emptied);
        Assert.Equal(new[] { Season.Winter }, pools.Forage.Single(p => p.ItemId == "(O)412").Seasons);
        Assert.Equal(new[] { Season.Winter }, pools.Forage.Single(p => p.ItemId == "(O)416").Seasons);
        Assert.Equal(Season.Winter, pools.DerivedSeasonPins["(O)412"]);
    }

    [Fact]
    public void ForageAdditions_FromTuning_JoinTheSeasonPool()
    {
        // Default tuning adds (O)404/(O)420 to Spring; give them object entries so they vet in.
        var pools = Build(
            objects: Objects(("404", Obj(category: -81)), ("420", Obj(category: -81)),
                             ("16", Obj(category: -81))),
            forage: new[] { new RawSpawnEntry("(O)16", Season.Spring, null, "Forest") });
        var ids = pools.Forage.Select(p => p.ItemId).ToList();
        Assert.Contains("(O)16", ids);
        Assert.Contains("(O)404", ids);
        Assert.Contains("(O)420", ids);
        Assert.Contains(Season.Spring, pools.Forage.First(p => p.ItemId == "(O)404").Seasons);
    }

    [Fact]
    public void DerivedSeasonPins_LaterThanSpringOnly()
    {
        var pools = Build(
            objects: Objects(("128", Obj(category: -4, type: "Fish")), ("129", Obj(category: -4, type: "Fish")),
                             ("130", Obj(category: -4, type: "Fish"))),
            fish: new[]
            {
                new RawSpawnEntry("(O)128", Season.Fall, null, "Beach"),   // earliest Fall -> pinned
                new RawSpawnEntry("(O)129", Season.Spring, null, "Beach"), // Spring -> no pin
                new RawSpawnEntry("(O)130", null, null, "Beach"),          // any season -> no pin
            });
        Assert.Equal(Season.Fall, pools.DerivedSeasonPins["(O)128"]);
        Assert.False(pools.DerivedSeasonPins.ContainsKey("(O)129"));
        Assert.False(pools.DerivedSeasonPins.ContainsKey("(O)130"));
    }

    [Fact]
    public void DerivedSeasonPins_IncludeSeasonLimitedTrapFish()
    {
        var pools = Build(
            objects: Objects(("715", Obj(category: -4, type: "Fish"))),
            fish: new[] { new RawSpawnEntry("(O)715", Season.Fall, null, "Beach") },
            trap: new HashSet<string> { "715" });
        Assert.Equal(Season.Fall, pools.DerivedSeasonPins["(O)715"]);
    }

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
