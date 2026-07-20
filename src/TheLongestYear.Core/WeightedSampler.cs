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
    public static List<PoolItem> Sample(
        IReadOnlyList<PoolItem> candidates, int count, Random rng)
    {
        var remaining = candidates.ToList();
        var picked = new List<PoolItem>(count);
        for (int i = 0; i < count; i++)
        {
            int total = remaining.Sum(p => p.Weight);
            int roll = rng.Next(total);
            int cursor = 0;
            for (int j = 0; j < remaining.Count; j++)
            {
                cursor += remaining[j].Weight;
                if (roll < cursor)
                {
                    picked.Add(remaining[j]);
                    remaining.RemoveAt(j);
                    break;
                }
            }
        }
        return picked;
    }
}
