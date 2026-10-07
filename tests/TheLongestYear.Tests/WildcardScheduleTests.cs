using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WildcardScheduleTests
{
    private const int WeeksPerYear = 16;
    private const int WeeksPerSeason = 4;
    private const int DaysPerWeek = 7;
    private const int LastDay = 28;

    [Fact]
    public void Day_is_inside_its_week_and_never_blocked_or_28()
    {
        for (int seed = 0; seed < 200; seed++)
            for (int week = 1; week <= WeeksPerYear; week++)
            {
                var blocked = WildcardSchedule.BlockedDays((week - 1) / WeeksPerSeason);
                int day = WildcardSchedule.DayFor(seed, week, blocked);
                int start = ((week - 1) % WeeksPerSeason) * DaysPerWeek + 1;
                if (day == 0) continue;
                Assert.InRange(day, start, start + DaysPerWeek - 1);
                Assert.DoesNotContain(day, blocked);
                Assert.NotEqual(LastDay, day);
            }
    }

    [Fact]
    public void Blocked_days_add_28_to_the_cart_blocked_days()
    {
        for (int s = 0; s < 4; s++)
        {
            var blocked = WildcardSchedule.BlockedDays(s);
            Assert.Contains(LastDay, blocked);
            foreach (int d in CartSchedule.BlockedDays(s)) Assert.Contains(d, blocked);
        }
    }

    [Fact]
    public void A_week_with_every_day_blocked_returns_zero()
    {
        var all = Enumerable.Range(1, LastDay).ToList();
        Assert.Equal(0, WildcardSchedule.DayFor(7, 2, all));
    }

    [Fact]
    public void Spring_week_two_never_lands_on_the_13th()
    {
        var blocked = WildcardSchedule.BlockedDays(0);
        for (int seed = 0; seed < 500; seed++)
            Assert.NotEqual(13, WildcardSchedule.DayFor(seed, 2, blocked));
    }

    [Fact]
    public void Every_twist_shows_up_over_many_seeds()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 2000; seed++) seen.Add(WildcardSchedule.TwistFor(seed, 3, true));
        Assert.Equal(WildcardSchedule.AllTwists.Count, seen.Count);
    }

    [Fact]
    public void Rockslide_never_comes_up_while_the_minecarts_are_unrepaired()
    {
        for (int seed = 0; seed < 2000; seed++)
            Assert.NotEqual(WildcardSchedule.Rockslide, WildcardSchedule.TwistFor(seed, 5, false));
    }

    [Fact]
    public void Rolls_are_deterministic()
    {
        var blocked = WildcardSchedule.BlockedDays(1);
        Assert.Equal(WildcardSchedule.DayFor(42, 6, blocked), WildcardSchedule.DayFor(42, 6, blocked));
        Assert.Equal(WildcardSchedule.TwistFor(42, 6, true), WildcardSchedule.TwistFor(42, 6, true));
    }

    [Fact]
    public void Twist_pool_has_the_thirteen_ids()
    {
        Assert.Equal(13, WildcardSchedule.AllTwists.Count);
        Assert.Equal(13, WildcardSchedule.AllTwists.Distinct().Count());
    }

    [Fact]
    public void Wildcard_run_state_round_trips_and_clears_on_a_new_run()
    {
        var run = new RunState { WildcardWeek = 3, WildcardDay = 17, WildcardTwist = "snow_day", WildcardTwistDay = 17 };
        RunState back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(3, back.WildcardWeek);
        Assert.Equal(17, back.WildcardDay);
        Assert.Equal("snow_day", back.WildcardTwist);
        Assert.Equal(17, back.WildcardTwistDay);
        back.BeginNewRun(1);
        Assert.Equal(-1, back.WildcardWeek);
        Assert.Equal(0, back.WildcardDay);
        Assert.Null(back.WildcardTwist);
        Assert.Equal(0, back.WildcardTwistDay);
    }
}
