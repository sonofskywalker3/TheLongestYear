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
