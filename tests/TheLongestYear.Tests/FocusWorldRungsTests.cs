using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FocusWorldRungsTests
{
    [Theory, InlineData(-1, 0), InlineData(0, 1), InlineData(1, 1), InlineData(2, 2), InlineData(3, 3), InlineData(4, 3)]
    public void Rod_levels_map_to_rungs(int level, int rung) => Assert.Equal(rung, FocusWorldRungs.ToolRung("fishing_rod", level));

    [Theory, InlineData(-3, 0), InlineData(0, 0), InlineData(2, 2), InlineData(4, 4), InlineData(9, 4)]
    public void Metal_tool_levels_clamp(int level, int rung) => Assert.Equal(rung, FocusWorldRungs.ToolRung("axe", level));

    [Fact]
    public void Coop_rung_is_the_best_coop() => Assert.Equal(2, FocusWorldRungs.BuildingRung("coop", new[] { "Coop", "Big Coop", "Barn" }));

    [Fact]
    public void Barn_silo_and_pond_rungs()
    {
        Assert.Equal(3, FocusWorldRungs.BuildingRung("barn", new[] { "Barn", "Deluxe Barn" }));
        Assert.Equal(1, FocusWorldRungs.BuildingRung("silo", new[] { "Silo" }));
        Assert.Equal(1, FocusWorldRungs.BuildingRung("fish_pond", new[] { "Fish Pond" }));
        Assert.Equal(0, FocusWorldRungs.BuildingRung("silo", new[] { "Coop" }));
    }

    [Theory, InlineData(0, false, 0), InlineData(0, true, 1), InlineData(2, false, 1), InlineData(2, true, 2), InlineData(3, false, 2)]
    public void House_rungs(int level, bool pending, int rung) => Assert.Equal(rung, FocusWorldRungs.HouseRung(level, pending));

    [Theory, InlineData(12, 0), InlineData(24, 1), InlineData(35, 1), InlineData(36, 2)]
    public void Backpack_rungs(int size, int rung) => Assert.Equal(rung, FocusWorldRungs.BackpackRung(size));
}
