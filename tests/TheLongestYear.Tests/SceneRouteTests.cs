using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-09-23: Shane walks the town's own paths past the hall, so his route comes
/// from the game's pathfinder and is cut down to the part the camera can see, plus a few tiles out
/// of shot at each end so he walks in and out rather than appearing and vanishing.</summary>
public class SceneRouteTests
{
    // The frame is the whole tiles from (10,10) to (19,19).
    private const int Left = 10, Top = 10, Width = 10, Height = 10;

    private static List<(int X, int Y)> Column(int x, int fromY, int toY)
    {
        var tiles = new List<(int X, int Y)>();
        int step = toY >= fromY ? 1 : -1;
        for (int y = fromY; y != toY + step; y += step) tiles.Add((x, y));
        return tiles;
    }

    [Fact]
    public void A_route_into_the_frame_keeps_its_last_tiles_out_of_shot_and_everything_after()
    {
        // From far below the frame up to (15,15).
        List<(int X, int Y)> route = Column(15, 40, 15);
        IReadOnlyList<(int X, int Y)> kept = SceneRoute.IntoFrame(route, Left, Top, Width, Height, 3);
        Assert.Equal((15, 22), kept[0]);
        Assert.Equal((15, 15), kept[kept.Count - 1]);
        Assert.Equal(8, kept.Count);
    }

    [Fact]
    public void A_route_out_of_the_frame_keeps_everything_before_it_leaves_and_a_few_tiles_beyond()
    {
        List<(int X, int Y)> route = Column(15, 15, 40);
        IReadOnlyList<(int X, int Y)> kept = SceneRoute.OutOfFrame(route, Left, Top, Width, Height, 3);
        Assert.Equal((15, 15), kept[0]);
        Assert.Equal((15, 22), kept[kept.Count - 1]);
    }

    [Fact]
    public void A_route_that_never_leaves_the_frame_is_kept_whole()
    {
        List<(int X, int Y)> route = Column(15, 12, 18);
        Assert.Equal(route, SceneRoute.IntoFrame(route, Left, Top, Width, Height, 3));
        Assert.Equal(route, SceneRoute.OutOfFrame(route, Left, Top, Width, Height, 3));
    }

    [Fact]
    public void A_route_that_starts_only_just_outside_keeps_what_it_has()
    {
        List<(int X, int Y)> route = Column(15, 21, 15);
        IReadOnlyList<(int X, int Y)> kept = SceneRoute.IntoFrame(route, Left, Top, Width, Height, 3);
        Assert.Equal(route, kept);
    }

    [Fact]
    public void A_route_that_weaves_out_and_back_in_is_cut_at_its_last_way_in()
    {
        var route = new List<(int X, int Y)>();
        route.AddRange(Column(5, 30, 15));          // well out to the left
        route.AddRange(new[] { (6, 15), (7, 15), (8, 15), (9, 15), (10, 15), (11, 15) }); // in at x 10
        route.AddRange(new[] { (11, 16), (11, 17) });
        IReadOnlyList<(int X, int Y)> kept = SceneRoute.IntoFrame(route, Left, Top, Width, Height, 2);
        Assert.Equal((8, 15), kept[0]);
        Assert.Equal((11, 17), kept.Last());
    }

    [Fact]
    public void An_empty_route_stays_empty()
    {
        Assert.Empty(SceneRoute.IntoFrame(new List<(int X, int Y)>(), Left, Top, Width, Height, 3));
        Assert.Empty(SceneRoute.OutOfFrame(new List<(int X, int Y)>(), Left, Top, Width, Height, 3));
    }
}
