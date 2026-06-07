using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeatherSchedulerTests
{
    private static int CountDays(string[] schedule, string weather)
        => schedule.Skip(1).Count(d => d == weather);  // skip 1 because schedule is 1-indexed

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99999)]
    [InlineData(-1)]
    public void Spring_has_at_least_2_rain_days(int seed)
    {
        var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex: 0);
        Assert.True(CountDays(schedule, "Rain") >= 2,
            $"Spring (seed={seed}) had only {CountDays(schedule, "Rain")} rain days.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99999)]
    public void Summer_has_at_least_2_storm_days_and_2_rain_days(int seed)
    {
        var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex: 1);
        Assert.True(CountDays(schedule, "Storm") >= 2,
            $"Summer (seed={seed}) had only {CountDays(schedule, "Storm")} storm days.");
        Assert.True(CountDays(schedule, "Rain") >= 2,
            $"Summer (seed={seed}) had only {CountDays(schedule, "Rain")} rain days.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99999)]
    public void Fall_has_at_least_2_rain_days(int seed)
    {
        var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex: 2);
        Assert.True(CountDays(schedule, "Rain") >= 2,
            $"Fall (seed={seed}) had only {CountDays(schedule, "Rain")} rain days.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99999)]
    public void Winter_has_at_least_2_snow_days(int seed)
    {
        var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex: 3);
        Assert.True(CountDays(schedule, "Snow") >= 2,
            $"Winter (seed={seed}) had only {CountDays(schedule, "Snow")} snow days.");
    }

    [Theory]
    [InlineData(0, "Rain")]   // Spring
    [InlineData(1, "Rain")]   // Summer
    [InlineData(2, "Rain")]   // Fall
    [InlineData(3, "Snow")]   // Winter
    public void Each_season_has_its_special_weather_in_week_one(int seasonIndex, string weather)
    {
        // Days 1-2 are forced Sun, so the guaranteed week-1 special day must land in days 3-7.
        for (int seed = 0; seed < 50; seed++)
        {
            var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex);
            bool inWeekOne = false;
            for (int d = 3; d <= 7; d++)
                if (schedule[d] == weather) inWeekOne = true;
            Assert.True(inWeekOne,
                $"Season {seasonIndex} (seed={seed}) had no {weather} day in week 1 (days 3-7).");
        }
    }

    [Fact]
    public void Spring_never_has_storms()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex: 0);
            Assert.Equal(0, CountDays(schedule, "Storm"));
        }
    }

    [Theory]
    [InlineData(0)]  // Spring
    [InlineData(1)]  // Summer
    [InlineData(2)]  // Fall
    [InlineData(3)]  // Winter
    public void Days_1_and_2_are_always_Sun(int seasonIndex)
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var schedule = WeatherScheduler.BuildSchedule(seed, seasonIndex);
            Assert.Equal("Sun", schedule[1]);
            Assert.Equal("Sun", schedule[2]);
        }
    }

    [Fact]
    public void Festival_days_are_marked_Festival()
    {
        // Spring festivals: 13 (Egg Festival), 24 (Flower Dance).
        var schedule = WeatherScheduler.BuildSchedule(uniqueId: 42, seasonIndex: 0);
        Assert.Equal("Festival", schedule[13]);
        Assert.Equal("Festival", schedule[24]);
    }

    [Fact]
    public void Same_seed_produces_same_schedule()
    {
        var a = WeatherScheduler.BuildSchedule(uniqueId: 12345, seasonIndex: 1);
        var b = WeatherScheduler.BuildSchedule(uniqueId: 12345, seasonIndex: 1);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_seeds_produce_different_schedules()
    {
        // Not strictly guaranteed (RNG collision), but two seeds picked far apart should differ
        // in at least one slot. If this ever fails, the seed→placement mapping has degenerated.
        var a = WeatherScheduler.BuildSchedule(uniqueId: 1, seasonIndex: 0);
        var b = WeatherScheduler.BuildSchedule(uniqueId: 999999, seasonIndex: 0);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Different_seasons_produce_different_schedules_for_same_seed()
    {
        // Spring and Summer schedules with the same uniqueId should differ — proves the
        // seasonIndex feeds into the RNG seed (not just the placement rules).
        var spring = WeatherScheduler.BuildSchedule(uniqueId: 7, seasonIndex: 0);
        var summer = WeatherScheduler.BuildSchedule(uniqueId: 7, seasonIndex: 1);
        Assert.NotEqual(spring, summer);
    }

    [Fact]
    public void WeatherFor_returns_null_for_out_of_range_inputs()
    {
        Assert.Null(WeatherScheduler.WeatherFor(1, 0, 0));
        Assert.Null(WeatherScheduler.WeatherFor(1, 0, 29));
        Assert.Null(WeatherScheduler.WeatherFor(1, -1, 5));
        Assert.Null(WeatherScheduler.WeatherFor(1, 4, 5));
    }

    [Fact]
    public void Every_day_of_schedule_is_filled()
    {
        var schedule = WeatherScheduler.BuildSchedule(uniqueId: 555, seasonIndex: 1);
        for (int d = 1; d <= WeatherScheduler.DaysPerMonth; d++)
            Assert.NotNull(schedule[d]);
    }
}
