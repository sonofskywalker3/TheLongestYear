using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class TapperAvailabilityTests
{
    private static RawWildTree Tree(string id, double seedChance)
        => new(id, "(O)1", seedChance, seedChance, true, new List<RawWildTreeDrop>());

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
        WildTrees = new List<RawWildTree>
        {
            Tree("1", 0.05), Tree("2", 0.05), Tree("3", 0.05), Tree("12", 0.05), Tree("13", 0),
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

    /// <summary>Spots come from the live world, so chopping every oak must not move Oak Resin.</summary>
    [Fact]
    public void A_tree_that_drops_its_seed_needs_no_spot()
    {
        var chopped = new EffortData { TapItems = Data.TapItems, WildTrees = Data.WildTrees };
        Assert.Equal(5, TapperAvailability.Derive("(O)725", chopped, null)!.EarliestWeek);
    }

    /// <summary>The tap starts once the player has both the Tapper and the tree.</summary>
    [Fact]
    public void The_wait_counts_from_the_later_of_skill_and_tree()
    {
        var late = new PlaceWeek(9, 6, 3);
        var data = new EffortData
        {
            TapItems = new List<RawTapItem> { new("1", "(O)725", 7) },
            WildTreeSpots = new List<RawWildTreeSpot> { new("1", "Late") },
        };
        var weeks = LocationWeeks.FromReached(new Dictionary<string, PlaceWeek> { ["Late"] = late });
        Assert.Equal(10, TapperAvailability.Derive("(O)725", data, weeks)!.EarliestWeek);
    }
}
