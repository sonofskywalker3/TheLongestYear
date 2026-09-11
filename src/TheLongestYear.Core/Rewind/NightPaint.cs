namespace TheLongestYear.Core.Rewind;

/// <summary>What the HUD has to read for the bedroom beats of the rewind: the night of the day that
/// failed. The mirror of <see cref="SpringPaintValues"/> at the other end of the sequence.</summary>
public readonly record struct NightPaintValues(
    Season Season,
    int DayOfMonth,
    int TimeOfDay);

/// <summary>The bedroom beats open on the dark wake frame of the morning AFTER the failed day, so
/// every global the HUD reads has already rolled forward: a Spring 28 failure shows Summer 1 at 6am
/// (playtest 2026-09-11). The scene is the night of the day that failed, so it paints that instead.
///
/// The clock reads 3am rather than a plausible bedtime for a fiction reason (Jeff, 2026-09-11): it
/// leaves room for the player to have passed out somewhere else and been carried home.</summary>
public static class NightPaint
{
    /// <summary>Deep night, hours before the 6am wake. See the class summary for why 3am and not a
    /// bedtime.</summary>
    public const int NightTime = 300;

    /// <summary>Gates are checked on the last day of a season, so the failed day is always the
    /// last one.</summary>
    public const int FailedDayOfMonth = Calendar.DaysPerMonth;

    public static NightPaintValues Values(Season failed)
        => new(failed, FailedDayOfMonth, NightTime);
}
