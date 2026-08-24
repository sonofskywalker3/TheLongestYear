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
