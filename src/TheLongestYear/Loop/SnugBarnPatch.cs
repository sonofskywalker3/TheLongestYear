using System;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Snug Barn (spec 2026-10-09 power 11): on a day the animals cannot go out (rain, storm,
    /// green rain, any Winter day, TLY's snow day), an animal indoors from 6pm gains its HappinessDrain
    /// every ten minutes at any happiness, as if a Heater were on. Prefix records happiness; the postfix
    /// sets it to before + drain, so vanilla's own rule (and WildcardAnimalPatch's transpiler on the same
    /// method) still runs first and is simply overridden on those evenings. No friendship change.</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.updatePerTenMinutes))]
    internal static class SnugBarnPatch
    {
        private const int EveningStart = 1800;

        /// <summary>Happiness before the vanilla rule ran, or -1 when Snug Barn does not apply.</summary>
        private static void Prefix(FarmAnimal __instance, int timeOfDay, GameLocation environment, out int __state)
        {
            __state = -1;
            if (__instance == null || environment == null || timeOfDay < EveningStart || environment.IsOutdoors) return;
            if (UpgradeChecker.HasUpgrade == null || !Game1.IsMasterGame || !UpgradeChecker.HasUpgrade(AnimalPowers.SnugBarn)) return;
            try
            {
                Farm farm = Game1.getFarm();
                if (farm == null) return;
                if (!AnimalPowers.IsStuckIndoorsDay(farm.IsRainingHere(), WildcardAnimalPatch.WinterOrSnowDay(farm))) return;
                __state = __instance.happiness.Value;
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"SnugBarnPatch prefix: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Postfix(FarmAnimal __instance, int timeOfDay, int __state)
        {
            if (__state < 0 || __instance == null) return;
            int drain = __instance.GetAnimalData()?.HappinessDrain ?? 0;
            int after = AnimalPowers.SnugBarnHappiness(__state, drain);
            if (after == __instance.happiness.Value) return;
            __instance.happiness.Value = (byte)after;
            PatchLog.Trace($"{AnimalPowers.SnugBarn}: {__instance.displayName} happiness {__state} -> {after} at {timeOfDay}.");
        }
    }
}
