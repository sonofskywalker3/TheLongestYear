using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Artisan liability (cooked_food_weak): cooked food (Data/Objects category -7)
    /// restores half its energy and health and gives no buffs. Week-scoped: the items are
    /// untouched, only what eating them does this week. Three postfixes on the methods
    /// Farmer.doneEating reads (decompile Farmer.cs:8884 onward), so the eat HUD and the
    /// tooltip show the halved numbers too.</summary>
    internal static class CookedFoodWeak
    {
        public const string LiabilityId = "cooked_food_weak";
        public const int HalfDivisor = 2;

        public static bool Applies(StardewValley.Object item)
            => item != null
               && item.Category == StardewValley.Object.CookingCategory
               && ActiveEffectsProvider.ActiveLiability(LiabilityId);
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.staminaRecoveredOnConsumption))]
    internal static class CookedFoodWeakStaminaPatch
    {
        private static void Postfix(StardewValley.Object __instance, ref int __result)
        {
            if (__result <= 0 || !CookedFoodWeak.Applies(__instance)) return;
            int before = __result;
            __result /= CookedFoodWeak.HalfDivisor;
            PatchLog.Trace($"{CookedFoodWeak.LiabilityId}: {__instance.Name} energy {before} -> {__result}.");
        }
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.healthRecoveredOnConsumption))]
    internal static class CookedFoodWeakHealthPatch
    {
        private static void Postfix(StardewValley.Object __instance, ref int __result)
        {
            if (__result <= 0 || !CookedFoodWeak.Applies(__instance)) return;
            __result /= CookedFoodWeak.HalfDivisor;
        }
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.GetFoodOrDrinkBuffs))]
    internal static class CookedFoodWeakBuffsPatch
    {
        private static void Postfix(StardewValley.Object __instance, ref IEnumerable<Buff> __result)
        {
            if (!CookedFoodWeak.Applies(__instance)) return;
            __result = Enumerable.Empty<Buff>();
        }
    }
}
