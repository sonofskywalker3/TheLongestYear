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
/// The figure stored is the MEAN of the three runs, EXCEPT where a row is marked island-halved:
/// the sweep save had Ginger Island created, so the island cave's all-season mushroom spawns were
/// counted as if a loop could reach them, and those rows were halved by ruling. See
/// <see cref="AskCeilingRulings"/> for the whole story and for the ceilings that are judgement
/// rather than data.
///
/// The bands (Jeff, 2026-08-30) are:
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

    /// <summary>Forage that only exists in the Calico Desert, which a loop has to UNLOCK - the bus
    /// costs the Vault bundle, and TLY paces that at LocationGating/AvailabilityWeeks.SkullCavernWeek
    /// (week 9, i.e. Fall 1; DesertHardWeek 6 is the earliest a player who rushes it can be there).
    ///
    /// The measurements were taken on a save that already had the desert open, so the sweep happily
    /// counted Cactus Fruit and Coconut from Spring 1 - about 38 a season in Spring and Summer, which
    /// no real loop can reach. Those two rows per item are therefore DROPPED: the ceiling for desert
    /// forage comes only from Fall and Winter, the seasons a loop can actually be standing there.
    ///
    /// This is a caution for anything added later, not just these two: a measured count is only as
    /// honest as the access the measuring save had. Any location-gated forage (Secret Woods behind
    /// the Steel Axe, the Sewer, the Island) has the same trap.</summary>
    private static readonly IReadOnlySet<string> DesertOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)90",   // Cactus Fruit
        "(O)88",   // Coconut
    };

    /// <summary>True for forage that needs the desert unlocked, so it cannot be asked for in the
    /// seasons before the bus is repaired.</summary>
    public static bool IsDesertOnly(string itemId)
        => itemId != null && DesertOnly.Contains(itemId);

    /// <summary>First season a desert item can honestly be asked for: week 9 is Fall 1.</summary>
    public static Season DesertOpensIn => AvailabilityWeeks.SeasonOf(AvailabilityWeeks.SkullCavernWeek);

    /// <summary>True when the season is too early for this item's location to be reachable.</summary>
    public static bool IsBeforeUnlock(Season season, string itemId)
        => IsDesertOnly(itemId) && season < DesertOpensIn;

    /// <summary>Everything Wild Seeds can grow (decompile Crop.getRandomWildCropForSeason, line
    /// 739). These are NEVER capped below the 99 stack limit, because their supply is not the wild
    /// spawn rate at all: the Wild Seeds recipe turns 4 forage into 10 seeds, each seed grows one of
    /// these back, and the cycle repeats. Supply is bounded by tilled watered land and time, not by
    /// what the maps happen to drop, so a measured spawn count is meaningless as a ceiling for them.
    ///
    /// Note what is NOT here, which is the whole reason the distinction matters: you cannot grow a
    /// shell. Rainbow Shell, Coral, Sea Urchin, Clam, Nautilus Shell, Cactus Fruit, Coconut,
    /// Fiddlehead Fern and the Red/Purple/Chanterelle mushrooms are all absent, so their measured
    /// ceilings stand - Summer Seeds cannot be farmed into a Rainbow Shell no matter how much land
    /// is given over to them.</summary>
    private static readonly IReadOnlySet<string> WildSeedGrowable = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)16", "(O)18", "(O)20", "(O)22",                  // Spring: Wild Horseradish, Daffodil, Leek, Dandelion
        "(O)396", "(O)398", "(O)402",                        // Summer: Spice Berry, Grape, Sweet Pea
        "(O)404", "(O)406", "(O)408", "(O)410",              // Fall:   Common Mushroom, Wild Plum, Hazelnut, Blackberry
        "(O)412", "(O)414", "(O)416", "(O)418",              // Winter: Winter Root, Crystal Fruit, Snow Yam, Crocus
    };

    /// <summary>True when the item can be farmed from Wild Seeds, so no measured cap applies.</summary>
    public static bool IsWildSeedGrowable(string itemId)
        => itemId != null && WildSeedGrowable.Contains(itemId);

    /// <summary>Ceilings set by JUDGEMENT, not by measurement, and which beat the measured table.
    ///
    /// Every row here exists because the sweep's number for that item was not the item's real
    /// supply. Keep the reason with the row: a number no one can retrace is a number no one can
    /// argue with later.
    ///
    /// The Ginger Island contamination (found 2026-08-30, the same trap as the desert in
    /// v0.16.175): tly_sweepforage walks Game1.locations, and the throwaway save has the island
    /// created, so IslandNorthCave1 was swept every day. That cave is the only island map with
    /// Data/Locations forage rows, and it spawns Chanterelle, Common Mushroom, Red Mushroom and
    /// Purple Mushroom at chance 0.9 in EVERY season. The island is content a loop never reaches
    /// (see ItemPoolBuilder.BuiltInExcludedItemIds), so every mushroom count was inflated by it.
    /// Purple Mushroom is the proof: it has no mainland Data/Locations forage row anywhere in the
    /// game, yet the sweep credited it 17-19 a season. The other three mushrooms' measured rows
    /// were halved in place (Jeff, 2026-08-30: "just cut what you found in half"); Purple Mushroom
    /// had nothing honest left to halve, so its rows were deleted and this ruling stands instead.
    ///
    /// Purple Mushroom = 5 (Jeff, 2026-08-30): its real mainland supply is the mines' mushroom
    /// floors (CcItemCatalog puts it at floor 80+ / Skull Cavern), which yield about that much,
    /// and a player who knows what they are doing can farm them. A judgement, not a measurement.</summary>
    private static readonly IReadOnlyDictionary<string, int> AskCeilingRulings =
        new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)422"] = 5,   // Purple Mushroom
    };

    /// <summary>A ruled ceiling for the item, or null when none was ruled. Season-independent:
    /// a ruling is a statement about the item's supply, not about one season's spawn table.</summary>
    public static int? RuledMaxAsk(string itemId)
        => itemId != null && AskCeilingRulings.TryGetValue(itemId, out int max) ? max : null;

    /// <summary>Measured mean count per season, from three full-year runs (loops 120/121/122).</summary>
    private static readonly IReadOnlyDictionary<(Season Season, string ItemId), double> Measured =
        new Dictionary<(Season, string), double>
    {
        // ---- Spring ----
        [(Season.Spring, "(O)16")] = 55.3,   // Wild Horseradish: 54/51/61, max ask 45
        [(Season.Spring, "(O)393")] = 53.3,   // Coral: 50/59/51, max ask 43
        [(Season.Spring, "(O)18")] = 51.3,   // Daffodil: 49/57/48, max ask 42
        [(Season.Spring, "(O)22")] = 39.7,   // Dandelion: 47/46/26, max ask 32
        [(Season.Spring, "(O)404")] = 17.0,   // Common Mushroom: 33/35/34 island-halved, max ask 14 (exempt: wild seed)
        [(Season.Spring, "(O)281")] = 16.9,   // Chanterelle: 28/36/37 island-halved, max ask 14
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
        [(Season.Summer, "(O)420")] = 14.2,   // Red Mushroom: 24/26/35 island-halved, max ask 12
        [(Season.Summer, "(O)259")] = 24.7,   // Fiddlehead Fern: 21/30/23, max ask 20
        [(Season.Summer, "(O)404")] = 9.7,   // Common Mushroom: 16/23/19 island-halved, max ask 8 (exempt: wild seed)
        [(Season.Summer, "(O)281")] = 9.2,   // Chanterelle: 18/15/22 island-halved, max ask 8
        [(Season.Summer, "(O)372")] = 17.0,   // Clam: 13/21/17, max ask 14
        [(Season.Summer, "(O)397")] = 13.3,   // Sea Urchin: 14/16/10, max ask 11
        [(Season.Summer, "(O)394")] = 7.3,   // Rainbow Shell: 11/5/6, max ask 6
        [(Season.Summer, "(O)719")] = 6.7,   // Mussel: 2/7/11, max ask 6
        // ---- Fall ----
        [(Season.Fall, "(O)404")] = 28.9,   // Common Mushroom: 56/62/55 island-halved, max ask 24 (exempt: wild seed)
        [(Season.Fall, "(O)410")] = 57.3,   // Blackberry: 59/72/41, max ask 46
        [(Season.Fall, "(O)393")] = 54.7,   // Coral: 55/54/55, max ask 44
        [(Season.Fall, "(O)90")] = 40.7,   // Cactus Fruit: 38/43/41, max ask 33
        [(Season.Fall, "(O)88")] = 32.0,   // Coconut: 32/28/36, max ask 26
        [(Season.Fall, "(O)281")] = 13.0,   // Chanterelle: 23/27/28 island-halved, max ask 11
        [(Season.Fall, "(O)408")] = 22.3,   // Hazelnut: 24/20/23, max ask 18
        [(Season.Fall, "(O)420")] = 11.2,   // Red Mushroom: 22/22/23 island-halved, max ask 9
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
        [(Season.Winter, "(O)281")] = 9.0,   // Chanterelle: 15/16/23 island-halved, max ask 8
        [(Season.Winter, "(O)420")] = 7.9,   // Red Mushroom: 14/16/17 island-halved, max ask 7
        [(Season.Winter, "(O)397")] = 12.0,   // Sea Urchin: 14/11/11, max ask 10
        [(Season.Winter, "(O)392")] = 12.0,   // Nautilus Shell: 12/12/12, max ask 10
        [(Season.Winter, "(O)404")] = 5.2,   // Common Mushroom: 12/9/10 island-halved, max ask 5 (exempt: wild seed)
        [(Season.Winter, "(O)372")] = 10.0,   // Clam: 9/10/11, max ask 8
    };

    /// <summary>The largest quantity a bundle may ask for, or null when the item was not measured
    /// (or sits under <see cref="MinMeasuredAverage"/>), in which case the caller must not clamp.</summary>
    public static int? MaxAsk(Season season, string itemId)
    {
        if (IsWildSeedGrowable(itemId)) return null;   // farmable without limit
        int? ruled = RuledMaxAsk(itemId);
        if (ruled != null) return ruled;
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
        if (itemId == null || IsWildSeedGrowable(itemId)) return null;
        int? ruled = RuledMaxAsk(itemId);
        if (ruled != null) return ruled;
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
