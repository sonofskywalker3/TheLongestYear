using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Farming bonus / Fishing liability: deterministic per-day modifier on watered crops.
    ///
    /// 2026-05-28 spec update from probabilistic ("25% chance per day to gain/lose a tick") to
    /// deterministic ("hard code in the number of ticks missed for the week and just apply
    /// that programattically"). New model:
    ///   - On day-of-week 2 and 5 of every TLY week (i.e. dayOfMonth-mod-7 = 2 or 5), apply
    ///     the modifier to every watered crop. That's 2 days out of 7 = ~28.6% effect — close
    ///     enough to the original 25% spec, but predictable instead of luck-dependent.
    ///   - <c>crop_growth_up</c>: grant one extra growth tick on the modifier day.
    ///   - <c>crop_growth_down</c>: SKIP today's growth tick (crop neither advances nor
    ///     regresses). 2026-05-28 user correction: "we don't want them to lose some, we just
    ///     don't want them to gain one." Implemented as a prefix that snapshots phase +
    ///     dayOfCurrentPhase, paired with a postfix that restores them — guarantees identical
    ///     state at end-of-day, even at phase-boundary edges where decrement-by-1 would have
    ///     left a phase advance in place.
    ///
    /// The bonus path stays as a postfix add-tick. Note: Farming liability is fish_bite_down
    /// (FishBiteRatePatch), not crop_growth_down — that is the Fishing liability.
    /// </summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.newDay))]
    internal static class CropGrowthPatch
    {
        /// <summary>Days within the TLY week (1-indexed) on which the modifier fires. 2 days
        /// out of 7 = 28.6% effective rate. The pair (2, 5) gives both an early-week and a
        /// late-week hit so the slowdown spreads instead of compressing into a single day.</summary>
        private static bool IsModifierDay(int dayOfMonth)
        {
            int dayOfWeek = ((dayOfMonth - 1) % 7) + 1;
            return dayOfWeek == 2 || dayOfWeek == 5;
        }

        /// <summary>True when crop_growth_down liability should fire on a watered crop this tick.</summary>
        private static bool ShouldSkipTickThisDay(Crop crop, int state)
        {
            if (crop.dead.Value) return false;
            if (crop.fullyGrown.Value) return false;  // fully-grown crops are on a separate dayOfCurrentPhase=harvest cooldown; don't touch
            if (state != 1) return false;  // unwatered: base method already skips growth
            if (!ActiveEffectsProvider.ActiveLiability("crop_growth_down")) return false;
            if (!IsModifierDay(Game1.dayOfMonth)) return false;
            return true;
        }

        // ReSharper disable InconsistentNaming — Harmony convention.

        /// <summary>Snapshot the crop's growth state before <see cref="Crop.newDay"/> runs when
        /// the liability is active. The matching <see cref="Postfix_Restore"/> restores it.</summary>
        private static void Prefix(Crop __instance, int state, out (int phase, int dayOfPhase)? __state)
        {
            __state = null;
            if (!ShouldSkipTickThisDay(__instance, state)) return;
            __state = (__instance.currentPhase.Value, __instance.dayOfCurrentPhase.Value);
        }

        /// <summary>Handle bonus tick (postfix add) and liability restore (postfix snapshot revert).
        /// Bonus runs only when the liability prefix didn't capture state.</summary>
        private static void Postfix(Crop __instance, int state, (int phase, int dayOfPhase)? __state)
        {
            // Liability path: restore snapshot. Crop ends the day exactly where it started
            // — no advance, no regression. Wins over bonus when both are somehow active.
            if (__state.HasValue)
            {
                __instance.currentPhase.Value = __state.Value.phase;
                __instance.dayOfCurrentPhase.Value = __state.Value.dayOfPhase;
                return;
            }

            // Bonus path: grant an extra growth tick on the modifier day.
            if (__instance.dead.Value) return;
            if (__instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0) return;
            if (state != 1) return;
            if (!ActiveEffectsProvider.ActiveBonus("crop_growth_up")) return;
            if (!IsModifierDay(Game1.dayOfMonth)) return;
            if (__instance.fullyGrown.Value) return;

            int maxForPhase = (__instance.phaseDays.Count > 0)
                ? __instance.phaseDays[System.Math.Min(__instance.phaseDays.Count - 1, __instance.currentPhase.Value)]
                : 0;
            __instance.dayOfCurrentPhase.Value = System.Math.Min(
                __instance.dayOfCurrentPhase.Value + 1, maxForPhase);

            if (__instance.dayOfCurrentPhase.Value >= maxForPhase
                && __instance.currentPhase.Value < __instance.phaseDays.Count - 1)
            {
                __instance.currentPhase.Value++;
                __instance.dayOfCurrentPhase.Value = 0;
            }
        }
    }
}
