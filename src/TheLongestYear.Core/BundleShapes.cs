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
/// across the dial, in line with the other created bundles.
///
/// Rare Crops (Jeff, 2026-11-16): vanilla's Remixed Rare Crops shows two and needs one. A bundle
/// that needs one item is due as soon as its easiest item is, which leaves the other no real
/// choice (JazzDaxx: 3 Apple or 25 Cactus Fruit, due before Fall). Same six and four.
///
/// The bundles that needed two (Jeff, 2026-11-16): Easy's dial took them down to one, and no
/// bundle should need a single item. They need three of four now, so the dial reads 2/3/4/4.
/// Gil's Trophies is authored and carries the same count in AuthoredBundles. Helper's stays two
/// of two: it re-rolls from its own two vanilla items and has no third to show.</summary>
public static class BundleShapes
{
    private static readonly IReadOnlyDictionary<string, (int Shown, int Needs)> Shapes =
        new Dictionary<string, (int Shown, int Needs)>(StringComparer.Ordinal)
        {
            ["Sticky"] = (6, 4),
            ["Rare Crops"] = (6, 4),
            ["Adventurer's"] = (4, 3),
            ["Fish Farmer's"] = (4, 3),
            ["Forager's"] = (4, 3),
            ["Master Fisher's"] = (4, 3),
            ["Winter Star"] = (4, 3),
        };

    public static (int Shown, int Needs)? For(string? bundleName)
        => bundleName != null && Shapes.TryGetValue(bundleName, out (int Shown, int Needs) shape) ? shape : null;
}
