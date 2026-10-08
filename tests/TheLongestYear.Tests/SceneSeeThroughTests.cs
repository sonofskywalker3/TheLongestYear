using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08: the thief's chest was hidden behind a tree. Whatever is drawn in
/// front of the chest, its lid or his path goes see-through for the scene.</summary>
public class SceneSeeThroughTests
{
    private static readonly HashSet<(int X, int Y)> ChestAt69_21 = SceneSeeThrough.Watched((69, 21), null);

    [Fact]
    public void The_chest_and_its_lid_are_watched()
    {
        Assert.Contains((69, 21), ChestAt69_21);
        Assert.Contains((69, 20), ChestAt69_21);
        Assert.Equal(2, ChestAt69_21.Count);
    }

    [Fact]
    public void The_walk_is_watched_with_his_head_above_it()
    {
        var watched = SceneSeeThrough.Watched((10, 10), new[] { (12, 10), (11, 10) });
        Assert.Contains((12, 9), watched);
        Assert.Contains((11, 10), watched);
    }

    [Theory]
    [InlineData(69, 22, true)]   // trunk just below: the canopy covers the chest
    [InlineData(69, 26, true)]   // five rows below still reaches the lid
    [InlineData(70, 23, true)]   // a column to the side
    [InlineData(69, 27, false)]  // too far below
    [InlineData(71, 22, false)]  // two columns over
    [InlineData(69, 19, false)]  // behind the chest, drawn behind it
    public void A_tree_in_front_hides_the_chest(int x, int y, bool hides)
        => Assert.Equal(hides, SceneSeeThrough.Hides(SceneSeeThrough.Tree(x, y), ChestAt69_21));

    [Theory]
    [InlineData(69, 25, true)]
    [InlineData(69, 26, false)]
    public void A_fruit_tree_is_a_row_shorter(int x, int y, bool hides)
        => Assert.Equal(hides, SceneSeeThrough.Hides(SceneSeeThrough.FruitTree(x, y), ChestAt69_21));

    [Theory]
    [InlineData(69, 22, 0, true)]   // a small bush just below covers the chest
    [InlineData(67, 22, 2, true)]   // a big bush is three tiles wide
    [InlineData(66, 22, 2, false)]
    [InlineData(69, 23, 0, false)]  // two rows below covers nothing of it
    public void A_bush_covers_the_row_above_it(int x, int y, int extraWide, bool hides)
        => Assert.Equal(hides, SceneSeeThrough.Hides(SceneSeeThrough.Bush(x, y, extraWide), ChestAt69_21));

    [Fact]
    public void A_building_hides_what_is_behind_its_roof()
    {
        // A coop-sized building: footprint 6 wide, 3 high at (66,22), sprite 7 tiles tall.
        TileBox box = SceneSeeThrough.Building(66, 22, 6, 3, 7);
        Assert.True(SceneSeeThrough.Hides(box, ChestAt69_21));
        Assert.False(SceneSeeThrough.Hides(SceneSeeThrough.Building(66, 26, 6, 3, 7), ChestAt69_21));
    }

    [Fact]
    public void Fading_stops_at_vanillas_opacity()
    {
        float a = 1f;
        for (int i = 0; i < 20; i++) a = SceneSeeThrough.FadeToward(a);
        Assert.Equal(SceneSeeThrough.FadedAlpha, a);
    }
}
