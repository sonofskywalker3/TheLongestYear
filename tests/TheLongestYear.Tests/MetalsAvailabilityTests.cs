using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MetalsAvailabilityTests
{
    private static PoolItem Metal(string id, int price = 100)
        => new PoolItem(id, price, 1, new List<Season>(), new List<string>());

    [Theory]
    [InlineData("(O)378", Season.Spring)]   // Copper Ore, mine area 0
    [InlineData("(O)380", Season.Spring)]   // Iron Ore, mine area 40
    [InlineData("(O)384", Season.Spring)]   // Gold Ore, mine area 80
    [InlineData("(O)386", Season.Fall)]     // Iridium Ore, Skull Cavern behind the bus
    [InlineData("(O)334", Season.Spring)]   // Copper Bar
    [InlineData("(O)335", Season.Spring)]   // Iron Bar
    [InlineData("(O)336", Season.Spring)]   // Gold Bar
    [InlineData("(O)337", Season.Fall)]     // Iridium Bar
    public void Each_Metal_Gates_At_Its_Mine_Depth(string id, Season expected)
    {
        ItemAvailability? result = MetalsAvailability.Derive(Metal(id));

        Assert.NotNull(result);
        // The gate season (2026-08-28 mine pacing spec): every mine area gates in Spring now,
        // gold's goal week is Spring week 3 and its gate is Spring too.
        Assert.Equal(expected, result!.Gate);
    }

    [Fact]
    public void A_Bar_Costs_More_Effort_Than_Its_Ore()
    {
        int ore = MetalsAvailability.Derive(Metal("(O)378"))!.Effort;
        int bar = MetalsAvailability.Derive(Metal("(O)334"))!.Effort;

        Assert.True(bar > ore, $"bar {bar} should outscore ore {ore}");
    }

    [Fact]
    public void Deeper_Metal_Costs_More_Effort()
    {
        int copper = MetalsAvailability.Derive(Metal("(O)378"))!.Effort;
        int gold = MetalsAvailability.Derive(Metal("(O)384"))!.Effort;
        int iridium = MetalsAvailability.Derive(Metal("(O)386"))!.Effort;

        Assert.True(copper < gold);
        Assert.True(gold < iridium);
    }

    [Fact]
    public void An_Unrecognised_Id_Returns_Null_So_The_Composer_Falls_Through()
        => Assert.Null(MetalsAvailability.Derive(Metal("(O)9999")));

    [Fact]
    public void The_Basis_Explains_The_Depth()
    {
        ItemAvailability result = MetalsAvailability.Derive(Metal("(O)384"))!;

        Assert.Contains("80", result.Basis);
    }
}
