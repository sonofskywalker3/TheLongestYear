using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Jeff, 2026-08-26: "why doesn't the price reduction jp buy change the posted item prices?"
///
/// It used to discount ShopMenu.chargePlayer, the gold-deduction chokepoint, which left the shelf
/// price at vanilla's number AND left vanilla gating the sale on the full price
/// (ShopMenu.cs:1631 checks the player's gold against the undiscounted amount before it ever calls
/// chargePlayer). So with Shop Discount V and 90g you could not buy a 100g item you would only have
/// been charged 75g for. The discount now lands on the shop's own price.
/// </summary>
public class ShopDiscountTests
{
    [Theory]
    [InlineData(0, 100, 100)]   // no upgrade owned
    [InlineData(1, 100, 95)]
    [InlineData(2, 100, 90)]
    [InlineData(3, 100, 85)]
    [InlineData(4, 100, 80)]
    [InlineData(5, 100, 75)]
    public void Each_tier_takes_five_percent(int tier, int price, int expected)
    {
        Assert.Equal(expected, ShopDiscount.Apply(price, tier));
    }

    [Fact]
    public void Rounds_away_from_zero_like_the_old_till_discount()
    {
        // 25 * 0.95 = 23.75 -> 24, matching what the chargePlayer patch used to deduct.
        Assert.Equal(24, ShopDiscount.Apply(25, 1));
    }

    [Fact]
    public void Never_makes_anything_free()
    {
        // A 1g item at 25% off rounds to 1, not 0: a free item would let a shop be drained.
        Assert.Equal(1, ShopDiscount.Apply(1, 5));
    }

    [Fact]
    public void Leaves_zero_and_negative_prices_alone()
    {
        // Barter entries price at 0 and pay with a trade item; nothing to discount.
        Assert.Equal(0, ShopDiscount.Apply(0, 5));
        Assert.Equal(-5, ShopDiscount.Apply(-5, 5));
    }

    [Fact]
    public void Percent_form_adds_haggler_on_top_of_the_chain_and_floors_at_one()
    {
        Assert.Equal(25, ShopDiscount.PercentForTier(5));
        Assert.Equal(65, ShopDiscount.ApplyPercent(100, 25 + ShopDiscount.HagglerPercent));
        Assert.Equal(1, ShopDiscount.ApplyPercent(1, 35));
        Assert.Equal(100, ShopDiscount.ApplyPercent(100, 0));
        Assert.Equal(ShopDiscount.Apply(200, 3), ShopDiscount.ApplyPercent(200, 15));
    }

    [Fact]
    public void Tiers_outside_the_chain_are_clamped()
    {
        Assert.Equal(100, ShopDiscount.Apply(100, -1));
        Assert.Equal(75, ShopDiscount.Apply(100, 99));
    }
}
