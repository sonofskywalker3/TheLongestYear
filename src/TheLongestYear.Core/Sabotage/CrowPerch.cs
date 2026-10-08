using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The crows scene's scarecrow crow and the flock's pecking (designer, 2026-10-08).
///
/// ONE scarecrow, ever: a farm with several scarecrows near the patch still gets a single crow
/// perched on a single scarecrow, the one nearest the crops that also fits in the frame beside them.
/// The perched crow stands ON the scarecrow's hat, not on the ground next to it.
///
/// Pecking: each crow on the ground pecks on its own random timing, not in step with the others.
/// Pure arithmetic and an injected <see cref="Random"/>, no game types.</summary>
public static class CrowPerch
{
    /// <summary>Margin, in tiles, kept between the frame's side edges and the pair (patch centre and
    /// scarecrow) when the camera sits halfway between them.</summary>
    public const int FrameSideMarginTiles = 4;

    /// <summary>Margin, in tiles, kept top and bottom. Bigger than the sides: the scarecrow stands
    /// two tiles tall and the crow on its hat reaches two more above that.</summary>
    public const int FrameTopBottomMarginTiles = 6;

    /// <summary>The one scarecrow that gets the perched crow: the nearest to the patch centre
    /// within <paramref name="reachTiles"/>, and only one whose distance from the patch still lets
    /// the camera, halfway between them, hold both in a frame of the given size. Equally near
    /// scarecrows keep the order they came in. Null when none qualifies.</summary>
    public static (int X, int Y)? ChooseScarecrow(
        IReadOnlyList<(int X, int Y)> scarecrows, (int X, int Y) patchCentre, int reachTiles, int frameWidthTiles, int frameHeightTiles)
    {
        if (scarecrows is null) throw new ArgumentNullException(nameof(scarecrows));
        (int X, int Y)? best = null;
        double bestDistance = double.MaxValue;
        foreach ((int X, int Y) s in scarecrows)
        {
            int dx = Math.Abs(s.X - patchCentre.X), dy = Math.Abs(s.Y - patchCentre.Y);
            double distance = Math.Sqrt((double)dx * dx + (double)dy * dy);
            if (distance > reachTiles) continue;
            if (dx > frameWidthTiles - FrameSideMarginTiles || dy > frameHeightTiles - FrameTopBottomMarginTiles) continue;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = s;
        }
        return best;
    }

    // ---------------------------------------------------------------- perching on the hat

    /// <summary>A big craftable draws 16 by 32 sheet pixels at 4x with its foot on its tile, so its
    /// top edge is one tile above the tile's top (<c>Object.draw</c>, y - 64).</summary>
    public const int BigCraftableRisePixels = 64;

    /// <summary>The scarecrow's hat crown starts at sheet row 0 (read off the dumped Craftables sheet,
    /// tools/preview/_craftables_full.png, column 0 rows 32 to 63). The crow's frame has a couple of
    /// clear rows under its feet, so its foot point sits this far below the crown's top edge to read
    /// as standing on it rather than hovering.</summary>
    public const int FeetIntoHatPixels = 8;

    /// <summary>The perched crow's foot point, in world pixels, on the hat of the scarecrow at
    /// <paramref name="tileX"/>, <paramref name="tileY"/>.</summary>
    public static (float X, float Y) OnHat(int tileX, int tileY, int tileSize)
        => (tileX * tileSize + tileSize / 2f, tileY * tileSize - BigCraftableRisePixels + FeetIntoHatPixels);

    // ---------------------------------------------------------------- pecking

    /// <summary>One peck: head down for the first half, the strike for the second.</summary>
    public const int PeckLengthMs = 250;
    /// <summary>A crow looks about for this long, give or take, after it lands before its first peck.</summary>
    public const int FirstPeckMinMs = 100;
    public const int FirstPeckMaxMs = 700;
    /// <summary>A peck is followed straight away by another (a quick double) one time in three;
    /// otherwise the crow pauses this long before the next.</summary>
    public const int PauseMinMs = 250;
    public const int PauseMaxMs = 1000;
    public const int QuickGapMaxMs = 60;
    private const int QuickDoubleOneIn = 3;

    /// <summary>The start times of one crow's pecks while it is down, from
    /// <paramref name="landedAtMs"/> until <paramref name="liftOffAtMs"/>. Every peck ends before
    /// the lift-off; none overlap. A window long enough for one peck always gets at least one, so no
    /// crow just stands on its crop.</summary>
    public static IReadOnlyList<int> PeckTimes(Random rng, int landedAtMs, int liftOffAtMs)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        var pecks = new List<int>();
        int lastStart = liftOffAtMs - PeckLengthMs;
        if (lastStart < landedAtMs) return pecks;
        int at = landedAtMs + rng.Next(FirstPeckMinMs, FirstPeckMaxMs + 1);
        while (at <= lastStart)
        {
            pecks.Add(at);
            int gap = rng.Next(QuickDoubleOneIn) == 0 ? rng.Next(0, QuickGapMaxMs + 1) : rng.Next(PauseMinMs, PauseMaxMs + 1);
            at += PeckLengthMs + gap;
        }
        if (pecks.Count == 0) pecks.Add(landedAtMs + (lastStart - landedAtMs) / 2);
        return pecks;
    }

    /// <summary>What the crow's head is doing at <paramref name="elapsedMs"/>.</summary>
    public enum PeckPose { Standing, HeadDown, Strike }

    public static PeckPose PoseAt(IReadOnlyList<int> peckTimes, int elapsedMs)
    {
        if (peckTimes is null) return PeckPose.Standing;
        foreach (int start in peckTimes)
        {
            int since = elapsedMs - start;
            if (since < 0) break;
            if (since < PeckLengthMs) return since < PeckLengthMs / 2 ? PeckPose.HeadDown : PeckPose.Strike;
        }
        return PeckPose.Standing;
    }
}
