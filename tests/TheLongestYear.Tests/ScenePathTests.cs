using System;
using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: the thief walks in to the chest the darkness picked and must never
/// cross a wall, a fence or a crop on the way. The walk is chosen here, so it is tested here.</summary>
public class ScenePathTests
{
    /// <summary>A grid from rows of characters, so a test reads like the room it describes.
    /// A dot is passable, a hash is not.</summary>
    private static bool[,] Grid(params string[] rows)
    {
        var grid = new bool[rows[0].Length, rows.Length];
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                grid[x, y] = rows[y][x] == '.';
        return grid;
    }

    private static List<(int X, int Y)> Starts(params (int, int)[] tiles) => new List<(int X, int Y)>(tiles);

    [Fact]
    public void The_walk_runs_from_the_way_in_to_the_tile_beside_the_target()
    {
        bool[,] grid = Grid("......");
        IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(grid, (5, 0), Starts((0, 0)), 20, 8);
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) }, walk);
    }

    [Fact]
    public void It_goes_round_a_wall_rather_than_through_it()
    {
        bool[,] grid = Grid(
            "....",
            ".##.",
            "....");
        IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(grid, (2, 1), Starts((0, 1)), 20, 8);
        // (1,1) and (2,1) are wall, so the only tile beside the target is (3,1) or (2,0) or (2,2).
        Assert.NotEmpty(walk);
        Assert.Equal((0, 1), walk[0]);
        foreach ((int X, int Y) tile in walk) Assert.True(grid[tile.X, tile.Y]);
        // Every step is one tile, so nothing was jumped over.
        for (int i = 1; i < walk.Count; i++)
            Assert.Equal(1, Math.Abs(walk[i].X - walk[i - 1].X) + Math.Abs(walk[i].Y - walk[i - 1].Y));
    }

    [Fact]
    public void The_nearest_way_in_wins_whatever_order_they_come_in()
    {
        bool[,] grid = Grid("..........");
        IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(grid, (5, 0), Starts((0, 0), (9, 0)), 20, 8);
        Assert.Equal((9, 0), walk[0]);
        Assert.Equal((6, 0), walk[walk.Count - 1]);
    }

    [Fact]
    public void A_way_in_that_is_too_far_is_dropped_for_a_start_inside()
    {
        bool[,] grid = Grid("....................");
        IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(grid, (19, 0), Starts((0, 0)), 14, 8);
        // (0,0) is 18 steps out, past the limit, so he starts 8 from the tile beside the chest.
        Assert.Equal((10, 0), walk[0]);
        Assert.Equal((18, 0), walk[walk.Count - 1]);
    }

    [Fact]
    public void A_way_in_that_cannot_be_reached_is_dropped_too()
    {
        bool[,] grid = Grid(
            ".#........",
            ".#........");
        IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(grid, (9, 0), Starts((0, 0)), 20, 4);
        Assert.NotEqual((0, 0), walk[0]);
        Assert.Equal((4, 0), walk[0]);
    }

    [Fact]
    public void A_target_with_nothing_passable_beside_it_has_no_walk()
    {
        bool[,] grid = Grid(
            "###",
            "#.#",
            "###");
        Assert.Empty(ScenePath.WalkTo(grid, (1, 1), Starts((0, 0)), 20, 8));
    }

    [Fact]
    public void A_target_off_the_grid_has_no_walk()
    {
        bool[,] grid = Grid("...");
        Assert.Empty(ScenePath.WalkTo(grid, (40, 40), Starts((0, 0)), 20, 8));
    }

    [Fact]
    public void The_walk_never_crosses_the_target_even_when_its_tile_is_passable()
    {
        // The only way from (0,0) to the far side is straight through (2,0), which is the target.
        // The walk must stop beside it rather than treat it as a corridor.
        bool[,] grid = Grid(
            "#####",
            ".....",
            "#####");
        IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(grid, (2, 1), Starts((4, 1)), 20, 8);
        Assert.DoesNotContain((2, 1), walk);
        Assert.Equal((4, 1), walk[0]);
        Assert.Equal((3, 1), walk[walk.Count - 1]);
        // And the tiles on the far side are unreachable, so a way in over there is ignored.
        IReadOnlyList<(int X, int Y)> farSide = ScenePath.WalkTo(grid, (2, 1), Starts((0, 1)), 20, 8);
        Assert.Equal((0, 1), farSide[0]);
        Assert.Equal((1, 1), farSide[farSide.Count - 1]);
        Assert.DoesNotContain((2, 1), farSide);
    }

    [Fact]
    public void The_same_room_always_gives_the_same_walk()
    {
        bool[,] grid = Grid(
            ".....",
            ".....",
            ".....");
        IReadOnlyList<(int X, int Y)> first = ScenePath.WalkTo(grid, (4, 1), new List<(int X, int Y)>(), 20, 3);
        IReadOnlyList<(int X, int Y)> again = ScenePath.WalkTo(grid, (4, 1), new List<(int X, int Y)>(), 20, 3);
        Assert.Equal(first, again);
        Assert.Equal(4, first.Count);
    }

    [Fact]
    public void Trimming_drops_the_far_end_and_keeps_the_tile_beside_the_target()
    {
        var walk = new List<(int X, int Y)> { (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) };
        Assert.Equal(new[] { (3, 0), (4, 0) }, ScenePath.Trim(walk, 1));
        Assert.Equal(walk, ScenePath.Trim(walk, 4));
        Assert.Equal(walk, ScenePath.Trim(walk, 40));
        Assert.Equal(new[] { (4, 0) }, ScenePath.Trim(walk, 0));
    }

    [Fact]
    public void Bad_arguments_are_refused()
    {
        bool[,] grid = Grid("...");
        Assert.Throws<ArgumentNullException>(() => ScenePath.WalkTo(null!, (1, 0), Starts(), 5, 5));
        Assert.Throws<ArgumentNullException>(() => ScenePath.WalkTo(grid, (1, 0), null!, 5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenePath.WalkTo(grid, (1, 0), Starts(), -1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenePath.WalkTo(grid, (1, 0), Starts(), 5, -1));
        Assert.Throws<ArgumentNullException>(() => ScenePath.Trim(null!, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenePath.Trim(new List<(int X, int Y)>(), -1));
    }
}
