namespace TheLongestYear.Core;

/// <summary>Randomizer double theme week (spec section 6): one week per season, week 2 or 3 of the
/// month, where the player picks two themes instead of one.</summary>
public static class DoubleWeek
{
    private const int Salt = 0x0D0B;
    private const int WeeksPerSeason = 4;
    private const int FirstEligibleWeekInMonth = 2, EligibleWeeks = 2;

    public static bool Is(int seed, int weekOfYear, bool enabled)
    {
        if (!enabled) return false;
        int seasonIndex = (weekOfYear - 1) / WeeksPerSeason;
        int weekInMonth = (weekOfYear - 1) % WeeksPerSeason + 1;
        int chosen = RollSeed.Rng(seed, seasonIndex, Salt).Next(EligibleWeeks) + FirstEligibleWeekInMonth;
        return weekInMonth == chosen;
    }
}
