using TheLongestYear.Core;
using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class SpringPaintTests
{
    [Fact]
    public void Values_paint_a_clear_spring_one_morning()
    {
        SpringPaintValues v = SpringPaint.Values();
        Assert.Equal(Season.Spring, v.Season);
        Assert.Equal(1, v.DayOfMonth);
        Assert.Equal(600, v.TimeOfDay);
    }

    [Fact]
    public void Values_clear_every_weather_flag_the_pan_may_have_set()
    {
        SpringPaintValues v = SpringPaint.Values();
        Assert.False(v.Raining);
        Assert.False(v.Snowing);
        Assert.False(v.DebrisWeather);
    }

    [Fact]
    public void Values_accept_a_different_wake_time()
    {
        Assert.Equal(620, SpringPaint.Values(620).TimeOfDay);
    }
}
