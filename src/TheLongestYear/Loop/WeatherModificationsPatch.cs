using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Replaces vanilla's day-by-day weather rolling with the seed-driven
    /// <see cref="WeatherScheduler"/>. Each season's schedule guarantees minimums (≥2 rain
    /// in Spring/Fall, ≥2 storm + ≥2 rain in Summer, ≥2 snow in Winter) and varies across
    /// loops because the reset rotates <c>Game1.uniqueIDForThisGame</c>.
    ///
    /// 2026-05-27 playtest ask: "at least 2 days of rain every season, at least 2 storms in
    /// summer, but mix them up every new seed." Also subsumes the prior day-3 forced-rain
    /// revert (Game1.cs:9557 — the rain-barrel intro override that repeated every loop) and
    /// the Summer 13/26 forced storms — the scheduler chooses those days itself.
    ///
    /// Postfix runs after every vanilla override has been applied. It returns the scheduler's
    /// choice unconditionally for non-festival days; festival days fall through with vanilla's
    /// result intact so per-festival weather (which can vary by festival) stays correct.
    ///
    /// Gated on <see cref="RunActivation.IsActive"/> so a non-TLY save (or a disabled mod) keeps
    /// vanilla weather behaviour with no lingering overrides.
    /// </summary>
    [HarmonyPatch(typeof(Game1), nameof(Game1.getWeatherModificationsForDate))]
    internal static class WeatherModificationsPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(WorldDate date, string default_weather, ref string __result)
        {
            if (!RunActivation.IsActive) return;

            int seasonIndex = (int)date.Season;
            int dayOfMonth = date.DayOfMonth;
            int uniqueId = unchecked((int)Game1.uniqueIDForThisGame);

            // Vanilla 1.6 green rain: the original method already set __result = "GreenRain" for
            // this year's pick, but returning the schedule unconditionally overwrote it — green
            // rain NEVER fired on a TLY save (khauser13 2026-06-11). Resolve the vanilla pick and
            // hand it to the scheduler, which reserves the day so the ≥2-storm/≥2-rain summer
            // minimums still place around it. The pick is seeded on (year, uniqueIDForThisGame),
            // so it lands on a different day each loop for free.
            int greenRainDay = seasonIndex == 1 ? GreenRainDay.VanillaSummerDay() : -1;

            string scheduled = WeatherScheduler.WeatherFor(uniqueId, seasonIndex, dayOfMonth, greenRainDay);
            if (scheduled == null) return;

            // Festival days: leave vanilla's per-festival weather alone (the scheduler stores
            // "Festival" as a sentinel for those days, but vanilla returns the actual weather
            // string the festival prefers — Sun, Rain, etc. — and we don't want to overwrite it).
            if (scheduled == "Festival") return;

            __result = scheduled;
        }
    }

    /// <summary>Resolves vanilla's summer green-rain day for the current year/seed.
    /// <c>Utility.isGreenRainDay</c> seeds on <c>(Game1.year * 777, Game1.uniqueIDForThisGame)</c>
    /// with game RNG the pure scheduler can't replicate, so the day is found game-side (by asking
    /// vanilla about each candidate day) and passed in as data.</summary>
    internal static class GreenRainDay
    {
        // Vanilla's candidate set (Utility.isGreenRainDay) — only these days can ever match.
        private static readonly int[] VanillaOptions = { 5, 6, 7, 14, 15, 16, 18, 23 };

        /// <summary>This year's green-rain day of summer, or -1 if none resolves.</summary>
        internal static int VanillaSummerDay()
        {
            foreach (int day in VanillaOptions)
            {
                if (Utility.isGreenRainDay(day, StardewValley.Season.Summer))
                    return day;
            }
            return -1;
        }
    }
}
