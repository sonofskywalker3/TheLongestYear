using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class JpCalculatorTests
{
    private static JpCalculator Make() => new JpCalculator(new JpSettings());

    [Theory]
    [InlineData(Rarity.Common, 1, 1)]
    [InlineData(Rarity.Rare, 1, 10)]
    [InlineData(Rarity.Rare, 11, 20)]
    [InlineData(Rarity.VeryRare, 11, 50)]
    public void PerItem_scales_by_rarity_and_week_depth(Rarity rarity, int week, long expected)
        => Assert.Equal(expected, Make().PerItem(rarity, week));

    [Fact]
    public void PerItem_rounds_half_away_from_zero()
        => Assert.Equal(3, Make().PerItem(Rarity.Common, 16)); // 1 * 2.5 = 2.5 -> 3

    [Fact]
    public void ForDonationBatch_sums_items_and_bundle_bonus()
    {
        var lines = new[] { new DonationLine(Rarity.Rare, 2), new DonationLine(Rarity.Common, 3) };
        Assert.Equal(38, Make().ForDonationBatch(lines, weekOfYear: 1, bundlesCompleted: 1, roomsCompleted: 0));
    }

    [Fact]
    public void ForDonationBatch_adds_room_bonus()
    {
        var lines = new[] { new DonationLine(Rarity.Common, 0) };
        Assert.Equal(60, Make().ForDonationBatch(lines, weekOfYear: 5, bundlesCompleted: 0, roomsCompleted: 1));
    }
}
