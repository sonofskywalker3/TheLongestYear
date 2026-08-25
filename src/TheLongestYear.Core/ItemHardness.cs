using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Hardness ranking for the reshuffle-path pity trim (spec 2026-08-25 section 3).
/// Score = rarity tier (Common 1 .. VeryRare 4) + 2 if the domain needs a station or recipe
/// + 1 if the item's earliest spawn season is Fall or Winter. Higher = harder.</summary>
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

    /// <summary>Removes up to <paramref name="count"/> items, hardest first (ties: higher ordinal
    /// id first, so the result is deterministic), never leaving fewer than
    /// <paramref name="minKeep"/>. Preserves the input order of the survivors. Drops by item id,
    /// which relies on the pool's items carrying distinct ids -- an invariant
    /// <see cref="ItemPoolBuilder"/> establishes by building each pool from an id-keyed
    /// dictionary before emitting it; a pool with duplicate ids would have this over-remove (one
    /// drop decision silently removing every item sharing that id).</summary>
    public static IReadOnlyList<PoolItem> Trim(
        IReadOnlyList<PoolItem> pool, int count, int minKeep, PoolDomain domain, RarityThresholds thresholds)
    {
        int removable = Math.Min(Math.Max(0, count), Math.Max(0, pool.Count - Math.Max(0, minKeep)));
        if (removable == 0)
            return pool;

        var drop = new HashSet<string>(
            pool.OrderByDescending(p => Score(p, domain, thresholds))
                .ThenByDescending(p => p.ItemId, StringComparer.Ordinal)
                .Take(removable)
                .Select(p => p.ItemId),
            StringComparer.Ordinal);
        return pool.Where(p => !drop.Contains(p.ItemId)).ToList();
    }
}
