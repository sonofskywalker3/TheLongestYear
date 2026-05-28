namespace TheLongestYear.Core;

/// <summary>
/// Deterministic weather forecast for the next N days, sourced from
/// <see cref="WeatherScheduler"/>. The scheduler decides each day of every season at
/// build time (seed-keyed), so the planning-hub Weather Sage preview can show concrete
/// values instead of the prior "?" placeholders for probabilistic days.
/// </summary>
public static class WeatherForecast
{
    /// <summary>
    /// Build the N-slot forecast starting from tomorrow (relative to the current game state).
    /// Returns array of weather label strings: "Sun", "Rain", "Storm", "Snow", "Festival".
    /// </summary>
    /// <param name="uniqueId">Game1.uniqueIDForThisGame (the per-loop master seed)</param>
    /// <param name="daysPlayedToday">Game1.stats.DaysPlayed today — kept for signature compat
    /// with older callers; not used in the scheduler-driven path.</param>
    /// <param name="currentDayOfMonth">today's day-of-month (1-28)</param>
    /// <param name="currentSeasonIndex">0=Spring 1=Summer 2=Fall 3=Winter</param>
    /// <param name="slotsToReveal">how many days forward to compute (from Weather Sage tier)</param>
    public static string[] Build(int uniqueId, int daysPlayedToday, int currentDayOfMonth,
        int currentSeasonIndex, int slotsToReveal)
    {
        var result = new string[slotsToReveal];
        int dayOfMonth = currentDayOfMonth;
        int seasonIndex = currentSeasonIndex;

        for (int slot = 0; slot < slotsToReveal; slot++)
        {
            // Advance one day.
            dayOfMonth++;
            if (dayOfMonth > WeatherScheduler.DaysPerMonth)
            {
                dayOfMonth = 1;
                seasonIndex = (seasonIndex + 1) % 4;
            }

            string scheduled = WeatherScheduler.WeatherFor(uniqueId, seasonIndex, dayOfMonth) ?? "?";
            result[slot] = scheduled;
        }

        return result;
    }
}
