using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Gear tab: Steady Line. Tier I keeps the bait and tackle on some casts (the Preserving effect),
    /// tier II also hooks the fish the moment it bites (the Auto-Hook effect). A rod that carries the
    /// real enchantment gets the vanilla effect only, never the upgrade on top.
    /// </summary>
    internal static class GearFishing
    {
        internal static bool HasUpgrade(FishingRod rod, int minTier)
        {
            if (rod == null || !GearPowers.Fits(GearPowers.Line, GearToolKind.KindOf(rod))) return false;
            return UpgradeChecker.GetTier(GearPowers.Line, GearPowers.LineTiers) >= minTier;
        }

        /// <summary>Steady Line I: this catch's bait and tackle are kept, when the roll says so.</summary>
        internal static bool KeepsBait(FishingRod rod)
        {
            int tier = UpgradeChecker.GetTier(GearPowers.Line, GearPowers.LineTiers);
            if (!HasUpgrade(rod, 1) || !GearPowers.LineSavesBait(tier)) return false;
            if (rod.hasEnchantmentOfType<PreservingEnchantment>()) return false;
            return Game1.random.NextDouble() < GearPowers.LineSaveChance;
        }

        /// <summary>
        /// Steady Line II: the value of vanilla's "has Auto-Hook" test. Vanilla's answer stands when the
        /// rod carries the enchantment; otherwise the upgrade supplies it.
        /// </summary>
        internal static bool AutoHooks(bool vanilla, FishingRod rod)
        {
            if (vanilla) return true;
            int tier = UpgradeChecker.GetTier(GearPowers.Line, GearPowers.LineTiers);
            return HasUpgrade(rod, 2) && GearPowers.LineAutoHook(tier);
        }
    }

    /// <summary>
    /// Steady Line I: the end of a cast (<c>doDoneFishing</c>) spends bait and tackle when asked to.
    /// The prefix turns that request off for the catches the upgrade saves. Only the local player's rod
    /// spends anything in vanilla, so only that rod rolls.
    /// </summary>
    [HarmonyPatch(typeof(FishingRod), "doDoneFishing")]
    internal static class GearLineSaveBaitPatch
    {
        // ReSharper disable InconsistentNaming: Harmony convention.
        private static void Prefix(FishingRod __instance, ref bool consumeBaitAndTackle)
        {
            if (!consumeBaitAndTackle || __instance.lastUser is not { IsLocalPlayer: true }) return;
            if (!GearFishing.KeepsBait(__instance)) return;
            consumeBaitAndTackle = false;
            GearReach.Monitor?.Log("Steady Line: kept the bait and tackle this cast.", LogLevel.Trace);
        }
        // ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Steady Line II: <c>FishingRod.tickUpdate</c> starts a nibble with
    /// <c>if (hasEnchantmentOfType&lt;AutoHookEnchantment&gt;())</c> and, when true, runs the hook itself
    /// (bobber, nibble timer, DoFunction, rumble) and skips the bite chime. The transpiler runs that one
    /// answer through <see cref="GearFishing.AutoHooks"/>, so the upgrade reuses the exact vanilla path
    /// once per bite instead of repeating it.
    /// </summary>
    [HarmonyPatch(typeof(FishingRod), nameof(FishingRod.tickUpdate))]
    internal static class GearLineAutoHookPatch
    {
        private static readonly MethodInfo AutoHooks
            = AccessTools.Method(typeof(GearFishing), nameof(GearFishing.AutoHooks));

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int at = codes.FindIndex(c => c.operand is MethodInfo { Name: nameof(Tool.hasEnchantmentOfType), IsGenericMethod: true } m
                                          && m.GetGenericArguments()[0] == typeof(AutoHookEnchantment));
            if (at < 0)
            {
                GearReach.Monitor?.Log(
                    "Steady Line: could not find the Auto-Hook check in FishingRod.tickUpdate; the line will not hook by itself.",
                    LogLevel.Warn);
                return codes;
            }
            // The call leaves a bool on the stack; hand the rod over and let the hook decide.
            codes.Insert(at + 1, new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0));
            codes.Insert(at + 2, new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AutoHooks));
            return codes;
        }
    }
}
