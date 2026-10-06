using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CartScheduleTests
{
    [Fact]
    public void Vanilla_days_are_friday_and_sunday()
        => Assert.Equal(new[] { 5, 7 }, CartSchedule.VanillaDaysInWeek(1));

    [Fact]
    public void Random_weeks_average_about_two_visits_and_never_zero()
    {
        var counts = Enumerable.Range(0, 2000).Select(s => CartSchedule.RandomDaysInWeek(s, 3, 15, Array.Empty<int>()).Count).ToList();
        Assert.DoesNotContain(0, counts);
        Assert.InRange(counts.Average(), 1.8, 2.6);
    }

    [Fact]
    public void Festival_days_never_get_a_cart()
    {
        var blocked = CartSchedule.BlockedDays(0);
        for (int s = 0; s < 500; s++)
            Assert.DoesNotContain(CartSchedule.RandomDaysInWeek(s, 2, 8, blocked), d => blocked.Contains(d));
    }

    [Fact]
    public void A_week_with_every_day_blocked_has_no_cart()
        => Assert.Empty(CartSchedule.RandomDaysInWeek(1, 2, 8, Enumerable.Range(8, 7).ToList()));

    [Fact]
    public void Days_stay_inside_the_week()
        => Assert.All(CartSchedule.RandomDaysInWeek(9, 4, 22, Array.Empty<int>()), d => Assert.InRange(d, 22, 28));

    [Fact]
    public void Off_gives_vanilla_days()
        => Assert.Equal(new[] { 12, 14 }, CartSchedule.ForWeek(new RunState(), 0, 10, random: false));

    [Fact]
    public void A_rolled_week_is_stored_and_reused()
    {
        var run = new RunState();
        var first = CartSchedule.ForWeek(run, 1, 10, random: true);
        Assert.Equal(6, run.CartDaysWeek);
        run.CartDays = new List<int> { 20 };
        Assert.Equal(new[] { 20 }, CartSchedule.ForWeek(run, 1, 10, random: true));
        Assert.NotNull(first);
    }

    [Fact]
    public void Two_calls_on_one_date_agree_and_store_under_the_dates_own_week()
    {
        var run = new RunState { Seed = 77, DayOfMonth = 1 }; // WeekOfYear lags: must be ignored
        var a = CartSchedule.ForWeek(run, 2, 22, random: true).ToList();
        var b = CartSchedule.ForWeek(run, 2, 22, random: true).ToList();
        Assert.Equal(a, b);
        Assert.Equal(Calendar.WeekOfYear(2, 22), run.CartDaysWeek);
        Assert.All(a, d => Assert.InRange(d, 22, 28));
    }

    [Fact]
    public void Night_market_and_desert_festival_are_blocked()
    {
        Assert.Contains(15, CartSchedule.BlockedDays(3));
        Assert.Contains(16, CartSchedule.BlockedDays(0));
    }
}
