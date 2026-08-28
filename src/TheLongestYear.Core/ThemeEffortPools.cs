using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which engine pool items a theme can ask for, for the effort review document
/// (tly_dumpeffort) and tly_itemmodel's tier. Room themes map to their pools; the activity themes
/// merge by item kind; Mixed is the union.</summary>
public static class ThemeEffortPools
{
    private const int GemCategory = -2;
    private const int MineralCategory = -12;
    private const int EggCategory = -5;
    private const int MilkCategory = -6;
    private const int AnimalProductCategory = -18;

    public static IReadOnlyList<string> IdsFor(
        Theme theme, ItemPools pools, IReadOnlyDictionary<string, RawObjectEntry> objects)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        if (objects == null) throw new ArgumentNullException(nameof(objects));
        IEnumerable<string> ids = theme switch
        {
            Theme.Foraging => Ids(pools.Forage),
            Theme.Farming => Ids(pools.Crops),
            Theme.Fishing => Ids(pools.Fish).Concat(Ids(pools.CrabPot)),
            Theme.Mining => Ids(pools.Metals).Concat(Ids(pools.GeodeMinerals)),
            _ => Ids(pools.Forage).Concat(Ids(pools.Crops)).Concat(Ids(pools.Fish)).Concat(Ids(pools.CrabPot))
                .Concat(Ids(pools.Metals)).Concat(Ids(pools.GeodeMinerals)).Concat(Ids(pools.MonsterDrops))
                .Concat(Ids(pools.Artifacts)).Concat(Ids(pools.ArtisanGoods)).Concat(Ids(pools.Cooking))
                .Concat(ByCategory(objects, GemCategory, MineralCategory, EggCategory, MilkCategory, AnimalProductCategory)),
        };
        return ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> Ids(IReadOnlyList<PoolItem> pool)
        => (pool ?? new List<PoolItem>()).Select(p => p.ItemId);

    private static IEnumerable<string> ByCategory(IReadOnlyDictionary<string, RawObjectEntry> objects, params int[] categories)
        => objects.Where(kv => Array.IndexOf(categories, kv.Value.Category) >= 0)
            .Select(kv => BundleParsing.NormalizeItemId(kv.Key));
}
