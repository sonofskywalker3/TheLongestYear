using System.Collections.Generic;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class TapperAvailabilityTests
{
    private static readonly EffortData Data = new()
    {
        TapItems = new List<RawTapItem>
        {
            new("1", "(O)725", 7),    // Oak Resin
            new("2", "(O)724", 9),    // Maple Syrup
            new("3", "(O)726", 5),    // Pine Tar
        },
    };

    [Theory]
    [InlineData("(O)724", 5)] [InlineData("(O)725", 5)] [InlineData("(O)726", 4)]
    public void Tapper_goods_follow_foraging_4_plus_nights(string id, int week)
        => Assert.Equal(week, TapperAvailability.Derive(id, Data)!.EarliestWeek);

    [Fact]
    public void Not_a_tap_item_is_null() => Assert.Null(TapperAvailability.Derive("(O)24", Data));
}
