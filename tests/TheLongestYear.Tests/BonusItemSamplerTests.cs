using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusItemSamplerTests
{
    [Fact]
    public void WeightFor_inverse_rarity_table()
    {
        Assert.Equal(8, BonusItemSampler.WeightFor(Rarity.Common));
        Assert.Equal(4, BonusItemSampler.WeightFor(Rarity.Uncommon));
        Assert.Equal(2, BonusItemSampler.WeightFor(Rarity.Rare));
        Assert.Equal(1, BonusItemSampler.WeightFor(Rarity.VeryRare));
    }
}
