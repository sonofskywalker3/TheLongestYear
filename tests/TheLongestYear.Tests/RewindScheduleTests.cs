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
    [Fact]
    public void CycleClockAt_repeats_dusk_to_dawn_on_its_own_clock()
    {
        // Fifteen cycles across a thirty second scene: every cycle looks like every other one, which
        // is what makes the light read as time coming undone over and over rather than as one sunset.
        Assert.Equal(RewindSchedule.CycleClockAt(0, 2000, 2200, 1700),
                     RewindSchedule.CycleClockAt(2000, 2000, 2200, 1700));
        Assert.Equal(RewindSchedule.CycleClockAt(500, 2000, 2200, 1700),
                     RewindSchedule.CycleClockAt(28500, 2000, 2200, 1700));
        Assert.Equal(2200, RewindSchedule.CycleClockAt(0, 2000, 2200, 1700));
    }

    [Fact]
    public void CycleClockAt_is_a_triangle_a_sunrise_then_a_sunset()
    {
        // A sawtooth had no sunrise in it: it only ever ran one way and cut back (playtest
        // 2026-09-11, "there is no sunrise"). The first half of a cycle runs the clock BACK from
        // dark to light, the second half runs it FORWARD from light to dark, and the two ends meet.
        const double cycle = 2000.0;

        Assert.Equal(2200, RewindSchedule.CycleClockAt(0, cycle, 2200, 1700));
        Assert.Equal(1700, RewindSchedule.CycleClockAt(cycle / 2.0, cycle, 2200, 1700));

        int quarterIn = RewindSchedule.CycleClockAt(cycle / 4.0, cycle, 2200, 1700);
        int threeQuartersIn = RewindSchedule.CycleClockAt(cycle * 3.0 / 4.0, cycle, 2200, 1700);
        Assert.InRange(quarterIn, 1701, 2199);          // mid sunrise
        Assert.InRange(threeQuartersIn, 1701, 2199);    // mid sunset
        Assert.Equal(quarterIn, threeQuartersIn);       // the same point, walked the other way
    }

    [Fact]
    public void CycleClockAt_never_jumps_between_neighbouring_moments()
    {
        // The whole complaint about the sawtooth was the hard cut at the seam. A triangle has no
        // seam: over a whole cycle no two adjacent milliseconds are more than one ten-minute step
        // apart, wrap included.
        const double cycle = 2000.0;
        int previous = RewindSchedule.CycleClockAt(0, cycle, 2200, 1700);
        for (int ms = 1; ms <= 4000; ms++)
        {
            int now = RewindSchedule.CycleClockAt(ms, cycle, 2200, 1700);
            Assert.True(System.Math.Abs(Minutes(now) - Minutes(previous)) <= 10,
                $"jumped from {previous} to {now} at {ms}ms");
            previous = now;
        }
    }

    private static int Minutes(int clock) => clock / 100 * 60 + clock % 100;

    [Fact]
    public void The_light_goes_round_fifteen_times_across_the_scene()
    {
        // Two seconds a cycle over a thirty second pan. Each cycle is a sunrise and a sunset, so
        // the valley lights and unlights itself fifteen times as the year comes undone.
        const double totalMs = 30000.0, cycleMs = 2000.0;

        int dawns = 0;
        for (int ms = 1; ms <= (int)totalMs; ms++)
        {
            bool wasDawn = RewindSchedule.CycleClockAt(ms - 1, cycleMs, 2200, 1700) != 1700;
            bool isDawn = RewindSchedule.CycleClockAt(ms, cycleMs, 2200, 1700) == 1700;
            if (wasDawn && isDawn) dawns++;   // reached full daylight: the bottom of a triangle
        }
        Assert.Equal((int)(totalMs / cycleMs), dawns);
    }
}
