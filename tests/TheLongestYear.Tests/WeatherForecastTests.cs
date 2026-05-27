using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeatherForecastTests
{
    [Fact]
    public void Day1_of_any_season_is_always_Sun()
    {
        // Spring day 1 = forced Sun regardless of seed.
        var forecast = WeatherForecast.Build(uniqueId: 123456, daysPlayedToday: 5,
            currentDayOfMonth: 28, currentSeasonIndex: 0, slotsToReveal: 7);
        // Next week starts on day 1 of next season — verify slot 0 is Sun.
        // DayOfMonth advances: from day 28, next day is 1 (new season).
        // forecast[0] = tomorrow = day 1 of Summer = "Sun"
        Assert.Equal("Sun", forecast[0]);
    }

    [Fact]
    public void Returns_correct_slot_count()
    {
        var forecast = WeatherForecast.Build(123, 10, 7, 0, 4);
        Assert.Equal(4, forecast.Length);
    }

    [Fact]
    public void Festival_days_return_Festival()
    {
        // Spring 13 = Egg Festival. Build a forecast from Spring 12 (dayOfMonth=12).
        var forecast = WeatherForecast.Build(1, 12, 12, 0, 2);
        // forecast[0] = day 13 of Spring = Festival
        Assert.Equal("Festival", forecast[0]);
    }

    [Fact]
    public void Summer_storm_day_returns_Storm()
    {
        // Summer day 13 % 13 == 0 => Storm. Build from Summer day 12.
        // seasonIndex 1 = Summer.
        var forecast = WeatherForecast.Build(1, 40, 12, 1, 2);
        // forecast[0] = Summer day 13 = Storm
        Assert.Equal("Storm", forecast[0]);
    }

    [Fact]
    public void Slots_beyond_forced_rules_return_unknown_marker()
    {
        // Spring day 2: no forced rule. Should be "?".
        var forecast = WeatherForecast.Build(42, 1, 1, 0, 3);
        // forecast[0] = Spring day 2 = no forced rule = "?"
        Assert.Equal("?", forecast[0]);
    }
}
