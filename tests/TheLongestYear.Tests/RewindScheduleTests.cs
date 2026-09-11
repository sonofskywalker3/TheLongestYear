using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class RewindScheduleTests
{
    [Fact]
    public void SeasonsToUnwind_runs_from_the_failed_season_down_to_spring()
    {
        Assert.Equal(new List<Season> { Season.Fall, Season.Summer, Season.Spring },
            RewindSchedule.SeasonsToUnwind(Season.Fall));
        Assert.Equal(new List<Season> { Season.Winter, Season.Fall, Season.Summer, Season.Spring },
            RewindSchedule.SeasonsToUnwind(Season.Winter));
    }

    [Fact]
    public void SeasonsToUnwind_for_spring_is_spring_alone()
    {
        Assert.Equal(new List<Season> { Season.Spring }, RewindSchedule.SeasonsToUnwind(Season.Spring));
    }

    [Fact]
    public void SwapFractions_divide_the_route_evenly_between_the_seasons()
    {
        Assert.Equal(new List<double> { 1.0 / 3.0, 2.0 / 3.0 }, RewindSchedule.SwapFractions(Season.Fall));
        Assert.Equal(new List<double> { 0.25, 0.5, 0.75 }, RewindSchedule.SwapFractions(Season.Winter));
        Assert.Equal(new List<double> { 0.5 }, RewindSchedule.SwapFractions(Season.Summer));
    }

    [Fact]
    public void SwapFractions_for_spring_is_empty_because_nothing_unwinds()
    {
        Assert.Empty(RewindSchedule.SwapFractions(Season.Spring));
    }

    [Theory]
    [InlineData(0.0, 2400)]
    [InlineData(1.0, 600)]
    [InlineData(0.5, 1500)]
    public void ClockAt_runs_backward_from_start_to_end(double progress, int expected)
    {
        Assert.Equal(expected, RewindSchedule.ClockAt(progress, 2400, 600));
    }

    [Fact]
    public void ClockAt_rounds_down_to_ten_minute_steps()
    {
        int t = RewindSchedule.ClockAt(0.37, 2400, 600);
        Assert.Equal(0, t % 10);
    }
}
