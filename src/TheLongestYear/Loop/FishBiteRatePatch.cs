using HarmonyLib;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Fishing bonus (fish_bite_up): reduces time-until-first-bite by ~30%
    /// (multiplier 0.77 applied to calculateTimeUntilFishingBite result).
    /// Farming liability (fish_bite_down): increases time-until-first-bite by ~30%
    /// (multiplier 1.43 — inverse of 0.77, per signed-off spec JC-1).
    /// The private method is patched by name — verified present in Android decompile.
    /// </summary>
    [HarmonyPatch(typeof(FishingRod), "calculateTimeUntilFishingBite")]
    internal static class FishBiteRatePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ref float __result)
        {
            if (ActiveEffectsProvider.ActiveBonus("fish_bite_up"))
                __result *= 0.77f;  // ~30% faster bite

            if (ActiveEffectsProvider.ActiveLiability("fish_bite_down"))
                __result *= 1.43f;  // ~30% slower bite (Farming liability)
        }
    }
}
