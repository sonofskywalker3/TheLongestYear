namespace TheLongestYear.Core;

/// <summary>
/// Cart Whisperer foresight helper. Computes which days this week the Traveling Cart visits
/// and how many item slots the player can preview. Item name resolution is done mod-side
/// via ShopBuilder.GetShopStock("Traveler") — this Core type is pure metadata.
///
/// The cart visits on days where <c>dayOfMonth % 7 % 5 == 0</c>
/// (Forest.ShouldTravelingMerchantVisitToday in the decompile).
/// In a 7-day week (days 1-7): day 5 and day 7 visit.
/// In week 2 (days 8-14): days 12 and 14. Week 3: 19 and 21. Week 4: 26 and 28.
/// </summary>
public static class CartStockPreview
{
    /// <summary>Cart visit days within the week starting at <paramref name="weekStartDay"/>.</summary>
    public static int[] CartVisitDaysInWeek(int weekStartDay)
    {
        var days = new System.Collections.Generic.List<int>();
        for (int d = weekStartDay; d < weekStartDay + 7; d++)
        {
            if (d % 7 % 5 == 0)
                days.Add(d);
        }
        return days.ToArray();
    }

    /// <summary>Total preview slots granted by tier N Cart Whisperer (tier N = 2N items).</summary>
    public static int SlotsToReveal(int cartWhispererTier)
        => cartWhispererTier * 2;
}
