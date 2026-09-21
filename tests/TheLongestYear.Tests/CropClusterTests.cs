using System;
using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: the crows scene points its camera at the thickest patch of the crops
/// that are about to die, so the grouping has to pick the right one and always the same one.</summary>
public class CropClusterTests
{
    private static List<(int X, int Y)> Tiles(params (int, int)[] tiles) => new List<(int X, int Y)>(tiles);

    [Fact]
    public void Largest_takes_the_biggest_patch_not_the_first()
    {
        var tiles = Tiles((0, 0), (1, 0), (40, 40), (41, 40), (42, 40), (41, 41));
        Assert.Equal(new[] { (40, 40), (41, 40), (42, 40), (41, 41) }, CropCluster.Largest(tiles, 6));
    }

    [Fact]
    public void A_tile_joins_a_group_only_within_reach_of_the_tile_that_opened_it()
    {
        // (7,0) is 7 from (0,0) and so opens its own group even though it is 1 from (6,0).
        var tiles = Tiles((0, 0), (6, 0), (7, 0), (8, 0), (9, 0));
        Assert.Equal(new[] { (7, 0), (8, 0), (9, 0) }, CropCluster.Largest(tiles, 6));
    }

    [Fact]
    public void A_tie_goes_to_the_group_that_opened_first()
    {
        var tiles = Tiles((0, 0), (1, 0), (40, 40), (41, 40));
        Assert.Equal(new[] { (0, 0), (1, 0) }, CropCluster.Largest(tiles, 6));
    }

    [Fact]
    public void One_tile_is_its_own_patch_and_no_tiles_is_no_patch()
    {
        Assert.Equal(new[] { (3, 4) }, CropCluster.Largest(Tiles((3, 4)), 6));
        Assert.Empty(CropCluster.Largest(Tiles(), 6));
    }

    [Fact]
    public void Centroid_is_the_middle_rounded_to_a_tile()
    {
        Assert.Equal((1, 0), CropCluster.Centroid(Tiles((0, 0), (1, 0), (2, 0))));
        Assert.Equal((5, 10), CropCluster.Centroid(Tiles((4, 9), (6, 11))));
    }

    [Fact]
    public void Centroid_refuses_an_empty_patch()
    {
        Assert.Throws<ArgumentException>(() => CropCluster.Centroid(Tiles()));
    }

    [Fact]
    public void Nearest_takes_the_closest_tiles_closest_first()
    {
        var tiles = Tiles((10, 0), (1, 0), (5, 0), (2, 0));
        Assert.Equal(new[] { (1, 0), (2, 0), (5, 0) }, CropCluster.Nearest(tiles, (0, 0), 3));
    }

    [Fact]
    public void Nearest_never_asks_for_more_than_it_has_and_zero_asks_for_nothing()
    {
        var tiles = Tiles((1, 1), (2, 2));
        Assert.Equal(2, CropCluster.Nearest(tiles, (0, 0), 6).Count);
        Assert.Empty(CropCluster.Nearest(tiles, (0, 0), 0));
    }

    [Fact]
    public void Equally_distant_tiles_keep_the_order_they_came_in()
    {
        var tiles = Tiles((0, 1), (1, 0), (-1, 0));
        Assert.Equal(new[] { (0, 1), (1, 0), (-1, 0) }, CropCluster.Nearest(tiles, (0, 0), 3));
    }

    [Fact]
    public void Nothing_here_accepts_a_null_list()
    {
        Assert.Throws<ArgumentNullException>(() => CropCluster.Largest(null!, 6));
        Assert.Throws<ArgumentNullException>(() => CropCluster.Centroid(null!));
        Assert.Throws<ArgumentNullException>(() => CropCluster.Nearest(null!, (0, 0), 1));
    }
}
