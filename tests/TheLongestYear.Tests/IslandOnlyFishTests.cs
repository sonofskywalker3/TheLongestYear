using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus bug report 2026-10-12 (iroeny, 0.19.21): "Weathermans bundle requires Lionfish
/// (Ginger Island)". In the live vanilla Data/Locations the island fish have rows only on island
/// maps (Lionfish: IslandSouth, IslandSouthEast, IslandWest; Blue Discus: IslandNorth, IslandWest;
/// Stingray: IslandSouthEastCave), and Slimejack only in the Mutant Bug Lair (BugLand), so the
/// location markers keep them out of a vanilla roll. But the only thing standing between them and
/// a board was where the rows said they bite: another mod rowing one of them on a map of its own
/// (a tropical cove, a modded beach) put it straight back in the fish pool, and the load-time
/// repair only swapped what the reachability read condemned, so an island fish already on a board
/// stayed whenever that read vouched for it or failed. They are banned by id now, like the island
/// crops, and the repair swaps every banned id it finds.</summary>
public class IslandOnlyFishTests
{
    private static readonly string[] IslandOnlyFish = { "(O)836", "(O)837", "(O)838", "(O)796" };

    private static RawObjectEntry Fish() => new("Fish", -4, 100, false, new[] { "fish_ocean", "season_all" });

    /// <summary>Real-shaped pool input: six Beach fish, plus a modded map (no "Island" in its key,
    /// so no marker excludes it) that rows Lionfish, Stingray, Blue Discus and Slimejack. The island
    /// rows themselves never reach this list (GameDataPools drops excluded maps), exactly as in game.</summary>
    private static ItemPools PoolsWithModdedTropicalCove()
    {
        var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal)
        {
            ["128"] = Fish(), ["129"] = Fish(), ["130"] = Fish(), ["131"] = Fish(),
            ["147"] = Fish(), ["150"] = Fish(),
            ["836"] = Fish(), ["837"] = Fish(), ["838"] = Fish(), ["796"] = Fish(),
        };
        var spawns = new List<RawSpawnEntry>();
        foreach (string id in new[] { "128", "129", "130", "131", "147", "150" })
            spawns.Add(new RawSpawnEntry($"(O){id}", null, null, "Beach"));
        foreach (string id in new[] { "836", "837", "838", "796" })
            spawns.Add(new RawSpawnEntry($"(O){id}", null, "LOCATION_Season Here Fall Spring Summer Winter", "Custom_TropicalCove"));
        return ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, new List<RawSpawnEntry>(), spawns,
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
    }

    [Fact]
    public void IslandOnlyFish_AreBuiltInBans()
    {
        foreach (string id in IslandOnlyFish)
            Assert.Contains(id, ItemPoolBuilder.BuiltInExcludedItemIds);
    }

    [Fact]
    public void AModMapRowingIslandFish_DoesNotPutThemInTheFishPool()
    {
        ItemPools pools = PoolsWithModdedTropicalCove();
        Assert.DoesNotContain(pools.Fish, p => IslandOnlyFish.Contains(p.ItemId));
        Assert.Contains(pools.Fish, p => p.ItemId == "(O)128");
        foreach (string id in IslandOnlyFish)
            Assert.Contains(id, pools.ExcludedIds);
    }

    [Fact]
    public void Weathermans_NeverAsksForAnIslandFish()
    {
        ItemPools pools = PoolsWithModdedTropicalCove();
        AuthoredBundleDef weathermans = AuthoredBundleCatalog.All.Single(d => d.Name == "Weatherman's");
        for (int seed = 0; seed < 200; seed++)
        {
            BundleSpec? spec = AuthoredBundleComposer.Compose(
                weathermans, 0, pools, new BundleGenerationTuning(), true, new Random(seed));
            Assert.NotNull(spec);
            Assert.DoesNotContain(spec!.Slots, s => IslandOnlyFish.Contains(s.ItemId));
        }
    }

    [Theory]
    [InlineData("(O)837")]
    [InlineData("(O)836")]
    [InlineData("(O)838")]
    [InlineData("(O)796")]
    [InlineData("(O)832")] // Pineapple: every built-in ban, not just the fish
    public void Repair_SwapsABannedAsk_EvenWhenReachabilityVouchesForIt(string id)
    {
        Assert.True(BoardRepairRules.MustReplace(id, _ => false));
        Assert.True(BoardRepairRules.MustReplace(id, null)); // the reachability read failed
    }

    [Fact]
    public void Repair_LeavesAnOrdinaryReachableAskAlone_AndStillSwapsAnUnreachableOne()
    {
        Assert.False(BoardRepairRules.MustReplace("(O)128", _ => false));
        Assert.False(BoardRepairRules.MustReplace("(O)128", null));
        Assert.True(BoardRepairRules.MustReplace("(O)128", _ => true));
    }

    [Fact]
    public void Repair_SaysWhyABannedAskWasSwapped()
        => Assert.False(string.IsNullOrWhiteSpace(BoardRepairRules.BannedReason("(O)837")));
}
