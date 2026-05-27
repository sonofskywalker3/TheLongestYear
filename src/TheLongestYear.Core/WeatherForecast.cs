namespace TheLongestYear.Core;

/// <summary>
/// Deterministic weather forecast for the next N days, computed without calling any
/// Stardew game types. Resolves forced-rule days (festivals, forced sun day 1, summer storms)
/// and marks probabilistic days as "?" so the UI can display partial information honestly.
/// Used by WeeklyHubMenu when Weather Sage upgrades are owned.
/// </summary>
public static class WeatherForecast
{
    // Fixed festival days per season (vanilla 1.6 defaults; ignores SVE/modded festivals).
    private static readonly int[] SpringFestivals = { 13, 24 };
    private static readonly int[] SummerFestivals = { 11, 28 };
    private static readonly int[] FallFestivals   = { 16, 27 };
    private static readonly int[] WinterFestivals = { 8, 25 };

    /// <summary>
    /// Build the N-slot forecast starting from tomorrow (relative to the current game state).
    /// Returns array of weather label strings: "Sun", "Rain", "Storm", "Festival", "GreenRain", "?".
    /// </summary>
    /// <param name="uniqueId">Game1.uniqueIDForThisGame (for seed; unused in v1 forced-rule-only logic)</param>
    /// <param name="daysPlayedToday">Game1.stats.DaysPlayed today</param>
    /// <param name="currentDayOfMonth">today's day-of-month (1-28)</param>
    /// <param name="currentSeasonIndex">0=Spring 1=Summer 2=Fall 3=Winter</param>
    /// <param name="slotsToReveal">how many days forward to compute (from Weather Sage tier)</param>
    public static string[] Build(int uniqueId, int daysPlayedToday, int currentDayOfMonth,
        int currentSeasonIndex, int slotsToReveal)
    {
        var result = new string[slotsToReveal];
        int dayOfMonth = currentDayOfMonth;
        int seasonIndex = currentSeasonIndex;
        int daysPlayed = daysPlayedToday;

        for (int slot = 0; slot < slotsToReveal; slot++)
        {
            // Advance one day.
            dayOfMonth++;
            daysPlayed++;
            if (dayOfMonth > 28)
            {
                dayOfMonth = 1;
                seasonIndex = (seasonIndex + 1) % 4;
            }

            result[slot] = ResolveDay(dayOfMonth, seasonIndex);
        }

        return result;
    }

    private static string ResolveDay(int dayOfMonth, int seasonIndex)
    {
        // Rule: day 1 of any season = forced Sun.
        if (dayOfMonth == 1)
            return "Sun";

        // Rule: festival day (checked before storm so conflicting days like Summer 26 resolve correctly).
        if (IsFestival(dayOfMonth, seasonIndex))
            return "Festival";

        // Rule: Summer storm (dayOfMonth % 13 == 0).
        if (seasonIndex == 1 && dayOfMonth % 13 == 0)
            return "Storm";

        // All other days: probabilistic (can't determine without GSQ engine).
        return "?";
    }

    private static bool IsFestival(int dayOfMonth, int seasonIndex)
    {
        int[] festivals = seasonIndex switch
        {
            0 => SpringFestivals,
            1 => SummerFestivals,
            2 => FallFestivals,
            3 => WinterFestivals,
            _ => System.Array.Empty<int>()
        };
        foreach (int fd in festivals)
            if (fd == dayOfMonth) return true;
        return false;
    }
}
