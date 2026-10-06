using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WildcardOddTwistsTests
{
    private static RunState RunAt(Season season, int day, int seed = 1234)
        => new() { Seed = seed, Season = season, DayOfMonth = day };

    [Fact]
    public void Free_footprint_needs_all_four_tiles()
    {
        Assert.True(DebrisPlacement.FreeFootprint((_, _) => true, 10, 20));
        var blocked = new[] { (10, 20), (11, 20), (10, 21), (11, 21) };
        foreach (var (bx, by) in blocked)
            Assert.False(DebrisPlacement.FreeFootprint((x, y) => !(x == bx && y == by), 10, 20));
    }

    [Fact]
    public void Free_footprint_ignores_tiles_outside_it()
    {
        var asked = new List<(int, int)>();
        Assert.True(DebrisPlacement.FreeFootprint((x, y) => { asked.Add((x, y)); return x < 12 && y < 22; }, 10, 20));
        Assert.Equal(4, asked.Count);
    }

    [Fact]
    public void Paths_markers_map_to_the_vanilla_clumps()
    {
        Assert.Equal(602, DebrisPlacement.ClumpFor(19));
        Assert.Equal(672, DebrisPlacement.ClumpFor(20));
        Assert.Equal(600, DebrisPlacement.ClumpFor(21));
        Assert.Null(DebrisPlacement.ClumpFor(18));
        Assert.Null(DebrisPlacement.ClumpFor(22));
        Assert.Null(DebrisPlacement.ClumpFor(-1));
    }

    [Fact]
    public void Night_event_order_tries_every_event_once_and_is_stable()
    {
        var order = WildcardNightEvents.TryOrder(42, 7);
        Assert.Equal(WildcardNightEvents.All.OrderBy(s => s), order.OrderBy(s => s));
        Assert.Equal(order, WildcardNightEvents.TryOrder(42, 7));
        var starts = new HashSet<string>();
        for (int seed = 0; seed < 500; seed++) starts.Add(WildcardNightEvents.TryOrder(seed, 7)[0]);
        Assert.Equal(WildcardNightEvents.All.Count, starts.Count);
    }

    [Fact]
    public void Snow_is_never_drawn_in_winter_or_on_day_one()
    {
        Assert.False(WildcardSchedule.SnowAllowed(3, 10));
        Assert.False(WildcardSchedule.SnowAllowed(0, 1));
        Assert.True(WildcardSchedule.SnowAllowed(1, 9));
        for (int seed = 0; seed < 2000; seed++)
            Assert.NotEqual(WildcardSchedule.SnowDay, WildcardSchedule.TwistFor(seed, 4, true, snowAllowed: false));
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 2000; seed++) seen.Add(WildcardSchedule.TwistFor(seed, 4, true, snowAllowed: true));
        Assert.Contains(WildcardSchedule.SnowDay, seen);
    }

    [Fact]
    public void Reveal_in_winter_never_rolls_snow()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var run = RunAt(Season.Winter, 8, seed);
            WildcardDays.PlanWeek(run, enabled: true);
            run.DayOfMonth = run.WildcardDay;
            Assert.NotEqual(WildcardSchedule.SnowDay, WildcardDays.RevealToday(run, () => true, out _));
        }
    }

    [Fact]
    public void Night_twist_only_for_snow_and_night_event()
    {
        var run = RunAt(Season.Spring, 10);
        Assert.Null(WildcardDays.NightTwistTonight(run));
        WildcardDays.ForceToday(run, WildcardSchedule.SnowDay);
        Assert.Equal(WildcardSchedule.SnowDay, WildcardDays.NightTwistTonight(run));
        WildcardDays.ForceToday(run, WildcardSchedule.NightEvent);
        Assert.Equal(WildcardSchedule.NightEvent, WildcardDays.NightTwistTonight(run));
        foreach (string other in new[] { WildcardSchedule.DebrisReturn, WildcardSchedule.Rockslide, WildcardSchedule.ExtraGrowth })
        {
            WildcardDays.ForceToday(run, other);
            Assert.Null(WildcardDays.NightTwistTonight(run));
        }
    }

    [Fact]
    public void Night_twist_never_on_the_rewind_night()
    {
        var run = RunAt(Season.Summer, 28);
        WildcardDays.ForceToday(run, WildcardSchedule.NightEvent);
        Assert.Null(WildcardDays.NightTwistTonight(run));
    }

    [Fact]
    public void Option_off_gives_no_night_twist()
    {
        var run = RunAt(Season.Spring, 10);
        WildcardDays.PlanWeek(run, enabled: false);
        Assert.Null(WildcardDays.RevealToday(run, () => true, out _));
        Assert.Null(WildcardDays.NightTwistTonight(run));
    }

    [Fact]
    public void A_new_run_forgets_the_night_twist()
    {
        var run = RunAt(Season.Spring, 10);
        run.WildcardNightTwist = WildcardSchedule.SnowDay;
        run.BeginNewRun(5);
        Assert.Null(run.WildcardNightTwist);
    }
}
