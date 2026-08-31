using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>How much of a forage item a season can actually produce, MEASURED IN GAME, and the
/// largest quantity a bundle may therefore ask for.
///
/// Why measured and not calculated: an expected-value pass over Data/Locations was tried first and
/// came out 2.3x too high (it predicted 11.7 Rainbow Shell for a Summer; three real years gave
/// 11/5/6). The spawn maths misses how many entries compete in a location's table and each entry's
/// own Chance. So these numbers come from <c>tly_sweepforage</c>: every spawned forage object on
/// every map, taken every day, for three full 112-day years. Raw data and the runs behind it are in
/// docs/superpowers/notes/forage-sweep-results.csv.
///
/// The figure stored is the MEAN of the three runs. The bands (Jeff, 2026-08-30) are:
///   easy    20%..50% of the mean
///   hard    50%..80% of the mean
///   ceiling 80% of the mean, rounded up - no slot may ever ask for more
///
/// Bonuses are deliberately excluded from the measurement (the sweep bypasses the pickup path that
/// ForageYieldPatch hooks, so Gatherer and forage_yield_up never fire). That is the design: an ask
/// should be out of reach on a lean month played bare, but reachable on a lean month played well.
/// The headroom is real - Overgrowth is 50 JP a week, so 200 JP holds forage_yield_up for a whole
/// season, and it stacks as its own roll alongside the weekly Foraging theme and vanilla Gatherer.
///
/// COVERAGE, and what is deliberately absent: forage only. Fish, bushes, dig spots, crab pots and
/// Green Rain are not measured here, so an item whose real supply comes from one of those is NOT in
/// this table and is NOT clamped by it - see <see cref="MinMeasuredAverage"/>. Clamping such an item
/// to its forage number alone would make it far rarer than it really is.</summary>
public static class ForageAskLimits
{
    /// <summary>Fraction of the measured mean that is the hard ceiling for any ask.</summary>
    public const double CeilingFraction = 0.80;

    /// <summary>Lower edge of the "hard" band, and the upper edge of "easy".</summary>
    public const double HardBandFloor = 0.50;

    /// <summary>Lower edge of the "easy" band.</summary>
    public const double EasyBandFloor = 0.20;

    /// <summary>An item averaging under this per season is left OUT of the table on purpose.
    ///
    /// Every item below the line turned out to be one whose real supply is not wild forage at all:
    /// Seaweed is a common rod catch, Fiddlehead Fern floods the forest on the single Green Rain day
    /// each Summer (Utility.isGreenRainDay: Summer only, one day drawn from 5/6/7/14/15/16/18/23),
    /// and Cockle, Mussel and Oyster all come out of crab pots in quantity. The forage sweep cannot
    /// see any of those routes, so its number for them is a floor and nowhere near a ceiling.
    /// Clamping on it would brand a perfectly gettable item as near-unobtainable, which is the very
    /// mistake this class exists to stop. They stay unclamped until their own routes are measured.</summary>
    public const double MinMeasuredAverage = 5.0;

