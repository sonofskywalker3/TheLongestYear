using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class AnimalHousingTests
{
    [Theory]
    [InlineData("Coop", "coop", 1)]
    [InlineData("Big Coop", "coop", 2)]
    [InlineData("Deluxe Coop", "coop", 3)]
    [InlineData("Barn", "barn", 1)]
    [InlineData("Big Barn", "barn", 2)]
    [InlineData("Deluxe Barn", "barn", 3)]
    [InlineData("Silo", "silo", 1)]
    [InlineData("Shed", "", 0)]
    [InlineData(null, "", 0)]
    public void Chain_gives_family_and_tier(string? blueprint, string family, int tier)
        => Assert.Equal((family, tier), AnimalHousing.Chain(blueprint));
}

public class KeptBuildingMatchTests
{
    [Fact]
    public void Exact_type_on_the_fresh_farm_is_reused()
        => Assert.Equal((KeptBuildingMatch.Exact, 1),
            AnimalHousing.FindOnFreshFarm("Coop", new[] { "Shipping Bin", "Coop" }));

    [Fact]
    public void A_starter_coop_is_replaced_by_a_kept_big_coop()
        => Assert.Equal((KeptBuildingMatch.LowerTier, 0),
            AnimalHousing.FindOnFreshFarm("Big Coop", new[] { "Coop", "Greenhouse" }));

    [Fact]
    public void A_starter_coop_is_replaced_by_a_kept_deluxe_coop()
        => Assert.Equal((KeptBuildingMatch.LowerTier, 0),
            AnimalHousing.FindOnFreshFarm("Deluxe Coop", new[] { "Coop" }));

    [Fact]
    public void Exact_wins_over_lower_tier()
        => Assert.Equal((KeptBuildingMatch.Exact, 1),
            AnimalHousing.FindOnFreshFarm("Big Coop", new[] { "Coop", "Big Coop" }));

    [Fact]
    public void Other_family_never_matches()
        => Assert.Equal((KeptBuildingMatch.None, -1),
            AnimalHousing.FindOnFreshFarm("Big Barn", new[] { "Coop" }));

    [Fact]
    public void Higher_tier_is_not_replaced()
        => Assert.Equal((KeptBuildingMatch.None, -1),
            AnimalHousing.FindOnFreshFarm("Coop", new[] { "Deluxe Coop" }));

    [Fact]
    public void Non_chain_building_matches_only_exactly()
    {
        Assert.Equal((KeptBuildingMatch.None, -1), AnimalHousing.FindOnFreshFarm("Fish Pond", new[] { "Coop" }));
        Assert.Equal((KeptBuildingMatch.Exact, 0), AnimalHousing.FindOnFreshFarm("Silo", new[] { "Silo" }));
    }
}
