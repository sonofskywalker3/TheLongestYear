using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Farming bonus: crop_growth_up — 25% chance to grant one extra growth tick per day.
    /// Fishing liability: crop_growth_down — 25% chance to treat a watered crop as unwatered
    /// for today, losing a day's growth.
    /// Both are postfixes on Crop.newDay so we see the result of the base call first.
    /// Note: Farming liability is fish_bite_down (FishBiteRatePatch), not crop_growth_down.
    /// </summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.newDay))]
    internal static class CropGrowthPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Crop __instance, int state)
        {
            if (__instance.dead.Value) return;
            if (__instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0) return;

            bool isBonus = ActiveEffectsProvider.ActiveBonus("crop_growth_up");
            bool isLiability = ActiveEffectsProvider.ActiveLiability("crop_growth_down");

            if (isBonus && !__instance.fullyGrown.Value && state == 1)
            {
                // 25% chance: grant extra day tick.
                if (Game1.random.NextDouble() < 0.25)
                {
                    __instance.dayOfCurrentPhase.Value = System.Math.Min(
                        __instance.dayOfCurrentPhase.Value + 1,
                        (__instance.phaseDays.Count > 0)
                            ? __instance.phaseDays[System.Math.Min(__instance.phaseDays.Count - 1, __instance.currentPhase.Value)]
                            : 0);

                    if (__instance.dayOfCurrentPhase.Value >= ((__instance.phaseDays.Count > 0)
                            ? __instance.phaseDays[System.Math.Min(__instance.phaseDays.Count - 1, __instance.currentPhase.Value)]
                            : 0)
                        && __instance.currentPhase.Value < __instance.phaseDays.Count - 1)
                    {
                        __instance.currentPhase.Value++;
                        __instance.dayOfCurrentPhase.Value = 0;
                    }
                }
            }
            else if (isLiability && !__instance.fullyGrown.Value && state == 1)
            {
                // 25% chance: undo today's growth advance (revert dayOfCurrentPhase by 1).
                if (Game1.random.NextDouble() < 0.25)
                {
                    __instance.dayOfCurrentPhase.Value = System.Math.Max(0, __instance.dayOfCurrentPhase.Value - 1);
                }
            }
        }
    }
}
