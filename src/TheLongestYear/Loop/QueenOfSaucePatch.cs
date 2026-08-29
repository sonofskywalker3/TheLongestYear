using System.Collections.Generic;
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Sneak Peek Boost (spec 2026-08-28-obtainable-board, section 8): for the season it was bought
    /// in, the Sunday Queen of Sauce airs the year-2 episode for the week (episode + 16), so every
    /// year-2 dish has a year-1 route. Wednesday reruns are untouched.
    ///
    /// Patches <c>TV.getWeeklyRecipe()</c> (protected virtual, no parameters). Vanilla computes the
    /// week as <c>DaysPlayed % 224 / 7</c>, which is 1..16 across year 1, and looks that up in
    /// Data/TV/CookingChannel; keys 17..32 are the year-2 episodes. The postfix re-runs vanilla's
    /// own private <c>getWeeklyRecipe(channelData, id)</c> overload through AccessTools so the
    /// dialogue strings and the cookingRecipes grant stay exactly vanilla's.
    /// </summary>
    [HarmonyPatch(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new System.Type[0])]
    internal static class QueenOfSaucePatch
    {
        private const int YearOneEpisodes = 16;
        private const int CycleDays = 224;
        private const string RerunDay = "Wed";

        private static System.Reflection.MethodInfo _byId;

        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void Postfix(StardewValley.Objects.TV __instance, ref string[] __result)
        {
            if (BoostChecker.SneakPeekActive?.Invoke() != true) return;
            if (Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == RerunDay) return;

            int week = (int)(Game1.stats.DaysPlayed % CycleDays / 7);
            if (week < 1 || week > YearOneEpisodes) return;

            Dictionary<string, string> channel = DataLoader.Tv_CookingChannel(Game1.temporaryContent);
            string key = (week + YearOneEpisodes).ToString();
            if (channel == null || !channel.ContainsKey(key)) return;

            _byId ??= AccessTools.Method(
                typeof(StardewValley.Objects.TV),
                "getWeeklyRecipe",
                new[] { typeof(Dictionary<string, string>), typeof(string) });
            if (_byId == null) return;

            string recipeName = channel[key].Split('/')[0];
            if (_byId.Invoke(__instance, new object[] { channel, key }) is string[] replaced)
            {
                __result = replaced;
                PatchLog.Trace($"Sneak Peek: week {week} airs episode {key} ({recipeName})");
            }
        }
    }
}
