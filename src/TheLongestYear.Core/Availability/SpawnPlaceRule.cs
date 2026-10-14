using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Whether a Data/Locations spawn row (fish or forage) sits on a map a run can walk to.
///
/// The location markers only drop maps whose NAME says "Island", "BugLand"... A mod map entered
/// only from Ginger Island has a name of its own, so its rows used to go straight into the pools
/// and count as proof that the item is reachable (Nexus bug report 2026-10-12, iroeny, 0.19.21:
/// Weatherman's asked for a Lionfish). Forager's Nooks and Crannies adds
/// "Wildflour.NooksCrannies_RainforestRetreat", whose only way in is a warp on Island_E and whose
/// exits all lead back to IslandEast; its rows list Lionfish, Super Cucumber, Tuna and the island
/// crops. Read by doors, that map is island content.
///
/// A map is out of reach only when BOTH walks agree, so nothing legit is dropped:
/// <list type="bullet">
/// <item><see cref="ReachabilityGraph"/> (warps both ways, never through a forbidden map) cannot
/// reach it. That walk already keeps every map with no warps at all (scripted interiors, farm
/// types, maps entered by a mod's own warp code), and a map whose exits lead to the mainland.</item>
/// <item><see cref="LocationWeeks"/> (warps and door actions, one way from the farm) never
/// reached it either, and it carries no known gate of its own (the Desert by bus, the Sewer by
/// ladder: <see cref="LocationGating"/>).</item>
/// </list>
/// Either read missing (null) means nothing is out of reach: fail open, as before.</summary>
public sealed class SpawnPlaceRule
{
    /// <summary>The rule when the walks could not be read: every row stays.</summary>
    public static readonly SpawnPlaceRule Open = new(null, null);

    private readonly IReadOnlySet<string>? _unreachablePlaces;
    private readonly LocationWeeks? _weeks;

    /// <param name="unreachablePlaces"><see cref="ReachabilityGraph.UnreachableLocations"/>.</param>
    /// <param name="weeks"><see cref="LocationWeeks.Build"/> over the same world.</param>
    public SpawnPlaceRule(IReadOnlySet<string>? unreachablePlaces, LocationWeeks? weeks)
    {
        _unreachablePlaces = unreachablePlaces;
        _weeks = weeks;
    }

    /// <summary>True when no walk from the farm reaches this Data/Locations key or map name.</summary>
    public bool IsOutOfReach(string? place)
    {
        if (string.IsNullOrEmpty(place) || _unreachablePlaces == null || _weeks == null) return false;
        return _unreachablePlaces.Contains(place) && !_weeks.TryGet(place, out _);
    }

    /// <summary>Splits spawn rows into the ones on a reachable map and the ones that are not.</summary>
    public SpawnRowSplit Split(IEnumerable<RawSpawnEntry>? rows)
    {
        var kept = new List<RawSpawnEntry>();
        var dropped = new List<RawSpawnEntry>();
        foreach (RawSpawnEntry row in rows ?? Array.Empty<RawSpawnEntry>())
        {
            if (row == null) continue;
            (IsOutOfReach(row.Location) ? dropped : kept).Add(row);
        }
        return new SpawnRowSplit(kept, dropped);
    }
}

/// <summary>Spawn rows on a reachable map (<paramref name="Kept"/>: they feed the pools and prove an
/// item reachable) and on an unreachable one (<paramref name="Dropped"/>: a fish row there is a known
/// but closed route, like a fish row on Ginger Island itself).</summary>
public sealed record SpawnRowSplit(IReadOnlyList<RawSpawnEntry> Kept, IReadOnlyList<RawSpawnEntry> Dropped)
{
    /// <summary>The qualified ids the dropped rows offer.</summary>
    public IEnumerable<string> DroppedItemIds()
    {
        foreach (RawSpawnEntry row in Dropped)
            if (!string.IsNullOrEmpty(row.ItemId))
                yield return BundleParsing.NormalizeItemId(row.ItemId);
    }
}
