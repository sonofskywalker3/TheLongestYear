using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus bug report 2026-10-12 (iroeny, 0.19.21): Weatherman's asked for a Lionfish. The
/// player runs Forager's Nooks and Crannies 2.2.1, whose "Wildflour.NooksCrannies_RainforestRetreat"
/// is entered only by a warp on Island_E and exits only to IslandEast. The rows below are its real
/// Data/Locations Fish and Forage rows (data/Locations/Rainforest_Retreat.json). Its name matches
/// no location marker, so every row went into the pools and counted as proof of reach.</summary>
public class SpawnPlaceRuleTests
{
    private const string Retreat = "Wildflour.NooksCrannies_RainforestRetreat";
    private const string ModCove = "Mod_SunnyCove";         // a modded mainland map with a door from the Beach
    private const string Grotto = "Mod_SecretGrotto";       // no warps at all: entered by a mod's own code
    private const string Attic = "Mod_Attic";               // entered by script, its one exit leads to Town

    private static readonly string[] RetreatFish = { "(O)155", "(O)130", "(O)128", "(O)267", "(O)837", "(O)SeaJelly" };
    private static readonly string[] RetreatForage =
    {
        "(O)829", "(O)830", "(O)832", "(O)834",
        "(O)Wildflour.AtelierGoods_Pistachio", "(O)Wildflour.AtelierGoods_Olive",
        "(O)Wildflour.AtelierGoods_Cashew", "(O)Wildflour.AtelierGoods_Jasmine",
    };

    /// <summary>Directed, as GameDataPools reads them (map warps plus door actions).</summary>
    private static readonly RawLocationLink[] Links =
    {
        new("Farm", "BusStop"), new("BusStop", "Farm"),
        new("BusStop", "Town"), new("Town", "BusStop"),
        new("Town", "Beach"), new("Beach", "Town"),
        new("Beach", "IslandSouth"), new("IslandSouth", "Beach"),
        new("IslandSouth", "IslandEast"), new("IslandEast", "IslandSouth"),
        // The Retreat: AddWarps on Maps/Island_E, and its own three exits back to IslandEast.
        new("IslandEast", Retreat), new(Retreat, "IslandEast"),
        new("Beach", ModCove), new(ModCove, "Beach"),
        new(Attic, "Town"),
    };

    private static readonly string[] AllLocations =
    {
        "Farm", "BusStop", "Town", "Beach", "IslandSouth", "IslandEast", Retreat, ModCove, Grotto, Attic,
    };

    private static bool Forbidden(string name)
        => ItemPoolBuilder.IsExcludedLocation(name, Array.Empty<string>());

    private static SpawnPlaceRule Rule()
        => new(ReachabilityGraph.UnreachableLocations(Links, AllLocations, Forbidden), LocationWeeks.Build(Links, Forbidden));

    private static List<RawSpawnEntry> FishRows()
    {
        var rows = RetreatFish.Select(id => new RawSpawnEntry(id, null, null, Retreat)).ToList();
        rows.Add(new RawSpawnEntry("(O)130", null, null, "Beach"));
        rows.Add(new RawSpawnEntry("(O)128", null, null, "Beach"));
        rows.Add(new RawSpawnEntry("(O)Mod_CoveFish", null, null, ModCove));
        rows.Add(new RawSpawnEntry("(O)Mod_GrottoFish", null, null, Grotto));
        rows.Add(new RawSpawnEntry("(O)Mod_AtticFish", null, null, Attic));
        rows.Add(new RawSpawnEntry("(O)Mod_FarmFish", null, null, "Farm_Standard"));
        return rows;
    }

    private static List<RawSpawnEntry> ForageRows()
    {
        var rows = RetreatForage.Select(id => new RawSpawnEntry(id, null, null, Retreat)).ToList();
        rows.Add(new RawSpawnEntry("(O)Mod_GrottoMoss", null, null, Grotto));
        rows.Add(new RawSpawnEntry("(O)16", null, null, "BusStop"));
        return rows;
    }

    private static RawObjectEntry Fish() => new("Fish", -4, 100, false, new[] { "fish_ocean", "season_all" });
    private static RawObjectEntry Forage() => new("Basic", -81, 50, false, new[] { "forage_item" });

    /// <summary>The reachability read built the way GameDataPools builds it: kept rows prove reach,
    /// a dropped fish row is a closed catch route.</summary>
    private static (SourceReachability Reach, SpawnRowSplit Fish, SpawnRowSplit Forage) Read()
    {
        SpawnPlaceRule rule = Rule();
        SpawnRowSplit fish = rule.Split(FishRows());
        SpawnRowSplit forage = rule.Split(ForageRows());
        var proof = new HashSet<string>(
            fish.Kept.Concat(forage.Kept).Select(r => BundleParsing.NormalizeItemId(r.ItemId)), StringComparer.Ordinal);
        var closedCatch = new HashSet<string>(fish.DroppedItemIds(), StringComparer.Ordinal);
        var reach = new SourceReachability(
            ReachabilityGraph.UnreachableLocations(Links, AllLocations, Forbidden),
            new List<RawShopListing>(), new List<RawShopPlacement>(), new List<RawCropEntry>(),
            new List<RawRecipeEntry>(), proof, excludedCatchIds: closedCatch);
        return (reach, fish, forage);
    }

