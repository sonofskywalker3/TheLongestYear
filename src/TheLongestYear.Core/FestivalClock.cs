using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// A slower clock for the festivals where normal time is too short (Nijah, Nexus, 2026-10-27).
///
/// With FestivalTimeFlow on, a festival runs on the normal clock: 7 real seconds per 10 game
/// minutes. Spirit's Eve runs 22:00 to 23:50, which is under a minute and a half of real time to
/// shop at Pierre's stall AND walk the haunted maze. While the player is on such a festival's
/// grounds the game minute is stretched by <see cref="ExtraMillisecondsPerGameMinute"/>, fed into
/// vanilla's own per-location knob <c>GameLocation.ExtraMillisecondsPerInGameMinute</c>.
///
/// Only Spirit's Eve qualifies. The Moonlight Jellies were considered and left out: the jellies
/// themselves are the festival's main event, a scripted sequence the game runs with
/// playerControlSequence off, and <see cref="FestivalPhase"/> already freezes the clock for those.
/// The Night Market is a passive festival (no festival map, no clock change) and is not touched.
/// </summary>
public static class FestivalClock
{
    /// <summary>Festival ids (the part after "festival_") that get the slower clock.</summary>
    private static readonly HashSet<string> SlowedFestivalIds = new(StringComparer.Ordinal)
    {
        "fall27", // Spirit's Eve
    };

    /// <summary>Read-only view of the qualifying festival ids.</summary>
    public static IReadOnlyCollection<string> SlowedFestivals => SlowedFestivalIds;

    /// <summary>Lowest speed the setting allows: a quarter of normal speed.</summary>
    public const double MinSpeed = 0.25;

    /// <summary>Normal speed: the setting's top end, and today's behaviour.</summary>
    public const double NormalSpeed = 1.0;

    /// <summary>Vanilla festival event ids are "festival_&lt;season&gt;&lt;day&gt;".</summary>
    private const string FestivalEventPrefix = "festival_";

    /// <summary>True when the festival with this event id ("festival_fall27") runs on the slower clock.</summary>
    public static bool IsSlowed(string? eventId)
    {
        if (eventId == null || !eventId.StartsWith(FestivalEventPrefix, StringComparison.Ordinal))
            return false;
        return SlowedFestivalIds.Contains(eventId.Substring(FestivalEventPrefix.Length));
    }

    /// <summary>The setting clamped to [<see cref="MinSpeed"/>, <see cref="NormalSpeed"/>]. A
    /// hand-edited config with 0, a negative or NaN gets the slowest allowed speed rather than a
    /// stopped or broken clock; anything above normal is normal (this setting only slows).</summary>
    public static double ClampSpeed(double speed)
    {
        if (double.IsNaN(speed) || speed < MinSpeed) return MinSpeed;
        return speed > NormalSpeed ? NormalSpeed : speed;
    }

    /// <summary>
    /// Extra real milliseconds per game minute that make the clock run at <paramref name="speed"/>
    /// of normal. A game minute normally lasts <paramref name="realMsPerGameMinute"/> (700 in
    /// vanilla); at speed s it should last 700 / s, so the extra is 700 * (1 / s - 1).
    /// 0.5 gives 700 (twice as long), 1.0 gives 0 (unchanged).
    /// </summary>
    public static int ExtraMillisecondsPerGameMinute(double speed, int realMsPerGameMinute)
    {
        if (realMsPerGameMinute <= 0) return 0;
        double s = ClampSpeed(speed);
        return (int)Math.Round(realMsPerGameMinute * (1.0 / s - 1.0), MidpointRounding.AwayFromZero);
    }

    /// <summary>The extra milliseconds per game minute for the festival with this event id: the
    /// slowed amount for a qualifying festival, 0 for every other one.</summary>
    public static int ExtraMillisecondsFor(string? eventId, double speed, int realMsPerGameMinute)
        => IsSlowed(eventId) ? ExtraMillisecondsPerGameMinute(speed, realMsPerGameMinute) : 0;

    /// <summary>
    /// Real milliseconds one 10-minute clock advance takes, mirroring vanilla's
    /// <c>Game1.UpdateGameClock</c>: the advance fires once the accumulated interval exceeds
    /// realMsPerTenMinutes + extraPerMinute * 10. Lets the tests check the whole festival window.
    /// </summary>
    public static int RealMillisecondsPerTenMinutes(int realMsPerGameMinute, int extraMsPerGameMinute)
        => (realMsPerGameMinute + extraMsPerGameMinute) * 10;
}
