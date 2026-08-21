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

    /// <summary>Season index for Summer (0=Spring 1=Summer 2=Fall 3=Winter) — the only season
    /// where a green-rain day can be reserved.</summary>
    public const int SummerSeasonIndex = 1;

    private const string Sun       = "Sun";
    private const string Rain      = "Rain";
    private const string Storm     = "Storm";
    private const string Snow      = "Snow";
    private const string Wind      = "Wind";
    private const string Festival  = "Festival";

    /// <summary>Vanilla 1.6's green-rain weather string — public so UI/patch code compares
    /// against the constant instead of re-typing the literal.</summary>
    public const string GreenRain = "GreenRain";

    // Vanilla 1.6 festival days per season (ignores SVE / mod festivals — same set as
    // WeatherForecast.SpringFestivals etc. so the two stay in sync).
    private static readonly int[] SpringFestivals = { 13, 24 };
    private static readonly int[] SummerFestivals = { 11, 28 };
    private static readonly int[] FallFestivals   = { 16, 27 };
    private static readonly int[] WinterFestivals = { 8, 25 };

    // Guaranteed MINIMUMS per season (2026-05-27 design ask: "at least 2 days of rain every season,
    // at least 2 storms in summer, but mix them up every new seed"). Placed first, then every other
    // open day is ROLLED with the same per-loop seeded RNG at vanilla-like odds — so the total number
    // of wet days varies loop to loop, the minimums always hold, and Weather Sage foresight stays
    // exact (same seed → same roll). 0.9.18–0.11.60 filled the remainder with Sun, which turned the
    // minimums into a hard cap of 2 wet days a season ("it never rains", Nexus bug 1107279).
    private const int MinRainDays = 2;
    private const int MinSummerStormDays = 2;
    private const int MinSnowDays = 2;

    // Per-day odds for the random fill (cumulative thresholds, roughly vanilla Data/LocationContexts
    // Default). Spring/Fall: ~18% rain, ~15% wind. Summer: ~10% storm, ~12% rain. Winter: ~63% snow.
    private const double SpringFallRainChance = 0.18;
    private const double SpringFallWindChance = 0.15;
    private const double SummerStormChance = 0.10;
    private const double SummerRainChance = 0.12;
    private const double WinterSnowChance = 0.63;

    private const int ForcedSunDay1 = 1;
    private const int ForcedSunDay2 = 2;

    // Week 1 spans days 1-7; days 1-2 are forced Sun, so the open week-1 window is days 3-7.
    // One special-weather day per season is guaranteed within this window.
    private const int WeekOneLastDay = 7;

    /// <summary>
    /// Build the 28-day weather schedule for a season as a 1-indexed array (index 0 unused).
    /// Result strings are one of: Sun, Rain, Storm, Snow, Wind, Festival, GreenRain.
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
        if (seasonIndex == SummerSeasonIndex && summerGreenRainDay >= 1 && summerGreenRainDay <= DaysPerMonth
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
                PlaceN(schedule, available, rng, Rain, MinRainDays - 1);
                break;
            case 1: // Summer: ≥2 storms; ≥2 rain; one rain in week 1.
                PlaceN(schedule, available, rng, Storm, MinSummerStormDays);
                PlaceOneInWeekOne(schedule, available, rng, Rain);
                PlaceN(schedule, available, rng, Rain, MinRainDays - 1);
                break;
            case 2: // Fall: ≥2 rain; one rain in week 1.
                PlaceOneInWeekOne(schedule, available, rng, Rain);
                PlaceN(schedule, available, rng, Rain, MinRainDays - 1);
                break;
            case 3: // Winter: ≥2 snow; one snow in week 1.
                PlaceOneInWeekOne(schedule, available, rng, Snow);
                PlaceN(schedule, available, rng, Snow, MinSnowDays - 1);
                break;
        }

        // Roll every remaining open day (ascending, so the seeded sequence is stable).
        for (int d = 1; d <= DaysPerMonth; d++)
            if (schedule[d] == null)
                schedule[d] = RollDay(seasonIndex, rng);

        return schedule;
    }

    /// <summary>Look up a single day's scheduled weather. Returns null for out-of-range days.</summary>
    public static string? WeatherFor(int uniqueId, int seasonIndex, int dayOfMonth, int summerGreenRainDay = -1)
    {
        if (dayOfMonth < 1 || dayOfMonth > DaysPerMonth) return null;
        if (seasonIndex < 0 || seasonIndex > 3) return null;
        return BuildSchedule(uniqueId, seasonIndex, summerGreenRainDay)[dayOfMonth];
    }

    /// <summary>One day's random weather for the fill step, at vanilla-like odds per season.</summary>
    private static string RollDay(int seasonIndex, Random rng)
    {
        double r = rng.NextDouble();
        switch (seasonIndex)
        {
            case 0:
            case 2:
                if (r < SpringFallRainChance) return Rain;
                if (r < SpringFallRainChance + SpringFallWindChance) return Wind;
                return Sun;
            case 1:
                if (r < SummerStormChance) return Storm;
                if (r < SummerStormChance + SummerRainChance) return Rain;
                return Sun;
            case 3:
                return r < WinterSnowChance ? Snow : Sun;
            default:
                return Sun;
        }
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
