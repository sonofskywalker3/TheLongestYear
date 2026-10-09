using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Bug 2026-09-25: with two buildings of one family, the rewind put the kept one on the
/// other one's spot, because the snapshot remembered whichever came last on the farm.</summary>
public class KeptBuildingSpotPickerTests
{
    private static readonly Dictionary<string, BuildingSpot> None = new();

    private static FamilyBuilding B(string family, int tier, int x, int y) => new(family, tier, new BuildingSpot(x, y));

    [Fact]
    public void Kept_big_coop_keeps_its_spot_when_a_plain_coop_is_built_later()
    {
        // The reported case: kept Big Coop at (52,20), second plain Coop at (60,27).
        var spots = KeptBuildingSpotPicker.Pick(
            new[] { B("coop", 2, 52, 20), B("coop", 1, 60, 27) }, None);
        Assert.Equal(new BuildingSpot(52, 20), spots["coop"]);
    }

    [Fact]
    public void Highest_tier_wins_whatever_the_farm_order()
    {
        var spots = KeptBuildingSpotPicker.Pick(
            new[] { B("barn", 1, 10, 10), B("barn", 3, 20, 20), B("barn", 2, 30, 30) }, None);
        Assert.Equal(new BuildingSpot(20, 20), spots["barn"]);
    }

    [Fact]
    public void Equal_tiers_with_no_memory_keep_the_first_built()
    {
        var spots = KeptBuildingSpotPicker.Pick(
            new[] { B("silo", 1, 44, 20), B("silo", 1, 36, 28) }, None);
        Assert.Equal(new BuildingSpot(44, 20), spots["silo"]);
    }

    [Fact]
    public void Equal_tiers_prefer_the_building_on_the_remembered_spot()
    {
        var previous = new Dictionary<string, BuildingSpot> { ["silo"] = new BuildingSpot(36, 28) };
        var spots = KeptBuildingSpotPicker.Pick(
            new[] { B("silo", 1, 44, 20), B("silo", 1, 36, 28) }, previous);
        Assert.Equal(new BuildingSpot(36, 28), spots["silo"]);
    }

    [Fact]
    public void Families_are_picked_independently_and_unknown_buildings_ignored()
    {
        var spots = KeptBuildingSpotPicker.Pick(
            new[] { B("coop", 1, 1, 1), B("", 0, 5, 5), B("barn", 1, 2, 2), B("Greenhouse", 1, 3, 3) }, None);
        Assert.Equal(3, spots.Count);
        Assert.Equal(new BuildingSpot(1, 1), spots["coop"]);
        Assert.Equal(new BuildingSpot(2, 2), spots["barn"]);
        Assert.Equal(new BuildingSpot(3, 3), spots["Greenhouse"]);
    }

    [Fact]
    public void A_family_with_no_live_building_keeps_its_old_spot_and_other_keys_survive()
    {
        var previous = new Dictionary<string, BuildingSpot>
        {
            ["barn"] = new BuildingSpot(46, 12),
            [FishPondKeep.SpotKey] = new BuildingSpot(40, 40),
        };
        var spots = KeptBuildingSpotPicker.Pick(new[] { B("coop", 1, 1, 1) }, previous);
        Assert.Equal(new BuildingSpot(46, 12), spots["barn"]);
        Assert.Equal(new BuildingSpot(40, 40), spots[FishPondKeep.SpotKey]);
        Assert.Equal(new BuildingSpot(1, 1), spots["coop"]);
    }
}
