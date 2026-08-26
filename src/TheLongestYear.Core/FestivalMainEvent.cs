namespace TheLongestYear.Core;

/// <summary>
/// Once-per-day guard for a festival's main event (the Egg Hunt, the ice fishing contest, the
/// Flower Dance, grange judging).
///
/// Vanilla has no such guard and does not need one: finishing a festival ends the day, the clock
/// jumps past the festival's end time, and re-entering the map is impossible. TLY removes exactly
/// that property on purpose (see FestivalTimeFlow: the hours inside a festival have to count in a
/// time-loop), which leaves the festival re-entrant for the rest of its window. Walk out, walk back
/// into Town, and the host offers the main event again. Jeff caught it on emmalution's stream:
/// she ran the Egg Hunt three times on the same day.
///
/// The stamp is the festival id plus the absolute day it was played, so it expires by itself at the
/// next sunrise and cannot leak across a rewind.
/// </summary>
public static class FestivalMainEvent
{
    /// <summary>Record that today's festival main event has been played. No-op on a null run.</summary>
    public static void MarkPlayed(RunState run, string festivalId, int totalDays)
    {
        if (run == null || string.IsNullOrEmpty(festivalId)) return;
        run.FestivalMainEventId = festivalId;
        run.FestivalMainEventDay = totalDays;
    }

    /// <summary>True when this exact festival's main event has already run on this exact day.</summary>
    public static bool AlreadyPlayed(RunState run, string festivalId, int totalDays)
    {
        if (run == null || string.IsNullOrEmpty(festivalId)) return false;
        return run.FestivalMainEventDay == totalDays
            && string.Equals(run.FestivalMainEventId, festivalId, System.StringComparison.Ordinal);
    }
}
