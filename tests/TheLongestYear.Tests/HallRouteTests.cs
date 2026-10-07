using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Shane's walk past the hall is checked tile by tile against Town's own map (Jeff,
/// 2026-09-25: "You've gotta stop making up your own paths").
///
/// <see cref="TownPaths"/> is Town's Back layer from <c>patch export Maps/Town</c> (game 1.6), the
/// tiles from (44,24) to (79,44): <c>d</c> is a Back tile of Type Dirt, <c>s</c> Stone, <c>w</c>
/// Wood (the bridge), each with no Buildings tile and no Paths layer object on it. A dot is
/// anything else: grass, the lawn, a building, a tree, water.</summary>
public class HallRouteTests
{
    private const int MaskLeft = 44;
    private const int MaskTop = 24;

    private static readonly string[] TownPaths =
    {
        "..........................ddd.......", // 24
        "..........dd..............dd........", // 25
        ".........ddd..............dd........", // 26
        "..........dd.............ddd........", // 27
        "..........ddddddddddddddddddddd.....", // 28
        "...........ddddddddddddddddddddd....", // 29
        "...........ddddddddddddddddd.dww....", // 30
        "............ddddddddddddd.....ww....", // 31
        "............dddd...dddddd.....ww....", // 32
        "...........dddd.....d.........ww....", // 33
        "..........ddd.................ww....", // 34
        "..........dd..................dd..dd", // 35
        ".....dddddd.................ddddddd.", // 36
        "ddddddd....................ddddddd..", // 37
        "....dd....................dddddd....", // 38
        ".....d.................dddd.........", // 39
        ".....dd................dd...........", // 40
        ".....d.................d............", // 41
        "....dd.................d............", // 42
        ".......................d............", // 43
        "....ss.................d........dddd", // 44
    };

    /// <summary>The Community Center's own tiles (Town.refurbishCommunityCenter) and the cobbled
    /// approach below its door, which he must never set foot on.</summary>
    private const int HallLeft = 47, HallRight = 58;
    private static readonly (int X, int Y)[] Approach =
    {
        (52, 21), (53, 21), (52, 22), (53, 22), (52, 23), (53, 23),
    };

    private static bool IsTownPath(int x, int y)
    {
        int row = y - MaskTop, col = x - MaskLeft;
        if (row < 0 || row >= TownPaths.Length || col < 0 || col >= TownPaths[row].Length) return false;
        return TownPaths[row][col] != '.';
    }

    public static IEnumerable<object[]> Parts() => new[]
    {
        new object[] { "way in" },
        new object[] { "back away" },
        new object[] { "way out" },
    };

    private static IReadOnlyList<(int X, int Y)> Part(string name) => name switch
    {
        "way in" => HallRoute.WayIn,
        "back away" => HallRoute.BackAway,
        "way out" => HallRoute.WayOut,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Parts))]
    public void Every_tile_he_steps_on_is_a_path_tile_on_Town_s_map(string part)
    {
        foreach ((int x, int y) in Part(part))
            Assert.True(IsTownPath(x, y), $"{part}: ({x},{y}) is not a path tile on Town's map");
    }

    [Theory]
    [MemberData(nameof(Parts))]
    public void Every_step_is_one_tile_and_never_diagonal(string part)
    {
        IReadOnlyList<(int X, int Y)> tiles = Part(part);
        for (int i = 1; i < tiles.Count; i++)
            Assert.Equal(1, Math.Abs(tiles[i].X - tiles[i - 1].X) + Math.Abs(tiles[i].Y - tiles[i - 1].Y));
    }

    [Theory]
    [MemberData(nameof(Parts))]
    public void He_never_walks_up_to_the_door_or_onto_the_lawn_below_the_hall(string part)
    {
        foreach ((int x, int y) in Part(part))
        {
            Assert.DoesNotContain((x, y), Approach);
            // Everything between the hall's foot (row 21) and the road (row 28) is lawn and the
            // approach. He stays on the road or below it.
            Assert.True(y >= 28, $"{part}: ({x},{y}) is above the road, toward the hall");
        }
    }

    [Fact]
    public void The_three_parts_join_up()
    {
        Assert.Equal(HallRoute.Stop, HallRoute.BackAway[0]);
        Assert.Equal(HallRoute.BackAway[HallRoute.BackAway.Count - 1], HallRoute.WayOut[0]);
    }

    [Fact]
    public void He_stops_level_with_the_hall_on_the_road_below_it()
    {
        (int x, int y) = HallRoute.Stop;
        Assert.InRange(x, HallLeft, HallRight);
        Assert.Equal(28, y);
    }

    [Fact]
    public void He_passes_the_hall_rather_than_turning_back_the_way_he_came()
    {
        // In from the east, out to the south west: the way home never retraces the way in.
        var cameIn = new HashSet<(int X, int Y)>(HallRoute.WayIn);
        Assert.DoesNotContain(HallRoute.WayOut.Skip(1), t => cameIn.Contains(t));
        Assert.True(HallRoute.WayIn[0].X > HallRight);
        Assert.True(HallRoute.WayOut[HallRoute.WayOut.Count - 1].X < HallRoute.Stop.X);
    }

    // ------------------------------------------------------------------ the way home on any screen
    // Task 9 review, fix round 3: on a screen tall enough to show the whole way home, the cut kept
    // all of it and the old playback extrapolated him south past (49,42) onto tiles nobody checked.

    /// <summary>Every place he can be along a cut way home, from before he sets off to long after
    /// the scene would have ended, must lie on a checked tile or between two consecutive ones.</summary>
    private static void AssertNeverOffTheCheckedTiles(IReadOnlyList<(int X, int Y)> cut)
    {
        var checkedTiles = new HashSet<(int X, int Y)>(HallRoute.WayOut);
        Assert.All(cut, t => Assert.Contains(t, checkedTiles));
        for (float tiles = -5f; tiles <= 200f; tiles += 0.05f)
        {
            (float x, float y) = TileRoute.At(cut, tiles);
            var low = ((int)Math.Floor(x), (int)Math.Floor(y));
            var high = ((int)Math.Ceiling(x), (int)Math.Ceiling(y));
            Assert.True(checkedTiles.Contains(low), $"at {tiles} tiles he is on ({low.Item1},{low.Item2}), which is not a checked tile");
            Assert.True(checkedTiles.Contains(high), $"at {tiles} tiles he is on ({high.Item1},{high.Item2}), which is not a checked tile");
        }
    }

    [Fact]
    public void On_a_screen_that_shows_the_whole_way_home_he_stops_on_its_last_checked_tile()
    {
        // The whole of Town (130 by 110 tiles) in shot: the way home never leaves the frame.
        IReadOnlyList<(int X, int Y)> cut = HallRoute.WayOutInShot(HallRoute.WayOut, 0, 0, 130, 110);
        Assert.Equal(HallRoute.WayOut, cut);
        AssertNeverOffTheCheckedTiles(cut);
        Assert.Equal((49f, 42f), TileRoute.At(cut, 1000f));
    }

    [Fact]
    public void On_a_tall_4k_screen_at_75_percent_zoom_he_never_leaves_the_checked_tiles()
    {
        // 3840x2160 at 0.75 zoom is 5120x2880 world pixels, 80 by 45 tiles, centred on the road
        // framing (50.5, 24): whole tiles 11 to 90 across and 2 to 46 down, so (49,42) is in shot.
        IReadOnlyList<(int X, int Y)> cut = HallRoute.WayOutInShot(HallRoute.WayOut, 11, 2, 80, 45);
        Assert.Contains((49, 42), cut);
        AssertNeverOffTheCheckedTiles(cut);
    }

    [Fact]
    public void At_1920_by_1080_he_walks_on_until_wholly_out_of_shot_and_stops_on_a_checked_tile()
    {
        // The road framing at 1920x1080: whole tiles 36 to 64 across, 16 to 31 down.
        IReadOnlyList<(int X, int Y)> cut = HallRoute.WayOutInShot(HallRoute.WayOut, 36, 16, 29, 16);
        AssertNeverOffTheCheckedTiles(cut);
        (int x, int y) last = cut[cut.Count - 1];
        // His head is a tile above his feet: standing on the last tile, all of him is below the
        // frame's partly visible bottom row (31) and the row under it.
        Assert.True(last.y - 1 > 31 + 1, $"he stops at ({last.x},{last.y}), close enough to the frame to show");
    }
}
