using System;

namespace TheLongestYear.Core;

/// <summary>The debris_return wildcard twist (spec section 8): the farm map's own Paths-layer
/// markers say where the big debris starts (vanilla <c>loadPathsLayerObjectsInArea</c>); a
/// marker gets its 2x2 clump back only when all four tiles are free.</summary>
public static class DebrisPlacement
{
    public const int Size = 2;

    public const int HollowLogMarker = 19;
    public const int BoulderMarker = 20;
    public const int StumpMarker = 21;

    public const int HollowLog = 602;
    public const int Boulder = 672;
    public const int Stump = 600;

    /// <summary>The resource clump for a Paths-layer tile index, or null when it is not a debris marker.</summary>
    public static int? ClumpFor(int pathsTileIndex) => pathsTileIndex switch
    {
        HollowLogMarker => HollowLog,
        BoulderMarker => Boulder,
        StumpMarker => Stump,
        _ => null,
    };

    /// <summary>True when every tile of the 2x2 footprint at (x, y) is free.</summary>
    public static bool FreeFootprint(Func<int, int, bool> isFree, int x, int y)
    {
        for (int dy = 0; dy < Size; dy++)
            for (int dx = 0; dx < Size; dx++)
                if (!isFree(x + dx, y + dy)) return false;
        return true;
    }
}
