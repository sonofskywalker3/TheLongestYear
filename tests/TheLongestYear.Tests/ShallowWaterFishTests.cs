using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Player report 2026-10-20: a Quality Fish bundle asked for a gold Goby, and every Goby
/// she caught at the Forest waterfall pool came out base quality. Fish size, and so quality, scales
/// with the clear water between the bobber and the nearest land; a cast from the only ledge above
/// that pool lands on its edge, one tile from land, where a level 10 farmer can never reach silver.
/// A fish whose every reachable catch spot is that shallow must never be asked for with a star.</summary>
public class ShallowWaterFishTests
{
    // ---- the game's size formula (FishingRod.startMinigameEndFunction, BobberBar ctor) ----

    [Fact]
    public void OneTileFromLand_AtLevelTen_TopsOutBelowSilver()
    {
        double best = FishStarDepth.MaxSizeFraction(1, FishStarDepth.YearOneFishingLevel);
        Assert.InRange(best, 0.316, 0.317);
        Assert.True(best < FishStarDepth.SilverSizeFraction);
    }

    [Fact]
    public void TwoTilesFromLand_AtLevelTen_CanRollSilver()
        => Assert.True(FishStarDepth.MaxSizeFraction(2, FishStarDepth.YearOneFishingLevel) >= FishStarDepth.SilverSizeFraction);

    [Fact]
    public void NextToLand_IsAlwaysTheSmallestSize()
        => Assert.Equal(0.0, FishStarDepth.MaxSizeFraction(0, FishStarDepth.YearOneFishingLevel));

    [Fact]
    public void LowLevels_UseTheFiveRollCeiling_AndOpenWaterClamps()
    {
        // Below level 10 the size roll is Next(1 + level/2, 6), so its best is 5.
        Assert.Equal(FishStarDepth.MaxSizeFraction(5, 0), FishStarDepth.MaxSizeFraction(5, 9));
        Assert.Equal(1.0, FishStarDepth.MaxSizeFraction(5, 14));
    }

    [Theory]
    [InlineData(null, true)]   // the map could not be read: keep the old behaviour
    [InlineData(-1, true)]     // no cast the walk can see lands where the fish bites: not proof of shallow water
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void CanRollStar_FollowsTheDeepestReachableSpot(int? maxClearWater, bool expected)
        => Assert.Equal(expected, FishStarDepth.CanRollStar(maxClearWater));

    // ---- the map walk ----

