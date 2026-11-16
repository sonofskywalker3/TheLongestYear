using System.Linq;
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

    private const string Carp = "(O)142";
    private const string ModFish = "(O)Mod_Fish";

    private static LocationSpawn Row(string item, bool? competitor = null, int precedence = 0)
        => new("Town", item, null, null, 1, 0, false, 0, Precedence: precedence, CompetitorOnly: competitor);

    private static FishRow Bites(string item) => new(item, false, "both", 0, "600 2600", 0, 0.5, 0);

    private static ObtainabilityInputs Water(params LocationSpawn[] rows) => new()
    {
        LocationFish = rows,
        FishRows = new System.Collections.Generic.Dictionary<string, FishRow> { [Carp] = Bites(Carp) },
        CompetitorFishRows = new System.Collections.Generic.Dictionary<string, FishRow> { [ModFish] = Bites(ModFish) },
    };

    [Fact]
    public void ACompetitorRowTakesCastsAndIsCreditedNothing()
    {
        double alone = FishCatchRates.Build(Water(Row(Carp)))[Carp].BestDay;
        var shared = FishCatchRates.Build(Water(Row(Carp), Row(ModFish, competitor: true)));
        Assert.True(shared[Carp].BestDay < alone, "the mod's fish did not take any of the casts");
        Assert.False(shared.ContainsKey(ModFish));

        // The mod rowing the known fish itself gives that fish nothing more.
        var twice = FishCatchRates.Build(Water(Row(Carp), Row(Carp, competitor: true)));
        Assert.True(twice[Carp].BestDay < alone);
    }

    [Fact]
    public void AFishCrowdedOutEntirelyStaysInTheResultAsRare()
    {
        // The mod's row is tried first and always passes (no Data/Fish row: it bites every cast).
        var inputs = Water(Row(Carp), Row("(O)Mod_Other", competitor: true, precedence: -10));
        var rates = FishCatchRates.Build(inputs);
        Assert.Equal(0, rates[Carp].BestDay);
        Assert.True(FishCatchRates.IsRare(rates, Carp));
    }

    [Fact]
    public void ACompetitorRowGivesNoSource()
    {
        var inputs = Water(Row(Carp), Row(ModFish, competitor: true));
        var sources = SpawnSources.LocationFish(inputs.LocationFish, inputs.FishRows, inputs.Objects, inputs.Festivals,
            new ObtainabilityModel(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<ObtainSource>>())).Select(s => s.ItemId).ToList();
        Assert.Equal(new[] { Carp }, sources);
    }
}
