using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Skips the overnight <see cref="FarmEvent"/> (owl / UFO sound event, meteorite, fairy, witch,
    /// the CC room-restoration scene…) on a night whose morning is a FAIL rewind.
    ///
    /// Why: vanilla nulls <c>Game1.farmEvent</c> one or two ticks BEFORE the post-event warp runs
    /// <c>showEndOfNightStuff</c>, which unconditionally replaces <c>activeClickableMenu</c> with
    /// the <c>SaveGameMenu</c>. <c>SoundInTheNightEvent</c>'s owl branch pauses the game on that
    /// exact tick, so the day-28 driver deterministically opened the Fail scene in the gap, the
    /// save menu then orphaned it, its completion callback never fired, and the reset was silently
    /// dropped — the player woke on Summer 1 (Nexus post by faldans, 2026-08-11; same mechanism as
    /// the June #1b bus-scene race). Whatever the event would have done is rewound anyway, so on a
    /// fail night there is nothing to lose by skipping it. Win/Continue nights keep their events;
    /// the driver's watchdog covers the (rarer) race there.
    /// </summary>
    [HarmonyPatch(typeof(Utility), nameof(Utility.pickFarmEvent))]
    internal static class FarmEventSuppressionPatch
    {
        /// <summary>Set by ModEntry: true when tonight's gate outcome is a FAIL rewind.</summary>
        internal static Func<bool> SuppressTonight;
        internal static IMonitor Monitor;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(ref FarmEvent __result)
        {
            if (!RunActivation.IsActive) return;
            if (__result == null) return;
            if (SuppressTonight == null || !SuppressTonight()) return;

            Monitor?.Log(
                $"Fail loop: suppressed tonight's overnight FarmEvent ({__result.GetType().Name}) — the " +
                "rewind would undo it, and its end-of-event warp would orphan the Fail scene.",
                LogLevel.Info);
            __result = null;
        }
    }
}
