namespace TheLongestYear.Core;

/// <summary>
/// A single forecast slot: the calendar day it lands on plus its scheduled weather.
/// Carries <see cref="DayOfMonth"/> so the UI can label rows by day-of-month
/// (e.g. "Day 26: Rain") without re-deriving the date.
/// </summary>
/// <param name="SeasonIndex">0=Spring 1=Summer 2=Fall 3=Winter</param>
/// <param name="DayOfMonth">1-28</param>
/// <param name="Weather">"Sun", "Rain", "Storm", "Snow", "Festival", "GreenRain" (or "?" if unschedulable)</param>
public readonly record struct ForecastDay(int SeasonIndex, int DayOfMonth, string Weather);

/// <summary>
/// Deterministic weather forecast for the next N days, sourced from
/// <see cref="WeatherScheduler"/>. The scheduler decides each day of every season at
/// build time (seed-keyed), so the Weather Sage preview can show concrete values
/// instead of the prior "?" placeholders for probabilistic days.
/// </summary>
public static class WeatherForecast
{
    /// <summary>
    /// Build the N-slot rolling forecast starting from <em>tomorrow</em> (today is excluded —
    /// its weather is already locked in). Each slot records the calendar day it lands on so the
    /// caller can label by day-of-month.
    /// </summary>
    /// <param name="uniqueId">Game1.uniqueIDForThisGame (the per-loop master seed)</param>
    /// <param name="daysPlayedToday">Game1.stats.DaysPlayed today — kept for signature compat
    /// with older callers; not used in the scheduler-driven path.</param>
    /// <param name="currentDayOfMonth">today's day-of-month (1-28)</param>
    /// <param name="currentSeasonIndex">0=Spring 1=Summer 2=Fall 3=Winter</param>
    /// <param name="slotsToReveal">how many days forward to compute (from Weather Sage tier)</param>
    /// <param name="summerGreenRainDay">this year's summer green-rain day (vanilla pick, resolved
    /// game-side), or -1; applied to any summer day the forecast window covers</param>
    public static ForecastDay[] Build(int uniqueId, int daysPlayedToday, int currentDayOfMonth,
        int currentSeasonIndex, int slotsToReveal, int summerGreenRainDay = -1)
    {
        var result = new ForecastDay[slotsToReveal];
        int dayOfMonth = currentDayOfMonth;
        int seasonIndex = currentSeasonIndex;

        for (int slot = 0; slot < slotsToReveal; slot++)
        {
            // Advance one day (slot 0 = tomorrow).
            dayOfMonth++;
            if (dayOfMonth > WeatherScheduler.DaysPerMonth)
            {
                dayOfMonth = 1;
                seasonIndex = (seasonIndex + 1) % 4;
            }

            string scheduled = WeatherScheduler.WeatherFor(uniqueId, seasonIndex, dayOfMonth, summerGreenRainDay) ?? "?";
            result[slot] = new ForecastDay(seasonIndex, dayOfMonth, scheduled);
        }

        return result;
    }
}
