using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Mod-support work, 2026-10-08: Stardew Valley Expanded adds Holly, Crocus, Crystal
/// Fruit, Sweet Pea and Blackberry forage rows with no season to its Grampleton Suburbs, a map no
/// door leads to from anywhere a player walks. Read by name alone that map was open from day 1,
/// so Holly read as week-1 forage and a Spring gate could ask for it.</summary>
public class ForageLocationWeeksTests
{
    private static LocationWeeks SveWorld() => LocationWeeks.Build(
        new[]
        {
            new RawLocationLink("Farm", "Forest"), new RawLocationLink("Farm", "BusStop"),
            new RawLocationLink("BusStop", "Town"), new RawLocationLink("Farm", "Backwoods"),
            new RawLocationLink("Backwoods", "Mountain"),
            new RawLocationLink("Custom_GrampletonSuburbs", "Custom_GrampletonSuburbsTrainStation"),
        },
        _ => false);

    private static readonly RawSpawnEntry[] HollyRows =
    {
        new("(O)283", Season.Winter, null, "Forest"),
        new("(O)283", Season.Winter, null, "Mountain"),
        new("(O)283", null, null, "Custom_GrampletonSuburbs"),
        new("(O)283", null, null, "Custom_GrampletonSuburbsTrainStation"),
    };

    [Fact]
    public void A_Row_In_A_Map_No_Door_Reaches_Does_Not_Place_The_Forage()
    {
        ItemEffort? holly = CropForageAvailability.DeriveForage("(O)283", HollyRows, WeekMode.Pacing, SveWorld());

        Assert.NotNull(holly);
        Assert.Equal(AvailabilityWeeks.FirstWeekOf(Season.Winter), holly!.EarliestWeek);
        Assert.Contains("2 location(s)", holly.Basis);
    }

    [Fact]
    public void Without_Walked_Weeks_The_Old_Name_Reading_Stands()
    {
        ItemEffort? holly = CropForageAvailability.DeriveForage("(O)283", HollyRows);
        Assert.Equal(1, holly!.EarliestWeek);
    }

    [Fact]
    public void Forage_Found_Only_There_Is_Not_Placed()
    {
        var rows = new[] { new RawSpawnEntry("(O)Mod_Berry", null, null, "Custom_GrampletonSuburbs") };
        Assert.Null(CropForageAvailability.DeriveForage("(O)Mod_Berry", rows, WeekMode.Pacing, SveWorld()));
    }

    [Fact]
    public void The_Walked_Week_Of_A_Map_Behind_A_Gate_Is_Used()
    {
        LocationWeeks weeks = LocationWeeks.Build(
            new[] { new RawLocationLink("Farm", "Forest"), new RawLocationLink("Forest", "Woods"), new RawLocationLink("Woods", "Custom_Glade") },
            _ => false);
        var rows = new[] { new RawSpawnEntry("(O)Mod_Root", null, null, "Custom_Glade") };

        Assert.Equal(4, CropForageAvailability.DeriveForage("(O)Mod_Root", rows, WeekMode.Pacing, weeks)!.EarliestWeek);
    }
}
