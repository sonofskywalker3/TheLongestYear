using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class LocationGatingTests
{
    [Theory]
    [InlineData("Desert", Season.Fall)]
    [InlineData("Sewer", Season.Summer)]
    [InlineData("WitchSwamp", Season.Winter)]
    [InlineData("Mountain", Season.Spring)]
    [InlineData("Beach", Season.Spring)]
    [InlineData("UndergroundMine", Season.Spring)]
    public void A_Gated_Location_Carries_Its_Own_Season_Floor(string key, Season expected)
        => Assert.Equal(expected, LocationGating.FloorFor(key));

    [Fact]
    public void An_Unknown_Location_Is_Treated_As_Ungated()
        => Assert.Equal(Season.Spring, LocationGating.FloorFor("SomeModdedPlace"));

    /// <summary>Reaching ANY listed location is enough, so the easiest one wins.</summary>
    [Fact]
    public void The_Easiest_Location_In_A_Set_Wins()
        => Assert.Equal(Season.Spring,
            LocationGating.FloorForAny(new List<string> { "Desert", "Mountain" }));

    [Fact]
    public void An_Empty_Location_Set_Is_Ungated()
        => Assert.Equal(Season.Spring, LocationGating.FloorForAny(new List<string>()));
}

public class RawFishEntryTests
{
    // Vanilla Pufferfish row shape: name/difficulty/behavior/minSize/maxSize/times/seasons/
    // weather/unused/maxDepth/chance/depthMultiplier/minLevel/...
    private const string PufferfishRow =
        "Pufferfish/80/floater/1/36/1200 1600/summer/sunny/690 .4 .1/5/.4/.2/0";

    private const string LobsterTrapRow = "Lobster/trap/.05/688 .05/ocean/1/10";

    [Fact]
    public void A_Rod_Fish_Row_Parses_Every_Field_We_Gate_On()
    {
        RawFishEntry entry = RawFishEntry.Parse("128", PufferfishRow);

        Assert.Equal("128", entry.ItemId);
        Assert.False(entry.IsTrap);
        Assert.Equal(80, entry.Difficulty);
        Assert.Equal("1200 1600", entry.RawTimeSpans);
        Assert.Equal("sunny", entry.Weather);
        Assert.Equal(5, entry.MaxDepth);
        Assert.Equal(0, entry.MinFishingLevel);
    }

    [Fact]
    public void A_Trap_Row_Is_Flagged_And_Does_Not_Throw_On_Its_Short_Fields()
    {
        RawFishEntry entry = RawFishEntry.Parse("715", LobsterTrapRow);

        Assert.True(entry.IsTrap);
        Assert.Equal(0, entry.Difficulty);
    }

    [Fact]
    public void A_Malformed_Row_Degrades_Instead_Of_Throwing()
    {
        RawFishEntry entry = RawFishEntry.Parse("999", "Nonsense/notanumber");

        Assert.Equal("999", entry.ItemId);
        Assert.False(entry.IsTrap);
        Assert.Equal(0, entry.Difficulty);
    }

    [Fact]
    public void An_Empty_Row_Degrades_Instead_Of_Throwing()
    {
        RawFishEntry entry = RawFishEntry.Parse("999", "");

        Assert.Equal(0, entry.MinFishingLevel);
        Assert.Equal("", entry.Weather);
    }
}

