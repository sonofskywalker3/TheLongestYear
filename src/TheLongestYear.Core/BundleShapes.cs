using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Shown and needed counts for a re-rolled vanilla bundle whose own shape is wrong for
/// what the engine asks of it. The filler rolls <c>Shown</c> slots and needs <c>Needs</c>; the
/// Required Slots dial then moves the need as it does for every bundle (Easy -1, Hard +1, Extreme
/// all).
///
/// Sticky (Jeff, 2026-09-30): vanilla's Remixed Sticky is one slot of 500 Sap. Re-rolled as one
/// slot it asked for a single Acorn, Sugar or Ice Cream. Six shown and four needed reads 3/4/5/6
/// across the dial, in line with the other created bundles.</summary>
public static class BundleShapes
{
    private static readonly IReadOnlyDictionary<string, (int Shown, int Needs)> Shapes =
        new Dictionary<string, (int Shown, int Needs)>(StringComparer.Ordinal)
        {
            ["Sticky"] = (6, 4),
        };

    public static (int Shown, int Needs)? For(string? bundleName)
        => bundleName != null && Shapes.TryGetValue(bundleName, out (int Shown, int Needs) shape) ? shape : null;
}
