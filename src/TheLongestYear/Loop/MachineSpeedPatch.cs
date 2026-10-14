using System.Linq;
using HarmonyLib;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Artisan bonus (machines_fast), Spelunking liability (machines_slow) and Fast Hatch
    /// (animal_fast_hatch, spec 2026-10-09 power 8). Postfix on Object.OutputMachine (decompile
    /// Object.cs:2481), which is where MinutesUntilReady is set for every data-driven machine (kegs,
    /// jars, casks, bee houses, tappers, smokers, incubators). Scales the queued time by
    /// MachineReadyTime.Factor, rounded to 10 minutes, floor 10. For an incubator the time was already
    /// set by OutputIncubator and Coopmaster's x0.5, so Fast Hatch multiplies on top of both.</summary>
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.OutputMachine))]
    internal static class MachineSpeedPatch
    {
        public const string BonusId = "machines_fast";
        public const string LiabilityId = "machines_slow";

        private static void Postfix(StardewValley.Object __instance, bool probe, bool heldObjectOnly, bool __result)
        {
            if (!__result || probe || heldObjectOnly || __instance == null) return;
            int before = __instance.MinutesUntilReady;
            if (before <= 0) return;

            int fastStacks = ActiveEffectsProvider.BonusStacks(BonusId);
            bool slow = fastStacks == 0 && ActiveEffectsProvider.ActiveLiability(LiabilityId);
            bool fastHatch = UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade(AnimalPowers.FastHatch)
                && __instance.GetMachineData()?.IsIncubator == true;
            double factor = MachineReadyTime.Factor(fastStacks, slow, fastHatch);
            if (factor == 1.0) return;

            __instance.MinutesUntilReady = MachineReadyTime.Scale(before, factor);
            string effect = string.Join("+", new[]
            {
                fastStacks > 0 ? BonusId : null, slow ? LiabilityId : null, fastHatch ? AnimalPowers.FastHatch : null,
            }.Where(s => s != null));
            PatchLog.Info($"{effect}: {__instance.Name} ready in {__instance.MinutesUntilReady} min (was {before}).");
        }
    }
}
