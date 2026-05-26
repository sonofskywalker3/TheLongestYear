using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RarityTiersTests
{
    private static readonly RarityThresholds Default = new RarityThresholds();

    [Theory]
    [InlineData(0, Rarity.Common)]
    [InlineData(49, Rarity.Common)]
    [InlineData(50, Rarity.Uncommon)]
    [InlineData(199, Rarity.Uncommon)]
    [InlineData(200, Rarity.Rare)]
    [InlineData(599, Rarity.Rare)]
    [InlineData(600, Rarity.VeryRare)]
    [InlineData(5000, Rarity.VeryRare)]
    public void FromPrice_maps_price_to_tier(int price, Rarity expected)
        => Assert.Equal(expected, RarityTiers.FromPrice(price, Default));

    [Fact]
    public void FromPrice_respects_custom_thresholds()
    {
        var t = new RarityThresholds { UncommonAtLeast = 10, RareAtLeast = 20, VeryRareAtLeast = 30 };
        Assert.Equal(Rarity.Common, RarityTiers.FromPrice(9, t));
        Assert.Equal(Rarity.Uncommon, RarityTiers.FromPrice(10, t));
        Assert.Equal(Rarity.VeryRare, RarityTiers.FromPrice(99, t));
    }

    [Fact]
    public void FromPrice_null_thresholds_throws()
        => Assert.Throws<System.ArgumentNullException>(() => RarityTiers.FromPrice(1, null!));
}
