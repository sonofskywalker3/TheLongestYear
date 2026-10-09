using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-09: a Junimo was hidden behind something in a porch scene. The marks
/// must be clear of anything on the tile, drawn over it or standing on the tile above, and stay
/// evenly spaced about the farmer where they can.</summary>
public class PorchMarksTests
{
    private const int DoorX = 64, DoorY = 14;

    private static (int X, int Y) T(int dx, int dy) => (DoorX + dx, DoorY + dy);

    private static IReadOnlyList<(int X, int Y)> Choose(int count, params (int X, int Y)[] blocked)
        => PorchMarks.Choose(DoorX, DoorY, count, new HashSet<(int X, int Y)>(blocked));

    // The auto-placed planning shrine (furniture, a 16x32 sprite) on (-2,3) and the Junimo Stash
    // (a big craftable) on (3,3), each drawn over the tile above too.
    private static readonly (int X, int Y)[] UsualFixtures = { T(-2, 3), T(-2, 2), T(3, 3), T(3, 2) };

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void An_open_porch_keeps_the_scene_layout(int count)
        => Assert.Equal(PorchMarks.Default(count).Select(m => T(m.X, m.Y)), Choose(count));

    [Fact]
    public void The_layouts_are_the_scenes_own()
    {
        Assert.Equal(new[] { (1, 3), (-1, 3) }, PorchMarks.Default(2));
        Assert.Equal(new[] { (0, 3), (-2, 3), (2, 3) }, PorchMarks.Default(3));
        Assert.Equal(new[] { (0, 3), (-2, 3), (2, 3), (-4, 4) }, PorchMarks.Default(4));
    }

    [Fact]
    public void A_pair_clears_the_usual_fixtures_as_it_stands()
        => Assert.Equal(new[] { T(1, 3), T(-1, 3) }, Choose(2, UsualFixtures));

    [Fact]
    public void Three_with_the_shrine_on_a_mark_stand_shoulder_to_shoulder_under_the_steps()
        => Assert.Equal(new[] { T(0, 3), T(-1, 3), T(1, 3) }, Choose(3, UsualFixtures));

    // Live, 2026-10-09 (fresh Standard farm): with the shrine at -2 and the stash at +3 no even row
    // fits four (row 3 at gap 2 hits the stash, at gap 4 the shrine; row 4 stands under them), so
    // they stand shoulder to shoulder half a tile off centre, Junimo 0 under the farmer.
    [Fact]
    public void Four_with_the_usual_fixtures_stand_shoulder_to_shoulder()
        => Assert.Equal(new[] { T(0, 3), T(1, 3), T(-1, 3), T(2, 3) }, Choose(4, UsualFixtures));

    [Fact]
    public void A_free_lower_row_is_used_before_going_off_centre()
    {
        // Things on the porch edge above (3,3) and (-6,3) rule out row 3 at gaps 2 and 4, and
        // (-4,3) the scene's own (-4,4); row 4 is clear, and an even row there wins over going off
        // centre.
        IReadOnlyList<(int X, int Y)> marks = Choose(4, T(3, 2), T(-6, 2), T(-4, 3));
        AssertEvenAboutTheFarmer(marks);
        Assert.Equal(new[] { T(1, 4), T(-1, 4), T(3, 4), T(-3, 4) }, marks);
    }

    [Fact]
    public void A_chest_on_the_middle_mark_moves_the_three_off_it()
    {
        IReadOnlyList<(int X, int Y)> marks = Choose(3, T(0, 3));
        Assert.DoesNotContain(T(0, 3), marks);
        Assert.DoesNotContain(T(0, 4), marks);    // under the chest
        Assert.All(marks, m => Assert.True(PorchMarks.IsClear(m.X, m.Y, (x, y) => (x, y) == T(0, 3))));
        Assert.All(marks, m => Assert.InRange(m.Y, DoorY + PorchMarks.FirstRow, DoorY + PorchMarks.LastRow));
    }

    // Live, 2026-10-09: an extra chest on the pair's right mark, the shrine on the left: no even row
    // fits two, so the pair stands Junimo 0 under the farmer and Junimo 1 beside him.
    [Fact]
    public void A_pair_with_a_chest_on_its_mark_stands_off_centre_not_under_the_speech_box()
        => Assert.Equal(new[] { T(0, 3), T(-1, 3) }, Choose(2, UsualFixtures.Append(T(1, 3)).ToArray()));

