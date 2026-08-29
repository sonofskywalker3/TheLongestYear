using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CalendarDayOfYearTests
{
    [Theory]
    [InlineData(0, 1, 1)] [InlineData(0, 28, 28)] [InlineData(1, 1, 29)] [InlineData(3, 28, 112)]
    public void DayOfYear_counts_from_spring_1(int month, int day, int expected)
        => Assert.Equal(expected, Calendar.DayOfYear(month, day));

    [Theory]
    [InlineData(1, 7)] [InlineData(7, 7)] [InlineData(8, 14)] [InlineData(28, 28)] [InlineData(29, 35)] [InlineData(112, 112)]
    public void LastDayOfWeek_is_the_end_of_the_seven_day_block(int day, int expected)
        => Assert.Equal(expected, Calendar.LastDayOfWeek(day));

    [Theory]
    [InlineData(1, 28)] [InlineData(28, 28)] [InlineData(29, 56)] [InlineData(100, 112)]
    public void LastDayOfSeason_is_day_28_of_that_season(int day, int expected)
        => Assert.Equal(expected, Calendar.LastDayOfSeason(day));

    [Theory]
    [InlineData(1, Season.Spring)] [InlineData(28, Season.Spring)] [InlineData(29, Season.Summer)] [InlineData(112, Season.Winter)]
    public void SeasonOfDay_maps_the_four_blocks(int day, Season expected)
        => Assert.Equal(expected, Calendar.SeasonOfDay(day));
}
