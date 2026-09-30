using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Per-season weekly bases for items whose supply changes with the season a slot is due
/// (docs/superpowers/specs/2026-09-30-quantity-rules-design.md, sections 3 and 4). Order Spring,
/// Summer, Fall, Winter. <see cref="BasisByDeadline"/> takes the best season from Spring up to the
/// deadline, since anything bought, cooked or gathered earlier keeps. A 0 is a season the item cannot
/// exist in yet; it never contributes. Items left out on purpose (Apricot, Cherry, Mystic Syrup) stay
/// single.</summary>
public static class SeasonalAskBasis
{
    /// <summary>Hand rows for the Sticky bundle's shop goods and dishes (Jeff, 2026-09-30: "ice cream
    /// ... is hard in spring, trivial in summer, easy in later seasons"), plus gathered goods, pantry
    /// staples, Prize Tickets and the fish tools/fish-sim cannot simulate. Every dish also needs the
    /// kitchen (the first house upgrade), which is why Spring sits low across the board.</summary>
    public static readonly IReadOnlyDictionary<string, double[]> Rows = new Dictionary<string, double[]>(StringComparer.Ordinal)
    {
        // Pierre's, 100g, all year. Spring money is short: Normal asks 4 to 10, about 1,000g.
        ["(O)245"] = new double[] { 20, 40, 40, 40 },   // Sugar
        // First route is the Summer ice cream stand (250g); what you buy there keeps, so later
        // seasons read the same.
        ["(O)233"] = new double[] { 0, 25, 25, 25 },    // Ice Cream
        // Recipe from Sneak Peek (year-two episode) plus Maple Syrup (tapper, Foraging 4), Sugar, Flour.
        ["(O)731"] = new double[] { 0, 3, 3, 3 },       // Maple Bar
        // Gus's 7-heart recipe and a Fall crop; Winter cooks from a stocked cellar.
        ["(O)238"] = new double[] { 0, 0, 5, 8 },       // Cranberry Sauce
        // Mining 3 recipe; Cave Carrot, Sugar and Milk, so a cow first.
        ["(O)243"] = new double[] { 3, 6, 6, 6 },       // Miner's Treat

        // Tree seeds: 8 trees chopped a week (75% seed, 1-2 each) plus shaking about 30 trees (5%).
        ["(O)309"] = new double[] { 20, 20, 20, 20 },   // Acorn
        ["(O)310"] = new double[] { 20, 20, 17, 20 },   // Maple Seed: from Fall 14 a shake drops a Hazelnut
        ["(O)311"] = new double[] { 20, 20, 20, 20 },   // Pine Cone
        // Trees restart young each loop and moss needs growth stage 14; green rain (one Summer day) floods it.
        ["(O)Moss"] = new double[] { 5, 99, 45, 45 },
        // Fruit: 2 trees a species, one fruit a day in season, 28 days to mature.
        ["(O)635"] = new double[] { 0, 14, 14, 14 },    // Orange
        ["(O)636"] = new double[] { 0, 14, 14, 14 },    // Peach
        ["(O)613"] = new double[] { 0, 0, 14, 14 },     // Apple
        ["(O)637"] = new double[] { 0, 0, 14, 14 },     // Pomegranate
        // Spring: Caroline's sunroom tea bush (2 hearts), a leaf a day on days 22-28. Summer on: 10 own bushes.
        ["(O)815"] = new double[] { 7, 17, 17, 17 },    // Tea Leaves
        ["(O)78"] = new double[] { 15, 15, 15, 15 },    // Cave Carrot: mine barrels, about 30 a mine day, 4 days
        ["(O)399"] = new double[] { 35, 0, 0, 0 },      // Spring Onion: Spring forage, south-east Forest
        ["(O)296"] = new double[] { 80, 0, 0, 0 },      // Salmonberry: Spring 15-18, 20-25 bushes a day
        ["(O)178"] = new double[] { 60, 99, 99, 99 },   // Hay: Marnie 50g; Spring money-limited
        // Crab-pot junk (10 pots, about 26 a week) split five ways, plus fishing junk. Pots need Fishing 3.
        ["(O)168"] = new double[] { 6, 8, 8, 8 },       // Trash
        ["(O)169"] = new double[] { 6, 8, 8, 8 },       // Driftwood
        ["(O)170"] = new double[] { 6, 8, 8, 8 },       // Broken Glasses
        ["(O)171"] = new double[] { 6, 8, 8, 8 },       // Broken CD
        ["(O)172"] = new double[] { 6, 8, 8, 8 },       // Soggy Newspaper
        ["(O)246"] = new double[] { 20, 40, 40, 40 },   // Wheat Flour: Pierre 100g, like Sugar
        ["(O)247"] = new double[] { 10, 20, 20, 20 },   // Oil: Pierre 200g
        ["(O)419"] = new double[] { 10, 20, 20, 20 },   // Vinegar: Pierre 200g
        ["(O)423"] = new double[] { 10, 20, 20, 20 },   // Rice: Pierre 200g
        ["(O)PrizeTicket"] = new double[] { 2, 2, 3, 3 },  // every 3rd Help Wanted; town Special Orders from Fall
        // Fish tools/fish-sim cannot simulate. Fishing 10, bait, 2 catches a game hour, best 10 hours x 7.
        ["(O)Goby"] = new double[] { 21, 21, 21, 21 },  // Forest waterfall pool, 0.15 a cast, any season
        ["(O)158"] = new double[] { 10.5, 10.5, 10.5, 10.5 },  // Stonefish, floor 20, 0.075 a cast
        ["(O)161"] = new double[] { 9, 9, 9, 9 },       // Ice Pip, floor 60, 0.0645 a cast
        ["(O)162"] = new double[] { 7.6, 7.6, 7.6, 7.6 },  // Lava Eel, floor 100, 0.054 a cast
        ["(O)796"] = new double[] { 36.5, 36.5, 36.5, 36.5 },  // Slimejack, Mutant Bug Lair, 0.26 a cast
        // Night Market (Jeff, 2026-09-30: "only open for a few hours at night"): one 1,000g submarine ride a
        // night, about 4 game hours of fishing, 3 nights = 24 casts.
        ["(O)798"] = new double[] { 0, 0, 0, 5 },       // Midnight Squid, 0.207 a cast
        ["(O)799"] = new double[] { 0, 0, 0, 4 },       // Spook Fish, 0.162 a cast
        ["(O)800"] = new double[] { 0, 0, 0, 2 },       // Blobfish, 0.10 a cast
    };

    /// <summary>The best season's basis from Spring up to the deadline (Winter when there is none), or
    /// null when the item has no row or every season so far is 0.</summary>
    public static double? BasisByDeadline(string itemId, Season? deadline)
    {
        if (!Rows.TryGetValue(itemId, out double[]? bySeason))
            return null;
        Season last = deadline ?? Season.Winter;
        double? best = null;
        for (Season s = Season.Spring; s <= last; s++)
        {
            double basis = bySeason[(int)s];
            if (basis > 0 && (best == null || basis > best)) best = basis;
        }
        return best;
    }
}
