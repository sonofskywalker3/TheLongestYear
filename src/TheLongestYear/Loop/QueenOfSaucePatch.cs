using System.Collections.Generic;
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Sneak Peek Boost (spec 2026-08-28-obtainable-board, section 8): for the season it was bought
    /// in, the Queen of Sauce's WEDNESDAY slot stops being a rerun and airs the year-2 version of
    /// the episode that aired the Sunday just gone, so every year-2 dish has a year-1 route.
    ///
    /// Jeff, 2026-09-21. This replaced the original Sunday implementation, which aired the year-2
    /// episode on Sunday and quietly taught the year-1 recipe alongside it: the TV said one recipe
    /// and granted two, which is what a player reported as confusing. Two rulings shaped what
    /// replaced it — take the rerun slot rather than add a channel, and air the PRIOR Sunday's
    /// episode rather than the coming one, so the Boost never disagrees with the air dates a player
    /// looks up on the wiki.
    ///
    /// The week arithmetic falls out for free. Vanilla computes the week as
    /// <c>DaysPlayed % 224 / 7</c>, which is 1..16 across year 1 and only lands on the airing
    /// episode from its Sunday onward. Wednesdays are days 3/10/17/24 of a season, so on a
    /// Wednesday that expression already names the week of the Sunday just gone — no offset needed.
    /// Keys 17..32 are the year-2 episodes, so the episode to air is <c>week + 16</c>.
    ///
    /// The year's Wednesdays therefore cover weeks 1..15, or episodes 17..31. Week 16's episode
    /// (32, Shrimp Cocktail) has no Wednesday after it inside a one-year run and is unreachable;
    /// <see cref="TheLongestYear.Core.AvailabilityWeeks.YearTwoLastReachableEpisode"/> and the
    /// (O)733 ban in ItemPoolBuilder are the two places that encode that.
    ///
    /// Patches <c>TV.getWeeklyRecipe()</c> (protected virtual, no parameters) as a PREFIX that
    /// skips the original. Vanilla's body calls the private <c>getWeeklyRecipe(channelData, id)</c>
    /// overload, and THAT overload is what adds the recipe to <c>Game1.player.cookingRecipes</c>
    /// and builds the dialogue. Returning false means we choose which id it sees: exactly one, the
    /// year-2 key, so the TV's "you learned X" line and what lands in the cookbook are the same
    /// dish. Sunday is never touched now, so the week's normal episode is plain vanilla.
    ///
    /// Skipping the original also skips vanilla's <c>queenOfSauceRerunWeek</c> bookkeeping for that
    /// day, which is correct: there is no rerun on a Wednesday the Boost has taken over. The
    /// player gives up their reruns for the season, which the Boost's description says plainly.
    ///
    /// If the reflective call throws, the prefix warns and returns true, so the watch degrades to a
    /// plain vanilla rerun rather than eating the episode.
    /// </summary>
    [HarmonyPatch(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new System.Type[0])]
    internal static class QueenOfSaucePatch
    {
        private const int YearOneEpisodes = 16;
        private const int CycleDays = 224;

        /// <summary>The day the Boost takes over: vanilla's rerun slot.</summary>
        private const string PreviewDay = "Wed";

        /// <summary>The last week whose Wednesday can air a year-2 episode. Week 16's Wednesday
        /// would fall after Winter 28, which no one-year run has.</summary>
        private const int LastPreviewWeek = 15;

        private static System.Reflection.MethodInfo _byId;

        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static bool Prefix(StardewValley.Objects.TV __instance, ref string[] __result)
        {
            if (BoostChecker.SneakPeekActive?.Invoke() != true) return true;
            if (Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) != PreviewDay) return true;

            int week = (int)(Game1.stats.DaysPlayed % CycleDays / 7);
            if (week < 1 || week > LastPreviewWeek) return true;

            Dictionary<string, string> channel = DataLoader.Tv_CookingChannel(Game1.temporaryContent);
            string key = (week + YearOneEpisodes).ToString();
            if (channel == null || !channel.ContainsKey(key)) return true;

            _byId ??= AccessTools.Method(
                typeof(StardewValley.Objects.TV),
                "getWeeklyRecipe",
                new[] { typeof(Dictionary<string, string>), typeof(string) });
            if (_byId == null) return true;

            string[] replaced;
            try
            {
                if (_byId.Invoke(__instance, new object[] { channel, key }) is not string[] result)
                    return true;
                replaced = result;
            }
            catch (System.Exception ex)
            {
                PatchLog.Warn($"Sneak Peek: getWeeklyRecipe failed ({ex.Message}); vanilla rerun runs");
                return true;
            }

            string recipeName = channel[key].Split('/')[0];
            __result = replaced;
            PatchLog.Trace($"Sneak Peek: week {week} Wednesday airs episode {key} ({recipeName}) in place of the rerun");
            return false;
        }
    }
}
