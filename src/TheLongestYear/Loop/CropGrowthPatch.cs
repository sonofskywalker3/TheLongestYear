using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Farming bonus / Fishing liability: probabilistic per-crop-per-day modifier on watered
    /// crops.
    ///
    /// 2026-05-29 spec update (rebalance): the deterministic days-2-and-5 model is replaced
    /// with an independent 20% per crop per day roll for both bonus and liability — user
    /// preferred the variance ("I kinda like the chance of having each plant have a 20%
    /// chance to grow 2 days worth, or a 20% chance to grow 0 days worth each day for the
    /// liability"). Average rate is similar (20% per day per crop = ~1.4 days/week affected
    /// per crop), but the effect is spread across the whole week and per-crop independent
    /// instead of week-wide.
    ///
    ///   - <c>crop_growth_up</c>: 20% chance per watered crop per day to advance growth one
    ///     extra tick (i.e. that crop got 2 days' worth of growth in 1 day).
    ///   - <c>crop_growth_down</c>: 20% chance per watered crop per day to SKIP today's
    ///     growth tick (the crop neither advances nor regresses). 2026-05-28 user correction
    ///     still applies: "we don't want them to lose some, we just don't want them to gain
    ///     one." Implemented as a prefix that snapshots phase + dayOfCurrentPhase paired with
    ///     a postfix that restores them.
    ///
    /// Note: Farming liability is fish_bite_down (FishBiteRatePatch), not crop_growth_down
    /// — crop_growth_down is the Fishing liability.
    ///
    /// Stacking with passive accelerators: this patch and <see cref="GreenThumbPatch"/> both
    /// postfix Crop.newDay and roll independently. A Farming-week Green Thumb V owner gets
    /// 20% (theme) + 25% (passive) = up to 45% chance per crop per day to gain an extra
    /// growth day. Intentional — the whole point of the accelerator chains is to compound.
    /// </summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.newDay))]
    internal static class CropGrowthPatch
    {
        /// <summary>Per-crop-per-day probability for both bonus and liability rolls.</summary>
        private const double RollChance = 0.20;

        /// <summary>True when crop_growth_down liability should fire on a watered crop this tick.</summary>
        private static bool ShouldSkipTickThisDay(Crop crop, int state)
        {
            if (crop.dead.Value) return false;
            if (crop.fullyGrown.Value) return false;  // fully-grown crops are on a separate dayOfCurrentPhase=harvest cooldown; don't touch
            if (state != 1) return false;  // unwatered: base method already skips growth
            if (!ActiveEffectsProvider.ActiveLiability("crop_growth_down")) return false;
            return Game1.random.NextDouble() < RollChance;
        }

        // ReSharper disable InconsistentNaming — Harmony convention.

        /// <summary>Snapshot the crop's growth state before <see cref="Crop.newDay"/> runs when
        /// the liability roll succeeds. The postfix restores from the snapshot.</summary>
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

            // Bonus path: 20% chance per crop per day to grant an extra growth tick.
            if (__instance.dead.Value) return;
            if (__instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0) return;
            if (state != 1) return;
            if (!ActiveEffectsProvider.ActiveBonus("crop_growth_up")) return;
            if (Game1.random.NextDouble() >= RollChance) return;
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