    /// <summary>Measured mean count per season, from three full-year runs (loops 120/121/122).</summary>
    private static readonly IReadOnlyDictionary<(Season Season, string ItemId), double> Measured =
        new Dictionary<(Season, string), double>
    {
        // ---- Spring ----
        [(Season.Spring, "(O)16")] = 55.3,   // Wild Horseradish: 54/51/61, max ask 45
        [(Season.Spring, "(O)393")] = 53.3,   // Coral: 50/59/51, max ask 43
        [(Season.Spring, "(O)18")] = 51.3,   // Daffodil: 49/57/48, max ask 42
        [(Season.Spring, "(O)22")] = 39.7,   // Dandelion: 47/46/26, max ask 32
        [(Season.Spring, "(O)90")] = 38.7,   // Cactus Fruit: 37/44/35, max ask 31
        [(Season.Spring, "(O)88")] = 35.3,   // Coconut: 40/34/32, max ask 29
        [(Season.Spring, "(O)404")] = 34.0,   // Common Mushroom: 33/35/34, max ask 28
        [(Season.Spring, "(O)281")] = 33.7,   // Chanterelle: 28/36/37, max ask 27
        [(Season.Spring, "(O)20")] = 27.0,   // Leek: 31/19/31, max ask 22
        [(Season.Spring, "(O)372")] = 18.3,   // Clam: 18/14/23, max ask 15
        [(Season.Spring, "(O)257")] = 11.0,   // Morel: 10/11/12, max ask 9
        [(Season.Spring, "(O)397")] = 9.0,   // Sea Urchin: 7/9/11, max ask 8
        [(Season.Spring, "(O)723")] = 8.7,   // Oyster: 6/12/8, max ask 7
        [(Season.Spring, "(O)719")] = 7.7,   // Mussel: 8/7/8, max ask 7
        // ---- Summer ----
        [(Season.Summer, "(O)402")] = 84.3,   // Sweet Pea: 76/90/87, max ask 68
        [(Season.Summer, "(O)393")] = 56.3,   // Coral: 56/56/57, max ask 46
        [(Season.Summer, "(O)396")] = 48.0,   // Spice Berry: 45/47/52, max ask 39
        [(Season.Summer, "(O)398")] = 39.3,   // Grape: 31/37/50, max ask 32
        [(Season.Summer, "(O)90")] = 38.3,   // Cactus Fruit: 37/36/42, max ask 31
        [(Season.Summer, "(O)88")] = 38.3,   // Coconut: 28/42/45, max ask 31
        [(Season.Summer, "(O)420")] = 28.3,   // Red Mushroom: 24/26/35, max ask 23
        [(Season.Summer, "(O)259")] = 24.7,   // Fiddlehead Fern: 21/30/23, max ask 20
        [(Season.Summer, "(O)404")] = 19.3,   // Common Mushroom: 16/23/19, max ask 16
        [(Season.Summer, "(O)281")] = 18.3,   // Chanterelle: 18/15/22, max ask 15
        [(Season.Summer, "(O)372")] = 17.0,   // Clam: 13/21/17, max ask 14
        [(Season.Summer, "(O)422")] = 17.0,   // Purple Mushroom: 11/22/18, max ask 14
        [(Season.Summer, "(O)397")] = 13.3,   // Sea Urchin: 14/16/10, max ask 11
        [(Season.Summer, "(O)394")] = 7.3,   // Rainbow Shell: 11/5/6, max ask 6
        [(Season.Summer, "(O)719")] = 6.7,   // Mussel: 2/7/11, max ask 6
        // ---- Fall ----
        [(Season.Fall, "(O)404")] = 57.7,   // Common Mushroom: 56/62/55, max ask 47
        [(Season.Fall, "(O)410")] = 57.3,   // Blackberry: 59/72/41, max ask 46
        [(Season.Fall, "(O)393")] = 54.7,   // Coral: 55/54/55, max ask 44
        [(Season.Fall, "(O)90")] = 40.7,   // Cactus Fruit: 38/43/41, max ask 33
        [(Season.Fall, "(O)88")] = 32.0,   // Coconut: 32/28/36, max ask 26
        [(Season.Fall, "(O)281")] = 26.0,   // Chanterelle: 23/27/28, max ask 21
        [(Season.Fall, "(O)408")] = 22.3,   // Hazelnut: 24/20/23, max ask 18
        [(Season.Fall, "(O)420")] = 22.3,   // Red Mushroom: 22/22/23, max ask 18
        [(Season.Fall, "(O)422")] = 18.7,   // Purple Mushroom: 13/24/19, max ask 15
        [(Season.Fall, "(O)372")] = 17.7,   // Clam: 13/20/20, max ask 15
        [(Season.Fall, "(O)406")] = 15.0,   // Wild Plum: 14/12/19, max ask 12
        [(Season.Fall, "(O)397")] = 10.0,   // Sea Urchin: 10/12/8, max ask 8
        [(Season.Fall, "(O)719")] = 6.0,   // Mussel: 5/5/8, max ask 5
        [(Season.Fall, "(O)723")] = 5.3,   // Oyster: 8/3/5, max ask 5
        // ---- Winter ----
        [(Season.Winter, "(O)283")] = 78.3,   // Holly: 79/65/91, max ask 63
        [(Season.Winter, "(O)418")] = 49.3,   // Crocus: 52/49/47, max ask 40
        [(Season.Winter, "(O)393")] = 48.0,   // Coral: 58/44/42, max ask 39
        [(Season.Winter, "(O)414")] = 42.3,   // Crystal Fruit: 35/43/49, max ask 34
        [(Season.Winter, "(O)88")] = 36.7,   // Coconut: 34/36/40, max ask 30
        [(Season.Winter, "(O)90")] = 31.3,   // Cactus Fruit: 32/30/32, max ask 26
        [(Season.Winter, "(O)422")] = 19.0,   // Purple Mushroom: 23/19/15, max ask 16
        [(Season.Winter, "(O)281")] = 18.0,   // Chanterelle: 15/16/23, max ask 15
        [(Season.Winter, "(O)420")] = 15.7,   // Red Mushroom: 14/16/17, max ask 13
        [(Season.Winter, "(O)397")] = 12.0,   // Sea Urchin: 14/11/11, max ask 10
        [(Season.Winter, "(O)392")] = 12.0,   // Nautilus Shell: 12/12/12, max ask 10
        [(Season.Winter, "(O)404")] = 10.3,   // Common Mushroom: 12/9/10, max ask 9
        [(Season.Winter, "(O)372")] = 10.0,   // Clam: 9/10/11, max ask 8
    };

