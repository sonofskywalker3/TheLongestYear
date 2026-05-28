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
    ///   - <c>crop_growth_down</c>: revert today's growth advance on the modifier day.
    ///
    /// Both branches postfix <see cref="Crop.newDay"/> so we see the result of the base call
    /// first. Note: Farming liability is fish_bite_down (FishBiteRatePatch), not
    /// crop_growth_down — that is the Fishing liability.
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

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Crop __instance, int state)
        {
            if (__instance.dead.Value) return;
            if (__instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0) return;
            if (state != 1) return;  // unwatered crops don't tick — nothing for us to modify

            bool isBonus = ActiveEffectsProvider.ActiveBonus("crop_growth_up");
            bool isLiability = ActiveEffectsProvider.ActiveLiability("crop_growth_down");
            if (!isBonus && !isLiability) return;

            if (!IsModifierDay(Game1.dayOfMonth)) return;

            if (isBonus && !__instance.fullyGrown.Value)
            {
                // Grant an extra growth tick: bump dayOfCurrentPhase, advance phase if full.
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
            else if (isLiability && !__instance.fullyGrown.Value)
            {
                // Revert today's growth advance (decrement dayOfCurrentPhase by 1).
                __instance.dayOfCurrentPhase.Value = System.Math.Max(0, __instance.dayOfCurrentPhase.Value - 1);
            }
        }
    }
}
