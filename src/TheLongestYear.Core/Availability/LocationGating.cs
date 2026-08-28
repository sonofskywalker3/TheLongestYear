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
    private static readonly (string Marker, int Week)[] GatedMarkers =
    {
        // Bus repair costs 40,000g through the Vault bundle. Fall (Jeff, 2026-08-28: Skull
        // Cavern in the Fall gate and beyond).
        ("Desert",    AvailabilityWeeks.SkullCavernWeek),
        ("SkullCave", AvailabilityWeeks.SkullCavernWeek),
        // MountainUnlock clears the landslide on day 1; depth is handled per mine area
        // (AvailabilityWeeks.MineAreaWeek: 30 floors a week).
        ("UndergroundMine", 1),
        // Rusty Key: 60 museum donations. Reachable mid-run by a player who digs, not before.
        ("Sewer",     AvailabilityWeeks.SewerWeek),
        ("BugLand",   AvailabilityWeeks.SewerWeek),
        // Witch's Swamp needs the Dark Talisman, which needs the Sewer first, then the Mutant
        // Bug Lair quest. Last stop of a long chain.
        ("WitchSwamp", AvailabilityWeeks.SwampWeek),
        ("WitchHut",   AvailabilityWeeks.SwampWeek),
    };

    /// <summary>First week of the year the player can stand in this location.</summary>
    public static int WeekFor(string locationKey)
    {
        if (string.IsNullOrEmpty(locationKey))
            return 1;
        foreach ((string marker, int week) in GatedMarkers)
            if (locationKey.Contains(marker, StringComparison.Ordinal))
                return week;
        return 1;
    }

    /// <summary>The EASIEST week among the given locations, because reaching any one of them is
    /// enough to get the item. An empty list means no location signal, which reads as ungated.</summary>
    public static int WeekForAny(IReadOnlyList<string> locationKeys)
    {
        if (locationKeys == null || locationKeys.Count == 0)
            return 1;
        int best = Calendar.WeeksPerYear;
        foreach (string key in locationKeys)
            best = Math.Min(best, WeekFor(key));
        return best;
    }

    public static Season FloorFor(string locationKey) => AvailabilityWeeks.SeasonOf(WeekFor(locationKey));

    public static Season FloorForAny(IReadOnlyList<string> locationKeys)
        => AvailabilityWeeks.SeasonOf(WeekForAny(locationKeys));
}
