using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>How many of a fish a bundle may build an ask on, per season: what a level-10 player
/// with plain bait lands on the best ten-hour day for that fish, times seven days. Jeff's ruling,
/// 2026-09-04.
///
/// The numbers are MODELLED, not measured in the running game: the game's own fish pick
/// (GameLocation.GetFishFromLocationData) replayed in Python over the real Data/Locations and
/// Data/Fish tables, at two catches per game hour from the rod's timing constants. Method,
/// caveats and the full table: docs/superpowers/notes/fish-catch-rates-2026-09-04.md; generator:
/// tools/fish-sim/gen_cs.py. Regenerate this table from there, never edit a number by hand.
///
/// A rain-only fish (Walleye, Catfish, Eel...) takes the larger of the rain-weighted week and a
/// rainy-day catch times the season's expected rainy days, because a player after it waits for
/// rain. A fish landed less than twice a week has no row and stays a single ask, as do the five
/// legendaries (LegendaryFishRules) and the mine-floor fish MineShaft.getFish hard-codes outside
/// the data tables.</summary>
public static class FishAskBasis
{
    private static readonly IReadOnlyDictionary<(Season Season, string ItemId), double> Table =
        new Dictionary<(Season, string), double>
        {
            [(Season.Fall, "(O)705")] = 25.9,   // Albacore: 3.7/day x 7
            [(Season.Winter, "(O)705")] = 17.5,   // Albacore: 2.5/day x 7
            [(Season.Spring, "(O)129")] = 22.8,   // Anchovy: 3.3/day x 7
            [(Season.Fall, "(O)129")] = 27.7,   // Anchovy: 4.0/day x 7
            [(Season.Spring, "(O)132")] = 36.0,   // Bream: 5.1/day x 7
            [(Season.Summer, "(O)132")] = 34.5,   // Bream: 4.9/day x 7
            [(Season.Fall, "(O)132")] = 33.8,   // Bream: 4.8/day x 7
            [(Season.Winter, "(O)132")] = 24.6,   // Bream: 3.5/day x 7
            [(Season.Spring, "(O)700")] = 30.3,   // Bullhead: 4.3/day x 7
            [(Season.Summer, "(O)700")] = 27.8,   // Bullhead: 4.0/day x 7
            [(Season.Fall, "(O)700")] = 28.9,   // Bullhead: 4.1/day x 7
            [(Season.Winter, "(O)700")] = 24.6,   // Bullhead: 3.5/day x 7
            [(Season.Spring, "(O)142")] = 63.4,   // Carp: 9.1/day x 7
            [(Season.Summer, "(O)142")] = 63.0,   // Carp: 9.0/day x 7
            [(Season.Fall, "(O)142")] = 62.8,   // Carp: 9.0/day x 7
            [(Season.Winter, "(O)142")] = 64.6,   // Carp: 9.2/day x 7
            [(Season.Spring, "(O)143")] = 32.0,   // Catfish: 1.2/day x 7, rainy 32.0
            [(Season.Summer, "(O)143")] = 23.7,   // Catfish: 0.9/day x 7, rainy 23.7
            [(Season.Fall, "(O)143")] = 30.4,   // Catfish: 1.1/day x 7, rainy 30.4
            [(Season.Spring, "(O)CaveJelly")] = 6.9,   // Cave Jelly: 1.0/day x 7
            [(Season.Summer, "(O)CaveJelly")] = 6.7,   // Cave Jelly: 1.0/day x 7
            [(Season.Fall, "(O)CaveJelly")] = 6.9,   // Cave Jelly: 1.0/day x 7
            [(Season.Winter, "(O)CaveJelly")] = 6.8,   // Cave Jelly: 1.0/day x 7
            [(Season.Spring, "(O)702")] = 45.5,   // Chub: 6.5/day x 7
            [(Season.Summer, "(O)702")] = 35.8,   // Chub: 5.1/day x 7
            [(Season.Fall, "(O)702")] = 42.3,   // Chub: 6.0/day x 7
            [(Season.Winter, "(O)702")] = 30.5,   // Chub: 4.4/day x 7
            [(Season.Summer, "(O)704")] = 14.7,   // Dorado: 2.1/day x 7
            [(Season.Spring, "(O)148")] = 23.0,   // Eel: 0.8/day x 7, rainy 23.0
            [(Season.Fall, "(O)148")] = 27.3,   // Eel: 1.0/day x 7, rainy 27.3
            [(Season.Spring, "(O)267")] = 14.4,   // Flounder: 2.1/day x 7
            [(Season.Summer, "(O)267")] = 18.0,   // Flounder: 2.6/day x 7
            [(Season.Spring, "(O)156")] = 40.2,   // Ghostfish: 5.7/day x 7
            [(Season.Summer, "(O)156")] = 40.2,   // Ghostfish: 5.7/day x 7
            [(Season.Fall, "(O)156")] = 39.6,   // Ghostfish: 5.7/day x 7
            [(Season.Winter, "(O)156")] = 40.7,   // Ghostfish: 5.8/day x 7
            [(Season.Spring, "(O)153")] = 67.5,   // Green Algae: 9.6/day x 7
            [(Season.Summer, "(O)153")] = 59.1,   // Green Algae: 8.4/day x 7
            [(Season.Fall, "(O)153")] = 67.1,   // Green Algae: 9.6/day x 7
            [(Season.Winter, "(O)153")] = 55.0,   // Green Algae: 7.9/day x 7
            [(Season.Spring, "(O)708")] = 24.6,   // Halibut: 3.5/day x 7
            [(Season.Summer, "(O)708")] = 36.2,   // Halibut: 5.2/day x 7
            [(Season.Winter, "(O)708")] = 20.8,   // Halibut: 3.0/day x 7
            [(Season.Spring, "(O)147")] = 35.4,   // Herring: 5.1/day x 7
            [(Season.Winter, "(O)147")] = 29.9,   // Herring: 4.3/day x 7
            [(Season.Spring, "(O)136")] = 29.1,   // Largemouth Bass: 4.2/day x 7
            [(Season.Summer, "(O)136")] = 21.8,   // Largemouth Bass: 3.1/day x 7
            [(Season.Fall, "(O)136")] = 28.7,   // Largemouth Bass: 4.1/day x 7
            [(Season.Winter, "(O)136")] = 21.9,   // Largemouth Bass: 3.1/day x 7
            [(Season.Winter, "(O)707")] = 25.0,   // Lingcod: 3.6/day x 7
            [(Season.Fall, "(O)269")] = 14.3,   // Midnight Carp: 2.0/day x 7
            [(Season.Winter, "(O)269")] = 12.0,   // Midnight Carp: 1.7/day x 7
            [(Season.Summer, "(O)149")] = 8.4,   // Octopus: 1.2/day x 7
            [(Season.Winter, "(O)141")] = 49.7,   // Perch: 7.1/day x 7
            [(Season.Summer, "(O)144")] = 63.3,   // Pike: 9.0/day x 7
            [(Season.Winter, "(O)144")] = 45.0,   // Pike: 6.4/day x 7
            [(Season.Summer, "(O)128")] = 8.9,   // Pufferfish: 1.3/day x 7
            [(Season.Summer, "(O)138")] = 26.9,   // Rainbow Trout: 3.8/day x 7
            [(Season.Summer, "(O)146")] = 31.8,   // Red Mullet: 4.5/day x 7
            [(Season.Winter, "(O)146")] = 22.0,   // Red Mullet: 3.1/day x 7
            [(Season.Summer, "(O)150")] = 16.7,   // Red Snapper: 0.6/day x 7, rainy 16.7
            [(Season.Fall, "(O)150")] = 17.9,   // Red Snapper: 0.6/day x 7, rainy 17.9
            [(Season.Spring, "(O)RiverJelly")] = 14.8,   // River Jelly: 2.1/day x 7
            [(Season.Summer, "(O)RiverJelly")] = 14.7,   // River Jelly: 2.1/day x 7
            [(Season.Fall, "(O)RiverJelly")] = 14.7,   // River Jelly: 2.1/day x 7
            [(Season.Winter, "(O)RiverJelly")] = 15.0,   // River Jelly: 2.1/day x 7
            [(Season.Fall, "(O)139")] = 36.0,   // Salmon: 5.1/day x 7
            [(Season.Spring, "(O)164")] = 73.0,   // Sandfish: 10.4/day x 7
            [(Season.Summer, "(O)164")] = 72.2,   // Sandfish: 10.3/day x 7
            [(Season.Fall, "(O)164")] = 72.5,   // Sandfish: 10.4/day x 7
            [(Season.Winter, "(O)164")] = 71.7,   // Sandfish: 10.2/day x 7
            [(Season.Spring, "(O)131")] = 42.5,   // Sardine: 6.1/day x 7
            [(Season.Fall, "(O)131")] = 44.4,   // Sardine: 6.3/day x 7
            [(Season.Winter, "(O)131")] = 35.1,   // Sardine: 5.0/day x 7
            [(Season.Spring, "(O)165")] = 21.3,   // Scorpion Carp: 3.0/day x 7
            [(Season.Summer, "(O)165")] = 22.4,   // Scorpion Carp: 3.2/day x 7
            [(Season.Fall, "(O)165")] = 22.3,   // Scorpion Carp: 3.2/day x 7
            [(Season.Winter, "(O)165")] = 22.2,   // Scorpion Carp: 3.2/day x 7
            [(Season.Fall, "(O)154")] = 20.6,   // Sea Cucumber: 2.9/day x 7
            [(Season.Winter, "(O)154")] = 15.6,   // Sea Cucumber: 2.2/day x 7
            [(Season.Spring, "(O)SeaJelly")] = 4.4,   // Sea Jelly: 0.6/day x 7
            [(Season.Summer, "(O)SeaJelly")] = 6.1,   // Sea Jelly: 0.9/day x 7
            [(Season.Fall, "(O)SeaJelly")] = 5.2,   // Sea Jelly: 0.7/day x 7
            [(Season.Winter, "(O)SeaJelly")] = 4.2,   // Sea Jelly: 0.6/day x 7
            [(Season.Spring, "(O)152")] = 25.8,   // Seaweed: 3.7/day x 7
            [(Season.Summer, "(O)152")] = 37.4,   // Seaweed: 5.3/day x 7
            [(Season.Fall, "(O)152")] = 30.9,   // Seaweed: 4.4/day x 7
            [(Season.Winter, "(O)152")] = 21.8,   // Seaweed: 3.1/day x 7
            [(Season.Spring, "(O)706")] = 22.9,   // Shad: 0.8/day x 7, rainy 22.9
            [(Season.Summer, "(O)706")] = 23.2,   // Shad: 0.9/day x 7, rainy 23.2
            [(Season.Fall, "(O)706")] = 15.8,   // Shad: 0.6/day x 7, rainy 15.8
            [(Season.Spring, "(O)137")] = 65.8,   // Smallmouth Bass: 9.4/day x 7
            [(Season.Fall, "(O)137")] = 64.2,   // Smallmouth Bass: 9.2/day x 7
            [(Season.Winter, "(O)151")] = 19.5,   // Squid: 2.8/day x 7
            [(Season.Summer, "(O)698")] = 20.1,   // Sturgeon: 2.9/day x 7
            [(Season.Winter, "(O)698")] = 20.5,   // Sturgeon: 2.9/day x 7
            [(Season.Spring, "(O)145")] = 39.4,   // Sunfish: 5.6/day x 7
            [(Season.Summer, "(O)145")] = 32.1,   // Sunfish: 4.6/day x 7
            [(Season.Summer, "(O)155")] = 17.9,   // Super Cucumber: 2.6/day x 7
            [(Season.Fall, "(O)155")] = 14.1,   // Super Cucumber: 2.0/day x 7
            [(Season.Fall, "(O)699")] = 23.5,   // Tiger Trout: 3.4/day x 7
            [(Season.Winter, "(O)699")] = 20.3,   // Tiger Trout: 2.9/day x 7
            [(Season.Summer, "(O)701")] = 20.8,   // Tilapia: 3.0/day x 7
            [(Season.Fall, "(O)701")] = 19.9,   // Tilapia: 2.8/day x 7
            [(Season.Summer, "(O)130")] = 16.9,   // Tuna: 2.4/day x 7
            [(Season.Winter, "(O)130")] = 12.5,   // Tuna: 1.8/day x 7
            [(Season.Fall, "(O)140")] = 43.3,   // Walleye: 1.6/day x 7, rainy 43.3
            [(Season.Spring, "(O)157")] = 41.0,   // White Algae: 5.9/day x 7
            [(Season.Summer, "(O)157")] = 40.3,   // White Algae: 5.8/day x 7
            [(Season.Fall, "(O)157")] = 41.1,   // White Algae: 5.9/day x 7
            [(Season.Winter, "(O)157")] = 41.4,   // White Algae: 5.9/day x 7
            [(Season.Spring, "(O)734")] = 32.2,   // Woodskip: 4.6/day x 7
            [(Season.Summer, "(O)734")] = 32.3,   // Woodskip: 4.6/day x 7
            [(Season.Fall, "(O)734")] = 32.5,   // Woodskip: 4.6/day x 7
            [(Season.Winter, "(O)734")] = 34.4,   // Woodskip: 4.9/day x 7
        };

