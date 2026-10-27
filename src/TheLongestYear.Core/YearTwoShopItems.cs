using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

/// <summary>Items no shop sells in year 1: at least one shop line offers them behind a
/// <c>YEAR</c> clause a loop never meets, and no other shop line offers them in year 1. Read from
/// Data/Shops, so a mod's lines count the same way as vanilla's (Garlic, Red Cabbage and
/// Artichoke Seeds, Rice Shoot, Quality Fertilizer, Quality Retaining Soil, Deluxe Speed-Gro and
/// the Animal Catalogue on an unmodded game).
///
/// Left out: recipe lines, item queries (RANDOM_ITEMS and the like name no fixed item), and the
/// Traveling Cart's own lines, which are what this list feeds. Not seen at all: a Content Patcher
/// mod that adds its line only once the year is 2 (a <c>Year</c> token rather than a YEAR
/// condition). That line is not in Data/Shops during a loop, so there is nothing to read.</summary>
public static class YearTwoShopItems
{
    private const string CartShopId = "Traveler";

    /// <param name="sellable">True when the item can be sold at the cart (exists, has a price).</param>
    public static IReadOnlyList<string> Find(IEnumerable<ShopRow> rows, Func<string, bool> sellable)
    {
        var yearTwo = new HashSet<string>(StringComparer.Ordinal);
        var yearOne = new HashSet<string>(StringComparer.Ordinal);
        foreach (ShopRow row in rows ?? Array.Empty<ShopRow>())
        {
            if (row.IsRecipe || row.IsRandom || row.ShopId == CartShopId) continue;
            if (string.IsNullOrWhiteSpace(row.ItemId) || row.ItemId.Contains(' ')) continue;
            string id = BundleParsing.NormalizeItemId(row.ItemId);
            (YearOneCondition.Allows(row.Condition) ? yearOne : yearTwo).Add(id);
        }
        yearTwo.ExceptWith(yearOne);
        return yearTwo.Where(sellable).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }
}
