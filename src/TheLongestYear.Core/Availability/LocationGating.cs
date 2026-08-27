using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>The season floor a location imposes because the world locks it behind progress.
///
/// A fish's spawn seasons in Data/Locations say when it bites, not when the player can stand
/// next to that water. Without this, a Sandfish would read as Spring-available because the
/// Desert lists it in every season, and a Spring deadline on a Sandfish is unsatisfiable.
///
/// Values are judgements about a first-year run on this mod's 500g start, not vanilla speedrun
/// records. They lean LATE on purpose: BundleDeadlines clamps a deadline upward to the floor, so
/// too early permits an impossible gate while too late is merely lenient.</summary>
public static class LocationGating
{
    /// <summary>Matched as case-sensitive substrings of the location key, so "Desert",
    /// "SkullCave" and "IslandSouth" all catch their family of map keys.</summary>
    private static readonly (string Marker, Season Floor)[] GatedMarkers =
    {
        // Bus repair costs 40,000g through the Vault bundle. Not a Spring or Summer thing on a
        // 500g start with the board also demanding donations.
        ("Desert",     Season.Fall),
        ("SkullCave",  Season.Fall),
        // Rusty Key: 60 museum donations. Reachable mid-run by a player who digs, not before.
        ("Sewer",      Season.Summer),
        ("BugLand",    Season.Summer),
        // Witch's Swamp needs the Dark Talisman, which needs the Sewer first, then the Mutant
        // Bug Lair quest. Last stop of a long chain.
        ("WitchSwamp", Season.Winter),
        ("WitchHut",   Season.Winter),
    };

    public static Season FloorFor(string locationKey)
    {
        if (string.IsNullOrEmpty(locationKey))
            return Season.Spring;

        foreach ((string marker, Season floor) in GatedMarkers)
            if (locationKey.Contains(marker, StringComparison.Ordinal))
                return floor;

        return Season.Spring;
    }

    /// <summary>The EASIEST floor among the given locations, because reaching any one of them is
    /// enough to get the item. An empty list means no location signal, which reads as ungated.</summary>
    public static Season FloorForAny(IReadOnlyList<string> locationKeys)
    {
        if (locationKeys == null || locationKeys.Count == 0)
            return Season.Spring;

        Season best = Season.Winter;
        foreach (string key in locationKeys)
        {
            Season floor = FloorFor(key);
            if (floor < best) best = floor;
        }
        return best;
    }
}
