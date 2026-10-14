using System;

namespace TheLongestYear.Core;

/// <summary>Artisan bonus (machines_fast) and Spelunking liability (machines_slow): a machine's
/// queued ready time scaled by 0.75 or 1.25, rounded to the game's 10-minute tick, never under
/// one tick. Zero or negative (nothing queued) is returned unchanged.</summary>
public static class MachineReadyTime
{
    public const double FastFactor = 0.75;
    public const double SlowFactor = 1.25;
    private const int RoundTo = 10;
    private const int Floor = 10;

    /// <summary>The factor a just-loaded machine's time is scaled by: the Artisan theme / Full Steam
    /// (0.75 per stack, compounding) or, without them, the Spelunking liability (1.25); times Fast
    /// Hatch's 0.5 for an incubator when owned (spec 2026-10-09). 1.0 means leave it alone.</summary>
    public static double Factor(int fastStacks, bool slow, bool incubatorHalf)
    {
        double factor = fastStacks > 0 ? Math.Pow(FastFactor, fastStacks) : slow ? SlowFactor : 1.0;
        return incubatorHalf ? factor * AnimalPowers.IncubatorFactor : factor;
    }

    public static int Scale(int minutes, double factor)
    {
        if (minutes <= 0) return minutes;
        int scaled = (int)Math.Round(minutes * factor / RoundTo, MidpointRounding.AwayFromZero) * RoundTo;
        return Math.Max(Floor, scaled);
    }
}
