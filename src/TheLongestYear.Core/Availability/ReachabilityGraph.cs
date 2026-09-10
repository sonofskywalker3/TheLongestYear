using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Which places a run cannot get to, decided by walking doors rather than matching
/// map names.
///
/// The marker list (BundleGenerationTuning.ExcludedLocationMarkers) names the forbidden places
/// directly. This walk adds everything that can only be reached THROUGH one of them: start at the
/// farm, refuse to step into a forbidden location, and see what is left over.
///
/// Why names are not enough (2026-09-10, Nexus posts, pitytheviolins): The Fishmonger puts its
/// shop in a custom map called "VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside" whose only door
/// leads to IslandSouth. No substring of that name matches "Island", and the owning NPC's
/// HomeRegion is set to "Town". A map's name is a label its author picks freely. Its warps are how
/// players actually reach it.
///
/// Gates that open DURING the year are deliberately treated as passable: the bus to the Desert,
/// the Rusty Key to the Sewer, the Steel Axe to the Secret Woods. This walk answers "is this place
/// connected to the world other than through a forbidden one", not "can the player stand there on
/// Spring 1". Without that, a fresh board would call the Desert unreachable and strip Cactus Fruit,
/// contradicting BundleCatalogBuilder's ruling that such items are valid targets a player invests
/// in. Timing is LocationGating's job, and this rule must not duplicate it.</summary>
public static class ReachabilityGraph
{
    /// <summary>Where a run always begins.</summary>
    public const string FarmLocation = "Farm";

    /// <summary>Locations the walk cannot reach from <paramref name="start"/>, including the
    /// forbidden ones themselves.
    ///
    /// Fails open on purpose. An unknown start, or a start that is itself forbidden, returns the
    /// empty set rather than condemning the whole world: over-exclusion silently strips real
    /// content, which is the one failure mode a player would never understand.</summary>
    public static IReadOnlySet<string> UnreachableLocations(
        IReadOnlyList<RawLocationLink> links,
        IReadOnlyCollection<string> allLocations,
        Func<string, bool> isForbidden,
        string start = FarmLocation)
    {
        var unreachable = new HashSet<string>(StringComparer.Ordinal);
        if (links == null || allLocations == null || isForbidden == null) return unreachable;
        if (string.IsNullOrEmpty(start)) return unreachable;

        var known = new HashSet<string>(allLocations, StringComparer.Ordinal);
        if (!known.Contains(start) || isForbidden(start)) return unreachable;

        var neighbours = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Link(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (!neighbours.TryGetValue(from, out List<string>? list))
                neighbours[from] = list = new List<string>();
            if (!list.Contains(to, StringComparer.Ordinal)) list.Add(to);
        }
        foreach (RawLocationLink link in links)
        {
            if (link == null) continue;
            Link(link.From, link.To);
            Link(link.To, link.From);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string here = queue.Dequeue();
            if (!neighbours.TryGetValue(here, out List<string>? next)) continue;
            foreach (string there in next)
            {
                if (isForbidden(there)) continue;   // never step through a forbidden place
                if (!visited.Add(there)) continue;
                queue.Enqueue(there);
            }
        }

        foreach (string name in known)
        {
            if (visited.Contains(name)) continue;
            // A location with no doors at all was never proved unreachable, only never
            // explained. Verified in-game 2026-09-10: MovieTheater, WizardHouseBasement and
            // LewisBasement all load with zero warps because they are entered by scripted
            // actions. Condemning them would be inventing proof we do not have.
            if (!neighbours.ContainsKey(name) && !isForbidden(name)) continue;
            unreachable.Add(name);
        }
        return unreachable;
    }
}
