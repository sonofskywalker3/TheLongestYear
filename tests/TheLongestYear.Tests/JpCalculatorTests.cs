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

    [Theory]
    [InlineData(2500, 3)]      // 2500/1000 = 2.5 -> 3
    [InlineData(5000, 5)]
    [InlineData(10000, 10)]
    [InlineData(25000, 25)]
    public void VaultPayment_scales_with_gold(int gold, long expected)
        => Assert.Equal(expected, Make().VaultPayment(gold));

    [Fact]
    public void VaultPayment_is_at_least_one_jp()
        => Assert.Equal(1, Make().VaultPayment(100));   // 100/1000 = 0.1 -> floor would be 0; min 1

    [Fact]
    public void VaultPayment_does_not_season_scale()
    {
        // Same gold in any week returns the same JP (unlike PerItem). VaultPayment takes no week.
        Assert.Equal(Make().VaultPayment(25000), Make().VaultPayment(25000));
        Assert.True(Make().VaultPayment(25000) > Make().VaultPayment(2500));
    }

    [Fact]
    public void CheckpointBonus_ScalesByEnteringSeasonMultiplier()
    {
        var jp = new JpCalculator(new JpSettings());
        // Day 28 of Spring is week 4; the entering week is 5 (Summer, x1.5).
        Assert.Equal(150, jp.CheckpointBonus(5));
        // Entering Fall (week 9, x2.5) and Winter (week 13, x4.0).
        Assert.Equal(250, jp.CheckpointBonus(9));
        Assert.Equal(400, jp.CheckpointBonus(13));
    }

    [Fact]
    public void CheckpointBonus_UsesConfiguredBase()
    {
        var jp = new JpCalculator(new JpSettings { CheckpointCompletionBonus = 40 });
        Assert.Equal(60, jp.CheckpointBonus(5)); // 40 * 1.5
    }

    // ---- Difficulty: JP earned multiplier (spec 2026-08-26) ----

    [Fact]
    public void The_Default_Multiplier_Changes_Nothing()
    {
        var settings = new JpSettings();

        Assert.Equal(new JpCalculator(settings).PerItem(Rarity.VeryRare, 13),
                     new JpCalculator(settings, 1.0).PerItem(Rarity.VeryRare, 13));
    }

    [Fact]
    public void The_Earned_Multiplier_Scales_Per_Item_Jp()
    {
        var settings = new JpSettings();

        Assert.Equal(new JpCalculator(settings).PerItem(Rarity.Rare, 1) / 2,
                     new JpCalculator(settings, 0.5).PerItem(Rarity.Rare, 1));
    }

    [Fact]
    public void The_Earned_Multiplier_Scales_Completion_Bonuses()
    {
        var settings = new JpSettings();

        Assert.Equal(new JpCalculator(settings).RoomBonus(1) * 3 / 2,
                     new JpCalculator(settings, 1.5).RoomBonus(1));
        // 15 * 1.5 = 22.5, rounded away from zero. Spelled out rather than computed, because
        // integer arithmetic in the expectation would round the other way and hide a real change.
        Assert.Equal(15, new JpCalculator(settings).BundleBonus(1));
        Assert.Equal(23, new JpCalculator(settings, 1.5).BundleBonus(1));
        Assert.Equal(new JpCalculator(settings).WeeklyQuestBonus(1) * 3 / 2,
                     new JpCalculator(settings, 1.5).WeeklyQuestBonus(1));
    }

    [Fact]
    public void The_Earned_Multiplier_Scales_Vault_Payments()
    {
        var settings = new JpSettings();

        Assert.Equal(new JpCalculator(settings).VaultPayment(10000) / 2,
                     new JpCalculator(settings, 0.5).VaultPayment(10000));
    }

    /// <summary>Paying the vault always pays something, at every difficulty.</summary>
    [Fact]
    public void A_Vault_Payment_Still_Awards_At_Least_One_Jp()
    {
        Assert.True(new JpCalculator(new JpSettings(), 0.5).VaultPayment(100) >= 1);
    }

    /// <summary>The season ramp's SHAPE must be identical at every step: only its height moves,
    /// or late-season play would stop being worth more than early-season play.</summary>
    [Fact]
    public void The_Season_Ramp_Shape_Is_Unchanged_By_The_Multiplier()
    {
        var settings = new JpSettings();
        var hard = new JpCalculator(settings, 0.5);

        Assert.Equal(new JpCalculator(settings).Multiplier(13), hard.Multiplier(13));

        double springToWinter = (double)hard.PerItem(Rarity.Rare, 13) / hard.PerItem(Rarity.Rare, 1);
        Assert.Equal(4.0, springToWinter, 1);
    }
}
