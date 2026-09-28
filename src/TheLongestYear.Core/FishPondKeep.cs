using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Keep Fish Pond (elaineofshalott, Nexus 2026-09-27; Jeff's call: one EMPTY pond). The rewind
/// rebuilds one finished Fish Pond where the player had it, with no fish, no output and no
/// population gates, exactly as if Robin had just finished it. Pure spot logic lives here; the
/// glue is WorldResetService (SnapshotKeptBuildingSpots + RestoreKeptFishPond).
///
/// Several ponds: the one with the most fish is remembered, ties go to the first in the farm's
/// building list. Only that pond's top-left tile is stored, under <see cref="SpotKey"/> in
/// MetaState.KeptBuildingSpots.
/// </summary>
public static class FishPondKeep
{
    public const string UpgradeId = "keep_fish_pond";
    public const string BuildingType = "Fish Pond";
    public const string SpotKey = "fishpond";
    public const int Width = 5;
    public const int Height = 5;

    /// <summary>Standard-farm fallback when no spot is remembered or it is blocked: open ground
    /// southwest of the farmhouse, below the barn fallback (46,12, 7x4) and clear of the coop
    /// (54,9) and silo (51,9) fallbacks and the farmhouse door path.</summary>
    public static readonly BuildingSpot FallbackSpot = new(46, 18);

    /// <summary>How far (in tiles, square rings) the search walks out from the fallback.</summary>
    public const int SearchRadius = 20;

    /// <summary>Index of the pond to keep, given each pond's fish count in farm order: most fish
    /// wins, the first pond wins a tie. Null when there is no pond.</summary>
    public static int? PickKeptPond(IReadOnlyList<int> occupantsInFarmOrder)
    {
        int? best = null;
        for (int i = 0; i < occupantsInFarmOrder.Count; i++)
            if (best == null || occupantsInFarmOrder[i] > occupantsInFarmOrder[best.Value])
                best = i;
        return best;
    }

    /// <summary>True when every tile of the 5x5 footprint at <paramref name="spot"/> is free.</summary>
    public static bool FootprintFree(BuildingSpot spot, Func<int, int, bool> tileFree)
    {
        for (int dy = 0; dy < Height; dy++)
            for (int dx = 0; dx < Width; dx++)
                if (!tileFree(spot.X + dx, spot.Y + dy))
                    return false;
        return true;
    }

    /// <summary>Where the kept pond goes: the remembered spot if its footprint is free, else the
    /// fallback, else the nearest free footprint in square rings around the fallback (row by row
    /// inside a ring). Null when nothing within <see cref="SearchRadius"/> fits.</summary>
    public static BuildingSpot? ResolveSpot(BuildingSpot? remembered, Func<int, int, bool> tileFree)
    {
        if (remembered != null && FootprintFree(remembered, tileFree))
            return remembered;
        for (int r = 0; r <= SearchRadius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        continue; // interior of the ring was tried at a smaller radius
                    var candidate = new BuildingSpot(FallbackSpot.X + dx, FallbackSpot.Y + dy);
                    if (FootprintFree(candidate, tileFree))
                        return candidate;
                }
        return null;
    }
}
