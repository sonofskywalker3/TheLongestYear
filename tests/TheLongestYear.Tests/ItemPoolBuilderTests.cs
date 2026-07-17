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
        IReadOnlyList<RawMonsterDropEntry>? drops = null)
        => ItemPoolBuilder.Build(
            crops ?? new List<RawCropEntry>(),
            objects ?? new Dictionary<string, RawObjectEntry>(),
            forage ?? new List<RawSpawnEntry>(),
            fish ?? new List<RawSpawnEntry>(),
            trap ?? new HashSet<string>(),
            drops ?? new List<RawMonsterDropEntry>(),
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
    public void Vetting_QuestType_ExcludeFromRandomSale_LegendaryTag_ConfigList_AllExcluded()
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
                ("4", Obj(tags: "fish_legendary")),
                ("5", Obj())),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(), tuning);
        Assert.Equal(new[] { "(O)5" }, pools.Crops.Select(p => p.ItemId));
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
            new HashSet<string>(), new List<RawMonsterDropEntry>(), tuning);
        var metals = pools.Metals.ToDictionary(p => p.ItemId);
        Assert.Equal(3, metals["(O)334"].Weight);
        Assert.Equal(1, metals["(O)Mod.CopperThing"].Weight);
        Assert.Equal(1, metals["(O)337"].Weight); // RareRollWeights override
    }

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
}
