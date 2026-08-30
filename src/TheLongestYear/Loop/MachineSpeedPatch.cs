using HarmonyLib;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Artisan bonus (machines_fast) and Spelunking liability (machines_slow). Postfix
    /// on Object.OutputMachine (decompile Object.cs:2481), which is where MinutesUntilReady is set
    /// for every data-driven machine (kegs, jars, casks, bee houses, tappers, smokers). Scales the
    /// queued time by 0.75 or 1.25, rounded to 10 minutes, floor 10 (MachineReadyTime).</summary>
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

            double factor;
            string effect;
            int fastStacks = ActiveEffectsProvider.BonusStacks(BonusId);
            if (fastStacks > 0)
            {
                // 25% sooner per stack (Artisan theme + Full Steam boost), compounding.
                factor = System.Math.Pow(MachineReadyTime.FastFactor, fastStacks);
                effect = BonusId;
            }
            else if (ActiveEffectsProvider.ActiveLiability(LiabilityId)) { factor = MachineReadyTime.SlowFactor; effect = LiabilityId; }
            else return;

            __instance.MinutesUntilReady = MachineReadyTime.Scale(before, factor);
            PatchLog.Info($"{effect}: {__instance.Name} ready in {__instance.MinutesUntilReady} min (was {before}).");
        }
    }
}
