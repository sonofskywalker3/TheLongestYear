using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Seed-driven weather on TLY saves, in two halves:
    ///
    /// <b>1. Schedule writer</b> — postfix on <c>Game1.UpdateWeatherForNewDay</c>. After vanilla has
    /// applied today's weather and rolled a random tomorrow, overwrite TOMORROW with the
    /// <see cref="WeatherScheduler"/> pick. This runs in the morning, so anything that sets
    /// tomorrow's weather LATER in the day — a Rain Totem, CJB Cheats, <c>world_setweather</c> —
    /// wins over the schedule, exactly like vanilla.
    ///
    /// <b>2. Neutraliser</b> — postfix on <c>Game1.getWeatherModificationsForDate</c>. That method
    /// is the morning choke point that turns "tomorrow's weather" into today's, and it carries
    /// three rules that are hostile to a loop that resets <c>stats.DaysPlayed</c> to 1 every
    /// Spring 1: forced Sun while DaysPlayed ≤ 4, forced Rain on DaysPlayed == 3, forced Storm on
    /// Summer 13/26. Those would repeat EVERY loop (the scheduler already places week-1 rain and
    /// summer storms itself), so when vanilla's answer is explained purely by one of them we hand
    /// back the incoming value instead. Real overrides — festival days, weddings, green rain,
    /// passive-festival Sun — pass through untouched.
    ///
    /// History: 0.9.18–0.11.60 returned the schedule from half 2 unconditionally, which (a)
    /// discarded every Rain Totem / CJB / console write the next morning and (b) combined with
    /// the scheduler's all-Sun fill, meant a TLY save saw exactly 2 rain days a season and CJB
    /// reported "the game forces tomorrow's weather for sun" every day (Nexus bugs 1107279,
    /// 1116791). Gated on <see cref="RunActivation.IsActive"/> so non-TLY saves keep vanilla.
    /// </summary>
    [HarmonyPatch(typeof(Game1), nameof(Game1.UpdateWeatherForNewDay))]
    internal static class WeatherScheduleWriterPatch
    {
        private const string DefaultContext = "Default";
        private const string FestivalSentinel = "Festival";

        internal static IMonitor Monitor;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix()
        {
            if (!RunActivation.IsActive) return;
            if (!Game1.IsMasterGame) return;

            var tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays++;
            string scheduled = ScheduledFor(tomorrow);
            if (scheduled == null || scheduled == FestivalSentinel) return; // festivals: vanilla decides

            Game1.weatherForTomorrow = scheduled;
            Game1.netWorldState.Value.WeatherForTomorrow = scheduled;
            Game1.netWorldState.Value.GetWeatherForLocation(DefaultContext).WeatherForTomorrow = scheduled;
            Monitor?.Log($"Weather: scheduled {scheduled} for {tomorrow.Season} {tomorrow.DayOfMonth}.", LogLevel.Trace);
        }

        /// <summary>The scheduler's pick for a date under the current loop seed, or null.</summary>
        internal static string ScheduledFor(WorldDate date)
        {
            int seasonIndex = (int)date.Season;
            int uniqueId = unchecked((int)Game1.uniqueIDForThisGame);
            // Vanilla 1.6 green rain is seeded on (year, uniqueIDForThisGame) with game RNG the
            // pure scheduler can't replicate; resolve it game-side and let the scheduler reserve
            // the day so the summer storm/rain minimums place around it (khauser13 2026-06-11).
            int greenRainDay = seasonIndex == WeatherScheduler.SummerSeasonIndex
                ? GreenRainDay.VanillaSummerDay()
                : -1;
            return WeatherScheduler.WeatherFor(uniqueId, seasonIndex, date.DayOfMonth, greenRainDay);
        }
    }

    [HarmonyPatch(typeof(Game1), nameof(Game1.getWeatherModificationsForDate))]
    internal static class WeatherModificationsPatch
    {
        private const int ForcedSunThroughDaysPlayed = 4;   // vanilla: stats.DaysPlayed + offset <= 4 → Sun
        private const int ForcedRainDaysPlayed = 3;         // vanilla: stats.DaysPlayed + offset == 3 → Rain
        private const int SummerStormDayModulus = 13;       // vanilla: Summer day % 13 == 0 → Storm
        private const string Sun = "Sun";
        private const string Rain = "Rain";
        private const string Storm = "Storm";

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(WorldDate date, string default_weather, ref string __result)
        {
            if (!RunActivation.IsActive) return;
            if (__result == default_weather) return;            // vanilla changed nothing

            int daysPlayed = (int)Game1.stats.DaysPlayed + (date.TotalDays - Game1.Date.TotalDays);
            // Day 1 of a month is forced Sun in vanilla regardless of DaysPlayed — keep that parity
            // (the scheduler forces days 1-2 Sun anyway).
            bool hostileSun = date.DayOfMonth != 1 && daysPlayed <= ForcedSunThroughDaysPlayed && __result == Sun;
            bool hostileRain = daysPlayed == ForcedRainDaysPlayed && __result == Rain;
            bool hostileStorm = date.Season == StardewValley.Season.Summer
                && date.DayOfMonth % SummerStormDayModulus == 0 && __result == Storm;
            if (!(hostileSun || hostileRain || hostileStorm)) return;

            // A later vanilla rule may have produced the same string legitimately — never undo those.
            if (Utility.isFestivalDay(date.DayOfMonth, date.Season)) return;
            if (Utility.isGreenRainDay(date.DayOfMonth, date.Season)) return;

            __result = default_weather;
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
