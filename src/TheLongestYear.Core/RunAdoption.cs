namespace TheLongestYear.Core;

/// <summary>
/// Rescues a brand-new farm that was quit before its first night.
///
/// Stardew 1.6 writes a new farm's save file at character creation, and SMAPI deliberately does
/// not raise Saving for that write. The mod's run marker used to reach the save only through the
/// end-of-day Saving event, so a farm quit on its first Spring 1 had a save with no marker and
/// loaded as a vanilla save: no books, no shrine, no stash, Community Center locked (Nexus,
/// 2026-09-14). The marker is now written at SaveCreating as well; this rule adopts the saves
/// players already have from before that.
///
/// A save qualifies only when it carries no mod data at all and is still on its very first
/// morning: one day played, Spring 1 of year 1. A loop that rewinds to Spring 1 always carries
/// mod data, so it never matches.
/// </summary>
public static class RunAdoption
{
    private const int FirstDay = 1;
    private const int FirstYear = 1;
    private const uint FirstMorningDaysPlayed = 1;

    public static bool IsUnsavedFirstMorning(
        bool hasSavedMetaData, uint daysPlayed, Season season, int dayOfMonth, int year)
    {
        if (hasSavedMetaData) return false;
        return daysPlayed <= FirstMorningDaysPlayed
            && season == Season.Spring
            && dayOfMonth == FirstDay
            && year == FirstYear;
    }
}
