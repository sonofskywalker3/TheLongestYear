using TheLongestYear.Core;
using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class NightPaintTests
{
    [Theory]
    [InlineData(Season.Spring)]
    [InlineData(Season.Summer)]
    [InlineData(Season.Fall)]
    [InlineData(Season.Winter)]
    public void Values_paint_the_night_of_the_day_that_failed(Season failed)
    {
        NightPaintValues v = NightPaint.Values(failed);

        // The failed season, NOT the one the overnight transition already rolled the globals to:
        // the whole point is that a Spring 28 failure stops reading "Summer 1".
        Assert.Equal(failed, v.Season);
        Assert.Equal(28, v.DayOfMonth);
    }

    [Fact]
    public void Values_read_three_in_the_morning()
    {
        // Fiction, not simulation (Jeff, 2026-09-11): 3am leaves room for the player to have passed
        // out elsewhere and been carried to bed.
        Assert.Equal(300, NightPaint.Values(Season.Fall).TimeOfDay);
    }

    [Fact]
    public void Night_is_the_mirror_of_the_spring_paint_at_the_other_end()
    {
        NightPaintValues night = NightPaint.Values(Season.Winter);
        SpringPaintValues morning = SpringPaint.Values();

        Assert.NotEqual(night.Season, morning.Season);
        Assert.NotEqual(night.DayOfMonth, morning.DayOfMonth);
        Assert.True(night.TimeOfDay < morning.TimeOfDay);
    }
}
