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
