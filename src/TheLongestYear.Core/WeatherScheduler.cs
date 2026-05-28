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

    private const string Sun      = "Sun";
    private const string Rain     = "Rain";
    private const string Storm    = "Storm";
    private const string Snow     = "Snow";
    private const string Festival = "Festival";

    // Vanilla 1.6 festival days per season (ignores SVE / mod festivals — same set as
    // WeatherForecast.SpringFestivals etc. so the two stay in sync).
    private static readonly int[] SpringFestivals = { 13, 24 };
    private static readonly int[] SummerFestivals = { 11, 28 };
    private static readonly int[] FallFestivals   = { 16, 27 };
    private static readonly int[] WinterFestivals = { 8, 25 };

    private const int ForcedSunDay1 = 1;
    private const int ForcedSunDay2 = 2;

    /// <summary>
    /// Build the 28-day weather schedule for a season as a 1-indexed array (index 0 unused).
    /// Result strings are one of: Sun, Rain, Storm, Snow, Festival.
    /// </summary>
    public static string[] BuildSchedule(int uniqueId, int seasonIndex)
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
            case 0: // Spring: ≥2 rain, no storms.
                PlaceN(schedule, available, rng, Rain, 2);
                break;
            case 1: // Summer: ≥2 storms; ≥2 rain.
                PlaceN(schedule, available, rng, Storm, 2);
                PlaceN(schedule, available, rng, Rain, 2);
                break;
            case 2: // Fall: ≥2 rain.
                PlaceN(schedule, available, rng, Rain, 2);
                break;
            case 3: // Winter: ≥2 snow.
                PlaceN(schedule, available, rng, Snow, 2);
                break;
        }

        // Fill remaining open days with Sun.
        for (int d = 1; d <= DaysPerMonth; d++)
            if (schedule[d] == null)
                schedule[d] = Sun;

        return schedule;
    }

    /// <summary>Look up a single day's scheduled weather. Returns null for out-of-range days.</summary>
    public static string? WeatherFor(int uniqueId, int seasonIndex, int dayOfMonth)
    {
        if (dayOfMonth < 1 || dayOfMonth > DaysPerMonth) return null;
        if (seasonIndex < 0 || seasonIndex > 3) return null;
        return BuildSchedule(uniqueId, seasonIndex)[dayOfMonth];
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

    private static int[] FestivalsFor(int seasonIndex) => seasonIndex switch
    {
        0 => SpringFestivals,
        1 => SummerFestivals,
        2 => FallFestivals,
        3 => WinterFestivals,
        _ => Array.Empty<int>()
    };
}
