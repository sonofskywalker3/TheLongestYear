using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Shared weighted-sampling-without-replacement primitive (spec "expanded-pool
/// remix"): cumulative-weight walk over an ordered candidate list, removing each pick —
/// deterministic for a given rng stream and pool order. Extracted from
/// <see cref="BundleSlotFiller"/> (Plan 3 Task 3) so <see cref="AuthoredBundleComposer"/>
/// can reuse identical sampling semantics.</summary>
internal static class WeightedSampler
{
    /// <param name="capped">Optional group predicate: once <paramref name="cap"/> picks satisfy
    /// it, every remaining member of the group is dropped from the candidates, so the group
    /// contributes at most <paramref name="cap"/> picks (Night Fishing: one Night Market fish
    /// per bundle). Null = no cap. Same rng consumption as before when null.</param>
    public static List<PoolItem> Sample(
        IReadOnlyList<PoolItem> candidates, int count, Random rng,
        Func<PoolItem, bool>? capped = null, int cap = int.MaxValue)
    {
        var remaining = candidates.ToList();
        var picked = new List<PoolItem>(count);
        int cappedTaken = 0;
        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            int total = remaining.Sum(p => p.Weight);
            int roll = rng.Next(total);
            int cursor = 0;
            for (int j = 0; j < remaining.Count; j++)
            {
                cursor += remaining[j].Weight;
                if (roll < cursor)
                {
                    PoolItem pick = remaining[j];
                    picked.Add(pick);
                    remaining.RemoveAt(j);
                    if (capped != null && capped(pick) && ++cappedTaken >= cap)
                        remaining.RemoveAll(p => capped(p));
                    break;
                }
            }
        }
        return picked;
    }

    /// <summary>How many distinct picks <see cref="Sample"/> can make from these candidates
    /// under the group cap: every uncapped item plus at most <paramref name="cap"/> capped ones.</summary>
    public static int Capacity(IReadOnlyList<PoolItem> candidates, Func<PoolItem, bool>? capped, int cap)
    {
        if (capped == null)
            return candidates.Count;
        int inGroup = candidates.Count(capped);
        return candidates.Count - inGroup + Math.Min(inGroup, cap);
    }
}
