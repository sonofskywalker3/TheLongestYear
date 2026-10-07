using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Routes built from designed corners and checked against the map, and the path only
/// search that is the fallback when a map has changed (Jeff, 2026-09-25: no made up paths).</summary>
public class TileRouteTests
{
    [Fact]
    public void A_straight_leg_expands_to_every_tile_on_it()
    {
        IReadOnlyList<(int X, int Y)> tiles = TileRoute.Expand(new[] { (5, 2), (2, 2) });
        Assert.Equal(new[] { (5, 2), (4, 2), (3, 2), (2, 2) }, tiles);
    }

    [Fact]
    public void Corners_join_legs_without_repeating_the_corner()
    {
        IReadOnlyList<(int X, int Y)> tiles = TileRoute.Expand(new[] { (0, 0), (0, 2), (2, 2) });
        Assert.Equal(new[] { (0, 0), (0, 1), (0, 2), (1, 2), (2, 2) }, tiles);
    }

    [Fact]
    public void A_single_corner_is_a_route_of_one_tile()
    {
        Assert.Equal(new[] { (3, 4) }, TileRoute.Expand(new[] { (3, 4) }));
    }

    [Fact]
    public void A_diagonal_leg_is_refused()
    {
        Assert.Throws<ArgumentException>(() => TileRoute.Expand(new[] { (0, 0), (2, 3) }));
    }

    [Fact]
    public void No_corners_is_refused()
    {
        Assert.Throws<ArgumentException>(() => TileRoute.Expand(Array.Empty<(int X, int Y)>()));
    }

    [Fact]
    public void FirstOffPath_names_the_first_tile_that_is_not_path()
    {
        var route = new[] { (0, 0), (1, 0), (2, 0), (3, 0) };
        Assert.Equal(2, TileRoute.FirstOffPath(route, (x, _) => x != 2 && x != 3));
        Assert.Equal(-1, TileRoute.FirstOffPath(route, (_, _) => true));
    }

    private static bool[,] Grid(params string[] rows)
    {
        var grid = new bool[rows[0].Length, rows.Length];
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                grid[x, y] = rows[y][x] == 'd';
        return grid;
    }

    [Fact]
    public void Between_goes_round_the_lawn_on_the_path_and_never_across_it()
    {
        // A U of path round a block of lawn. The straight line across is shorter and is lawn.
        bool[,] path = Grid(
            "d...d",
            "d...d",
            "ddddd");
        IReadOnlyList<(int X, int Y)> walk = TileRoute.Between(path, (0, 0), (4, 0));
        Assert.Equal((0, 0), walk[0]);
        Assert.Equal((4, 0), walk[walk.Count - 1]);
        Assert.Equal(9, walk.Count);
        Assert.All(walk, t => Assert.True(path[t.X, t.Y], $"({t.X},{t.Y}) is not path"));
        for (int i = 1; i < walk.Count; i++)
            Assert.Equal(1, Math.Abs(walk[i].X - walk[i - 1].X) + Math.Abs(walk[i].Y - walk[i - 1].Y));
    }

    [Fact]
    public void Between_is_empty_when_the_path_does_not_connect()
    {
        bool[,] path = Grid("dd.dd");
        Assert.Empty(TileRoute.Between(path, (0, 0), (4, 0)));
    }

    [Fact]
    public void Between_is_empty_when_an_end_is_not_path()
    {
        bool[,] path = Grid("ddd.");
        Assert.Empty(TileRoute.Between(path, (0, 0), (3, 0)));
        Assert.Empty(TileRoute.Between(path, (9, 9), (0, 0)));
    }

    [Fact]
    public void Between_a_tile_and_itself_is_that_tile()
    {
        Assert.Equal(new[] { (1, 0) }, TileRoute.Between(Grid("ddd"), (1, 0), (1, 0)));
    }

    [Fact]
    public void LastSteps_keeps_the_end_of_a_long_route_and_all_of_a_short_one()
    {
        var route = Enumerable.Range(0, 10).Select(x => (x, 0)).ToList();
        IReadOnlyList<(int X, int Y)> kept = TileRoute.LastSteps(route, 3);
        Assert.Equal(new[] { (6, 0), (7, 0), (8, 0), (9, 0) }, kept);
        Assert.Equal(10, TileRoute.LastSteps(route, 50).Count);
        Assert.Single(TileRoute.LastSteps(route, 0));
    }
}
