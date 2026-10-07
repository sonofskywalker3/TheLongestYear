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
    public void Reveal_never_rolls_snow_on_a_day_with_paid_weather()
    {
        int snowSeeds = 0;
        for (int seed = 0; seed < 600; seed++)
        {
            var free = RunAt(Season.Summer, 8, seed);
            WildcardDays.PlanWeek(free, enabled: true);
            free.DayOfMonth = free.WildcardDay;
            if (WildcardDays.RevealToday(free, () => true, out _) == WildcardSchedule.SnowDay) snowSeeds++;

            foreach (string weather in new[] { BoostPurchase.Rain, BoostPurchase.Storm })
            {
                var paid = RunAt(Season.Summer, 8, seed);
                WildcardDays.PlanWeek(paid, enabled: true);
                paid.DayOfMonth = paid.WildcardDay;
                paid.WeatherOverrideDay = Calendar.DayOfYear((int)paid.Season, paid.DayOfMonth);
                paid.WeatherOverride = weather;
                Assert.NotEqual(WildcardSchedule.SnowDay, WildcardDays.RevealToday(paid, () => true, out _));
            }
        }
        Assert.True(snowSeeds > 0);
    }

    /// <summary>Jeff: green rain is rare and important; a wildcard snow day never overwrites it.</summary>
    [Fact]
    public void Reveal_never_rolls_snow_on_a_green_rain_day()
    {
        int snowSeeds = 0;
        for (int seed = 0; seed < 600; seed++)
        {
            var free = RunAt(Season.Summer, 8, seed);
            WildcardDays.PlanWeek(free, enabled: true);
            free.DayOfMonth = free.WildcardDay;
            if (WildcardDays.RevealToday(free, () => true, out _, () => false) == WildcardSchedule.SnowDay) snowSeeds++;

            var green = RunAt(Season.Summer, 8, seed);
            WildcardDays.PlanWeek(green, enabled: true);
            green.DayOfMonth = green.WildcardDay;
            Assert.NotEqual(WildcardSchedule.SnowDay, WildcardDays.RevealToday(green, () => true, out bool revealed, () => true));
            Assert.True(revealed);
        }
        Assert.True(snowSeeds > 0);
    }

    [Fact]
    public void Snow_is_allowed_today_only_without_green_rain_or_paid_weather()
    {
        var run = RunAt(Season.Summer, 9);
        Assert.True(WildcardDays.SnowAllowedToday(run, greenRainToday: false));
        Assert.False(WildcardDays.SnowAllowedToday(run, greenRainToday: true));
        run.WeatherOverrideDay = Calendar.DayOfYear((int)run.Season, run.DayOfMonth);
        run.WeatherOverride = BoostPurchase.Rain;
        Assert.False(WildcardDays.SnowAllowedToday(run, greenRainToday: false));
    }

    [Fact]
    public void Paid_weather_for_another_day_leaves_the_roll_unchanged()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var free = RunAt(Season.Spring, 15, seed);
            WildcardDays.PlanWeek(free, enabled: true);
            free.DayOfMonth = free.WildcardDay;
            var other = RunAt(Season.Spring, 15, seed);
            WildcardDays.PlanWeek(other, enabled: true);
            other.DayOfMonth = other.WildcardDay;
            other.WeatherOverrideDay = Calendar.DayOfYear((int)other.Season, other.DayOfMonth) + 1;
            other.WeatherOverride = BoostPurchase.Rain;
            Assert.Equal(WildcardDays.RevealToday(free, () => true, out _), WildcardDays.RevealToday(other, () => true, out _));
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