public class FishAvailabilityDeriveTests
{
    private static PoolItem Fish(
        string id, int price = 100, IReadOnlyList<Season>? seasons = null,
        IReadOnlyList<string>? locations = null)
        => new PoolItem(id, price, 1,
            seasons ?? new List<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter },
            locations ?? new List<string> { "Mountain" });

    private static RawFishEntry Row(
        int difficulty = 30, string times = "600 2600", string weather = "both",
        int maxDepth = 0, int minLevel = 0)
        => new RawFishEntry("x", false, difficulty, times, weather, maxDepth, minLevel);

    [Fact]
    public void An_Easy_Year_Round_Fish_Floors_At_Spring_And_Scores_Low()
    {
        ItemAvailability result = FishAvailability.Derive(Fish("(O)145"), Row(difficulty: 15));

        Assert.Equal(Season.Spring, result.EarliestSeason);
        Assert.True(result.Effort <= 2, $"expected an easy score, got {result.Effort}");
    }

    [Fact]
    public void A_Summer_Only_Fish_Floors_At_Summer()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)128", seasons: new List<Season> { Season.Summer }), Row());

        Assert.Equal(Season.Summer, result.EarliestSeason);
    }

    /// <summary>Sandfish lists every season in the Desert, but the Desert needs a 40,000g bus
    /// repair. Spawn seasons alone would read this as Spring and put an unsatisfiable Spring
    /// deadline on it.</summary>
    [Fact]
    public void A_Desert_Fish_Inherits_The_Deserts_Floor_Not_Its_Spawn_Seasons()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)164", locations: new List<string> { "Desert" }), Row());

        Assert.Equal(Season.Fall, result.EarliestSeason);
        Assert.Contains("Desert", result.Basis);
    }

    /// <summary>Cave Jelly is a mine fish from floor 100. Its spawn data reads as year round, so
    /// without the mine's depth gate it would floor at Spring and could draw a Spring deadline no
    /// 500g first year run can meet.</summary>
    [Fact]
    public void A_Mine_Fish_Inherits_The_Mines_Depth_Floor()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)CaveJelly", locations: new List<string> { "UndergroundMine" }), Row());

        // Floor 100 fish: goal week 4 (30 floors a week), gate Spring like the rest of area 80.
        Assert.Equal(4, result.Week);
        Assert.Equal(Season.Spring, result.Gate);
    }

    [Fact]
    public void The_Later_Of_Spawn_Season_And_Location_Floor_Wins()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)164",
                seasons: new List<Season> { Season.Winter },
                locations: new List<string> { "Desert" }),
            Row());

        Assert.Equal(Season.Winter, result.EarliestSeason);
    }

    [Fact]
    public void A_Hard_Restricted_Fish_Outscores_An_Easy_One()
    {
        int easy = FishAvailability.Derive(Fish("(O)145"), Row(difficulty: 15)).Effort;
        int hard = FishAvailability.Derive(
            Fish("(O)128", seasons: new List<Season> { Season.Summer }),
            Row(difficulty: 80, times: "1200 1600", weather: "sunny", maxDepth: 5, minLevel: 0))
            .Effort;

        Assert.True(hard > easy, $"hard {hard} should outscore easy {easy}");
    }

    [Fact]
    public void A_Rainy_Only_Fish_Costs_More_Than_An_All_Weather_One()
    {
        int both = FishAvailability.Derive(Fish("(O)1"), Row(weather: "both")).Effort;
        int rainy = FishAvailability.Derive(Fish("(O)2"), Row(weather: "rainy")).Effort;

        Assert.Equal(both + 2, rainy);
    }

    [Fact]
    public void A_High_Level_Requirement_Raises_The_Score()
    {
        int low = FishAvailability.Derive(Fish("(O)1"), Row(minLevel: 0)).Effort;
        int high = FishAvailability.Derive(Fish("(O)2"), Row(minLevel: 9)).Effort;

        Assert.Equal(low + 3, high);
    }

    [Fact]
    public void A_Fish_With_No_Data_Row_Still_Gets_A_Floor_From_Its_Spawn_Data()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)9999", seasons: new List<Season> { Season.Fall }), row: null);

        Assert.Equal(Season.Fall, result.EarliestSeason);
        Assert.Contains("no Data/Fish row", result.Basis);
    }

    [Fact]
    public void The_Basis_Names_The_Season_And_The_Score()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)128", seasons: new List<Season> { Season.Summer }), Row());

        Assert.Contains("Summer", result.Basis);
    }
}
