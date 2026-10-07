using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WildcardDaysTests
{
    private static RunState RunAt(Season season, int day, int seed = 1234)
        => new() { Seed = seed, Season = season, DayOfMonth = day };

    [Fact]
    public void Option_off_plans_nothing_and_writes_nothing()
    {
        var run = RunAt(Season.Spring, 8);
        Assert.False(WildcardDays.PlanWeek(run, enabled: false));
        Assert.Equal(-1, run.WildcardWeek);
        Assert.Equal(0, run.WildcardDay);
        Assert.Null(run.WildcardTwist);
        Assert.Null(WildcardDays.RevealToday(run, () => true, out bool revealed));
        Assert.False(revealed);
        Assert.False(WildcardDays.GrowthTonight(run));
    }

    [Fact]
    public void Option_off_drops_a_stale_plan()
    {
        var run = RunAt(Season.Spring, 1);
        WildcardDays.PlanWeek(run, enabled: true);
        run.DayOfMonth = 8;
        WildcardDays.PlanWeek(run, enabled: false);
        Assert.Equal(-1, run.WildcardWeek);
        Assert.Equal(0, run.WildcardDay);
    }

    [Fact]
    public void Plan_stores_the_schedule_day_once_per_week()
    {
        var run = RunAt(Season.Summer, 15);
        Assert.True(WildcardDays.PlanWeek(run, enabled: true));
        Assert.Equal(run.WeekOfYear, run.WildcardWeek);
        Assert.Equal(WildcardSchedule.DayFor(run.Seed, run.WeekOfYear, WildcardSchedule.BlockedDays(1)), run.WildcardDay);
        Assert.False(WildcardDays.PlanWeek(run, enabled: true));
    }

    [Fact]
    public void Twist_is_revealed_only_on_the_wildcard_morning_and_never_rerolled()
    {
        var run = RunAt(Season.Spring, 8);
        WildcardDays.PlanWeek(run, enabled: true);
        int wildDay = run.WildcardDay;
        Assert.NotEqual(0, wildDay);

        for (int d = 8; d < 15; d++)
        {
            if (d == wildDay) continue;
            run.DayOfMonth = d;
            Assert.Null(WildcardDays.RevealToday(run, () => true, out _));
        }

        run.DayOfMonth = wildDay;
        string? twist = WildcardDays.RevealToday(run, () => true, out bool first);
        Assert.True(first);
        Assert.Equal(WildcardSchedule.TwistFor(run.Seed, run.WeekOfYear, true), twist);
        Assert.Equal(wildDay, run.WildcardTwistDay);

        // A reload stores a different value than a re-roll would give; the stored one wins.
        run.WildcardTwist = WildcardSchedule.SnowDay;
        Assert.Equal(WildcardSchedule.SnowDay, WildcardDays.RevealToday(run, () => true, out bool again));
        Assert.False(again);
        Assert.Equal(WildcardSchedule.SnowDay, WildcardDays.StoredTwistToday(run));
    }

    [Fact]
    public void Stored_twist_is_not_today_on_the_next_day()
    {
        var run = RunAt(Season.Spring, 8);
        WildcardDays.ForceToday(run, WildcardSchedule.ExtraGrowth);
        Assert.True(WildcardDays.GrowthTonight(run));
        run.DayOfMonth = 9;
        Assert.Null(WildcardDays.StoredTwistToday(run));
        Assert.False(WildcardDays.GrowthTonight(run));
    }

    [Fact]
    public void Growth_night_only_for_extra_growth()
    {
        var run = RunAt(Season.Fall, 3);
        WildcardDays.ForceToday(run, WildcardSchedule.ShopSale);
        Assert.False(WildcardDays.GrowthTonight(run));
    }

    [Fact]
    public void A_new_run_forgets_the_plan_and_growth_night()
    {
        var run = RunAt(Season.Spring, 8);
        WildcardDays.ForceToday(run, WildcardSchedule.ExtraGrowth);
        run.WildcardGrowthNight = true;
        run.BeginNewRun(99);
        Assert.Equal(-1, run.WildcardWeek);
        Assert.Null(run.WildcardTwist);
        Assert.False(run.WildcardGrowthNight);
    }

    [Fact]
    public void Clear_twist_keeps_the_day()
    {
        var run = RunAt(Season.Spring, 8);
        WildcardDays.ForceToday(run, WildcardSchedule.MaxLuck);
        WildcardDays.ClearTwist(run);
        Assert.Null(WildcardDays.StoredTwistToday(run));
        Assert.Equal(8, run.WildcardDay);
    }
}

public class WildcardEffectsTests
{
    [Fact]
    public void Every_rule_is_the_identity_when_the_twist_is_off()
    {
        Assert.Equal(0, WildcardEffects.ForageExtraFor(false));
        Assert.Equal(1f, WildcardEffects.BiteFactor(false, false));
        Assert.Equal(15, WildcardEffects.ShopPercent(15, false));
        Assert.Equal(0, WildcardEffects.ShopPercent(0, false));
        Assert.Equal(137, WildcardEffects.SellPrice(137, false));
        Assert.Equal(-0.05, WildcardEffects.Luck(-0.05, false));
        Assert.Equal(40f, WildcardEffects.ScaleStamina(50f, 40f, false, 1200, false));
    }

    [Fact]
    public void Good_twists()
    {
        Assert.Equal(1, WildcardEffects.ForageExtraFor(true));
        Assert.Equal(0.70f, WildcardEffects.BiteFactor(true, false));
        Assert.Equal(25, WildcardEffects.ShopPercent(0, true));
        Assert.Equal(60, WildcardEffects.ShopPercent(35, true));
        Assert.Equal(0.10, WildcardEffects.Luck(-0.1, true));
    }

    [Fact]
    public void Bad_twists()
    {
        Assert.Equal(1.30f, WildcardEffects.BiteFactor(false, true));
        Assert.Equal(75, WildcardEffects.SellPrice(100, true));
        Assert.Equal(1, WildcardEffects.SellPrice(1, true));
        Assert.Equal(0, WildcardEffects.SellPrice(0, true));
        // The theme halving applies first, then the wildcard's 75%.
        Assert.Equal(37, WildcardEffects.SellPrice(System.Math.Max(1, 100 / 2), true));
    }

    [Fact]
    public void Energy_drain_scales_daytime_losses_only()
    {
        Assert.Equal(35f, WildcardEffects.ScaleStamina(50f, 40f, true, 1200, false));
        Assert.Equal(60f, WildcardEffects.ScaleStamina(50f, 60f, true, 1200, false));   // a gain passes
        Assert.Equal(40f, WildcardEffects.ScaleStamina(50f, 40f, true, 1200, true));    // night update
        Assert.Equal(40f, WildcardEffects.ScaleStamina(50f, 40f, true, 2600, false));   // pass-out time
        Assert.Equal(40f, WildcardEffects.ScaleStamina(50f, 40f, true, 550, false));
        Assert.Equal(-1f, WildcardEffects.ScaleStamina(5f, 1f, true, 600, false));
    }
}
