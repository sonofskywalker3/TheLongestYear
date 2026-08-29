using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for an artifact: the reach effort of the location whose dig
/// spots can yield it (Data/Locations ArtifactSpots, decompile GameLocation.cs:14062) plus a
/// rarity step from the spot chance, minimum over every location. This is what makes a Dinosaur
/// Egg (Mountain spots at 0.005) harder than a Diamond whatever the two sell for. The week is
/// week 1 (artifact spots and the museum exist on day 1) unless every spot is behind a gated
/// location (Desert week 9).</summary>
public static class ArtifactAvailability
{
    private const int TownReach = 1;
    private const double CommonChance = 0.1;
    private const double UncommonChance = 0.02;
    private const double RareChance = 0.005;

    /// <summary>Ordinal substring markers, first hit wins. Everything else (Farm, Town, Forest,
    /// Beach, Mountain, Bus Stop, Backwoods, the shared Default list) is a walk from the farm.</summary>
    private static readonly (string Marker, int Effort)[] ReachMarkers =
    {
        ("SkullCave", 7), ("Island", 7), ("Desert", 4), ("UndergroundMine", 2), ("Mine", 2),
    };

    public static int ReachEffort(string location)
    {
        if (string.IsNullOrEmpty(location)) return TownReach;
        foreach ((string marker, int effort) in ReachMarkers)
            if (location.Contains(marker, StringComparison.Ordinal))
                return effort;
        return TownReach;
    }

    public static int ChanceStep(double chance)
        => chance >= CommonChance ? 0 : chance >= UncommonChance ? 1 : chance >= RareChance ? 2 : 3;

    public static ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawArtifactSpot> spots)
    {
        if (spots == null) throw new ArgumentNullException(nameof(spots));
        ItemEffort? best = null;
        foreach (RawArtifactSpot spot in spots)
        {
            if (spot.ItemId != qualifiedId) continue;
            int step = ChanceStep(spot.Chance);
            int effort = ReachEffort(spot.Location) + step;
            int week = Math.Max(AvailabilityWeeks.ArtifactWeek, LocationGating.WeekFor(spot.Location));
            bool better = best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"artifact spot, {spot.Location} at {spot.Chance:0.####} (+{step}), week {week}, effort {effort}",
                    week, AvailabilityWeeks.SeasonOf(week),
                    HardWeek: Math.Max(AvailabilityWeeks.ArtifactWeek, LocationGating.HardWeekFor(spot.Location)));
        }
        return best;
    }
}
