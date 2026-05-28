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
        // forecast[0] = tomorrow = day 1 of Summer = "Sun"
        Assert.Equal("Sun", forecast[0]);
    }

    [Fact]
    public void Day2_of_any_season_is_always_Sun()
    {
        var forecast = WeatherForecast.Build(uniqueId: 42, daysPlayedToday: 1,
            currentDayOfMonth: 1, currentSeasonIndex: 0, slotsToReveal: 1);
        // forecast[0] = day 2 = Sun
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
    public void Forecast_returns_concrete_weather_for_open_days()
    {
        // The scheduler determines every day's weather seed-deterministically. No "?" markers
        // remain in the forecast — any non-festival day is one of Sun/Rain/Storm/Snow.
        var forecast = WeatherForecast.Build(uniqueId: 99, daysPlayedToday: 1,
            currentDayOfMonth: 2, currentSeasonIndex: 0, slotsToReveal: 5);
        foreach (string day in forecast)
        {
            Assert.NotEqual("?", day);
            Assert.Contains(day, new[] { "Sun", "Rain", "Storm", "Snow", "Festival" });
        }
    }
}
