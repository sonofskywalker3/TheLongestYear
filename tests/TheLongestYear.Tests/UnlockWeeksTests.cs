using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class UnlockWeeksTests
{
    [Theory]
    [InlineData(2, 2)] [InlineData(3, 3)] [InlineData(4, 4)] [InlineData(5, 5)] [InlineData(6, 6)]
    [InlineData(7, 8)] [InlineData(8, 9)] [InlineData(9, 10)] [InlineData(10, 12)] [InlineData(1, 1)]
    public void Hearts(int hearts, int week) => Assert.Equal(week, UnlockWeeks.ForHearts(hearts));

    [Theory]
    [InlineData(500, 1)] [InlineData(1000, 1)] [InlineData(3000, 2)] [InlineData(5000, 3)]
    [InlineData(10000, 5)] [InlineData(25000, 7)] [InlineData(50000, 10)] [InlineData(50001, 13)]
    public void Cost(int gold, int week) => Assert.Equal(week, UnlockWeeks.ForCost(gold));

    [Fact]
    public void Sandy_is_not_met_before_the_desert()
    {
        Assert.Equal(9, UnlockWeeks.ForFriendship("Sandy", 3));
        Assert.Equal(9, UnlockWeeks.ForFriendship("Sandy", 7));
        Assert.Equal(8, UnlockWeeks.ForFriendship("Caroline", 7));
        Assert.Null(UnlockWeeks.ForFriendship("Kent", 3));
        Assert.Null(UnlockWeeks.ForFriendship("Leo", 3));
    }
}
