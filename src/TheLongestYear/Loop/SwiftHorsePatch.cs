using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Swift Horse (spec 2026-10-09 power 5): +1.0 to the riding speed sum. Postfix on
    /// Farmer.getMovementSpeed (Farmer.cs:7994): vanilla's riding speed is
    /// <c>(speed + addedSpeed + 4.6 + carrot + book) x movementMultiplier x elapsed ms</c> in the
    /// control branch; this adds <c>1.0 x movementMultiplier x elapsed ms</c> (x 0.707 diagonally) in that
    /// same branch only. Not during events, not while walking, not when the slime-stuck buff zeroed it.
    /// No buff, so nothing shows in the buff bar.</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.getMovementSpeed))]
    internal static class SwiftHorsePatch
    {
        private static void Postfix(Farmer __instance, ref float __result)
        {
            if (__instance == null || __result <= 0f || UpgradeChecker.HasUpgrade == null) return;
            if (!__instance.IsLocalPlayer || !__instance.isRidingHorse() || Game1.eventUp) return;
            if (Game1.CurrentEvent != null && !Game1.CurrentEvent.playerControlSequence) return;
            if (__instance.UsingTool && __instance.canStrafeForToolUse()) return;
            if (!UpgradeChecker.HasUpgrade(AnimalPowers.SwiftHorse)) return;
            __result += (float)AnimalPowers.SwiftHorseBonus(__instance.movementMultiplier,
                Game1.currentGameTime.ElapsedGameTime.Milliseconds, __instance.movementDirections.Count > 1);
        }
    }
}
