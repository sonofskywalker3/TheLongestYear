using System;

namespace TheLongestYear.Core;

/// <summary>Maps an item's sale price to a <see cref="Rarity"/> using <see cref="RarityThresholds"/>.</summary>
public static class RarityTiers
{
    public static Rarity FromPrice(int price, RarityThresholds thresholds)
    {
        if (thresholds is null)
            throw new ArgumentNullException(nameof(thresholds));

        if (price >= thresholds.VeryRareAtLeast) return Rarity.VeryRare;
        if (price >= thresholds.RareAtLeast) return Rarity.Rare;
        if (price >= thresholds.UncommonAtLeast) return Rarity.Uncommon;
        return Rarity.Common;
    }
}
