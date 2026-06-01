using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeatherForecastTests
{
    [Fact]
    public void Day1_of_any_season_is_always_Sun()
    {
        // From Spring 28, tomorrow = Summer 1 = forced Sun regardless of seed.
        var forecast = WeatherForecast.Build(uniqueId: 123456, daysPlayedToday: 5,
            currentDayOfMonth: 28, currentSeasonIndex: 0, slotsToReveal: 6);
        Assert.Equal("Sun", forecast[0].Weather);
    }

    [Fact]
    public void Day2_of_any_season_is_always_Sun()
    {
        // From Spring 1, tomorrow = Spring 2 = forced Sun.
        var forecast = WeatherForecast.Build(uniqueId: 42, daysPlayedToday: 1,
            currentDayOfMonth: 1, currentSeasonIndex: 0, slotsToReveal: 1);
        Assert.Equal("Sun", forecast[0].Weather);
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
        // Spring 13 = Egg Festival. From Spring 12, slot 0 = Spring 13.
        var forecast = WeatherForecast.Build(1, 12, 12, 0, 2);
        Assert.Equal("Festival", forecast[0].Weather);
    }

    [Fact]
    public void Forecast_returns_concrete_weather_for_open_days()
    {
        // Every day is seed-deterministic — no "?" markers remain.
        var forecast = WeatherForecast.Build(uniqueId: 99, daysPlayedToday: 1,
            currentDayOfMonth: 2, currentSeasonIndex: 0, slotsToReveal: 5);
        foreach (var day in forecast)
        {
            Assert.NotEqual("?", day.Weather);
            Assert.Contains(day.Weather, new[] { "Sun", "Rain", "Storm", "Snow", "Festival" });
        }
    }

    [Fact]
    public void Slot_zero_is_tomorrow_not_today()
    {
        // From Spring 10, the first revealed day is Spring 11 (today is excluded).
        var forecast = WeatherForecast.Build(7, 10, 10, 0, 3);
        Assert.Equal(11, forecast[0].DayOfMonth);
        Assert.Equal(0, forecast[0].SeasonIndex);
    }

    [Fact]
    public void Day_numbers_advance_and_wrap_across_a_season_boundary()
    {
        // From Spring 25: next 6 days = Spring 26, 27, 28, then Summer 1, 2, 3.
        var forecast = WeatherForecast.Build(7, 25, 25, 0, 6);

        Assert.Equal((0, 26), (forecast[0].SeasonIndex, forecast[0].DayOfMonth));
        Assert.Equal((0, 27), (forecast[1].SeasonIndex, forecast[1].DayOfMonth));
        Assert.Equal((0, 28), (forecast[2].SeasonIndex, forecast[2].DayOfMonth));
        Assert.Equal((1, 1), (forecast[3].SeasonIndex, forecast[3].DayOfMonth));
        Assert.Equal((1, 2), (forecast[4].SeasonIndex, forecast[4].DayOfMonth));
        Assert.Equal((1, 3), (forecast[5].SeasonIndex, forecast[5].DayOfMonth));
    }
}
