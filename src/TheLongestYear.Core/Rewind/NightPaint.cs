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
/// The clock reads the small hours rather than a plausible bedtime for a fiction reason (Jeff,
/// 2026-09-11): it leaves room for the player to have passed out somewhere else and been carried
/// home.</summary>
public static class NightPaint
{
    /// <summary>Deep night, hours before the 6am wake. See the class summary for why the small
    /// hours and not a bedtime.
    ///
    /// 2am, NOT 3am. Stardew encodes the hours past midnight as 2400/2500/2600 and its clock dial
    /// runs 6am round to 2am, so a literal 300 is read as three in the MORNING and puts the hand
    /// before sunrise (playtest 2026-09-11: "the clock hand is before sunrise"). 2600 is the last
    /// position the dial has, it is a value the game itself produces, and it says the same thing
    /// about the hour.</summary>
    public const int NightTime = 2600;

    /// <summary>Gates are checked on the last day of a season, so the failed day is always the
    /// last one.</summary>
    public const int FailedDayOfMonth = Calendar.DaysPerMonth;

    public static NightPaintValues Values(Season failed)
        => new(failed, FailedDayOfMonth, NightTime);
}
