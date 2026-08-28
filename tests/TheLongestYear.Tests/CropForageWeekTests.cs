using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class CropForageWeekTests
{
    [Fact]
    public void Parsnip_is_week_1_and_cauliflower_week_2()
    {
        var crops = new List<RawCropGrowth>
        {
            new("(O)24", 4, false, false, new[] { Season.Spring }),
            new("(O)190", 12, false, false, new[] { Season.Spring }),
        };
        Assert.Equal(1, CropForageAvailability.DeriveCrop("(O)24", crops)!.EarliestWeek);
        Assert.Equal(2, CropForageAvailability.DeriveCrop("(O)190", crops)!.EarliestWeek);
    }

    [Fact]
    public void Melon_is_summer_week_6_and_a_long_crop_never_leaves_its_season()
    {
        var crops = new List<RawCropGrowth>
        {
            new("(O)254", 12, false, false, new[] { Season.Summer }),
            new("(O)276", 13, false, false, new[] { Season.Fall }),  // Pumpkin
        };
        Assert.Equal(6, CropForageAvailability.DeriveCrop("(O)254", crops)!.EarliestWeek);
        Assert.Equal(10, CropForageAvailability.DeriveCrop("(O)276", crops)!.EarliestWeek);
    }

    [Fact]
    public void Strawberry_waits_for_the_egg_festival()
    {
        var crops = new List<RawCropGrowth> { new("(O)400", 8, true, false, new[] { Season.Spring }) };
        Assert.Equal(3, CropForageAvailability.DeriveCrop("(O)400", crops)!.EarliestWeek);
    }

    [Fact]
    public void Crop_with_no_seasons_is_unknown()
    {
        var crops = new List<RawCropGrowth> { new("(O)454", 28, false, false) };
        Assert.Null(CropForageAvailability.DeriveCrop("(O)454", crops)!.EarliestWeek);
    }

    [Fact]
    public void Forage_takes_the_earliest_spawn_and_the_location_gate()
    {
        var spawns = new List<RawSpawnEntry>
        {
            new("(O)88", Season.Spring, null, "Desert"),      // Coconut, week 9 by location
            new("(O)78", null, null, "UndergroundMine20"),     // Cave Carrot, week 1
            new("(O)404", Season.Fall, null, "Forest"),        // Common Mushroom
            new("(O)404", Season.Spring, null, "Woods"),
        };
        Assert.Equal(9, CropForageAvailability.DeriveForage("(O)88", spawns)!.EarliestWeek);
        Assert.Equal(1, CropForageAvailability.DeriveForage("(O)78", spawns)!.EarliestWeek);
        Assert.Equal(1, CropForageAvailability.DeriveForage("(O)404", spawns)!.EarliestWeek);
    }

    [Fact]
    public void Bush_berries_are_placed_without_spawn_rows()
    {
        Assert.Equal(3, CropForageAvailability.DeriveForage("(O)296", new List<RawSpawnEntry>())!.EarliestWeek);
        Assert.Equal(10, CropForageAvailability.DeriveForage("(O)410", new List<RawSpawnEntry>())!.EarliestWeek);
    }

    [Fact]
    public void Sapling_is_week_1()
    {
        var saplings = new List<PoolItem> { new("(O)628", 3400, 1, new List<Season>(), new List<string>()) };
        Assert.Equal(1, CropForageAvailability.DeriveSapling("(O)628", saplings)!.EarliestWeek);
        Assert.Null(CropForageAvailability.DeriveSapling("(O)24", saplings));
    }
}