    private static readonly HashSet<string> Covered = BuildCovered();

    private static HashSet<string> BuildCovered()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<(Season Season, string ItemId), double> row in Table) set.Add(row.Key.ItemId);
        return set;
    }

    /// <summary>True when the fish has a basis in at least one season, so its ask is banded and
    /// the flat stack multiplier must leave it alone.</summary>
    public static bool Covers(string? itemId)
        => itemId != null && Covered.Contains(BundleParsing.NormalizeItemId(itemId));

    /// <summary>The week's catch for this fish in this season, or null when it is not catchable
    /// then (or too rare to build a stack on).</summary>
    public static double? Basis(Season season, string itemId)
        => itemId != null && Table.TryGetValue((season, BundleParsing.NormalizeItemId(itemId)), out double basis) ? basis : null;

    /// <summary>The most generous basis among the seasons a bundle due in
    /// <paramref name="deadline"/> can reach (Spring up to and including the deadline), or the
    /// whole year when there is no per-item deadline. Null when no reachable season has one.</summary>
    public static double? BasisByDeadline(string itemId, Season? deadline)
    {
        double? best = null;
        Season last = deadline ?? Season.Winter;
        for (Season s = Season.Spring; s <= last; s++)
        {
            double? b = Basis(s, itemId);
            if (b != null && (best == null || b > best)) best = b;
        }
        return best;
    }
}
