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