    /// <summary>Legend: ~ fishable water, w water the rod cannot fish (a waterfall), . land the
    /// farmer can stand on, # land he cannot (cliff), S a standable entry tile (a warp arrival).</summary>
    private sealed class AsciiMap : IFishingMap
    {
        private readonly string[] _rows;
        public AsciiMap(params string[] rows) => _rows = rows;
        public int Width => _rows[0].Length;
        public int Height => _rows.Length;
        private char At(int x, int y) => _rows[y][x];
        public bool IsWater(int x, int y) => At(x, y) is '~' or 'w';
        public bool IsFishable(int x, int y) => At(x, y) == '~';
        public bool IsStandable(int x, int y) => At(x, y) is '.' or 'S';
        public IEnumerable<(int X, int Y)> Seeds()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (At(x, y) == 'S') yield return (x, y);
        }
    }

    [Fact]
    public void ClearWaterDistance_MatchesTheGameRings()
    {
        var map = new AsciiMap(
            "...........",
            ".~~~~~~~~~.",
            ".~~~~~~~~~.",
            ".~~~~~~~~~.",
            ".~~~~~~~~~.",
            ".~~~~~~~~~.",
            "...........");
        Assert.Equal(0, FishCastReach.ClearWaterDistance(map, 1, 1));
        Assert.Equal(1, FishCastReach.ClearWaterDistance(map, 2, 2));
        Assert.Equal(2, FishCastReach.ClearWaterDistance(map, 3, 3));
        Assert.Equal(2, FishCastReach.ClearWaterDistance(map, 5, 3));
    }

    [Fact]
    public void ClearWaterDistance_CountsOffMapAsWater()
    {
        var map = new AsciiMap(
            "~~~~~~~~~~~~~",
            "~~~~~~~~~~~~~",
            "~~~~~~~~~~~~~",
            "~~~~~~~~~~~~~",
            "~~~~~~~~~~~~~",
            "~~~~~~~~~~~~~",
            ".............");
        Assert.Equal(5, FishCastReach.ClearWaterDistance(map, 6, 0));
        Assert.Equal(0, FishCastReach.ClearWaterDistance(map, 6, 5));
    }

    /// <summary>The Forest waterfall pool in miniature: a ledge (row 1, column 2) above a cliff,
    /// the waterfall column straight below it, and the pool beside the waterfall. A straight cast
    /// down lands on the waterfall, which the game nudges one tile sideways onto the pool's edge.</summary>
    private static AsciiMap WaterfallPool() => new(
        "S.........",
        "..........",
        "##w~~~~~##",
        "##w~~~~~##",
        "##w~~~~~##",
        "##w~~~~~##",
        "##w~~~~~##",
        "##########");

    [Fact]
    public void ALedgeAboveAWaterfall_ReachesOnlyThePoolEdge()
    {
        var map = WaterfallPool();
        // Every standable tile on rows 0-1 casts down; only column 2's cast lands on the waterfall
        // and is nudged onto column 3, the rest land in the pool itself.
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        Assert.Contains(landings, l => l.X == 3 && l.Y == 4);
        Assert.DoesNotContain(landings, l => map.IsWater(l.X, l.Y) && !map.IsFishable(l.X, l.Y));
    }

    [Fact]
    public void ACatchAreaReachedOnlyAtItsEdge_CannotCarryAStar()
    {
        // The real layout: the river runs into the waterfall (rows 2-3) between the farmer's bank
        // (columns 0-2) and a cliff, and the catch area is the pool below it (rows 4 and down).
        // The bank's casts down land on the waterfall column and slide onto the pool's edge,
        // one tile from the cliff, which is as deep as any cast there gets.
        var map = new AsciiMap(
            "S..~~~~~##",
            "...~~~~~##",
            "##.wwwww##",
            "###wwwww##",
            "##w~~~~~##",
            "##w~~~~~##",
            "##w~~~~~##",
            "##w~~~~~##",
            "##w~~~~~##",
            "##########");
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        int deepest = FishCastReach.MaxClearWater(map, landings, (x, y) => y >= 4);
        Assert.Equal(1, deepest);
        Assert.DoesNotContain(landings, l => l.Y >= 4 && l.X > 3);
        Assert.False(FishStarDepth.CanRollStar(deepest));
    }

    [Fact]
    public void AShoreFacingTheSamePool_CanReachItsMiddle()
    {
        var map = new AsciiMap(
            "S.........",
            "..........",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..........");
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        int deepest = FishCastReach.MaxClearWater(map, landings, (x, y) => true);
        Assert.Equal(2, deepest);
        Assert.True(FishStarDepth.CanRollStar(deepest));
    }

    [Fact]
    public void AStandableTileNoPathReaches_CastsNothing()
    {
        // The open water on the right is only castable from the walled-in tile at column 12;
        // the bank on the left is eight tiles away, past the longest cast.
        var map = new AsciiMap(
            "S.#############~~~~~",
            "..##########.##~~~~~",
            "..#############~~~~~");
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        Assert.Empty(landings);
        Assert.Equal(FishCastReach.NoCastableSpot, FishCastReach.MaxClearWater(map, landings, (x, y) => true));
    }

    [Fact]
    public void ARowsShoreDistanceLimits_KeepOnlyTheTilesItBitesOn()
    {
        var map = new AsciiMap(
            "S.........",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..........");
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        Assert.Equal(0, FishCastReach.MaxClearWater(map, landings, (x, y) => true, maxDistance: 0));
        Assert.Equal(2, FishCastReach.MaxClearWater(map, landings, (x, y) => true, minDistance: 3));
    }

    [Fact]
    public void AStandingArea_LimitsWhereTheCastStarts()
    {
        var map = new AsciiMap(
            "S.........",
            "..........",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..~~~~~~~.",
            "..........");
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        // Standing only in the top-left corner: the casts from there reach column 2 and row 2 only.
        int deepest = FishCastReach.MaxClearWater(map, landings, (x, y) => true,
            playerArea: new TileRect(0, 0, 2, 2));
        Assert.Equal(0, deepest);
    }

    // ---- the pool rule ----

    private static RawObjectEntry Fish() => new("Fish", -4, 100, false, new[] { "fish_river" });

    private static ItemPools Pools(params RawSpawnEntry[] spawns)
    {
        var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal);
        foreach (RawSpawnEntry s in spawns)
            objects[s.ItemId.Replace("(O)", "")] = Fish();
        return ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, new List<RawSpawnEntry>(), spawns.ToList(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
    }

    [Fact]
    public void AFishOnlyInShallowWater_LosesItsStar_AndIsListedAsShallow()
    {
        ItemPools pools = Pools(
            new RawSpawnEntry("(O)Goby", null, null, "Forest", MaxClearWater: 1),
            new RawSpawnEntry("(O)145", null, null, "Forest", MaxClearWater: 4),
            new RawSpawnEntry("(O)132", null, null, "Forest"));
        Assert.DoesNotContain("(O)Goby", pools.QualityEligibleIds!);
        Assert.Contains("(O)Goby", pools.ShallowWaterFishIds);
        Assert.Contains("(O)145", pools.QualityEligibleIds!);
        Assert.Contains("(O)132", pools.QualityEligibleIds!);
        Assert.DoesNotContain("(O)145", pools.ShallowWaterFishIds);
        Assert.DoesNotContain("(O)132", pools.ShallowWaterFishIds);
    }

    [Fact]
    public void ADeepSpotAnywhere_KeepsTheStar()
    {
        ItemPools pools = Pools(
            new RawSpawnEntry("(O)139", null, null, "Forest", MaxClearWater: 0),
            new RawSpawnEntry("(O)139", null, null, "Town", MaxClearWater: 3));
        Assert.Contains("(O)139", pools.QualityEligibleIds!);
        Assert.Empty(pools.ShallowWaterFishIds);
    }

    [Fact]
    public void AShallowFish_IsNeverRolledWithAStar()
    {
        ItemPools pools = Pools(
            new RawSpawnEntry("(O)Goby", null, null, "Forest", MaxClearWater: 1),
            new RawSpawnEntry("(O)145", null, null, "Forest", MaxClearWater: 4));
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0 };
        var spec = new BundleSpec("Pantry", 0, "River Fish", "River Fish", "O 1 1", 4, 2,
            new List<BundleSlotSpec> { new("(O)136", 1, 2), new("(O)137", 1, 2) });
        for (int seed = 0; seed < 50; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Fish, null), pools, tuning, new Random(seed));
            Assert.Contains(filled.Slots, s => s.ItemId == "(O)Goby");
            Assert.Contains(filled.Slots, s => s.ItemId == "(O)145" && s.Quality == 2);
            foreach (BundleSlotSpec slot in filled.Slots.Where(s => s.ItemId == "(O)Goby"))
                Assert.Equal(0, slot.Quality);
        }
    }

    // ---- the load-time repair ----

    private static readonly IReadOnlySet<string> Shallow = new HashSet<string>(StringComparer.Ordinal) { "(O)Goby" };

    [Fact]
    public void Repair_DropsTheStarOnAShallowFish_Only()
    {
        const string value = "Quality Fish/O 690 1/Goby 1 2 136 1 2 (O)Goby 1 1/4/3//Quality Fish";
        string? repaired = ShallowFishStars.RepairBundleValue(value, Shallow);
        Assert.Equal("Quality Fish/O 690 1/Goby 1 0 136 1 2 (O)Goby 1 0/4/3//Quality Fish", repaired);
    }

    [Fact]
    public void Repair_LeavesACleanBundleAlone()
    {
        Assert.Null(ShallowFishStars.RepairBundleValue("River Fish/O 685 30/Goby 1 0 145 1 2/6/2", Shallow));
        Assert.Null(ShallowFishStars.RepairBundleValue("River Fish/O 685 30/145 1 2/6/1", new HashSet<string>()));
        Assert.Null(ShallowFishStars.RepairBundleValue("broken", Shallow));
    }

    [Fact]
    public void Repair_NamesTheFishItLowered()
    {
        const string value = "Quality Fish/O 690 1/Goby 1 2 136 1 2/4/2";
        Assert.Equal(new[] { "(O)Goby" }, ShallowFishStars.LoweredIds(value, Shallow));
    }
}
