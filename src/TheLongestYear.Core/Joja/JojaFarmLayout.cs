using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Joja;

/// <summary>One building placed by <see cref="JojaFarmLayout"/>: the index into the kinds list and
/// its top-left footprint tile.</summary>
public readonly record struct FarmBuildingSpot(int Kind, int X, int Y);

/// <summary>A row of buildings side by side, all standing on the same bottom tile row.</summary>
public sealed record FarmBuildingRow(int Bottom, IReadOnlyList<FarmBuildingSpot> Spots)
{
    /// <summary>The leftmost footprint tile of the row.</summary>
    public int Left => Spots[0].X;
}

/// <summary>The bad ending's factory farm (spec 2026-09-25-joja-offer-design, "The bad ending"):
/// rows of coops and barns side by side across the whole farm. Pure: the caller says which tiles
/// can take a building (the game's own isBuildable after the farm is cleared), so the same layout
/// works on every farm type without a hard-coded tile.
///
/// Each row's buildings share a bottom row. Below that row is a lane of <c>lane</c> tiles, which
/// must also be buildable, where the animals stand before they go in. The next row's sprites rise
/// <c>spriteRows</c> tiles above its footprint's bottom, so rows are at least
/// <c>lane + spriteRows</c> apart: no building ever hides an animal in the lane of the row above.</summary>
public static class JojaFarmLayout
{
    /// <summary>Lays out the rows top to bottom. <paramref name="kinds"/> are footprint sizes (tiles),
    /// used alternately along a row (the next row starts on the other kind). Within each band of
    /// candidate bottom rows the one that fits the most buildings wins.</summary>
    public static IReadOnlyList<FarmBuildingRow> Rows(Func<int, int, bool> buildable, int width, int height,
        IReadOnlyList<(int Width, int Height)> kinds, int lane, int spriteRows, int gap, int minPerRow, int maxRows)
    {
        if (buildable == null) throw new ArgumentNullException(nameof(buildable));
        if (kinds == null || kinds.Count == 0) throw new ArgumentException("at least one building kind", nameof(kinds));
        int stride = lane + spriteRows;
        var rows = new List<FarmBuildingRow>();
        int start = spriteRows - 1;                     // the first row's sprite starts on the map's top row
        while (rows.Count < maxRows && start + lane < height)
        {
            List<FarmBuildingSpot>? best = null;
            int bestBottom = -1;
            for (int bottom = start; bottom < start + stride && bottom + lane < height; bottom++)
            {
                List<FarmBuildingSpot> spots = FillRow(buildable, width, height, kinds, lane, gap, bottom, rows.Count);
                if (best == null || spots.Count > best.Count)
                {
                    best = spots;
                    bestBottom = bottom;
                }
            }
            if (best != null && best.Count >= Math.Max(1, minPerRow))
            {
                rows.Add(new FarmBuildingRow(bestBottom, best));
                start = bestBottom + stride;
            }
            else
            {
                start += stride;
            }
        }
        return rows;
    }

    private static List<FarmBuildingSpot> FillRow(Func<int, int, bool> buildable, int width, int height,
        IReadOnlyList<(int Width, int Height)> kinds, int lane, int gap, int bottom, int rowIndex)
    {
        var spots = new List<FarmBuildingSpot>();
        int next = rowIndex % kinds.Count;
        int x = 0;
        while (x < width)
        {
            bool placed = false;
            for (int attempt = 0; attempt < kinds.Count; attempt++)
            {
                int kind = (next + attempt) % kinds.Count;
                (int w, int h) = kinds[kind];
                if (!Fits(buildable, width, height, x, bottom, w, h, lane)) continue;
                spots.Add(new FarmBuildingSpot(kind, x, bottom - h + 1));
                x += w + gap;
                next = (kind + 1) % kinds.Count;
                placed = true;
                break;
            }
            if (!placed) x++;
        }
        return spots;
    }

    /// <summary>The footprint (w x h, bottom row <paramref name="bottom"/>) and the lane under it.</summary>
    private static bool Fits(Func<int, int, bool> buildable, int width, int height, int x, int bottom, int w, int h, int lane)
    {
        if (x < 0 || x + w > width) return false;
        int top = bottom - h + 1;
        if (top < 0 || bottom + lane >= height) return false;
        for (int ty = top; ty <= bottom + lane; ty++)
            for (int tx = x; tx < x + w; tx++)
                if (!buildable(tx, ty)) return false;
        return true;
    }

    /// <summary>Where a building's animals stand before going in: tiles in its lane (rows
    /// bottom+2 .. bottom+lane, under the building's own columns), picked by
    /// <see cref="JojaScatter.Pick"/> so they are spread out and the same every run.</summary>
    public static IReadOnlyList<(int X, int Y)> LaneSpots(int x, int width, int bottom, int lane, int count, double spacing, int seed)
    {
        var candidates = new List<(int X, int Y)>();
        for (int ty = bottom + 2; ty <= bottom + lane; ty++)
            for (int tx = x; tx < x + width; tx++)
                candidates.Add((tx, ty));
        return JojaScatter.Pick(candidates, count, spacing, seed);
    }
}
