using System;

namespace TheLongestYear.Core;

/// <summary>
/// Shop Discount (shop_discount_1..5): 5% off per tier, up to 25%.
///
/// Applied to the shop's own price, not to the gold deduction. The original implementation
/// discounted <c>ShopMenu.chargePlayer</c>, which had two problems: the shelf still showed
/// vanilla's price, and vanilla gates the sale on that full price before it ever charges you
/// (ShopMenu.cs:1631), so the discount refunded you at the till without extending your buying
/// power - Shop Discount V with 90g still could not buy a 100g item. Discounting the price fixes
/// the display and the gate at once.
/// </summary>
public static class ShopDiscount
{
    /// <summary>Highest tier in the shop_discount chain.</summary>
    public const int MaxTier = 5;

    /// <summary>Fraction taken off per owned tier.</summary>
    private const double PerTier = 0.05;

    /// <summary>The discounted price for an owned tier. Prices at or below zero are returned
    /// untouched (barter entries price at 0 and pay with a trade item instead), and a positive
    /// price never rounds down to free.</summary>
    public static int Apply(int price, int tier)
    {
        if (price <= 0) return price;
        if (tier <= 0) return price;
        if (tier > MaxTier) tier = MaxTier;

        int discounted = (int)Math.Round(price * (1.0 - tier * PerTier), MidpointRounding.AwayFromZero);
        return discounted < 1 ? 1 : discounted;
    }
}
