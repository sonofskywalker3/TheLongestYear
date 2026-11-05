using System.Collections.Generic;
using TheLongestYear.Core;
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
            new("12", "(O)259", 2),   // Fiddlehead Fern, green rain fern tree
            new("13", "(O)MysticSyrup", 7),
            new("9", "(O)999", 1),    // a tree that stands only behind a late map
        },
        WildTreeSpots = new List<RawWildTreeSpot>
        {
            new("1", "Forest"), new("2", "Forest"), new("3", "Forest"), new("9", "Late"),
        },
    };

    [Theory]
    [InlineData("(O)724", 5)] [InlineData("(O)725", 5)] [InlineData("(O)726", 4)]
    public void Tapper_goods_follow_foraging_4_plus_nights(string id, int week)
        => Assert.Equal(week, TapperAvailability.Derive(id, Data, null)!.EarliestWeek);

    [Fact]
    public void Not_a_tap_item_is_null() => Assert.Null(TapperAvailability.Derive("(O)24", Data, null));

    [Theory]
    [InlineData("(O)259")] [InlineData("(O)MysticSyrup")]
    public void A_tree_standing_nowhere_waits_for_the_end_of_the_year(string id)
    {
        ItemEffort tapped = TapperAvailability.Derive(id, Data, null)!;
        Assert.Equal(Calendar.WeeksPerYear, tapped.EarliestWeek);
        Assert.Equal(Calendar.WeeksPerYear, tapped.HardWeek);
    }

    [Fact]
    public void A_tree_on_an_unwalked_map_counts_as_standing_nowhere()
    {
        var weeks = LocationWeeks.FromReached(new Dictionary<string, PlaceWeek> { ["Forest"] = PlaceWeek.OwnGate("Forest") });
        Assert.Equal(Calendar.WeeksPerYear, TapperAvailability.Derive("(O)999", Data, weeks)!.EarliestWeek);
        Assert.Equal(5, TapperAvailability.Derive("(O)724", Data, weeks)!.EarliestWeek);
    }
}