    [Fact]
    public void No_junimo_stands_lower_than_the_last_row()
    {
        IReadOnlyList<(int X, int Y)> marks = Choose(4, T(0, 3), T(1, 3), T(-1, 3), T(2, 3), T(-2, 3), T(3, 3), T(-3, 3));
        Assert.All(marks, m => Assert.True(m.Y <= DoorY + PorchMarks.LastRow));
    }

    [Fact]
    public void A_mark_under_something_on_the_tile_above_is_not_used()
    {
        // A chest on the porch edge at (0,2): the Junimo below it would stand pressed under it.
        IReadOnlyList<(int X, int Y)> marks = Choose(3, T(0, 2));
        Assert.DoesNotContain(T(0, 3), marks);
        AssertEvenAboutTheFarmer(marks);
    }

    [Fact]
    public void A_big_craftable_below_a_mark_covers_it()
    {
        // The caller marks the tile a taller sprite is drawn over as blocked; the rule honours it.
        IReadOnlyList<(int X, int Y)> marks = Choose(2, T(1, 3));
        Assert.DoesNotContain(T(1, 3), marks);
        AssertEvenAboutTheFarmer(marks);
    }

    [Theory]
    [InlineData(false, new[] { 0, -1, 1, -2 })]
    [InlineData(true, new[] { 0, 1, -1, 2 })]
    public void A_near_even_row_keeps_equal_gaps_half_a_tile_off_centre(bool mirrored, int[] xs)
        => Assert.Equal(xs, PorchMarks.NearEvenRow(4, 3, 1, mirrored)!.Select(m => m.X));

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void A_near_even_row_is_only_for_an_even_count_and_an_odd_gap(int count, int gap)
        => Assert.Null(PorchMarks.NearEvenRow(count, 3, gap, false));

    [Fact]
    public void With_no_even_row_each_mark_moves_to_the_nearest_clear_tile()
    {
        // Everything round the porch is blocked except two odd tiles (each with the tile above it).
        var blocked = new HashSet<(int X, int Y)>();
        for (int y = 2; y <= 5; y++)
            for (int x = -10; x <= 10; x++)
                blocked.Add(T(x, y));
        blocked.Remove(T(-1, 4)); blocked.Remove(T(4, 4)); blocked.Remove(T(4, 3)); blocked.Remove(T(-1, 3));
        IReadOnlyList<(int X, int Y)> marks = PorchMarks.Choose(DoorX, DoorY, 2, blocked);
        Assert.Equal(2, marks.Distinct().Count());
        Assert.All(marks, m => Assert.True(PorchMarks.IsClear(m.X, m.Y, (x, y) => blocked.Contains((x, y)))));
        Assert.All(marks, m => Assert.True(m.Y >= DoorY + PorchMarks.FirstRow));
    }

    [Fact]
    public void Nothing_clear_in_reach_keeps_the_layout()
    {
        IReadOnlyList<(int X, int Y)> marks = PorchMarks.Choose(DoorX, DoorY, 3, (_, _) => true);
        Assert.Equal(PorchMarks.Default(3).Select(m => T(m.X, m.Y)), marks);
    }

    [Theory]
    [InlineData(3, 2, new[] { 0, -2, 2 })]
    [InlineData(3, 1, new[] { 0, -1, 1 })]
    [InlineData(3, 3, new[] { 0, -3, 3 })]
    [InlineData(4, 2, new[] { 1, -1, 3, -3 })]
    [InlineData(4, 4, new[] { 2, -2, 6, -6 })]
    [InlineData(2, 2, new[] { 1, -1 })]
    [InlineData(2, 4, new[] { 2, -2 })]
    public void An_even_row_is_centred_on_the_farmer(int count, int gap, int[] xs)
        => Assert.Equal(xs, PorchMarks.EvenRow(count, 3, gap)!.Select(m => m.X));

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    public void An_even_count_cannot_centre_an_odd_gap(int count, int gap)
        => Assert.Null(PorchMarks.EvenRow(count, 3, gap));

    [Fact]
    public void No_junimos_no_marks()
        => Assert.Empty(Choose(0));

    /// <summary>Same row, mirrored about the farmer's column, equal gaps.</summary>
    private static void AssertEvenAboutTheFarmer(IReadOnlyList<(int X, int Y)> marks)
    {
        Assert.Single(marks.Select(m => m.Y).Distinct());
        var xs = marks.Select(m => m.X - DoorX).OrderBy(x => x).ToArray();
        Assert.Equal(xs, xs.Select(x => -x).OrderBy(x => x).ToArray());
        var gaps = xs.Zip(xs.Skip(1), (a, b) => b - a).Distinct().ToArray();
        Assert.Single(gaps);
    }
}
