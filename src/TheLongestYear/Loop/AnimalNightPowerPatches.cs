using System;
using HarmonyLib;
using StardewValley;
using StardewValley.GameData.FarmAnimals;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Busy Coop and Busy Barn, formerly Busy Barnyard (spec 2026-10-09 power 3, split into a coop and a barn row by Ruling 2).
    /// Vanilla's produce test in FarmAnimal.dayUpdate (FarmAnimal.cs:1005) is
    /// <c>daysSinceLastLay >= DaysToProduce - produceSpeedBonus</c>. The prefix adds
    /// <c>DaysToProduce - target</c> to daysSinceLastLay so the test passes at the target; the postfix
    /// takes it off again unless the animal produced (vanilla reset it to 0). The early-return path
    /// (animal brought in at night) undoes itself the same way. Fullness and happiness rolls are
    /// untouched, so an unfed or unhappy animal still skips. Data/FarmAnimals is never edited, and the
    /// bundle effort model is never told (Ruling 1).</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.dayUpdate))]
    internal static class BusyBarnyardPatch
    {
        private static void Prefix(FarmAnimal __instance, out int __state)
        {
            __state = 0;
            if (__instance == null || UpgradeChecker.HasUpgrade == null || !Game1.IsMasterGame) return;
            try
            {
                FarmAnimalData data = __instance.GetAnimalData();
                if (data == null) return;
                int offset = AnimalPowers.ProduceDayOffset(__instance.type.Value, data.DaysToProduce, UpgradeChecker.HasUpgrade);
                if (offset <= 0) return;
                __instance.daysSinceLastLay.Value += offset;
                __state = offset;
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"BusyBarnyardPatch prefix: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Postfix(FarmAnimal __instance, int __state)
        {
            if (__state <= 0 || __instance == null) return;
            if (__instance.daysSinceLastLay.Value == 0)
            {
                PatchLog.Trace($"Busy Coop/Barn: {__instance.displayName} ({__instance.type.Value}) produced tonight " +
                               $"(produce {__instance.currentProduce.Value ?? "dropped overnight"}).");
                return;
            }
            __instance.daysSinceLastLay.Value = Math.Max(0, __instance.daysSinceLastLay.Value - __state);
        }
    }

    /// <summary>Quick Growth (spec 2026-10-09 power 7): a baby that aged tonight takes one more step,
    /// the same way vanilla steps it (FarmAnimal.cs:984): growFully on its last baby day, else one day
    /// older. An unfed night still does not age it.</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.dayUpdate))]
    internal static class QuickGrowthPatch
    {
        /// <summary>Salt for the growFully random, which only matters for ProduceOnMature animals.</summary>
        private const int GrowSalt = 7741;

        private static void Prefix(FarmAnimal __instance, out int __state)
            => __state = __instance?.age.Value ?? -1;

        private static void Postfix(FarmAnimal __instance, int __state)
        {
            if (__state < 0 || __instance == null || UpgradeChecker.HasUpgrade == null || !Game1.IsMasterGame) return;
            if (!UpgradeChecker.HasUpgrade(AnimalPowers.QuickGrowth)) return;
            try
            {
                FarmAnimalData data = __instance.GetAnimalData();
                if (data == null) return;
                switch (AnimalPowers.QuickGrowthExtraStep(__state, __instance.age.Value, data.DaysToMature))
                {
                    case QuickGrowthStep.GrowFully:
                        __instance.growFully(Utility.CreateRandom(__instance.myID.Value, Game1.stats.DaysPlayed, GrowSalt));
                        PatchLog.Info($"{AnimalPowers.QuickGrowth}: {__instance.displayName} ({__instance.type.Value}) grew up (age {__instance.age.Value}).");
                        break;
                    case QuickGrowthStep.AgeOneDay:
                        __instance.age.Value++;
                        PatchLog.Info($"{AnimalPowers.QuickGrowth}: {__instance.displayName} ({__instance.type.Value}) age {__state} -> {__instance.age.Value} of {data.DaysToMature}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"QuickGrowthPatch: threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
