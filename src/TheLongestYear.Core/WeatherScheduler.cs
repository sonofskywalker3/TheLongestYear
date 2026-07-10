using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Per-season weather schedule with deterministic seed-driven placement of minimum rain/storm/
/// snow days. Built on demand per <c>(uniqueId, seasonIndex)</c> — same seed + same season =
/// same 28-day schedule. Vanilla rolls weather day-by-day with no guarantees, and the hard-
/// coded forced days (Spring 3 Y1 = Rain, Summer 13/26 = Storm) repeat every loop. This
/// scheduler replaces both: each season hits its design-spec minimums (≥2 rain in
/// Spring/Fall, ≥2 storm + ≥2 rain in Summer, ≥2 snow in Winter), and the placement varies
/// across loops because the per-loop reset rotates <c>Game1.uniqueIDForThisGame</c>.
///
/// Rules:
///   - Days 1 + 2 of every season force Sun (vanilla parity: the loop opens calmly).
///   - Each season guarantees ONE of its special-weather days lands in week 1 (days 3-7) at a
///     random position. This replaces vanilla's fixed "Spring 3 = Rain" — players still get an
///     early watering/weather day, but never the same day every loop (beta feedback, u/Tutorem).
///   - Festival days are returned as the literal string <c>"Festival"</c>; the live patch
///     leaves vanilla's per-festival weather alone for those days.
///   - The remaining open days are filled with the season's required special weather first,
///     then Sun.
///
/// Pure — no Stardew refs. All inputs flow in as parameters so this is testable + reusable
/// by <see cref="WeatherForecast"/> for the planning-hub preview.
/// </summary>
public static class WeatherScheduler
{
    public const int DaysPerMonth = 28;

    private const string Sun       = "Sun";
    private const string Rain      = "Rain";
    private const string Storm     = "Storm";
    private const string Snow      = "Snow";
    private const string Festival  = "Festival";
    private const string GreenRain = "GreenRain";

    // Vanilla 1.6 festival days per season (ignores SVE / mod festivals — same set as
    // WeatherForecast.SpringFestivals etc. so the two stay in sync).
    private static readonly int[] SpringFestivals = { 13, 24 };
    private static readonly int[] SummerFestivals = { 11, 28 };
    private static readonly int[] FallFestivals   = { 16, 27 };
    private static readonly int[] WinterFestivals = { 8, 25 };

    private const int ForcedSunDay1 = 1;
    private const int ForcedSunDay2 = 2;

    // Week 1 spans days 1-7; days 1-2 are forced Sun, so the open week-1 window is days 3-7.
    // One special-weather day per season is guaranteed within this window.
    private const int WeekOneLastDay = 7;

