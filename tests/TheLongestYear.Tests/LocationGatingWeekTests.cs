using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class LocationGatingWeekTests
{
    [Theory]
    [InlineData("UndergroundMine20", 1)] [InlineData("Farm", 1)] [InlineData("Desert", 9)]
    [InlineData("SkullCave", 9)] [InlineData("Sewer", 5)] [InlineData("BugLand", 5)] [InlineData("WitchSwamp", 13)]
    public void Week_for_location(string key, int week) => Assert.Equal(week, LocationGating.WeekFor(key));

    [Fact]
    public void Easiest_location_wins()
        => Assert.Equal(1, LocationGating.WeekForAny(new List<string> { "Desert", "Beach" }));

    [Theory]
    [InlineData("(O)378", 1, Season.Spring)]  // copper
    [InlineData("(O)380", 2, Season.Spring)]  // iron
    [InlineData("(O)384", 3, Season.Spring)]  // gold
    [InlineData("(O)386", 9, Season.Fall)]    // iridium
    [InlineData("(O)336", 3, Season.Spring)]  // gold bar
    public void Metals_carry_week_and_gate(string id, int week, Season gate)
    {
        ItemAvailability a = MetalsAvailability.Derive(new PoolItem(id, 100, 1, new List<Season>(), new List<string>()))!;
        Assert.Equal(week, a.Week);
        Assert.Equal(gate, a.Gate);
    }

    [Fact]
    public void Desert_fish_is_week_9_whatever_its_spawn_seasons_say()
    {
        var item = new PoolItem("(O)164", 75, 1, new List<Season>(), new List<string> { "Desert" });
        ItemAvailability a = FishAvailability.Derive(item, null);
        Assert.Equal(9, a.Week);
        Assert.Equal(Season.Fall, a.Gate);
    }

    [Fact]
    public void Summer_only_fish_is_week_5()
    {
        var item = new PoolItem("(O)145", 75, 1, new List<Season> { Season.Summer }, new List<string> { "Forest" });
        Assert.Equal(5, FishAvailability.Derive(item, null).Week);
    }
}
