using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CartStockPreviewTests
{
    [Fact]
    public void CartVisitDays_includes_day5_and_day7()
    {
        // Week starting day 1: cart visits days 5 and 7.
        int[] visitDays = CartStockPreview.CartVisitDaysInWeek(weekStartDay: 1);
        Assert.Contains(5, visitDays);
        Assert.Contains(7, visitDays);
    }

    [Fact]
    public void CartVisitDays_week2_includes_day12_and_day14()
    {
        // Week starting day 8: cart visits days 12 and 14.
        int[] visitDays = CartStockPreview.CartVisitDaysInWeek(weekStartDay: 8);
        Assert.Contains(12, visitDays);
        Assert.Contains(14, visitDays);
    }

    [Fact]
    public void SlotsToReveal_returns_correct_count()
    {
        int slots = CartStockPreview.SlotsToReveal(cartWhispererTier: 2);
        Assert.Equal(4, slots); // tier 2 = 2*2 items
    }

    [Fact]
    public void SlotsToReveal_tier0_returns_0()
    {
        Assert.Equal(0, CartStockPreview.SlotsToReveal(0));
    }
}
