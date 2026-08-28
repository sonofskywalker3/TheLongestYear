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
    [InlineData(MineAreas.Area40, 2, Season.Spring)] [InlineData(MineAreas.Area80, 3, Season.Summer)]
    [InlineData(MineAreas.SkullCavern, 9, Season.Fall)]
    public void Mine_area_week_and_gate(int area, int week, Season gate)
    {
        Assert.Equal(week, AvailabilityWeeks.MineAreaWeek(area));
        Assert.Equal(gate, AvailabilityWeeks.MineAreaGateSeason(area));
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
