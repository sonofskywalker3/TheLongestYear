using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Routes the 0.19.19 pond rule exposed: wild-tree seeds and wood, the beach tide
/// pools, geodes from mine stones, and the SquidFest Pearl.</summary>
public class WildTreeAvailabilityTests
{
    private static readonly RawWildTree Oak = new("1", "(O)309", 0.05, 0.75, DropsWood: true,
        new[] { new RawWildTreeDrop("(O)92", 1.0, null) });
    private static readonly RawWildTree Maple = new("2", "(O)310", 0.05, 0.75, DropsWood: true, new RawWildTreeDrop[0]);
    private static readonly RawWildTree Mahogany = new("8", "(O)292", 0.05, 0.5625, DropsWood: false,
        new[] { new RawWildTreeDrop("(O)709", 1.0, null) });
    private static readonly RawWildTree Mushroom = new("7", "(O)891", 0, 0, DropsWood: true, new RawWildTreeDrop[0]);
    private static readonly RawWildTree Seasonal = new("99", null, 0, 0, DropsWood: false,
        new[] { new RawWildTreeDrop("(O)408", 1.0, Season.Fall) });

    private static EffortData Data(params RawWildTreeSpot[] spots) => new()
    {
        WildTrees = new[] { Oak, Maple, Mahogany, Mushroom, Seasonal },
        WildTreeSpots = spots,
    };

    private static LocationWeeks Weeks() => LocationWeeks.Build(
        new[] { new RawLocationLink("Farm", "Forest"), new RawLocationLink("Forest", "Woods") }, _ => false);

    [Fact]
    public void A_seed_of_a_tree_standing_on_the_farm_is_week_1()
    {
        ItemEffort e = WildTreeAvailability.Derive("(O)309", Data(new RawWildTreeSpot("1", "Farm")), Weeks())!;
        Assert.Equal(1, e.EarliestWeek);
        Assert.Contains("wild tree 1", e.Basis);
    }

    [Fact]
    public void Wood_comes_from_any_tree_that_drops_wood_on_chop()
        => Assert.Equal(1, WildTreeAvailability.Derive(WildTreeAvailability.WoodItemId, Data(new RawWildTreeSpot("2", "Forest")), Weeks())!.EarliestWeek);

    [Fact]
    public void A_tree_type_with_no_standing_tree_gives_nothing()
        => Assert.Null(WildTreeAvailability.Derive("(O)310", Data(new RawWildTreeSpot("1", "Farm")), Weeks()));

    [Fact]
    public void A_tree_on_a_map_the_walk_cannot_date_gives_nothing()
        => Assert.Null(WildTreeAvailability.Derive("(O)309", Data(new RawWildTreeSpot("1", "IslandWest")), Weeks()));

    [Fact]
    public void Mahogany_waits_for_the_Secret_Woods()
    {
        ItemEffort seed = WildTreeAvailability.Derive("(O)292", Data(new RawWildTreeSpot("8", "Woods")), Weeks())!;
        Assert.Equal(LocationGating.WeekFor("Woods"), seed.EarliestWeek);
        Assert.Equal(LocationGating.WeekFor("Woods"), WildTreeAvailability.Derive("(O)709", Data(new RawWildTreeSpot("8", "Woods")), Weeks())!.EarliestWeek);
    }

    [Fact]
    public void A_tree_whose_seed_never_drops_gives_no_seed()
        => Assert.Null(WildTreeAvailability.Derive("(O)891", Data(new RawWildTreeSpot("7", "Farm")), Weeks()));

    [Fact]
    public void A_seasonal_chop_item_waits_for_its_season()
        => Assert.Equal(AvailabilityWeeks.FirstWeekOf(Season.Fall),
            WildTreeAvailability.Derive("(O)408", Data(new RawWildTreeSpot("99", "Farm")), Weeks())!.EarliestWeek);

    [Fact]
    public void Earliest_map_wins()
        => Assert.Equal(1, WildTreeAvailability.Derive("(O)309",
            Data(new RawWildTreeSpot("1", "Woods"), new RawWildTreeSpot("1", "Forest")), Weeks())!.EarliestWeek);
}

public class BeachTidePoolAvailabilityTests
{
    private static LocationWeeks Weeks() => LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Beach") }, _ => false);

    [Theory] [InlineData("(O)393")] [InlineData("(O)397")]
    public void Coral_and_Sea_Urchin_wait_for_the_bridge(string id)
    {
        ItemEffort e = BeachTidePoolAvailability.Derive(id, Weeks(), _ => 3)!;
        Assert.Equal(3, e.EarliestWeek);
        Assert.Contains("bridge", e.Basis);
    }

    [Fact]
    public void The_bridge_is_no_earlier_than_the_beach()
        => Assert.Equal(1, BeachTidePoolAvailability.Derive("(O)393", Weeks(), _ => 1)!.EarliestWeek);

    [Fact]
    public void Sea_Urchin_is_the_rarer_one() =>
        Assert.True(BeachTidePoolAvailability.Derive("(O)397", Weeks(), _ => 1)!.Effort
                    > BeachTidePoolAvailability.Derive("(O)393", Weeks(), _ => 1)!.Effort);

    [Fact]
    public void No_wood_week_means_no_bridge() => Assert.Null(BeachTidePoolAvailability.Derive("(O)393", Weeks(), _ => null));

    [Fact]
    public void Other_items_are_not_claimed() => Assert.Null(BeachTidePoolAvailability.Derive("(O)392", Weeks(), _ => 1));
}

public class MineStoneGeodeTests
{
    [Theory]
    [InlineData("(O)535", MineAreas.Area0)]
    [InlineData("(O)536", MineAreas.Area40)]
    [InlineData("(O)537", MineAreas.Area80)]
    public void Geodes_come_from_their_area_stones(string id, int area)
    {
        ItemEffort e = MineralNodeAvailability.Derive(id)!;
        Assert.Equal(MineAreas.Week(area), e.EarliestWeek);
        Assert.Contains("mine stones", e.Basis);
    }

    [Fact]
    public void Omni_Geode_is_a_rarer_find_from_floor_21()
    {
        ItemEffort e = MineralNodeAvailability.Derive("(O)749")!;
        Assert.Equal(MineAreas.Week(MineAreas.Area10), e.EarliestWeek);
        Assert.True(e.Effort > MineAreas.Effort(MineAreas.Area10));
    }
}

public class PearlPlacementTests
{
    [Fact]
    public void Pearl_is_the_SquidFest_reward_in_Winter()
    {
        ItemEffort e = ShopAvailability.Derive("(O)797")!;
        Assert.Equal(14, e.EarliestWeek);
        Assert.Contains("SquidFest", e.Basis);
    }

    [Fact]
    public void Treasure_Chest_is_the_SquidFest_day_two_reward()
    {
        ItemEffort e = ShopAvailability.Derive("(O)166")!;
        Assert.Equal(14, e.EarliestWeek);
        Assert.Contains("SquidFest", e.Basis);
    }
}
