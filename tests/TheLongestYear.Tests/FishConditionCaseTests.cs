using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The game reads a query's type name without regard to case
/// (GameStateQuery.QueryTypeLookup is ordinal-ignore-case), so a mod's
/// <c>LOCATION_Season Here spring</c> row bites exactly as vanilla's upper-case one does. The
/// catch model matched the raw token, so every such row (Alchemistry writes 21 of them) read as a
/// query it does not know and counted as never passing (Celjhyn, Nexus posts, 2026-11-07).</summary>
public class FishConditionCaseTests
{
    private const int Spring = 0;
    private const int Summer = 1;

    [Theory]
    [InlineData("LOCATION_SEASON Here spring")]
    [InlineData("LOCATION_Season Here spring")]
    [InlineData("location_season Here spring")]
    [InlineData("Season spring")]
    public void SeasonQueriesPassRegardlessOfCase(string condition)
    {
        Assert.Equal(1, FishCatchRates.ConditionChance(condition, Spring, rain: false));
        Assert.Equal(0, FishCatchRates.ConditionChance(condition, Summer, rain: false));
    }

    [Theory]
    [InlineData("Weather Here Rain")]
    [InlineData("random 0.25")]
    public void OtherQueriesPassRegardlessOfCase(string condition)
    {
        double rainy = FishCatchRates.ConditionChance(condition, Spring, rain: true);
        Assert.True(rainy > 0, $"{condition} read as a query the model does not know");
    }

    [Fact]
    public void UnknownQueriesStillCountAsNeverPassing()
    {
        Assert.Equal(0, FishCatchRates.ConditionChance("PLAYER_HAS_MAIL Current someFlag", Spring, rain: true));
    }
}
