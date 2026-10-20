using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Where a cast can land on a map and how deep the water is there (<see cref="FishCastReach"/>),
/// the map half of the gold Goby fix (player report 2026-10-20, see <see cref="ShallowWaterFishTests"/>).</summary>
public class FishCastReachTests
{
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

    /// <summary>The Forest waterfall pool in miniature: a ledge (rows 0-1, columns 0-2) beside a
    /// cliff, the waterfall column (2) straight below its edge, and the pool beside the waterfall.
    /// A straight cast down lands on the waterfall, which the game nudges one tile sideways onto the
    /// pool's edge; a sideways cast over the cliff is nudged onto the pool's top row.</summary>
    private static AsciiMap WaterfallPool() => new(
        "S..#######",
        "...#######",
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
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        Assert.Contains(landings, l => l.X == 3 && l.Y == 4);
        Assert.DoesNotContain(landings, l => map.IsWater(l.X, l.Y) && !map.IsFishable(l.X, l.Y));
        // The pool's middle (two tiles from any land) is never landed on.
        Assert.Equal(2, FishCastReach.ClearWaterDistance(map, 5, 4));
        Assert.DoesNotContain(landings, l => l.X >= 4 && l.Y >= 3);
        int deepest = FishCastReach.MaxClearWater(map, landings, (x, y) => true);
        Assert.Equal(1, deepest);
        Assert.False(FishStarDepth.CanRollStar(deepest));
    }

    [Fact]
    public void ASidewaysCastOverWaterTheRodCannotFish_IsNudgedUpOrDown()
    {
        // The farmer stands in a gap between two cliffs; a cast right flies over a channel the rod
        // cannot fish (row 1) and slides onto the fishable rows above and below it.
        var map = new AsciiMap(
            "##~~~~~~",
            "S.wwwwww",
            "##~~~~~~");
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        Assert.Contains(landings, l => l.X == 4 && l.Y == 0);
        Assert.Contains(landings, l => l.X == 4 && l.Y == 2);
        Assert.DoesNotContain(landings, l => l.Y == 1);
    }

    [Fact]
    public void ARowWithAFishArea_BitesOnlyOnThatAreasTiles()
    {
        // The open pool's middle is area "Lake"; its right edge column is area "Inlet".
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
        string AreaAt(int x, int y) => x >= 8 ? "Inlet" : "Lake";
        Assert.Equal(2, FishCastReach.MaxClearWater(map, landings, FishCastReach.RowBitesAt(null, null, AreaAt)));
        Assert.Equal(2, FishCastReach.MaxClearWater(map, landings, FishCastReach.RowBitesAt(null, "Lake", AreaAt)));
        Assert.Equal(0, FishCastReach.MaxClearWater(map, landings, FishCastReach.RowBitesAt(null, "Inlet", AreaAt)));
        Assert.Equal(FishCastReach.NoCastableSpot,
            FishCastReach.MaxClearWater(map, landings, FishCastReach.RowBitesAt(null, "Sea", AreaAt)));
        // A BobberPosition and an area together: both must hold.
        Assert.Equal(0, FishCastReach.MaxClearWater(map, landings,
            FishCastReach.RowBitesAt(new TileRect(2, 2, 1, 5), "Lake", AreaAt)));
    }

    [Fact]
    public void TheDepthMemo_WalksEachTileOnce_AndMatchesTheDirectWalk()
    {
        var map = WaterfallPool();
        var landings = FishCastReach.Landings(map, map.Seeds(), FishStarDepth.YearOneFishingLevel);
        var memo = new ClearWaterMemo(map);
        int first = FishCastReach.MaxClearWater(memo, landings, (x, y) => true);
        int walks = memo.Computed;
        int second = FishCastReach.MaxClearWater(memo, landings, (x, y) => y >= 4);
        Assert.Equal(FishCastReach.MaxClearWater(map, landings, (x, y) => true), first);
        Assert.Equal(walks, memo.Computed);
        Assert.Equal(landings.Select(l => (l.X, l.Y)).Distinct().Count(), walks);
        Assert.Equal(1, second);
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
}
