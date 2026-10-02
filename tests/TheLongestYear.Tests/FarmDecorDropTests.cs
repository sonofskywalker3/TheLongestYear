using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Addendum 2: a displaced decor piece drops on the ground next to whatever blocked it.
/// The planner names the blocking tile; DropSpot finds the nearest open tile.</summary>
public class FarmDecorDropTests
{
    private static IReadOnlyList<DecorTile> T(params (int x, int y)[] tiles) => tiles.Select(t => new DecorTile(t.x, t.y)).ToList();
    private static DecorClump Clump(int id, int index, int x, int y) => new(id, index, T((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)));
    private static TileBlock Open(int x, int y) => TileBlock.None;

    [Fact]
    public void A_piece_under_a_building_names_that_tile_as_its_blocker()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((5, 5)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new DecorClump[0],
            (x, y) => x == 5 && y == 5 ? TileBlock.Building : TileBlock.None, 0, 0);
        Assert.Equal(new DecorTile(5, 5), plan.BlockedAt[1]);
    }

    [Fact]
    public void The_first_blocked_tile_of_a_larger_piece_is_its_blocker()
    {
        var piece = new DecorPiece(2, DecorLayer.Object, T((5, 5), (6, 5)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { piece }, new DecorClump[0],
            (x, y) => x == 6 ? TileBlock.OtherObject : TileBlock.None, 0, 0);
        Assert.Equal(new DecorTile(6, 5), plan.BlockedAt[2]);
    }

    [Fact]
    public void A_piece_under_an_unbreakable_clump_names_the_overlapped_tile()
    {
        var path = new DecorPiece(3, DecorLayer.Ground, T((11, 11)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, 0, 0);
        Assert.Equal(new DecorTile(11, 11), plan.BlockedAt[3]);
    }

    [Fact]
    public void Placed_pieces_have_no_blocker()
    {
        var path = new DecorPiece(4, DecorLayer.Ground, T((1, 1)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new DecorClump[0], Open, 0, 0);
        Assert.Empty(plan.BlockedAt);
    }

    [Fact]
    public void Every_displaced_piece_has_a_blocker()
    {
        var a = new DecorPiece(1, DecorLayer.Ground, T((5, 5)));
        var b = new DecorPiece(2, DecorLayer.Ground, T((-1, 0)));
        var c = new DecorPiece(3, DecorLayer.Ground, T((20, 20)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { a, b, c }, new[] { Clump(9, 672, 20, 20) },
            (x, y) => x < 0 ? TileBlock.OffMap : x == 5 ? TileBlock.Building : TileBlock.None, 4, 0);
        Assert.Equal(plan.Displaced.OrderBy(i => i), plan.BlockedAt.Keys.OrderBy(i => i));
    }

    [Fact]
    public void An_open_start_tile_is_the_drop_spot()
        => Assert.Equal(new DecorTile(3, 3), DropSpot.Nearest(new DecorTile(3, 3), (x, y) => true, 5));

    [Fact]
    public void The_nearest_open_tile_is_taken_side_before_corner()
    {
        // Only (4,3) (side) and (4,4) (corner) are open around (3,3).
        DecorTile? spot = DropSpot.Nearest(new DecorTile(3, 3), (x, y) => (x, y) is (4, 3) or (4, 4), 5);
        Assert.Equal(new DecorTile(4, 3), spot);
    }

    [Fact]
    public void The_first_ring_beats_the_second()
    {
        DecorTile? spot = DropSpot.Nearest(new DecorTile(0, 0), (x, y) => (x, y) is (2, 0) or (-1, 1), 5);
        Assert.Equal(new DecorTile(-1, 1), spot);
    }

    [Fact]
    public void Nothing_open_within_the_radius_gives_no_spot()
        => Assert.Null(DropSpot.Nearest(new DecorTile(0, 0), (x, y) => System.Math.Abs(x) > 3, 3));

    [Fact]
    public void A_displaced_piece_drops_on_its_own_tile_when_that_is_open()
    {
        DecorTile? spot = DropSpot.ForDisplaced(T((5, 5), (6, 5)), new DecorTile(6, 5), (x, y) => x == 5 && y == 5, 5);
        Assert.Equal(new DecorTile(5, 5), spot);
    }

    [Fact]
    public void Otherwise_it_drops_next_to_the_blocker()
    {
        // A 2x2 clump at (10,10); the piece sat on (11,11). The nearest open tile to it is (12,11).
        bool IsOpen(int x, int y) => !(x is 10 or 11 && y is 10 or 11);
        DecorTile? spot = DropSpot.ForDisplaced(T((11, 11)), new DecorTile(11, 11), IsOpen, 5);
        Assert.Equal(new DecorTile(12, 11), spot);
    }
}
