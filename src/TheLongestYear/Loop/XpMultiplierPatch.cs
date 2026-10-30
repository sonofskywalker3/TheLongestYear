using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Applies the XP upgrades (Exp Boost, Mastery Boost, and the retired per-skill chains and
    /// Junimo Insight until their hand-over): prefix on Farmer.gainExperience scaling howMuch by
    /// XpMultiplierRules.PercentFor. At vanilla Level >= 25 the game adds the same howMuch to MasteryExp and
    /// to the skill. The prefix scales howMuch for the skill part (Exp Boost, only while that skill is
    /// below 10); a transpiler swaps the amount of vanilla's own MasteryExp increment for the planned
    /// Mastery Boost amount, computed from the original howMuch, so vanilla's level-up check and
    /// announcement run once on the real value.
    ///
    /// MetaState is reached via the same explicit <see cref="Connect"/> wiring
    /// JunimoStashCapacityPatch/JunimoStashCapPatch use (ModEntry.OnSaveLoaded) —
    /// PercentFor needs the real MetaState instance (not just an owned-upgrade
    /// bool), so this bypasses the lighter UpgradeChecker.HasUpgrade delegate
    /// used by the simpler tiered passives.
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.gainExperience))]
    internal static class XpMultiplierPatch
    {
        private static MetaState _meta;

        public static void Connect(MetaState meta) => _meta = meta;

        private const string MasteryExpKey = "MasteryExp";

        /// <summary>The unscaled howMuch of the gainExperience call in flight on this thread; 0
        /// when TLY is not boosting it. Read by <see cref="MasteryAmount"/> from inside vanilla.</summary>
        [ThreadStatic] private static int _originalHowMuch;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Prefix(Farmer __instance, int which, ref int howMuch, out int __state)
        {
            __state = _originalHowMuch;
            _originalHowMuch = 0;
            if (!RunActivation.IsActive) return;
            if (!__instance.IsLocalPlayer) return;
            if (howMuch <= 0 || which < 0 || which > 4) return;

            var meta = _meta;
            if (meta == null) return;

            _originalHowMuch = howMuch;
            howMuch = XpMultiplierRules.PlanGain(
                meta, which, howMuch, __instance.GetUnmodifiedSkillLevel(which), __instance.Level).Skill;
        }

        // Finalizer so the thread-static is restored even if vanilla throws.
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static Exception Finalizer(Exception __exception, int __state)
        {
            _originalHowMuch = __state;
            return __exception;
        }

        /// <summary>Called by the transpiled MasteryExp increment inside vanilla gainExperience.
        /// Returns the amount to add: the planned mastery, or vanilla's own when TLY is not
        /// boosting this call.</summary>
        public static int MasteryAmount(int vanillaAmount, int which)
        {
            int original = _originalHowMuch;
            var meta = _meta;
            if (original <= 0 || meta == null) return vanillaAmount;
            return XpMultiplierRules.PlannedMastery(meta, which, original);
        }

        private static readonly MethodInfo IncrementInt =
            AccessTools.Method(typeof(Stats), nameof(Stats.Increment), new[] { typeof(string), typeof(int) });
        private static readonly MethodInfo MathMaxInt =
            AccessTools.Method(typeof(Math), nameof(Math.Max), new[] { typeof(int), typeof(int) });
        private static readonly MethodInfo MasteryAmountMethod =
            AccessTools.Method(typeof(XpMultiplierPatch), nameof(MasteryAmount));

        // Pattern: ldstr "MasteryExp" ... call Math.Max(int,int); callvirt/call Stats.Increment(string,int).
        // Inserted before the Increment: ldarg.1 (which); call MasteryAmount(int,int), so the amount
        // vanilla computed (already Math.Max'd) is replaced by the planned one. Vanilla's own
        // before/after mastery-level check and announcement then run on the real final value.
        // ReSharper disable once UnusedMember.Local: Harmony.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int inc = -1;
            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].operand is MethodBase m && m == IncrementInt
                    && codes[i - 1].operand is MethodBase prev && prev == MathMaxInt
                    && codes.FindLastIndex(i, c => c.opcode == OpCodes.Ldstr
                        && (c.operand as string) == MasteryExpKey) >= 0)
                {
                    inc = i;
                    break;
                }
            }
            if (inc < 0)
            {
                PatchLog.WarnOnce("xp-mastery-transpiler",
                    "XP boosts: could not find the MasteryExp increment in Farmer.gainExperience; Mastery Boost will not apply (Exp Boost still works).");
                return codes;
            }

            var hook = new CodeInstruction(OpCodes.Ldarg_1);
            hook.labels.AddRange(codes[inc].labels);
            codes[inc].labels.Clear();
            codes.Insert(inc, hook);
            codes.Insert(inc + 1, new CodeInstruction(OpCodes.Call, MasteryAmountMethod));
            return codes;
        }
    }
}
