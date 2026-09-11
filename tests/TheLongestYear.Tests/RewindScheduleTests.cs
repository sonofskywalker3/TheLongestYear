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
    public void DateAt_starts_on_the_failed_date_and_ends_on_spring_one()
    {
        Assert.Equal((Season.Fall, 28), RewindSchedule.DateAt(0.0, Season.Fall));
        Assert.Equal((Season.Spring, 1), RewindSchedule.DateAt(1.0, Season.Fall));

        Assert.Equal((Season.Winter, 28), RewindSchedule.DateAt(0.0, Season.Winter));
        Assert.Equal((Season.Spring, 1), RewindSchedule.DateAt(1.0, Season.Winter));
    }

    [Fact]
    public void DateAt_for_spring_still_counts_the_month_down_with_nothing_to_repaint()
    {
        Assert.Equal((Season.Spring, 28), RewindSchedule.DateAt(0.0, Season.Spring));
        Assert.Equal((Season.Spring, 14), RewindSchedule.DateAt(0.5, Season.Spring));
        Assert.Equal((Season.Spring, 1), RewindSchedule.DateAt(1.0, Season.Spring));
    }

    [Fact]
    public void DateAt_changes_season_exactly_where_the_map_repaints()
    {
        // The date dial and the tilesheet swaps have to agree on which season is on screen, or the
        // HUD reads one season over a map painted as another.
        foreach (Season failed in new[] { Season.Summer, Season.Fall, Season.Winter })
        {
            IReadOnlyList<Season> seasons = RewindSchedule.SeasonsToUnwind(failed);
            IReadOnlyList<double> swaps = RewindSchedule.SwapFractions(failed);
            for (int i = 0; i < swaps.Count; i++)
            {
                Assert.Equal(seasons[i], RewindSchedule.DateAt(swaps[i] - 1e-9, failed).Season);
                Assert.Equal(seasons[i + 1], RewindSchedule.DateAt(swaps[i], failed).Season);
            }
        }
    }

    [Fact]
    public void DateAt_never_leaves_the_month()
    {
        for (int i = 0; i <= 300; i++)
        {
            (Season _, int day) = RewindSchedule.DateAt(i / 300.0, Season.Winter);
            Assert.InRange(day, 1, 28);
        }
    }

    [Fact]
    public void DateAt_only_ever_runs_backward()
    {
        (Season Season, int DayOfMonth) previous = RewindSchedule.DateAt(0.0, Season.Fall);
        for (int i = 1; i <= 300; i++)
        {
            (Season Season, int DayOfMonth) now = RewindSchedule.DateAt(i / 300.0, Season.Fall);
            bool sameSeasonAndNotLater = now.Season == previous.Season && now.DayOfMonth <= previous.DayOfMonth;
            bool steppedBackASeason = (int)now.Season == (int)previous.Season - 1;
            Assert.True(sameSeasonAndNotLater || steppedBackASeason);
            previous = now;
        }
    }

    [Fact]
    public void CycleClockAt_repeats_dusk_to_dawn_on_its_own_clock()
    {
        // Fifteen cycles across a thirty second scene: every cycle looks like every other one, which
        // is what makes the light read as time coming undone over and over rather than as one sunset.
        Assert.Equal(RewindSchedule.CycleClockAt(0, 2000, 2400, 600),
                     RewindSchedule.CycleClockAt(2000, 2000, 2400, 600));
        Assert.Equal(RewindSchedule.CycleClockAt(500, 2000, 2400, 600),
                     RewindSchedule.CycleClockAt(28500, 2000, 2400, 600));
        Assert.Equal(2400, RewindSchedule.CycleClockAt(0, 2000, 2400, 600));
    }

    [Fact]
    public void CycleClockAt_runs_backward_within_one_cycle()
    {
        int early = RewindSchedule.CycleClockAt(200, 2000, 2400, 600);
        int late = RewindSchedule.CycleClockAt(1800, 2000, 2400, 600);
        Assert.True(late < early);
    }

    [Fact]
    public void The_light_and_the_date_are_not_synchronised()
    {
        // Jeff, 2026-09-11, was explicit: the date must not line up with the visual sunset. Over the
        // thirty seconds the date unwinds ONCE, start to end, while the light goes round fifteen
        // times, so the two never track each other.
        const double totalMs = 30000.0, cycleMs = 2000.0;

        int dusks = 0;
        int previous = RewindSchedule.CycleClockAt(0, cycleMs, 2400, 600);
        for (int ms = 1; ms <= (int)totalMs; ms++)
        {
            int now = RewindSchedule.CycleClockAt(ms, cycleMs, 2400, 600);
            if (now > previous) dusks++;   // the clock jumped back up: a new dusk
            previous = now;
        }
        Assert.Equal((int)(totalMs / cycleMs), dusks);

        // The date, over the same stretch, goes from the failed date to Spring 1 and no further.
        Assert.Equal((Season.Fall, 28), RewindSchedule.DateAt(0.0, Season.Fall));
        Assert.Equal((Season.Spring, 1), RewindSchedule.DateAt(1.0, Season.Fall));
    }
}
