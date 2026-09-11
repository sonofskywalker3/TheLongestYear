using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Rewind;

/// <summary>The arithmetic behind the rewind pan: which seasons unwind, where along the camera
/// route each tilesheet swap lands, and what the backward clock reads at a given progress. Pure so
/// the odd cases (a Spring failure unwinds nothing) are settled in tests, not in a playtest.</summary>
public static class RewindSchedule
{
    /// <summary>The failed season first, descending to Spring inclusive. A Spring failure yields
    /// Spring alone: there is nothing earlier in the year to wash back to.</summary>
    public static IReadOnlyList<Season> SeasonsToUnwind(Season failed)
    {
        var list = new List<Season>();
        for (int s = (int)failed; s >= 0; s--)
            list.Add((Season)s);
        return list;
    }

    /// <summary>Progress points (exclusive of both ends) where the map repaints. One fewer than the
    /// number of seasons, spaced evenly, so the camera spends equal distance in each.</summary>
    public static IReadOnlyList<double> SwapFractions(Season failed)
    {
        int count = SeasonsToUnwind(failed).Count;
        var fractions = new List<double>();
        for (int i = 1; i < count; i++)
            fractions.Add((double)i / count);
        return fractions;
    }

    /// <summary>A sunrise-and-sunset loop: the clock runs BACKWARD from <paramref name="darkTime"/>
    /// to <paramref name="lightTime"/> over the first half of <paramref name="cycleMs"/>, then
    /// FORWARD again over the second half, and repeats for as long as the pan lasts. A triangle,
    /// not a sawtooth.
    ///
    /// The sawtooth is what the first version did, and it had no sunrise in it (playtest
    /// 2026-09-11): running dusk down to dawn and then snapping back to dusk means the world only
    /// ever fades one way and the other half of every cycle is a hard cut to black. Jeff's fix, in
    /// his words: "reversing from maybe 10pm for 1 second until it's full daylight, and then advance
    /// for the next second to fade to dark instead of just flipping to it."
    ///
    /// Impressionistic rather than a real calendar: the point is the valley lighting and unlighting
    /// itself over and over as time comes undone, not a day counter.
    /// <c>Game1.UpdateGameClock</c> recomputes <c>outdoorLight</c> from <c>Game1.timeOfDay</c> every
    /// tick, so driving this is the whole light effect.</summary>
    public static int CycleClockAt(double elapsedMs, double cycleMs, int darkTime, int lightTime)
    {
        if (cycleMs <= 0.0) return darkTime;
        double within = elapsedMs % cycleMs;
        if (within < 0.0) within += cycleMs;

        double half = cycleMs / 2.0;
        return within < half
            ? ClockAt(within / half, darkTime, lightTime)          // sunrise: dark running back to light
            : ClockAt((within - half) / half, lightTime, darkTime); // sunset: light running on to dark
    }

    /// <summary>The clock at a point along the route, running from <paramref name="startTime"/> down
    /// to <paramref name="endTime"/>. Stardew stores time as HHmm and ticks in ten-minute steps, so
    /// the result is floored to a multiple of 10 to avoid values the game never produces.</summary>
    public static int ClockAt(double progress, int startTime, int endTime)
    {
        double clamped = Math.Clamp(progress, 0.0, 1.0);
        double minutesStart = ToMinutes(startTime);
        double minutesEnd = ToMinutes(endTime);
        double minutes = minutesStart + (minutesEnd - minutesStart) * clamped;
        int stepped = (int)(Math.Floor(minutes / 10.0) * 10.0);
        return ToClock(stepped);
    }

    private static double ToMinutes(int clock) => clock / 100 * 60 + clock % 100;

    private static int ToClock(int minutes) => minutes / 60 * 100 + minutes % 60;
}
