using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Gear tab: Wide Reach (hoe and watering can charge further) and Deep Can (the watering can holds
    /// twice as much, then never runs dry). Nothing is written onto the tool that a save keeps:
    /// <c>waterCanMax</c> is <c>[XmlIgnore]</c> and rebuilt from the upgrade level, and Deep Can II
    /// tops <c>WaterLeft</c> up instead of setting <c>IsBottomless</c>.
    /// </summary>
    internal static class GearReach
    {
        internal static IMonitor Monitor;

        private static readonly MethodInfo UpgradeLevelChanged
            = AccessTools.Method(typeof(WateringCan), "OnUpgradeLevelChanged");

        /// <summary>
        /// The extra charge levels the held tool gets past its upgrade level. Vanilla passes 1 with a
        /// Reaching enchantment and 0 without; owning Wide Reach raises it, never lowers it.
        /// </summary>
        internal static int ChargeBonus(int vanillaBonus) => ChargeBonusFor(Game1.player?.CurrentTool, vanillaBonus);

        private static int ChargeBonusFor(Tool tool, int vanillaBonus)
        {
            if (tool == null) return vanillaBonus;
            int tier = UpgradeChecker.GetTier(GearPowers.Reach, GearPowers.ReachTiers);
            if (tier <= 0 || !GearPowers.Fits(GearPowers.Reach, GearToolKind.KindOf(tool))) return vanillaBonus;
            int level = tool.UpgradeLevel;
            return Math.Max(vanillaBonus, GearPowers.MaxChargePower(level, vanillaBonus > 0, tier) - level);
        }

        /// <summary>
        /// The upgrade level <c>Tool.beginUsing</c> compares against 0. Vanilla ends a basic tool's use at
        /// once (no charge); a basic hoe or can that Wide Reach lets charge reads as level 1 for that one
        /// comparison only, so it starts charging like a copper tool. The tool itself is not changed.
        /// </summary>
        internal static int BeginUsingLevel(int level, Tool tool)
            => level <= 0 && ChargeBonusFor(tool, 0) > 0 ? 1 : level;

        /// <summary>Deep Can I: called after vanilla has set <c>waterCanMax</c> for the can's level.</summary>
        internal static void ApplyCapacity(WateringCan can)
        {
            int tier = UpgradeChecker.GetTier(GearPowers.Can, GearPowers.CanTiers);
            can.waterCanMax = GearPowers.CanCapacity(can.waterCanMax, tier);
        }

        /// <summary>Rebuild a can's capacity from its level and the owned tier, and keep the water in range.</summary>
        internal static void RefreshCan(WateringCan can)
        {
            // Vanilla's own level switch runs, and the postfix below applies the tier on top. The game's
            // OnUpgradeLevelChanged also fills the can (an upgrade comes back full), so the water is put back.
            int water = can.WaterLeft;
            UpgradeLevelChanged?.Invoke(can, null);
            can.WaterLeft = Math.Min(water, can.waterCanMax);
            int tier = UpgradeChecker.GetTier(GearPowers.Can, GearPowers.CanTiers);
            if (GearPowers.CanBottomless(tier) && !can.IsBottomless) can.WaterLeft = can.waterCanMax;
        }
    }

    /// <summary>
    /// Wide Reach: vanilla keeps charging while <c>upgradeLevel + (Reaching ? 1 : 0) &gt; toolPower</c>
    /// (Game1.UpdateControlInput, inside the delegate it hands to the mod hooks, so the code lives in a
    /// compiler-generated method). The transpiler runs the Reaching value through
    /// <see cref="GearReach.ChargeBonus"/> before it is stored. Hold time per level and energy stay vanilla.
    /// </summary>
    [HarmonyPatch]
    internal static class GearReachChargePatch
    {
        private const string HostMethod = "UpdateControlInput";

        private static readonly MethodInfo ChargeBonus
            = AccessTools.Method(typeof(GearReach), nameof(GearReach.ChargeBonus));

        private static MethodBase _target;

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static bool Prepare()
        {
            _target ??= FindChargeCheck();
            if (_target == null)
                GearReach.Monitor?.Log(
                    "Wide Reach: could not find the tool charge check in Game1.UpdateControlInput; hoe and watering can reach stay vanilla.",
                    LogLevel.Warn);
            return _target != null;
        }

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static MethodBase TargetMethod() => _target;

        /// <summary>The Game1 method, or compiler-generated delegate body, that reads the Reaching enchantment.</summary>
        private static MethodBase FindChargeCheck()
        {
            var candidates = new List<MethodInfo>(AccessTools.GetDeclaredMethods(typeof(Game1)));
            foreach (Type nested in typeof(Game1).GetNestedTypes(AccessTools.all))
                candidates.AddRange(AccessTools.GetDeclaredMethods(nested));
            foreach (MethodInfo method in candidates)
            {
                if (!method.Name.Contains(HostMethod) || method.ContainsGenericParameters) continue;
                try
                {
                    if (PatchProcessor.GetOriginalInstructions(method).Any(IsReachingCheck)) return method;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
                {
                    GearReach.Monitor?.Log($"Wide Reach: skipped {method.Name} ({ex.GetType().Name}).", LogLevel.Trace);
                }
            }
            return null;
        }

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int call = codes.FindIndex(IsReachingCheck);
            int store = call < 0 ? -1 : codes.FindIndex(call + 1, c => c.IsStloc());
            if (store < 0)
            {
                GearReach.Monitor?.Log(
                    "Wide Reach: could not find the tool charge check in Game1.UpdateControlInput; hoe and watering can reach stay vanilla.",
                    LogLevel.Warn);
                return codes;
            }

            // The ternary's two branches meet at the store, so the store's labels move onto the call.
            var hook = new CodeInstruction(OpCodes.Call, ChargeBonus);
            hook.labels.AddRange(codes[store].labels);
            codes[store].labels.Clear();
            codes.Insert(store, hook);
            return codes;
        }

        private static bool IsReachingCheck(CodeInstruction code)
            => code.operand is MethodInfo { Name: nameof(Tool.hasEnchantmentOfType), IsGenericMethod: true } method
               && method.GetGenericArguments()[0] == typeof(ReachingToolEnchantment);
    }

    /// <summary>
    /// Wide Reach on a basic tool: <c>Tool.beginUsing</c> ends the use of any non-rod tool whose
    /// <c>upgradeLevel &lt;= 0</c> before charging starts. The transpiler runs that one level read through
    /// <see cref="GearReach.BeginUsingLevel"/>, which answers vanilla's level unless Wide Reach applies.
    /// Raising <c>upgradeLevel</c> itself is not an option: the can's level event refills it.
    /// </summary>
    [HarmonyPatch(typeof(Tool), nameof(Tool.beginUsing))]
    internal static class GearReachBasicToolPatch
    {
        private static readonly FieldInfo UpgradeLevelField = AccessTools.Field(typeof(Tool), nameof(Tool.upgradeLevel));
        private static readonly MethodInfo BeginUsingLevel
            = AccessTools.Method(typeof(GearReach), nameof(GearReach.BeginUsingLevel));

        // ReSharper disable once UnusedMember.Local: Harmony.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // ldarg.0; ldfld upgradeLevel; callvirt get_Value; ldc.i4.0; bgt: the basic-tool early exit.
            int read = -1;
            for (int i = 0; i + 2 < codes.Count; i++)
            {
                if (codes[i].LoadsField(UpgradeLevelField)
                    && codes[i + 1].operand is MethodInfo { Name: "get_Value" }
                    && codes[i + 2].opcode == OpCodes.Ldc_I4_0)
                {
                    read = i + 1;
                    break;
                }
            }
            if (read < 0)
            {
                GearReach.Monitor?.Log(
                    "Wide Reach: could not find the basic tool check in Tool.beginUsing; a basic hoe or watering can will not charge.",
                    LogLevel.Warn);
                return codes;
            }
            codes.Insert(read + 1, new CodeInstruction(OpCodes.Ldarg_0));
            codes.Insert(read + 2, new CodeInstruction(OpCodes.Call, BeginUsingLevel));
            return codes;
        }
    }

    /// <summary>
    /// Deep Can I: double the capacity vanilla sets for the can's level. The game fills the can to the
    /// level's capacity in the same method, so a can that was just filled is filled to the bigger one.
    /// </summary>
    [HarmonyPatch(typeof(WateringCan), "OnUpgradeLevelChanged")]
    internal static class GearCanCapacityPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(WateringCan __instance)
        {
            bool wasFull = __instance.WaterLeft == __instance.waterCanMax;
            GearReach.ApplyCapacity(__instance);
            if (wasFull) __instance.WaterLeft = __instance.waterCanMax;
        }
    }

    /// <summary>Deep Can I for a new basic can: level 0 never fires the level-changed event.</summary>
    [HarmonyPatch(typeof(WateringCan), MethodType.Constructor)]
    internal static class GearCanNewPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(WateringCan __instance) => GearReach.ApplyCapacity(__instance);
    }

    /// <summary>
    /// Deep Can: the prefix rebuilds the capacity before vanilla refills or waters (a can taken out of
    /// a chest mid-day has the right size); Deep Can II then tops the water back up after each use.
    /// </summary>
    [HarmonyPatch(typeof(WateringCan), nameof(WateringCan.DoFunction))]
    internal static class GearCanWaterPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Prefix(WateringCan __instance) => GearReach.RefreshCan(__instance);

        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(WateringCan __instance)
        {
            int tier = UpgradeChecker.GetTier(GearPowers.Can, GearPowers.CanTiers);
            if (GearPowers.CanBottomless(tier) && !__instance.IsBottomless)
                __instance.WaterLeft = __instance.waterCanMax;
        }
    }
}
