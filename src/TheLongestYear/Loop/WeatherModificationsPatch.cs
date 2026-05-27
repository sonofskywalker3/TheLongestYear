using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Vanilla <c>Game1.getWeatherModificationsForDate</c> hard-codes Spring day 3 of every
    /// new save to "Rain" (Game1.cs:9557-9560 — the override that introduces the Demetrius
    /// rain-barrel beat). In a time-loop run, the calendar resets to Spring 1 every reset and
    /// <c>stats.DaysPlayed</c> is rewound to 1, so the same forced rain hits day 3 of every
    /// loop — defeating the variety the player expects across runs (user playtest note
    /// 2026-05-27: "it always rains on day 3").
    ///
    /// Postfix restores the seed-driven default when the only reason rain was picked is the
    /// day-3 override. We keep vanilla's other forced rules intact: forced sun on days 1-2
    /// (so the loop opens calm), green-rain, festival, summer storms. Just the one branch is
    /// suppressed.
    /// </summary>
    [HarmonyPatch(typeof(Game1), nameof(Game1.getWeatherModificationsForDate))]
    internal static class WeatherModificationsPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(WorldDate date, string default_weather, ref string __result)
        {
            // Mirror vanilla's day-3 condition exactly (line 9557): DaysPlayed + (date - today) == 3.
            int delta = date.TotalDays - Game1.Date.TotalDays;
            bool wouldHitDay3Rule = (Game1.stats.DaysPlayed + delta) == 3;
            if (!wouldHitDay3Rule || __result != "Rain")
                return;

            // The day-3 rule fired. But: it might still be the right answer if a LATER vanilla
            // override (green rain / festival / passive festival sun) intentionally chose Rain
            // for this day — extremely unlikely on Spring 3 Y1 since those don't apply, but be
            // defensive. Only revert when the seed-driven default is something other than Rain;
            // if the seed itself rolled Rain we let it stand.
            if (default_weather == "Rain")
                return;

            __result = default_weather;
        }
    }
}