    /// <summary>
    /// Build the 28-day weather schedule for a season as a 1-indexed array (index 0 unused).
    /// Result strings are one of: Sun, Rain, Storm, Snow, Festival, GreenRain.
    /// </summary>
    /// <param name="summerGreenRainDay">Vanilla 1.6's green-rain day for this year's summer
    /// (one of 5/6/7/14/15/16/18/23, from <c>Utility.isGreenRainDay</c>), or -1 for none.
    /// Flows in from the caller because the vanilla pick uses game RNG this pure class can't
    /// replicate. Ignored outside summer. Reserved like a festival day so the storm/rain
    /// minimums place around it — the scheduler used to overwrite vanilla's green-rain
    /// override entirely, so green rain never fired on a TLY save (khauser13 2026-06-11).</param>
    public static string[] BuildSchedule(int uniqueId, int seasonIndex, int summerGreenRainDay = -1)
    {
        var schedule = new string[DaysPerMonth + 1];
        int[] festivals = FestivalsFor(seasonIndex);

        // Forced sun days (1-2).
        schedule[ForcedSunDay1] = Sun;
        schedule[ForcedSunDay2] = Sun;

        // Festival days.
        foreach (int d in festivals)
            if (d >= 1 && d <= DaysPerMonth)
                schedule[d] = Festival;

        // Green rain day (summer only). Forced-sun and festival days win — mirrors vanilla's
        // override order in getWeatherModificationsForDate, where the festival check runs after
        // the green-rain check. (Vanilla's options never collide with either in practice.)
        if (seasonIndex == 1 && summerGreenRainDay >= 1 && summerGreenRainDay <= DaysPerMonth
            && schedule[summerGreenRainDay] == null)
        {
            schedule[summerGreenRainDay] = GreenRain;
        }

        // Open day pool: every unfilled day. Sorted ascending for deterministic ordering
        // before the seeded shuffle below.
        var available = new List<int>();
        for (int d = 1; d <= DaysPerMonth; d++)
            if (schedule[d] == null)
                available.Add(d);

        // Deterministic RNG keyed on (uniqueId, seasonIndex). The 0x9E3779B1 mix const is
        // Knuth's golden-ratio multiplicative hash — keeps season indices from cancelling
        // simple-id seeds (e.g. uniqueId 0 still varies by season).
        var rng = new Random(unchecked(uniqueId ^ (seasonIndex * (int)0x9E3779B1)));

        switch (seasonIndex)
        {
            case 0: // Spring: ≥2 rain, no storms; one rain in week 1.
                PlaceOneInWeekOne(schedule, available, rng, Rain);
                PlaceN(schedule, available, rng, Rain, 1);
                break;
            case 1: // Summer: ≥2 storms; ≥2 rain; one rain in week 1.
                PlaceN(schedule, available, rng, Storm, 2);
                PlaceOneInWeekOne(schedule, available, rng, Rain);
                PlaceN(schedule, available, rng, Rain, 1);
                break;
            case 2: // Fall: ≥2 rain; one rain in week 1.
                PlaceOneInWeekOne(schedule, available, rng, Rain);
                PlaceN(schedule, available, rng, Rain, 1);
                break;
            case 3: // Winter: ≥2 snow; one snow in week 1.
                PlaceOneInWeekOne(schedule, available, rng, Snow);
                PlaceN(schedule, available, rng, Snow, 1);
                break;
        }

        // Fill remaining open days with Sun.
        for (int d = 1; d <= DaysPerMonth; d++)
            if (schedule[d] == null)
                schedule[d] = Sun;

        return schedule;
    }

    /// <summary>Look up a single day's scheduled weather. Returns null for out-of-range days.</summary>
    public static string? WeatherFor(int uniqueId, int seasonIndex, int dayOfMonth, int summerGreenRainDay = -1)
    {
        if (dayOfMonth < 1 || dayOfMonth > DaysPerMonth) return null;
        if (seasonIndex < 0 || seasonIndex > 3) return null;
        return BuildSchedule(uniqueId, seasonIndex, summerGreenRainDay)[dayOfMonth];
    }

    private static void PlaceN(string[] schedule, List<int> available, Random rng, string weather, int n)
    {
        for (int i = 0; i < n && available.Count > 0; i++)
        {
            int idx = rng.Next(available.Count);
            int day = available[idx];
            schedule[day] = weather;
            available.RemoveAt(idx);
        }
    }

    /// <summary>
    /// Place one <paramref name="weather"/> day in week 1 (open days ≤ <see cref="WeekOneLastDay"/>),
    /// chosen at random among those days. Falls back to a normal anywhere-placement if week 1 has
    /// no open days left (shouldn't happen — days 3-7 are always free after forced-Sun + festivals).
    /// </summary>
    private static void PlaceOneInWeekOne(string[] schedule, List<int> available, Random rng, string weather)
    {
        var weekOneSlots = new List<int>();
        foreach (int day in available)
            if (day <= WeekOneLastDay)
                weekOneSlots.Add(day);

        if (weekOneSlots.Count == 0)
        {
            PlaceN(schedule, available, rng, weather, 1);
            return;
        }

        int day1 = weekOneSlots[rng.Next(weekOneSlots.Count)];
        schedule[day1] = weather;
        available.Remove(day1);
    }

    private static int[] FestivalsFor(int seasonIndex) => seasonIndex switch
    {
        0 => SpringFestivals,
        1 => SummerFestivals,
        2 => FallFestivals,
        3 => WinterFestivals,
        _ => Array.Empty<int>()
    };
}
