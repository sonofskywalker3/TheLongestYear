using System;
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
    /// TLY run is active or the week's Randomizer snapshot has the option off.
    /// </summary>
    [HarmonyPatch(typeof(Forest), nameof(Forest.ShouldTravelingMerchantVisitToday))]
    internal static class CartDaysPatch
    {
        private const int DaysPerWeek = 7;

        internal static Func<RunState> RunProvider;

        /// <summary>The week's snapshot via <c>RunController.Randomizer</c>, so a mid-week toggle waits for next week.</summary>
        internal static Func<RandomizerSettings> Settings;

        /// <summary>True when this week's snapshot has random cart days on.</summary>
        internal static bool RandomOn => RunActivation.IsActive && RunProvider != null && Settings != null
            && (Settings()?.RandomCartDays ?? false);

        /// <summary>Cart days for the week containing <paramref name="dayOfMonth"/>, this week's roll.</summary>
        internal static System.Collections.Generic.IReadOnlyList<int> ThisWeekDays(int dayOfMonth)
        {
            RunState run = RunProvider();
            return CartSchedule.ForWeek(run, run.WeekOfYear, Game1.seasonIndex, WeekStart(dayOfMonth), random: true);
        }

        internal static int WeekStart(int dayOfMonth) => ((dayOfMonth - 1) / DaysPerWeek) * DaysPerWeek + 1;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(ref bool __result)
        {
            if (!RandomOn) return;
            __result = ThisWeekDays(Game1.dayOfMonth).Contains(Game1.dayOfMonth);
        }
    }
}
