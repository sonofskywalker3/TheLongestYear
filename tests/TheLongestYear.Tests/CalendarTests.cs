using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CalendarTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(21, 3)]
    [InlineData(28, 4)]
    public void WeekInMonth_maps_day_to_week(int day, int expected)
        => Assert.Equal(expected, Calendar.WeekInMonth(day));

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(14, true)]
    [InlineData(28, true)]
    public void IsWeekEnd_true_on_multiples_of_seven(int day, bool expected)
        => Assert.Equal(expected, Calendar.IsWeekEnd(day));

    [Theory]
    [InlineData(28, true)]
    [InlineData(21, false)]
    public void IsMonthEnd_true_only_on_day_28(int day, bool expected)
        => Assert.Equal(expected, Calendar.IsMonthEnd(day));

    [Theory]
    [InlineData(0, 7, 1)]
    [InlineData(1, 1, 5)]
    [InlineData(3, 28, 16)]
    public void WeekOfYear_combines_month_and_week(int month, int day, int expected)
        => Assert.Equal(expected, Calendar.WeekOfYear(month, day));

    [Fact]
    public void WeekInMonth_rejects_out_of_range_day()
        => Assert.Throws<System.ArgumentOutOfRangeException>(() => Calendar.WeekInMonth(0));
}
