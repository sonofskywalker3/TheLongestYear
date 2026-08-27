using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleHoldPricingTests
{
    private static readonly long[] Default = { 0, 50, 100, 200, 300 };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(2, 100)]
    [InlineData(3, 200)]
    [InlineData(4, 300)]
    [InlineData(9, 300)]
    public void Default_curve_first_free_then_escalates_and_caps(int holds, long expected)
    {
        Assert.Equal(expected, BundleHoldPricing.CostFor(holds, Default));
    }

    [Fact]
    public void Config_default_matches_spec_curve()
    {
        Assert.Equal(Default, new GameplayConfig().BundleHoldCosts);
    }

    [Fact]
    public void Custom_curve_is_honoured_and_last_value_repeats()
    {
        var curve = new long[] { 10, 20 };
        Assert.Equal(10, BundleHoldPricing.CostFor(0, curve));
        Assert.Equal(20, BundleHoldPricing.CostFor(1, curve));
        Assert.Equal(20, BundleHoldPricing.CostFor(5, curve));
    }

    [Fact]
    public void Empty_curve_is_free()
    {
        Assert.Equal(0, BundleHoldPricing.CostFor(3, System.Array.Empty<long>()));
    }
}

public class HoldPriceDifficultyTests
{
    private static readonly List<long> Curve = new() { 0, 50, 100, 200, 300 };

    /// <summary>Zero times anything is zero, so the step makes REPEATED holds expensive rather
    /// than taxing the first mistake.</summary>
    [Fact]
    public void The_First_Hold_Is_Free_At_Every_Step()
    {
        Assert.Equal(0, BundleHoldPricing.CostFor(0, Curve, 4.0));
        Assert.Equal(0, BundleHoldPricing.CostFor(0, Curve, 0.5));
    }

    [Theory]
    [InlineData(0.5, 25)]
    [InlineData(1.0, 50)]
    [InlineData(2.0, 100)]
    [InlineData(4.0, 200)]
    public void Later_Holds_Scale(double factor, long expected)
        => Assert.Equal(expected, BundleHoldPricing.CostFor(1, Curve, factor));

    [Fact]
    public void A_Factor_Of_One_Changes_Nothing()
        => Assert.Equal(300, BundleHoldPricing.CostFor(99, Curve, 1.0));

    /// <summary>The quoted price and the charged price come from the same call, so they cannot
    /// disagree. This is the shape of bug 0.14.2 fixed in Shop Discount.</summary>
    [Fact]
    public void Apply_Charges_Exactly_What_NextCost_Quoted()
    {
        var meta = new MetaState { JunimoPoints = 200, ConsecutiveHolds = 1 };
        long quoted = BundleHold.NextCost(meta, Curve, 2.0);

        Assert.Equal(100, quoted);
        Assert.Equal(BundleHold.HoldResult.Kept, BundleHold.Apply(meta, keep: true, Curve, 2.0));
        Assert.Equal(100, meta.JunimoPoints);
    }

    [Fact]
    public void Apply_Refuses_When_The_Scaled_Price_Is_Unaffordable()
    {
        var meta = new MetaState { JunimoPoints = 60, ConsecutiveHolds = 1 };

        Assert.Equal(BundleHold.HoldResult.NotEnoughJp,
            BundleHold.Apply(meta, keep: true, Curve, 4.0));
        Assert.Equal(60, meta.JunimoPoints);
    }
}

public class SeasonPityDifficultyTests
{
    private static MetaState Stuck(DifficultyStep step, GameplayConfig cfg)
        => new()
        {
            SeasonFailCounts = new List<int> { 99, 0, 0, 0 },
            Difficulty = DifficultyResolver.Resolve(
                new DifficultySettings { SeasonPity = step }, cfg),
        };

    [Fact]
    public void Extreme_Never_Eases_A_Season()
    {
        var cfg = new GameplayConfig();

        Assert.Equal(0, SeasonPity.EaseSteps(Stuck(DifficultyStep.Extreme, cfg), Season.Spring, cfg));
    }

    [Fact]
    public void Normal_Still_Eases_Exactly_As_Before()
    {
        var cfg = new GameplayConfig();

        Assert.Equal(94, SeasonPity.EaseSteps(Stuck(DifficultyStep.Normal, cfg), Season.Spring, cfg));
    }

    /// <summary>Hard raises the threshold from 5 to 8, so three fewer ease steps have accrued.</summary>
    [Fact]
    public void Hard_Starts_Easing_Later()
    {
        var cfg = new GameplayConfig();

        Assert.Equal(91, SeasonPity.EaseSteps(Stuck(DifficultyStep.Hard, cfg), Season.Spring, cfg));
    }

    [Fact]
    public void Easy_Starts_Easing_Sooner()
    {
        var cfg = new GameplayConfig();

        Assert.Equal(96, SeasonPity.EaseSteps(Stuck(DifficultyStep.Easy, cfg), Season.Spring, cfg));
    }

    /// <summary>Counting always runs, whatever the step: a player who sets pity to Extreme, gets
    /// stuck, and drops back to Normal must find his accrued fails intact.</summary>
    [Fact]
    public void Counting_Still_Runs_At_Extreme()
    {
        var cfg = new GameplayConfig();
        var meta = Stuck(DifficultyStep.Extreme, cfg);

        SeasonPity.RecordFail(meta, Season.Spring);

        Assert.Equal(100, SeasonPity.Counts(meta)[(int)Season.Spring]);

        meta.Difficulty = DifficultyResolver.Resolve(new DifficultySettings(), cfg);
        Assert.Equal(95, SeasonPity.EaseSteps(meta, Season.Spring, cfg));
    }

    [Fact]
    public void Hard_Eases_Less_Per_Step_And_Stops_Higher()
    {
        var cfg = new GameplayConfig();
        var hard = DifficultyResolver.Resolve(
            new DifficultySettings { SeasonPity = DifficultyStep.Hard }, cfg).Pity;

        Assert.Equal(0.95, SeasonPity.QuotaFactor(1, hard), 6);   // 1 - 0.05
        Assert.Equal(0.75, SeasonPity.QuotaFactor(99, hard), 6);  // floor
    }

    [Fact]
    public void A_Legacy_Save_With_No_Stamp_Behaves_As_Before()
    {
        var cfg = new GameplayConfig();
        var meta = new MetaState { SeasonFailCounts = new List<int> { 99, 0, 0, 0 } };

        Assert.Null(meta.Difficulty);
        Assert.Equal(94, SeasonPity.EaseSteps(meta, Season.Spring, cfg));
    }
}
