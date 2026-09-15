using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

/// <summary>WeekMask facts re-homed here (phase 2 task 2 fix round 1) after
/// ObtainabilityModelTests.cs was wholesale replaced per the phase 2 plan: these still-live WeekMask
/// behaviours had no other home. ShiftLater and the instance FromWeekOnward were dead after the
/// DayTable conversion and Task 5 deleted them.</summary>
public class WeekMaskTests
{
    [Fact]
    public void A_season_is_its_four_weeks()
    {
        WeekMask winter = WeekMask.ForSeason(Season.Winter);
        Assert.False(winter.Contains(12));
        Assert.True(winter.Contains(13));
        Assert.True(winter.Contains(16));
        Assert.Equal(13, winter.Earliest);
    }

    [Fact]
    public void Days_map_to_weeks_across_the_year()
    {
        Assert.Equal(1, WeekMask.WeekOfDay(1));
        Assert.Equal(1, WeekMask.WeekOfDay(7));
        Assert.Equal(2, WeekMask.WeekOfDay(8));
        Assert.Equal(15, WeekMask.WeekOfDay(84 + 15)); // Winter 15
        Assert.Equal(16, WeekMask.WeekOfDay(112));
        Assert.Equal(WeekMask.Of(15), WeekMask.ForDays(84 + 15, 84 + 17)); // Night Market
    }

    [Fact]
    public void A_spring_and_winter_fish_is_absent_in_summer()
    {
        WeekMask m = WeekMask.ForSeasons(new[] { Season.Spring, Season.Winter });
        Assert.True(m.Contains(2));
        Assert.False(m.Contains(5));
        Assert.False(m.Contains(6));
        Assert.True(m.Contains(14));
        Assert.Equal("1-4,13-16", m.ToString());
        Assert.Equal(WeekMask.Range(1, 12), WeekMask.All.Except(WeekMask.ForSeason(Season.Winter)));
    }
}
