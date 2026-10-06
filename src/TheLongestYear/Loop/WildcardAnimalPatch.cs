using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard snow_day, the animals (spec section 8): on the snow day animals treat the valley as
    /// Winter. Vanilla keys both rules on <c>GameLocation.IsWinterHere()</c>:
    ///   - leaving the barn (<c>FarmAnimal.updateWhenNotCurrentLocation</c>): door open, not raining,
    ///     not Winter. On the snow day they stay inside.
    ///   - the evening mood tick (<c>FarmAnimal.updatePerTenMinutes</c>, 6pm on): indoors in Winter a
    ///     content animal (happiness over 150) loses its HappinessDrain every ten minutes without a
    ///     heater and gains it with one. Vanilla's own Winter numbers; nothing dies.
    /// A transpiler swaps each <c>IsWinterHere()</c> call in those two methods for
    /// <see cref="WinterOrSnowDay"/>, which answers exactly like the vanilla call on every other day
    /// and whenever TLY is dormant (DayEffects.Has is false then).
    /// </summary>
    [HarmonyPatch]
    internal static class WildcardAnimalPatch
    {
        private static readonly MethodInfo IsWinterHere
            = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.IsWinterHere));
        private static readonly MethodInfo Replacement
            = AccessTools.Method(typeof(WildcardAnimalPatch), nameof(WinterOrSnowDay));

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.updateWhenNotCurrentLocation));
            yield return AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.updatePerTenMinutes));
        }

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var codes = new List<CodeInstruction>(instructions);
            int swapped = 0;
            foreach (CodeInstruction code in codes)
            {
                if (!code.Calls(IsWinterHere)) continue;
                // Same stack shape: the GameLocation instance becomes the static call's argument.
                code.opcode = OpCodes.Call;
                code.operand = Replacement;
                swapped++;
            }
            if (swapped == 0)
                throw new InvalidOperationException(
                    $"no IsWinterHere() call in {original.DeclaringType?.Name}.{original.Name}; the snow day cannot keep animals inside");
            return codes;
        }

        /// <summary>Vanilla's <c>IsWinterHere()</c>, plus the wildcard snow day.</summary>
        internal static bool WinterOrSnowDay(GameLocation location)
            => location.IsWinterHere() || WildcardWeatherPatch.SnowDayHere(location);
    }
}
