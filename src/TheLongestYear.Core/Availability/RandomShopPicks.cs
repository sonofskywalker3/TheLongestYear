using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Lexicon;

namespace TheLongestYear.Core.Availability;

/// <summary>The shop listings as the shop weeks read them, with each layer's daily pick
/// (<see cref="RandomShopItem"/>) listed in full. Cornucopia's exclusive cart seeds are one id that
/// Content Patcher rolls per save and day (<c>{{Random: ...}}</c>), so Data/Shops held Peppercorn
/// Seeds on one fresh farm and not on another, and the availability stamp differed between them. A
/// listing on the pick's shop, with its condition when the pick names one, selling one of its items
/// is replaced by every item the pick can roll, each marked <see cref="RawShopListing.RandomPick"/>:
/// a year-1 source, never a week. The same match the obtain side's
/// <see cref="LayerSet.Normalize"/> uses.</summary>
public static class RandomShopPicks
{
    public static IReadOnlyList<RawShopListing> Expand(IReadOnlyList<RawShopListing> listings, IReadOnlyList<RandomShopItem>? picks)
    {
        if (listings == null) throw new ArgumentNullException(nameof(listings));
        if (picks == null || picks.Count == 0) return listings;
        var result = new List<RawShopListing>(listings.Count);
        var done = new HashSet<RandomShopItem>();
        foreach (RawShopListing listing in listings)
        {
            RandomShopItem? pick = listing == null ? null : picks.FirstOrDefault(p => Matches(p, listing));
            if (pick == null) { result.Add(listing!); continue; }
            if (!done.Add(pick)) continue;   // another line rolled from the same pick, already listed in full
            foreach (string id in pick.ItemIds)
                result.Add(listing! with { ItemId = BundleParsing.NormalizeItemId(id), RandomPick = true });
        }
        return result;
    }

    private static bool Matches(RandomShopItem pick, RawShopListing listing)
        => pick.ShopId == listing.ShopId
           && (pick.Condition == null || pick.Condition == listing.Condition)
           && pick.ItemIds.Any(id => BundleParsing.NormalizeItemId(id) == BundleParsing.NormalizeItemId(listing.ItemId));
}
