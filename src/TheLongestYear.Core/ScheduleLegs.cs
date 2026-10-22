using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The parts of a villager's day that cross one map, as start and end tiles, worked out the way
/// <c>NPC.pathfindToNextScheduleLocation</c> builds a schedule route (decompile, NPC.cs 5194): the
/// day runs stop to stop from the villager's home tile, each stop's maps come from the warp route
/// cache, a leg on a map starts where the previous map's warp lands (or at the last stop, on the
/// first map) and ends at the warp to the next map (or at the stop itself, on the last map).
///
/// The rewind pan plays these backwards so its extras walk the town the way they really do (Jeff,
/// 2026-09-16: "can you not just use their normal paths they're assigned by the game?"). Reversed,
/// every leg ends where the villager came onto the map: a door or the map's edge.
/// </summary>
public static class ScheduleLegs
{
    public readonly record struct Stop(string Location, (int X, int Y) Tile);

    /// <param name="EntersMap">True when <paramref name="From"/> is where the villager came onto the
    /// map through a warp (a door or the map's edge), false when it is a stop on the map itself or a
    /// home tile there.</param>
    public readonly record struct Leg((int X, int Y) From, (int X, int Y) To, bool EntersMap);

    /// <param name="map">The map whose legs are wanted.</param>
    /// <param name="home">Where the day starts: the villager's default map and tile.</param>
    /// <param name="stops">The schedule's stops in time order.</param>
    /// <param name="route">The maps walked from one location to another, or null when there is no route.</param>
    /// <param name="warpTo">The warp tile on a map that leads to the next map, or null when there is none.</param>
    /// <param name="warpTarget">Where a warp tile on a map lands on the next map.</param>
    public static List<Leg> In(
        string map, string home, (int X, int Y) homeTile, IEnumerable<Stop> stops,
        Func<string, string, string[]?> route,
        Func<string, string, (int X, int Y)?> warpTo,
        Func<string, (int X, int Y), (int X, int Y)> warpTarget)
    {
        var legs = new List<Leg>();
        string fromLocation = home;
        (int X, int Y) fromTile = homeTile;
        foreach (Stop stop in stops)
        {
            string[]? maps = fromLocation == stop.Location
                ? new[] { fromLocation }
                : route(fromLocation, stop.Location);
            if (maps == null || maps.Length == 0)
                continue;

            var found = new List<Leg>();
            (int X, int Y) start = fromTile;
            bool entered = false;
            bool walkable = true;
            for (int i = 0; i < maps.Length; i++)
            {
                bool last = i == maps.Length - 1;
                (int X, int Y) end;
                if (last)
                {
                    end = stop.Tile;
                }
                else
                {
                    (int X, int Y)? exit = warpTo(maps[i], maps[i + 1]);
                    if (exit == null) { walkable = false; break; }
                    end = exit.Value;
                }

                if (maps[i] == map && start != end)
                    found.Add(new Leg(start, end, entered));
                if (!last)
                {
                    start = warpTarget(maps[i], end);
                    entered = true;
                }
            }

            if (!walkable)
                continue;
            legs.AddRange(found);
            fromLocation = stop.Location;
            fromTile = stop.Tile;
        }
        return legs;
    }

    /// <summary>The front of a forward route, cut so a backwards walk of it starts a little past the
    /// point where it comes closest to the camera's line, or null when it never comes within
    /// <paramref name="maxDistance"/> tiles of that line. Kept from index 0 because that end is
    /// where the villager came onto the map, the door or edge a backwards walk vanishes into.</summary>
    /// <param name="lead">Tiles kept past the closest point, so the walker is already moving when
    /// the camera arrives.</param>
    public static List<(int X, int Y)>? ForCamera(
        IReadOnlyList<(int X, int Y)> route, (double X, double Y) from, (double X, double Y) to,
        double maxDistance, int lead, int minTiles)
    {
        if (route == null || route.Count == 0) return null;
        int closest = 0;
        double best = double.MaxValue;
        for (int i = 0; i < route.Count; i++)
        {
            double d = DistanceToSegment(route[i], from, to);
            if (d < best) { best = d; closest = i; }
        }
        if (best > maxDistance) return null;

        int count = Math.Min(route.Count, closest + lead + 1);
        if (count < minTiles) return null;
        var kept = new List<(int X, int Y)>(count);
        for (int i = 0; i < count; i++) kept.Add(route[i]);
        return kept;
    }

    private static double DistanceToSegment((int X, int Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lengthSquared = dx * dx + dy * dy;
        double t = lengthSquared <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        double cx = a.X + t * dx - p.X, cy = a.Y + t * dy - p.Y;
        return Math.Sqrt(cx * cx + cy * cy);
    }
}
