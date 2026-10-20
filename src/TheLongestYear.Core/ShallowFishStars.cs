using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Load-time repair for boards rolled before <see cref="FishStarDepth"/>: a silver or
/// gold ask for a fish that only bites in shallow water (<see cref="ItemPools.ShallowWaterFishIds"/>)
/// drops to base quality. The fish itself stays: it is catchable, only the star was impossible,
/// which is how every other impossible-star slot is repaired (BoardRepairService.KeepQuality).</summary>
public static class ShallowFishStars
{
    /// <summary>The value with every starred ask for a shallow-water fish lowered to base quality,
    /// or null when nothing changed.</summary>
    public static string? RepairBundleValue(string value, IReadOnlySet<string> shallowFishIds)
        => shallowFishIds == null || shallowFishIds.Count == 0
            ? null
            : BundleAskRewrite.LowerQualities(value, (id, quality) => IsShallow(id, shallowFishIds) ? 0 : quality);

    /// <summary>The normalized ids of the shallow-water fish this value asks for with a star.</summary>
    public static IReadOnlyList<string> LoweredIds(string value, IReadOnlySet<string> shallowFishIds)
    {
        var ids = new List<string>();
        if (shallowFishIds == null || shallowFishIds.Count == 0) return ids;
        BundleAskRewrite.LowerQualities(value, (id, quality) =>
        {
            if (quality > 0 && IsShallow(id, shallowFishIds)) ids.Add(BundleParsing.NormalizeItemId(id));
            return quality;
        });
        return ids.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool IsShallow(string id, IReadOnlySet<string> shallowFishIds)
        => shallowFishIds.Contains(BundleParsing.NormalizeItemId(id));
}
