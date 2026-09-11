using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Hardness ranking for an item in a pool: rarity tier (Common 1 .. VeryRare 4) + 2 if
/// the domain needs a station or recipe + 1 if the item's earliest spawn season is Fall or Winter.
/// Higher = harder. Written for the reshuffle-path pity trim (spec 2026-08-25 section 3) and kept
/// when that was retired, because <see cref="RarityBias"/> ranks with the same score.</summary>
public static class ItemHardness
{
    private const int StationBonus = 2;
    private const int LateSpawnBonus = 1;

    public static bool NeedsStation(PoolDomain domain) => domain == PoolDomain.ArtisanGoods;

    public static int Score(PoolItem item, PoolDomain domain, RarityThresholds thresholds)
    {
        int score = RarityTiers.FromPrice(item.Price, thresholds) switch
        {
            Rarity.VeryRare => 4,
            Rarity.Rare => 3,
            Rarity.Uncommon => 2,
            _ => 1,
        };
        if (NeedsStation(domain)) score += StationBonus;
        if (item.Seasons.Count > 0 && item.Seasons.Min() >= Season.Fall) score += LateSpawnBonus;
        return score;
    }
}
