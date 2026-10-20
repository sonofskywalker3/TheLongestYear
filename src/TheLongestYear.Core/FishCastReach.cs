using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>A location's tiles as the fishing rod sees them. Off-map tiles are never asked for.</summary>
public interface IFishingMap
{
    int Width { get; }
    int Height { get; }
    /// <summary>The Back layer marks the tile as water (GameLocation.isWaterTile).</summary>
    bool IsWater(int x, int y);
    /// <summary>A bobber landing here starts fishing (GameLocation.isTileFishable).</summary>
    bool IsFishable(int x, int y);
    /// <summary>Land the farmer can stand on (not water, GameLocation.isTilePassable).</summary>
    bool IsStandable(int x, int y);
}

public readonly record struct TileRect(int X, int Y, int Width, int Height)
{
    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>A tile a cast can land on, and the tile the farmer cast it from.</summary>
public readonly record struct CastLanding(int X, int Y, int StandX, int StandY);

/// <summary>Where a cast can actually land on a map, and how deep the water is there.
///
/// The walk: every standable tile a path reaches from the map's entry tiles, casting in each of
/// the four directions at every power. Decompile FishingRod: a cast flies at least two tiles and
/// at most <c>added + 3</c> up or down and <c>added + 4</c> sideways, where <c>added</c> is 3 from
/// fishing level 8 to 14. A bobber over a tile the rod cannot fish is nudged one tile sideways
/// onto a fishable neighbour (FishingRod.tickUpdate), which is how the Forest waterfall pool is
/// fished at all: the cast from the ledge above lands on the waterfall and slides onto the pool's
/// edge. A player can also steer a bobber mid-flight; that is skill, not something an ask may
/// count on, so it is left out.
///
/// <see cref="ClearWaterDistance"/> mirrors FishingRod.distanceToLand: rings of growing size around
/// the tile, the first ring holding a land tile gives the distance, off-map tiles count as water,
/// and no land within five rings gives 5.</summary>
public static class FishCastReach
{
    /// <summary>No cast lands on a tile the fish bites on.</summary>
    public const int NoCastableSpot = -1;

    private const int MaxRing = 5;
    private const int MinCastTiles = 2;
    private const int VerticalBaseTiles = 3;
    private const int SidewaysBaseTiles = 4;

    private static readonly (int Dx, int Dy)[] Directions = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    public static int ClearWaterDistance(IFishingMap map, int x, int y)
    {
        for (int ring = 1; ring <= MaxRing; ring++)
            for (int dx = -ring; dx <= ring; dx++)
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                    int tx = x + dx, ty = y + dy;
                    if (OnMap(map, tx, ty) && !map.IsWater(tx, ty)) return ring - 1;
                }
        return MaxRing;
    }

    /// <summary>FishingRod.getAddedDistance.</summary>
    public static int AddedCastTiles(int fishingLevel)
        => fishingLevel >= 15 ? 4 : fishingLevel >= 8 ? 3 : fishingLevel >= 4 ? 2 : fishingLevel >= 1 ? 1 : 0;

    /// <summary>Every landing a farmer at this level can cast from a tile a path reaches from
    /// <paramref name="entryTiles"/>. Entry tiles that are not standable are skipped.</summary>
    public static IReadOnlyList<CastLanding> Landings(IFishingMap map, IEnumerable<(int X, int Y)> entryTiles, int fishingLevel)
    {
        int added = AddedCastTiles(fishingLevel);
        var landings = new List<CastLanding>();
        var seen = new HashSet<CastLanding>();
        foreach ((int sx, int sy) in Walkable(map, entryTiles))
            foreach ((int dx, int dy) in Directions)
            {
                int reach = (dx == 0 ? VerticalBaseTiles : SidewaysBaseTiles) + added;
                for (int k = MinCastTiles; k <= reach; k++)
                {
                    int tx = sx + dx * k, ty = sy + dy * k;
                    if (!OnMap(map, tx, ty)) break;
                    if (map.IsFishable(tx, ty)) { Add(tx, ty, sx, sy); continue; }
                    // The nudge runs across the flight, so either side may catch it.
                    Nudge(tx + dy, ty + dx, sx, sy);
                    Nudge(tx - dy, ty - dx, sx, sy);
                }
            }
        return landings;

        void Nudge(int x, int y, int sx, int sy)
        {
            if (OnMap(map, x, y) && map.IsFishable(x, y)) Add(x, y, sx, sy);
        }

        void Add(int x, int y, int sx, int sy)
        {
            var landing = new CastLanding(x, y, sx, sy);
            if (seen.Add(landing)) landings.Add(landing);
        }
    }

    /// <summary>GameLocation.getFish's tile test for one Data/Locations row: inside its
    /// BobberPosition (null = anywhere) and, for a row with a FishAreaId, on a tile whose fish area
    /// (<paramref name="areaAt"/>, null where none) is that id. The area is only read for such rows.</summary>
    public static Func<int, int, bool> RowBitesAt(TileRect? bobber, string? fishAreaId, Func<int, int, string?> areaAt)
        => (x, y) => (bobber is not TileRect b || b.Contains(x, y))
            && (fishAreaId == null || string.Equals(areaAt(x, y), fishAreaId, StringComparison.Ordinal));

    /// <summary>The largest clear-water distance among landings where the fish bites, or
    /// <see cref="NoCastableSpot"/>. <paramref name="minDistance"/> and <paramref name="maxDistance"/>
    /// are the spawn row's MinDistanceFromShore / MaxDistanceFromShore (-1 = no limit); a fish
    /// splash adds one to the depth the row checks, so a tile one short of the minimum still
    /// counts. <paramref name="playerArea"/> is the row's PlayerPosition, where the farmer must stand.</summary>
    public static int MaxClearWater(
        IFishingMap map, IReadOnlyList<CastLanding> landings, Func<int, int, bool> bitesAt,
        int minDistance = 0, int maxDistance = -1, TileRect? playerArea = null)
        => MaxClearWater(new ClearWaterMemo(map), landings, bitesAt, minDistance, maxDistance, playerArea);

    /// <summary><see cref="MaxClearWater(IFishingMap, IReadOnlyList{CastLanding}, Func{int, int, bool}, int, int, TileRect?)"/>
    /// over a shared per-map memo, so every fish row on a map reads each tile's distance once.</summary>
    public static int MaxClearWater(
        ClearWaterMemo depths, IReadOnlyList<CastLanding> landings, Func<int, int, bool> bitesAt,
        int minDistance = 0, int maxDistance = -1, TileRect? playerArea = null)
    {
        int best = NoCastableSpot;
        foreach (CastLanding landing in landings)
        {
            if (playerArea is TileRect area && !area.Contains(landing.StandX, landing.StandY)) continue;
            if (!bitesAt(landing.X, landing.Y)) continue;
            int distance = depths.At(landing.X, landing.Y);
            if (distance + 1 < minDistance) continue;
            if (maxDistance >= 0 && distance > maxDistance) continue;
            best = Math.Max(best, distance);
        }
        return best;
    }

    private static IEnumerable<(int X, int Y)> Walkable(IFishingMap map, IEnumerable<(int X, int Y)> entryTiles)
    {
        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int X, int Y)>();
        foreach ((int x, int y) in entryTiles)
            if (OnMap(map, x, y) && map.IsStandable(x, y) && seen.Add((x, y)))
                queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            yield return (x, y);
            foreach ((int dx, int dy) in Directions)
            {
                int nx = x + dx, ny = y + dy;
                if (OnMap(map, nx, ny) && map.IsStandable(nx, ny) && seen.Add((nx, ny)))
                    queue.Enqueue((nx, ny));
            }
        }
    }

    private static bool OnMap(IFishingMap map, int x, int y) => x >= 0 && y >= 0 && x < map.Width && y < map.Height;
}

/// <summary><see cref="FishCastReach.ClearWaterDistance"/> for one map, each tile worked out once.
/// A map's casts land on the same tiles from many standing spots, and every fish row on the map
/// asks about the same landings, so the ring walk (up to 120 tile reads) runs once per tile.</summary>
public sealed class ClearWaterMemo
{
    private const sbyte Unknown = -1;
    private readonly IFishingMap _map;
    private readonly sbyte[,] _distance;

    public ClearWaterMemo(IFishingMap map)
    {
        _map = map;
        _distance = new sbyte[map.Width, map.Height];
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                _distance[x, y] = Unknown;
    }

    /// <summary>Ring walks actually run (a test hook: repeats must not add to it).</summary>
    public int Computed { get; private set; }

    public int At(int x, int y)
    {
        if (_distance[x, y] == Unknown)
        {
            _distance[x, y] = (sbyte)FishCastReach.ClearWaterDistance(_map, x, y);
            Computed++;
        }
        return _distance[x, y];
    }
}
