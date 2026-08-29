using System.Collections.Generic;
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Sneak Peek Boost (spec 2026-08-28-obtainable-board, section 8): for the season it was bought
    /// in, the Queen of Sauce (any non-Wednesday watch) airs the year-2 episode for the week
    /// (episode + 16), so every year-2 dish has a year-1 route. Wednesday reruns are untouched.
    ///
    /// Patches <c>TV.getWeeklyRecipe()</c> (protected virtual, no parameters). Vanilla computes the
    /// week as <c>DaysPlayed % 224 / 7</c>, which is 1..16 across year 1, and looks that up in
    /// Data/TV/CookingChannel; keys 17..32 are the year-2 episodes.
    ///
    /// This is a PREFIX that skips the original, not a postfix. Vanilla's body itself calls the
    /// private <c>getWeeklyRecipe(channelData, id)</c> overload, and that overload is what adds the
    /// episode's recipe to <c>Game1.player.cookingRecipes</c>. A postfix therefore let the original
    /// grant the year-1 recipe first and then granted the year-2 one on top: one watch, two
    /// recipes (round-1 review, 2026-08-29). Returning false here means the private overload runs
    /// exactly once, for the year-2 id only.
    ///
    /// When the boost does not apply (no boost, Wednesday, week outside 1..16, missing year-2 key)
    /// the prefix returns true and vanilla runs untouched. The Wednesday rerun path is deliberately
    /// NOT re-implemented here; it stays vanilla's, including its
    /// <c>queenOfSauceRerunWeek</c> bookkeeping.
    /// </summary>
    [HarmonyPatch(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new System.Type[0])]
    internal static class QueenOfSaucePatch
    {
        private const int YearOneEpisodes = 16;
        private const int CycleDays = 224;
        private const string RerunDay = "Wed";

        private static System.Reflection.MethodInfo _byId;

        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static bool Prefix(StardewValley.Objects.TV __instance, ref string[] __result)
        {
            if (BoostChecker.SneakPeekActive?.Invoke() != true) return true;
            if (Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == RerunDay) return true;

            int week = (int)(Game1.stats.DaysPlayed % CycleDays / 7);
            if (week < 1 || week > YearOneEpisodes) return true;

            Dictionary<string, string> channel = DataLoader.Tv_CookingChannel(Game1.temporaryContent);
            string key = (week + YearOneEpisodes).ToString();
            if (channel == null || !channel.ContainsKey(key)) return true;

            _byId ??= AccessTools.Method(
                typeof(StardewValley.Objects.TV),
                "getWeeklyRecipe",
                new[] { typeof(Dictionary<string, string>), typeof(string) });
            if (_byId == null) return true;

            string recipeName = channel[key].Split('/')[0];
            if (_byId.Invoke(__instance, new object[] { channel, key }) is not string[] replaced)
                return true;

            __result = replaced;
            PatchLog.Trace($"Sneak Peek: week {week} airs episode {key} ({recipeName})");
            return false;
        }
    }
}
