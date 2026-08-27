using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Price of the NEXT keep-bundles hold, from the config curve. Pure.</summary>
public static class BundleHoldPricing
{
    /// <param name="factor">The run's difficulty hold-price factor (spec 2026-08-26). The first
    /// hold stays free at every step because the curve starts at 0 and zero times anything is
    /// zero: the step makes REPEATED holds expensive rather than taxing the first mistake.</param>
    public static long CostFor(int consecutiveHolds, IReadOnlyList<long>? curve, double factor = 1.0)
    {
        if (curve == null || curve.Count == 0) return 0;
        int index = Math.Clamp(consecutiveHolds, 0, curve.Count - 1);
        long cost = Math.Max(0, curve[index]);
        if (factor == 1.0 || cost == 0) return cost;
        return Math.Max(0, (long)Math.Round(cost * factor, MidpointRounding.AwayFromZero));
    }
}
