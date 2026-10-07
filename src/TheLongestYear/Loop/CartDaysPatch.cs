using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Random Cart Days: replaces the vanilla Fri/Sun rule in
    /// <c>Forest.ShouldTravelingMerchantVisitToday</c> with the week's rolled days. Dormant when no
    /// TLY run is active or the week's Randomizer snapshot has the option off. The week always comes
    /// from the game date: vanilla calls this during the night transition, before TLY advances
    /// Run.WeekOfYear.
    /// </summary>
    [HarmonyPatch(typeof(Forest), nameof(Forest.ShouldTravelingMerchantVisitToday))]
    internal static class CartDaysPatch
    {
        internal static Func<RunState> RunProvider;

        /// <summary>Settings for a week of year (RunController.RandomizerForWeekPeek).</summary>
        internal static Func<int, RandomizerSettings> Settings;

        /// <summary>True when the week containing this date has random cart days on.</summary>
        internal static bool RandomOn(int seasonIndex, int dayOfMonth) => RunActivation.IsActive
            && RunProvider != null && Settings != null && RunProvider() != null
            && (Settings(Calendar.WeekOfYear(seasonIndex, dayOfMonth))?.RandomCartDays ?? false);

        /// <summary>Cart days for the week containing the date (stored roll).</summary>
        internal static IReadOnlyList<int> DaysFor(int seasonIndex, int dayOfMonth)
            => CartSchedule.ForWeek(RunProvider(), seasonIndex, dayOfMonth, random: true);

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(ref bool __result)
        {
            if (!RandomOn(Game1.seasonIndex, Game1.dayOfMonth)) return;
            __result = DaysFor(Game1.seasonIndex, Game1.dayOfMonth).Contains(Game1.dayOfMonth);
        }
    }
}
