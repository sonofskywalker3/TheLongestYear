using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard energy_drain (spec section 8): on that day every energy loss is 1.5x. A prefix on
    /// the <see cref="Farmer.Stamina"/> setter, the one place every tool swing, fishing cast and
    /// energy-costing action writes through. Only the local player, only decreases, and only
    /// during the waking day: the night's day update (Game1.newDay) and the pass-out time write
    /// stamina for sleep bookkeeping and are never scaled (<see cref="WildcardEffects.ScaleStamina"/>).
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.Stamina), MethodType.Setter)]
    internal static class EnergyDrainPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Prefix(Farmer __instance, ref float value)
        {
            if (!DayEffects.Has(WildcardSchedule.EnergyDrain)) return;   // false while dormant
            if (__instance == null || !ReferenceEquals(__instance, Game1.player)) return;
            value = WildcardEffects.ScaleStamina(__instance.Stamina, value, true, Game1.timeOfDay, Game1.newDay);
        }
    }
}
