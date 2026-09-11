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

    /// <summary>The date at a point along the route: which season is on screen and what the day
    /// counter reads. The route is divided evenly between the seasons being unwound, matching
    /// <see cref="SwapFractions"/> exactly, and inside each segment the day runs from the last day
    /// of the month down to the first. So a Fall failure is three segments of the same length, a
    /// Winter failure four, and a Spring failure one whole-route segment that still counts 28 down
    /// to 1 even though the map never repaints.
    ///
    /// This is the dial the HUD shows, and it is deliberately NOT the same dial as the light
    /// (<see cref="CycleClockAt"/>): Jeff, 2026-09-11, was explicit that the date must not line up
    /// with the visual sunset. The date unwinds once, steadily, across the whole scene; the light
    /// loops dusk to dawn many times over the same stretch.</summary>
    public static (Season Season, int DayOfMonth) DateAt(double progress, Season failed)
    {
        IReadOnlyList<Season> seasons = SeasonsToUnwind(failed);
        double clamped = Math.Clamp(progress, 0.0, 1.0);

        int index = (int)(clamped * seasons.Count);
        if (index >= seasons.Count) index = seasons.Count - 1;   // progress exactly 1

        double within = clamped * seasons.Count - index;
        int day = Calendar.DaysPerMonth - (int)(within * Calendar.DaysPerMonth);
        return (seasons[index], Math.Clamp(day, 1, Calendar.DaysPerMonth));
    }

    /// <summary>A dusk-to-dawn loop: the clock runs from <paramref name="startTime"/> down to
    /// <paramref name="endTime"/> over <paramref name="cycleMs"/>, then snaps back and does it
    /// again, for as long as the pan lasts. Impressionistic rather than a real calendar (Jeff,
    /// 2026-09-11): the point is the valley lighting itself over and over as time comes undone, not
    /// a day counter. <c>Game1.UpdateGameClock</c> recomputes <c>outdoorLight</c> from
    /// <c>Game1.timeOfDay</c> every tick, so driving this is the whole light effect.</summary>
    public static int CycleClockAt(double elapsedMs, double cycleMs, int startTime, int endTime)
    {
        if (cycleMs <= 0.0) return ClockAt(0.0, startTime, endTime);
        double within = elapsedMs % cycleMs;
        if (within < 0.0) within += cycleMs;
        return ClockAt(within / cycleMs, startTime, endTime);
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
