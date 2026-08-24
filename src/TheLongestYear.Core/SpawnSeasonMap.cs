using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Per-item spawn seasons derived from the engine's fish/crab-pot pools (whose
/// Seasons come from Data/Locations spawn entries + GSQ season conditions). Feeds the
/// CcItem catalog's SeasonResolver so seasonal fish stop defaulting to "year-round" —
/// before this, a Spring weekly theme could ask for Pike, a Summer/Winter-only fish
/// (Nexus 1122423, 2026-08-24). An empty Seasons list on a pool item means "spawns in
/// any season" and maps to all four.</summary>
public static class SpawnSeasonMap
{
    private static readonly Season[] AllSeasons =
        { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    public static IReadOnlyDictionary<string, IReadOnlySet<Season>> FromPools(ItemPools pools)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        var map = new Dictionary<string, IReadOnlySet<Season>>(StringComparer.Ordinal);
        Merge(map, pools.Fish);
        Merge(map, pools.CrabPot);
        return map;
    }

    private static void Merge(
        Dictionary<string, IReadOnlySet<Season>> map, IReadOnlyList<PoolItem> pool)
    {
        foreach (PoolItem item in pool ?? Array.Empty<PoolItem>())
        {
            IReadOnlyList<Season> seasons = item.Seasons.Count == 0 ? AllSeasons : item.Seasons;
            if (map.TryGetValue(item.ItemId, out IReadOnlySet<Season>? existing))
            {
                var union = new HashSet<Season>(existing);
                union.UnionWith(seasons);
                map[item.ItemId] = union;
            }
            else
            {
                map[item.ItemId] = new HashSet<Season>(seasons);
            }
        }
    }
}
