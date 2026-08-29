using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class AvailabilityWeeksTests
{
    [Theory]
    [InlineData(1, Season.Spring)] [InlineData(4, Season.Spring)] [InlineData(5, Season.Summer)]
    [InlineData(9, Season.Fall)] [InlineData(13, Season.Winter)] [InlineData(16, Season.Winter)]
    public void Season_of_week(int week, Season expected) => Assert.Equal(expected, AvailabilityWeeks.SeasonOf(week));

    [Theory]
    [InlineData(Season.Spring, 1, 4)] [InlineData(Season.Summer, 5, 8)]
    [InlineData(Season.Fall, 9, 12)] [InlineData(Season.Winter, 13, 16)]
    public void First_and_last_week_of_season(Season season, int first, int last)
    {
        Assert.Equal(first, AvailabilityWeeks.FirstWeekOf(season));
        Assert.Equal(last, AvailabilityWeeks.LastWeekOf(season));
    }

    [Theory]
    [InlineData(MineAreas.Area0, 1, Season.Spring)] [InlineData(MineAreas.Area10, 1, Season.Spring)]
    [InlineData(MineAreas.Area40, 2, Season.Spring)] [InlineData(MineAreas.Area80, 3, Season.Spring)]
    [InlineData(MineAreas.SkullCavern, 9, Season.Fall)]
    public void Mine_area_week_and_gate(int area, int week, Season gate)
    {
        Assert.Equal(week, AvailabilityWeeks.MineAreaWeek(area));
        Assert.Equal(gate, AvailabilityWeeks.MineAreaGateSeason(area));
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(30, 1)] [InlineData(31, 2)] [InlineData(60, 2)]
    [InlineData(61, 3)] [InlineData(90, 3)] [InlineData(91, 4)] [InlineData(120, 4)]
    public void Thirty_floors_a_week(int floor, int week) => Assert.Equal(week, AvailabilityWeeks.MineFloorWeek(floor));

    [Fact]
    public void Every_mine_area_gates_in_spring_and_skull_cavern_in_fall()
    {
        Assert.Equal(Season.Spring, MineAreas.GateSeason(MineAreas.Area80));
        Assert.Equal(3, MineAreas.Week(MineAreas.Area80));
        Assert.Equal(Season.Fall, MineAreas.GateSeason(MineAreas.SkullCavern));
    }

    [Fact]
    public void Desert_has_a_fall_pacing_week_and_a_summer_hard_week()
    {
        Assert.Equal(9, LocationGating.WeekFor("Desert"));
        Assert.Equal(6, LocationGating.HardWeekFor("Desert"));
        Assert.Equal(6, LocationGating.HardWeekFor("SkullCave"));
        Assert.Equal(1, LocationGating.HardWeekFor("Town"));
    }

    [Fact]
    public void Gold_ore_is_a_spring_gate_at_week_3()
    {
        var gold = MetalsAvailability.Derive(new PoolItem("(O)384", 25, 3, new List<Season>(), new List<string>()))!;
        Assert.Equal(3, gold.Week);
        Assert.Equal(Season.Spring, gold.Gate);
    }

    [Theory]
    [InlineData(0, 2)] [InlineData(2, 2)] [InlineData(3, 3)] [InlineData(4, 4)] [InlineData(5, 4)]
    [InlineData(6, 6)] [InlineData(7, 6)] [InlineData(8, 7)] [InlineData(9, 7)] [InlineData(10, 9)]
    public void Machine_level_week(int level, int week) => Assert.Equal(week, AvailabilityWeeks.MachineLevelWeek(level));

    [Theory]
    [InlineData(0, 2)] [InlineData(1, 5)] [InlineData(2, 9)] [InlineData(3, 9)]
    public void Housing_tier_week(int links, int week) => Assert.Equal(week, AvailabilityWeeks.HousingTierWeek(links));

    [Fact]
    public void Record_week_falls_back_to_the_first_week_of_its_season()
    {
        var legacy = new ItemAvailability(Season.Fall, 3, "test");
        Assert.Equal(9, legacy.Week);
        Assert.Equal(Season.Fall, legacy.Gate);
        var explicitWeek = new ItemAvailability(Season.Spring, 3, "test", EarliestWeek: 3, GateSeason: Season.Summer);
        Assert.Equal(3, explicitWeek.Week);
        Assert.Equal(Season.Summer, explicitWeek.Gate);
    }
}
