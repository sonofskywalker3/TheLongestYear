using System;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityDayTableTests
{
    private static bool Summer(int day) => day >= 29 && day <= 56;

    [Fact]
    public void Available_lands_on_the_first_available_day_at_or_after_the_start()
    {
        DayTable t = DayTable.Available(Summer);
        Assert.Equal(29, t.Lands(1));      // wait for Summer
        Assert.Equal(30, t.Lands(30));     // already Summer
        Assert.Null(t.Lands(57));          // Fall onward: never
        Assert.Equal(5, t.LandingWeek(1));
        Assert.False(t.IsEmpty);
        Assert.True(DayTable.None.IsEmpty);
        Assert.Equal(1, DayTable.Always.Lands(1));
        Assert.Equal(112, DayTable.Always.Lands(112));
    }

    [Fact]
    public void InWeeks_matches_the_week_mask_day_by_day()
    {
        DayTable t = DayTable.InWeeks(WeekMask.ForSeason(Season.Winter));
        Assert.Equal(85, t.Lands(1));
        Assert.Equal(13, t.LandingWeek(1));
        Assert.Null(DayTable.InWeeks(WeekMask.None).Lands(1));
    }

    [Fact]
    public void Exact_closes_over_waiting_so_a_dead_planting_day_is_skipped()
    {
        // A crop that only works when planted on day 10 (lands day 14) or day 20 (lands day 24).
        DayTable t = DayTable.Exact(p => p == 10 ? 14 : p == 20 ? 24 : (int?)null);
        Assert.Equal(14, t.Lands(1));
        Assert.Equal(14, t.Lands(10));
        Assert.Equal(24, t.Lands(11));
        Assert.Null(t.Lands(21));
    }

    [Fact]
    public void Delay_adds_days_and_drops_landings_past_winter_28()
    {
        DayTable t = DayTable.Always.Delay(7);
        Assert.Equal(8, t.Lands(1));
        Assert.Equal(112, t.Lands(105));
        Assert.Null(t.Lands(106));
        Assert.Same(t, t.Delay(0));
    }

    [Fact]
    public void Then_chains_the_landing_of_one_table_into_the_start_of_the_next()
    {
        DayTable seed = DayTable.Available(d => d <= 28);          // sold in Spring only
        DayTable grow = DayTable.Exact(p => p + 4 <= 28 ? p + 4 : (int?)null);   // 4 days, must finish in Spring
        DayTable crop = seed.Then(grow);
        Assert.Equal(5, crop.Lands(1));
        Assert.Equal(28, crop.Lands(24));
        Assert.Null(crop.Lands(25));     // seed on 25, but no planting day finishes in Spring
        Assert.Null(crop.Lands(29));     // no seed at all after Spring
    }

    [Fact]
    public void Earliest_takes_the_sooner_landing_and_Latest_needs_both()
    {
        DayTable a = DayTable.Available(d => d >= 10);
        DayTable b = DayTable.Available(d => d >= 20 && d <= 30);
        Assert.Equal(10, a.Earliest(b).Lands(1));
        Assert.Equal(20, a.Latest(b).Lands(1));
        Assert.Null(a.Latest(b).Lands(31));
        Assert.Equal(40, a.Earliest(b).Lands(40));
    }

    [Fact]
    public void Except_keeps_only_starts_where_the_dependable_table_is_later_or_never()
    {
        DayTable any = DayTable.Available(d => d >= 1);
        DayTable dependable = DayTable.Available(d => d >= 29);
        DayTable luckOnly = any.Except(dependable);
        Assert.Equal(1, luckOnly.Lands(1));
        Assert.Null(luckOnly.Lands(29));   // dependable lands the same day, nothing luck-only left
    }

    [Fact]
    public void CanObtain_checks_the_deadline_and_equality_is_by_content()
    {
        DayTable t = DayTable.Available(Summer);
        Assert.True(t.CanObtain(1, 29));
        Assert.False(t.CanObtain(1, 28));
        Assert.Equal(DayTable.Available(Summer), t);
        Assert.NotEqual(DayTable.Always, t);
        Assert.Equal(29, DayTable.SeasonStartDay(Season.Summer));
    }

    [Fact]
    public void ToString_reports_the_landing_week_from_each_season_start()
    {
        Assert.Equal("lands wk5/wk5/never/never", DayTable.Available(Summer).ToString());
        Assert.Equal("lands wk1/wk5/wk9/wk13", DayTable.Always.ToString());
        Assert.Equal("never", DayTable.None.ToString());
    }
}