    [Fact]
    public void AMapEnteredOnlyFromTheIsland_IsOutOfReach()
        => Assert.True(Rule().IsOutOfReach(Retreat));

    [Fact]
    public void MainlandModMap_WarplessMap_ScriptEnteredMap_AndFarmKeys_StayInReach()
    {
        SpawnPlaceRule rule = Rule();
        Assert.False(rule.IsOutOfReach(ModCove));
        Assert.False(rule.IsOutOfReach(Grotto));
        Assert.False(rule.IsOutOfReach(Attic));
        Assert.False(rule.IsOutOfReach("Farm_Standard"));
        Assert.False(rule.IsOutOfReach("Beach"));
        Assert.False(rule.IsOutOfReach("Default"));
    }

    [Fact]
    public void RainforestRetreatRows_AreDropped_AndEveryOtherRowIsKept()
    {
        var (_, fish, forage) = Read();
        Assert.All(fish.Dropped, r => Assert.Equal(Retreat, r.Location));
        Assert.All(forage.Dropped, r => Assert.Equal(Retreat, r.Location));
        Assert.Equal(RetreatFish.Length, fish.Dropped.Count);
        Assert.Equal(RetreatForage.Length, forage.Dropped.Count);
        Assert.DoesNotContain(fish.Kept, r => r.Location == Retreat);
    }

    [Fact]
    public void Lionfish_FromTheRetreatOnly_IsUnreachable()
    {
        var (reach, _, _) = Read();
        Assert.True(reach.IsUnreachable("(O)837"));
        Assert.True(reach.IsUnreachable("(O)155"));   // Super Cucumber: no reachable row in this world
        Assert.True(reach.IsUnreachable("(O)267"));
    }

    [Fact]
    public void AFishTheRetreatSharesWithAReachableMap_StaysReachable()
    {
        var (reach, _, _) = Read();
        Assert.False(reach.IsUnreachable("(O)130"));
        Assert.False(reach.IsUnreachable("(O)128"));
        Assert.False(reach.IsUnreachable("(O)Mod_CoveFish"));
        Assert.False(reach.IsUnreachable("(O)Mod_GrottoFish"));
        Assert.False(reach.IsUnreachable("(O)Mod_AtticFish"));
    }

    [Fact]
    public void ForageFromTheRetreatOnly_IsNotProof_ButIsNotACondemningRouteEither()
    {
        // Forage rows prove nothing either way once dropped: the game hands some forage out from
        // code, so a closed forage row is not evidence the item is gone (SourceReachability doc).
        var (reach, _, _) = Read();
        Assert.False(reach.IsUnreachable("(O)Wildflour.AtelierGoods_Pistachio"));
    }

    [Fact]
    public void Pools_BuiltFromKeptRows_LeaveRetreatOnlyItemsOut()
    {
        var (reach, fish, forage) = Read();
        var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal)
        {
            ["155"] = Fish(), ["130"] = Fish(), ["128"] = Fish(), ["267"] = Fish(), ["837"] = Fish(), ["SeaJelly"] = Fish(),
            ["Mod_CoveFish"] = Fish(), ["Mod_GrottoFish"] = Fish(), ["Mod_AtticFish"] = Fish(), ["Mod_FarmFish"] = Fish(),
            ["16"] = Forage(), ["Mod_GrottoMoss"] = Forage(), ["Wildflour.AtelierGoods_Pistachio"] = Forage(),
        };
        ItemPools pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, forage.Kept, fish.Kept,
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning(), reachability: reach);

        foreach (string id in new[] { "(O)155", "(O)267", "(O)837", "(O)SeaJelly" })
            Assert.DoesNotContain(pools.Fish, p => p.ItemId == id);
        foreach (string id in new[] { "(O)130", "(O)128", "(O)Mod_CoveFish", "(O)Mod_GrottoFish", "(O)Mod_AtticFish", "(O)Mod_FarmFish" })
            Assert.Contains(pools.Fish, p => p.ItemId == id);
        Assert.DoesNotContain(pools.Forage, p => p.ItemId == "(O)Wildflour.AtelierGoods_Pistachio");
        Assert.Contains(pools.Forage, p => p.ItemId == "(O)Mod_GrottoMoss");
        Assert.Contains(pools.Forage, p => p.ItemId == "(O)16");
    }

    [Fact]
    public void WithoutEitherWalk_NothingIsOutOfReach()
    {
        Assert.False(SpawnPlaceRule.Open.IsOutOfReach(Retreat));
        Assert.False(new SpawnPlaceRule(ReachabilityGraph.UnreachableLocations(Links, AllLocations, Forbidden), null).IsOutOfReach(Retreat));
        Assert.False(new SpawnPlaceRule(null, LocationWeeks.Build(Links, Forbidden)).IsOutOfReach(Retreat));
        Assert.Empty(SpawnPlaceRule.Open.Split(FishRows()).Dropped);
    }

    [Fact]
    public void AMapWithASecondDoorToTheMainland_StaysInReach()
    {
        var links = Links.Concat(new[] { new RawLocationLink("Beach", Retreat) }).ToList();
        var rule = new SpawnPlaceRule(ReachabilityGraph.UnreachableLocations(links, AllLocations, Forbidden), LocationWeeks.Build(links, Forbidden));
        Assert.False(rule.IsOutOfReach(Retreat));
    }
}
