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
    /// "SkullCave" and "IslandSouth" all catch their family of map keys. Hard is the first week
    /// the location can exist at all (facts); Week is the pacing week (a judgement call).</summary>
    private static readonly (string Marker, int Week, int Hard)[] GatedMarkers =
    {
        // Bus repair costs 40,000g through the Vault bundle. Fall pacing, Summer hard (Jeff,
        // 2026-08-28: a Spring bus is possible but not fun; Hard may ask from Summer week 2).
        ("Desert",    AvailabilityWeeks.SkullCavernWeek, AvailabilityWeeks.DesertHardWeek),
        ("SkullCave", AvailabilityWeeks.SkullCavernWeek, AvailabilityWeeks.DesertHardWeek),
        // MountainUnlock clears the landslide on day 1; depth is handled per mine area
        // (AvailabilityWeeks.MineAreaWeek: 30 floors a week).
        ("UndergroundMine", 1, 1),
        // Rusty Key: 60 museum donations. Reachable mid-run by a player who digs, not before.
        ("Sewer",     AvailabilityWeeks.SewerWeek, AvailabilityWeeks.SewerWeek),
        ("BugLand",   AvailabilityWeeks.SewerWeek, AvailabilityWeeks.SewerWeek),
        // Witch's Swamp needs the Dark Talisman, which needs the Sewer first, then the Mutant
        // Bug Lair quest. Last stop of a long chain.
        ("WitchSwamp", AvailabilityWeeks.SwampWeek, AvailabilityWeeks.SwampWeek),
        ("WitchHut",   AvailabilityWeeks.SwampWeek, AvailabilityWeeks.SwampWeek),
        // Secret Woods needs the Steel Axe (Morel, Fiddlehead, Woodskip, hardwood stumps).
        ("Woods", 4, 4),
    };

    /// <summary>First week of the year the player can stand in this location.</summary>
    public static int WeekFor(string locationKey)
    {
        if (string.IsNullOrEmpty(locationKey))
            return 1;
        foreach ((string marker, int week, int _) in GatedMarkers)
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

    /// <summary>First week the location can exist at all (facts, not pacing): a Hard-mode ask
    /// may demand it this early.</summary>
    public static int HardWeekFor(string locationKey)
    {
        if (string.IsNullOrEmpty(locationKey)) return 1;
        foreach ((string marker, int _, int hard) in GatedMarkers)
            if (locationKey.Contains(marker, StringComparison.Ordinal)) return hard;
        return 1;
    }

    /// <summary>The EASIEST hard week among the given locations, because reaching any one of
    /// them is enough to get the item.</summary>
    public static int HardWeekForAny(IReadOnlyList<string> locationKeys)
    {
        if (locationKeys == null || locationKeys.Count == 0) return 1;
        int best = Calendar.WeeksPerYear;
        foreach (string key in locationKeys) best = Math.Min(best, HardWeekFor(key));
        return best;
    }

    public static Season FloorFor(string locationKey) => AvailabilityWeeks.SeasonOf(WeekFor(locationKey));

    public static Season FloorForAny(IReadOnlyList<string> locationKeys)
        => AvailabilityWeeks.SeasonOf(WeekForAny(locationKeys));
}
