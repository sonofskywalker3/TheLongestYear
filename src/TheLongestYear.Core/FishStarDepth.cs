using System;

namespace TheLongestYear.Core;

/// <summary>Whether a rod-caught fish can come out silver or gold, from how far the bobber sits
/// from land (the game's clearWaterDistance, 0 to 5). Player report 2026-10-20: a Quality Fish
/// bundle asked for a gold Goby, and the only cast that reaches the Forest waterfall pool lands on
/// its edge, one tile from land, where every Goby is base quality.
///
/// The game's size roll (decompile FishingRod.startMinigameEndFunction, then the BobberBar ctor):
/// <c>size = distance / 5 * Next(1 + level / 2, max(6, 1 + level / 2)) / 5</c>, times 1.2 when
/// the fish took its favourite bait, times a 0.9 to 1.1 jitter, clamped to 0..1. Below 0.33 the
/// fish is base quality, below 0.66 silver, above that gold; a perfect catch lifts silver to gold
/// but never lifts base. So a spot that cannot reach 0.33 at its best roll can never give a star
/// without the Quality Bobber, which a first year does not reach before Fall 2 (fishing 6, the
/// Iridium Rod and one of Willy's special orders).
///
/// "Year one" is fishing level 10 with no food buff: a loop ask must be fair to a player who
/// maxed the skill, not one who also eats the right dish before every cast.</summary>
public static class FishStarDepth
{
    public const int YearOneFishingLevel = 10;

    /// <summary>The size fraction at which a fish turns silver (BobberBar ctor).</summary>
    public const double SilverSizeFraction = 0.33;

    private const double FullSizeDistance = 5.0;
    private const double RollDivisor = 5.0;
    private const int RollCeilingFloor = 6;
    private const double FavouriteBaitBonus = 1.2;
    private const double BestJitter = 1.1;

    /// <summary>The largest size fraction a catch at this clear-water distance can roll at this
    /// fishing level, every random factor taken at its best.</summary>
    public static double MaxSizeFraction(int clearWaterDistance, int fishingLevel)
    {
        if (clearWaterDistance <= 0) return 0.0;
        int floor = 1 + Math.Max(0, fishingLevel) / 2;
        // Next(floor, max(6, floor)) is exclusive at the top, so its best is 5 until floor reaches 6.
        int bestRoll = floor >= RollCeilingFloor ? floor : RollCeilingFloor - 1;
        double size = clearWaterDistance / FullSizeDistance * (bestRoll / RollDivisor) * FavouriteBaitBonus * BestJitter;
        return Math.Min(1.0, size);
    }

    /// <summary>True when a fish whose deepest reachable catch spot sits this far from land can
    /// carry a silver or gold ask. Null means the map could not be read, and
    /// <see cref="FishCastReach.NoCastableSpot"/> that the walk found no cast landing where the
    /// fish bites (a spot reached over a bridge or from a dock the walk cannot see, like the tip of
    /// Arrowhead Island): neither proves the water shallow, so neither takes a star away.</summary>
    public static bool CanRollStar(int? maxClearWaterDistance)
        => maxClearWaterDistance is not int distance
           || distance == FishCastReach.NoCastableSpot
           || MaxSizeFraction(distance, YearOneFishingLevel) >= SilverSizeFraction;
}
