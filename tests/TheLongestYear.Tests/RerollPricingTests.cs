using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RerollPricingTests
{
    [Theory]
    [InlineData(0, 50)] [InlineData(1, 100)] [InlineData(2, 200)] [InlineData(5, 1600)]
    public void Costs_jp_doubles_each_reroll_from_fifty(int already, long cost)
        => Assert.Equal(cost, RerollPricing.CostOf(RerollMode.CostsJp, already));

    [Fact]
    public void Free_rerolls_cost_nothing() => Assert.Equal(0, RerollPricing.CostOf(RerollMode.Free, 7));

    [Fact]
    public void A_huge_count_never_overflows()
        => Assert.Equal(RerollPricing.MaxCost, RerollPricing.CostOf(RerollMode.CostsJp, 80));
}
