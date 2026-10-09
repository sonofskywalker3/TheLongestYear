using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

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
    ///
    /// Other mods rewrite these methods too (0.19.21, pitytheviolins' report). ExtraAnimalConfig swaps
    /// every <c>IsWinterHere()</c> for its own <c>AnimalUtils.AnimalAffectedByWinter</c>, which asks
    /// <c>IsWinterHere()</c> inside; Better Pigs routes the barn-door check through its own helpers.
    /// So each method is its own patch class (one failing never drops the other), the transpiler runs
    /// last (it sees what the other mods did), never throws, and also follows the calls into other
    /// mods' code (<see cref="OtherModHookRules"/>): any helper there that asks
    /// <c>IsWinterHere()</c> gets the same swap at GameLaunched. A helper hooked this way answers
    /// "Winter" on the snow day for every caller, which is the snow day's rule anyway. When nothing
    /// can be hooked the snow day simply leaves that method alone, with one Info line, no error.
    /// </summary>
    internal static class WildcardAnimalPatch
    {
        internal static IMonitor Monitor;

        private static readonly MethodInfo IsWinterHere
            = AccessTools.Method(typeof(GameLocation), nameof(GameLocation.IsWinterHere));
        private static readonly MethodInfo Replacement
            = AccessTools.Method(typeof(WildcardAnimalPatch), nameof(WinterOrSnowDay));

        /// <summary>The latest transpiler result per patched method. Harmony re-runs the transpiler
        /// whenever any mod patches the method again, so this always matches the live code.</summary>
        private static readonly Dictionary<MethodBase, (int Swapped, List<MethodInfo> Helpers)> Outcomes = new();
        private static readonly HashSet<MethodBase> HookedHelpers = new();

        [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.updateWhenNotCurrentLocation))]
        internal static class LeaveBarnPatch
        {
            [HarmonyPriority(Priority.Last)]
            // ReSharper disable once UnusedMember.Local: Harmony.
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
                => SwapWinterChecks(instructions, original);
        }

        [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.updatePerTenMinutes))]
        internal static class EveningMoodPatch
        {
            [HarmonyPriority(Priority.Last)]
            // ReSharper disable once UnusedMember.Local: Harmony.
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
                => SwapWinterChecks(instructions, original);
        }

        private static IEnumerable<CodeInstruction> SwapWinterChecks(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var codes = new List<CodeInstruction>(instructions);
            int swapped = Swap(codes);
            var helpers = new List<MethodInfo>();
            FindOtherModHelpers(codes, OtherModHookRules.MaxHelperDepth, new HashSet<MethodBase>(), helpers);
            Outcomes[original] = (swapped, helpers);
            return codes;
        }

        /// <summary>Transpiler for another mod's helper that asks <c>IsWinterHere()</c>.</summary>
        [HarmonyPriority(Priority.Last)]
        private static IEnumerable<CodeInstruction> HelperTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            Swap(codes);
            return codes;
        }

        private static int Swap(List<CodeInstruction> codes)
        {
            int swapped = 0;
            foreach (CodeInstruction code in codes)
            {
                if (!code.Calls(IsWinterHere)) continue;
                // Same stack shape: the GameLocation instance becomes the static call's argument.
                code.opcode = OpCodes.Call;
                code.operand = Replacement;
                swapped++;
            }
            return swapped;
        }

        /// <summary>Collects the methods of other mods, reachable from these instructions within
        /// <paramref name="depth"/> calls, whose own body asks <c>IsWinterHere()</c>.</summary>
        private static void FindOtherModHelpers(IEnumerable<CodeInstruction> codes, int depth, HashSet<MethodBase> seen, List<MethodInfo> found)
        {
            if (depth <= 0) return;
            foreach (CodeInstruction code in codes)
            {
                if (code.operand is not MethodInfo method || !seen.Add(method)) continue;
                if (!OtherModHookRules.IsOtherModAssembly(method.DeclaringType?.Assembly.GetName().Name)) continue;
                if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                List<CodeInstruction> body;
                try
                {
                    body = PatchProcessor.GetOriginalInstructions(method);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or BadImageFormatException)
                {
                    Monitor?.Log($"Snow day animals: could not read {Describe(method)} ({ex.GetType().Name}); skipped.", LogLevel.Trace);
                    continue;
                }
                if (body.Any(c => c.Calls(IsWinterHere)))
                    found.Add(method);
                FindOtherModHelpers(body, depth - 1, seen, found);
            }
        }

        /// <summary>At GameLaunched (every mod's Entry patches are in): hook the other mods' helpers the
        /// transpilers found, then log one line per method. Never an error.</summary>
        internal static void HookOtherModHelpers(Harmony harmony)
        {
            foreach ((MethodBase target, (int swapped, List<MethodInfo> helpers)) in Outcomes.ToList())
            {
                var hooked = new List<string>();
                foreach (MethodInfo helper in helpers)
                {
                    if (HookedHelpers.Contains(helper)) { hooked.Add(Describe(helper)); continue; }
                    try
                    {
                        harmony.Patch(helper, transpiler: new HarmonyMethod(typeof(WildcardAnimalPatch), nameof(HelperTranspiler)));
                        HookedHelpers.Add(helper);
                        hooked.Add(Describe(helper));
                    }
                    catch (Exception ex) when (ex is HarmonyException or ArgumentException or InvalidOperationException or NotSupportedException)
                    {
                        Monitor?.Log($"Snow day animals: could not hook {Describe(helper)} ({ex.GetType().Name}: {ex.Message}).", LogLevel.Trace);
                    }
                }

                string name = $"{target.DeclaringType?.Name}.{target.Name}";
                if (swapped > 0 || hooked.Count > 0)
                {
                    Monitor?.Log(
                        $"Snow day animals: {name} keeps the rule ({swapped} direct check(s)"
                        + (hooked.Count > 0 ? $", via {string.Join(", ", hooked)}" : "") + ").",
                        hooked.Count > 0 ? LogLevel.Debug : LogLevel.Trace);
                    continue;
                }

                string owners = string.Join(", ", (Harmony.GetPatchInfo(target)?.Transpilers ?? Enumerable.Empty<Patch>())
                    .Select(p => p.owner).Where(o => o != harmony.Id).Distinct());
                Monitor?.Log(
                    $"Snow day animals: {name} was rewritten by another mod ({(owners.Length > 0 ? owners : "unknown")}); "
                    + "the snow day leaves that part to it.",
                    LogLevel.Info);
            }
        }

        private static string Describe(MethodBase method)
            => $"{method.DeclaringType?.FullName}.{method.Name} ({method.DeclaringType?.Assembly.GetName().Name})";

        /// <summary>Vanilla's <c>IsWinterHere()</c>, plus the wildcard snow day.</summary>
        internal static bool WinterOrSnowDay(GameLocation location)
            => location.IsWinterHere() || WildcardWeatherPatch.SnowDayHere(location);
    }
}