    /// <summary>The largest quantity a bundle may ask for, or null when the item was not measured
    /// (or sits under <see cref="MinMeasuredAverage"/>), in which case the caller must not clamp.</summary>
    public static int? MaxAsk(Season season, string itemId)
    {
        double? mean = MeanFor(season, itemId);
        return mean == null ? null : Math.Max(1, (int)Math.Ceiling(mean.Value * CeilingFraction));
    }

    /// <summary>The measured mean, or null when the item is not covered.</summary>
    public static double? MeanFor(Season season, string itemId)
    {
        if (itemId == null) return null;
        return Measured.TryGetValue((season, itemId), out double mean) ? mean : null;
    }

    /// <summary>Clamp a rolled stack to the measured ceiling. Returns <paramref name="stack"/>
    /// unchanged for anything this table does not cover, so non-forage slots are untouched.</summary>
    public static int Clamp(Season season, string itemId, int stack)
    {
        int? max = MaxAsk(season, itemId);
        return max == null || stack <= max.Value ? stack : max.Value;
    }

    /// <summary>The ceiling for an item without knowing which season will ask for it: the most
    /// generous season it was measured in.
    ///
    /// BundleSpec carries no season, and a seasonal-forage bundle only ever draws items that are in
    /// season for it, so the item's best season is the one that will be asking. Taking the maximum
    /// is deliberately the safe direction - it can never clamp BELOW what that bundle's own season
    /// can produce, so this can only ever be too lenient, never impossible.</summary>
    public static int? MaxAskAnySeason(string itemId)
    {
        if (itemId == null) return null;
        double best = 0;
        foreach (KeyValuePair<(Season Season, string ItemId), double> row in Measured)
            if (string.Equals(row.Key.ItemId, itemId, StringComparison.Ordinal) && row.Value > best)
                best = row.Value;
        return best <= 0 ? null : Math.Max(1, (int)Math.Ceiling(best * CeilingFraction));
    }

    /// <summary>Clamp without a season. See <see cref="MaxAskAnySeason"/>.</summary>
    public static int ClampAnySeason(string itemId, int stack)
    {
        int? max = MaxAskAnySeason(itemId);
        return max == null || stack <= max.Value ? stack : max.Value;
    }
}
