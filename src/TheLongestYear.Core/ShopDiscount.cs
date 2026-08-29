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

    /// <summary>Percent taken off per owned tier.</summary>
    private const int PercentPerTier = 5;

    /// <summary>The Haggler boost's extra percent, additive with the chain (spec 2026-08-29, 2.7).</summary>
    public const int HagglerPercent = 10;

    public static int PercentForTier(int tier) => Math.Clamp(tier, 0, MaxTier) * PercentPerTier;

    /// <summary>The discounted price for an owned tier. Prices at or below zero are returned
    /// untouched (barter entries price at 0 and pay with a trade item instead), and a positive
    /// price never rounds down to free.</summary>
    public static int Apply(int price, int tier) => ApplyPercent(price, PercentForTier(tier));

    /// <summary>Take <paramref name="percent"/> off; non-positive prices untouched; never below 1g.</summary>
    public static int ApplyPercent(int price, int percent)
    {
        if (price <= 0 || percent <= 0) return price;
        int discounted = (int)Math.Round(price * (1.0 - percent / 100.0), MidpointRounding.AwayFromZero);
        return discounted < 1 ? 1 : discounted;
    }
}
