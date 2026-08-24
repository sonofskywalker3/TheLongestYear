using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Price of the NEXT keep-bundles hold, from the config curve. Pure.</summary>
public static class BundleHoldPricing
{
    public static long CostFor(int consecutiveHolds, IReadOnlyList<long> curve)
    {
        if (curve.Count == 0) return 0;
        int index = Math.Clamp(consecutiveHolds, 0, curve.Count - 1);
        return Math.Max(0, curve[index]);
    }
}
