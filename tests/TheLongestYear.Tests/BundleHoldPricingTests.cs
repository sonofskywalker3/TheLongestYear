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
