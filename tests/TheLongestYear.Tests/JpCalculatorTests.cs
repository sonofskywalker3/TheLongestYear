using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class JpCalculatorTests
{
    private static JpCalculator Make() => new JpCalculator(new JpSettings());

    [Theory]
    [InlineData(1, 1.0)]     // Spring week 1
    [InlineData(4, 1.0)]     // Spring week 4
    [InlineData(5, 1.5)]     // Summer week 1
    [InlineData(8, 1.5)]     // Summer week 4
    [InlineData(9, 2.5)]     // Fall   week 1
    [InlineData(12, 2.5)]    // Fall   week 4
    [InlineData(13, 4.0)]    // Winter week 1
    [InlineData(16, 4.0)]    // Winter week 4
    public void Multiplier_matches_per_season_table(int weekOfYear, double expected)
        => Assert.Equal(expected, Make().Multiplier(weekOfYear));

    [Theory]
    [InlineData(Rarity.Common, 1, 1)]        // Spring: 1 * 1.0
    [InlineData(Rarity.Common, 5, 2)]        // Summer: 1 * 1.5 -> 2
    [InlineData(Rarity.Common, 9, 3)]        // Fall:   1 * 2.5 -> 3
    [InlineData(Rarity.Common, 13, 4)]       // Winter: 1 * 4.0
    [InlineData(Rarity.Rare, 1, 10)]
    [InlineData(Rarity.Rare, 11, 25)]        // Fall:   10 * 2.5
    [InlineData(Rarity.VeryRare, 11, 63)]    // Fall:   25 * 2.5 -> 62.5 -> 63
    [InlineData(Rarity.VeryRare, 13, 100)]   // Winter: 25 * 4.0
    public void PerItem_scales_by_rarity_and_season_multiplier(Rarity rarity, int week, long expected)
        => Assert.Equal(expected, Make().PerItem(rarity, week));

    [Fact]
    public void BundleBonus_scales_by_season()
    {
        Assert.Equal(15, Make().BundleBonus(1));    // Spring 15 * 1.0
        Assert.Equal(38, Make().BundleBonus(9));    // Fall   15 * 2.5 = 37.5 -> 38
        Assert.Equal(60, Make().BundleBonus(13));   // Winter 15 * 4.0
    }

    [Fact]
    public void RoomBonus_scales_by_season()
    {
        Assert.Equal(60, Make().RoomBonus(1));      // Spring 60 * 1.0
        Assert.Equal(90, Make().RoomBonus(5));      // Summer 60 * 1.5
        Assert.Equal(240, Make().RoomBonus(13));    // Winter 60 * 4.0
    }

    [Fact]
    public void CompletedContractBonus_scales_by_season()
    {
        Assert.Equal(50, Make().CompletedContractBonus(1));     // Spring 50 * 1.0
        Assert.Equal(75, Make().CompletedContractBonus(5));     // Summer 50 * 1.5
        Assert.Equal(125, Make().CompletedContractBonus(9));    // Fall   50 * 2.5
        Assert.Equal(200, Make().CompletedContractBonus(13));   // Winter 50 * 4.0
    }

    [Fact]
    public void ForDonationBatch_sums_items_and_bundle_bonus_at_spring_rate()
    {
        // Spring (mult 1.0): 2 Rare * 10 + 3 Common * 1 + 1 bundle * 15 = 38.
        var lines = new[] { new DonationLine(Rarity.Rare, 2), new DonationLine(Rarity.Common, 3) };
        Assert.Equal(38, Make().ForDonationBatch(lines, weekOfYear: 1, bundlesCompleted: 1, roomsCompleted: 0));
    }

    [Fact]
    public void ForDonationBatch_room_bonus_scales_in_summer()
    {
        // Summer (mult 1.5): 0 items, 1 room * 60 * 1.5 = 90.
        var lines = new[] { new DonationLine(Rarity.Common, 0) };
        Assert.Equal(90, Make().ForDonationBatch(lines, weekOfYear: 5, bundlesCompleted: 0, roomsCompleted: 1));
    }
}
