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
    /// Gated on <see cref="Enabled"/> — toggled by ModEntry from <c>_config.Enabled</c> so the
    /// player can disable TLY entirely without lingering weather overrides.
    /// </summary>
    [HarmonyPatch(typeof(Game1), nameof(Game1.getWeatherModificationsForDate))]
    internal static class WeatherModificationsPatch
    {
        /// <summary>Master toggle, set from ModEntry. False keeps vanilla weather behaviour.</summary>
        public static bool Enabled;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(WorldDate date, string default_weather, ref string __result)
        {
            if (!Enabled) return;

            int seasonIndex = (int)date.Season;
            int dayOfMonth = date.DayOfMonth;
            int uniqueId = unchecked((int)Game1.uniqueIDForThisGame);

            string scheduled = WeatherScheduler.WeatherFor(uniqueId, seasonIndex, dayOfMonth);
            if (scheduled == null) return;

            // Festival days: leave vanilla's per-festival weather alone (the scheduler stores
            // "Festival" as a sentinel for those days, but vanilla returns the actual weather
            // string the festival prefers — Sun, Rain, etc. — and we don't want to overwrite it).
            if (scheduled == "Festival") return;

            __result = scheduled;
        }
    }
}
