using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Gear tab: Swift Hands (swing speed), Light Touch (energy refund) and Heavy Hitter (pickaxe and
    /// axe power). The bonus comes only from owning the upgrade; nothing is written onto the tool.
    /// </summary>
    internal static class GearToolHelper
    {
        /// <summary>Per-swing state carried from the prefix to the finalizer.</summary>
        internal struct SwingState
        {
            public float Stamina;
            public int ExtraPower;
        }

        internal static SwingState Begin(Tool tool, Farmer who, bool addsPower)
        {
            var state = new SwingState { Stamina = who?.Stamina ?? 0f };
            if (!addsPower) return state;

            ToolKind kind = GearToolKind.KindOf(tool);
            int tier = UpgradeChecker.GetTier(GearPowers.Heavy, GearPowers.HeavyTiers);
            if (tier <= 0 || !GearPowers.Fits(GearPowers.Heavy, kind)) return state;

            int powerful = tool.enchantments.OfType<PowerfulEnchantment>().Select(e => e.GetLevel()).DefaultIfEmpty(0).Max();
            int extra = GearPowers.ExtraPower(tier, kind == ToolKind.Axe, powerful);
            if (extra <= 0) return state;

            if (tool is Pickaxe pick) pick.additionalPower.Value += extra;
            else if (tool is Axe axe) axe.additionalPower.Value += extra;
            state.ExtraPower = extra;
            return state;
        }

        internal static void End(Tool tool, Farmer who, SwingState state)
        {
            if (state.ExtraPower != 0)
            {
                if (tool is Pickaxe pick) pick.additionalPower.Value -= state.ExtraPower;
                else if (tool is Axe axe) axe.additionalPower.Value -= state.ExtraPower;
            }

            if (who == null || tool.IsEfficient) return;
            if (!GearPowers.Fits(GearPowers.Light, GearToolKind.KindOf(tool))) return;
            int tier = UpgradeChecker.GetTier(GearPowers.Light, GearPowers.LightTiers);
            if (tier <= 0) return;
            who.Stamina += GearPowers.RefundFor(state.Stamina, who.Stamina, tier);
        }
    }

    /// <summary>Swift Hands: the faster of the tool's own swing factor and the tier's.</summary>
    [HarmonyPatch(typeof(Tool), nameof(Tool.AnimationSpeedModifier), MethodType.Getter)]
    internal static class GearSwingSpeedPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Tool __instance, ref float __result)
        {
            int tier = UpgradeChecker.GetTier(GearPowers.Swift, GearPowers.SwiftTiers);
            if (tier > 0 && GearPowers.Fits(GearPowers.Swift, GearToolKind.KindOf(__instance)))
                __result = GearPowers.BetterSwing(__result, tier);
        }
    }

    [HarmonyPatch(typeof(Hoe), nameof(Hoe.DoFunction))]
    internal static class GearHoeSwingPatch
    {
        private static void Prefix(Hoe __instance, Farmer who, out GearToolHelper.SwingState __state)
            => __state = GearToolHelper.Begin(__instance, who, false);
        private static void Finalizer(Hoe __instance, Farmer who, GearToolHelper.SwingState __state)
            => GearToolHelper.End(__instance, who, __state);
    }

    [HarmonyPatch(typeof(WateringCan), nameof(WateringCan.DoFunction))]
    internal static class GearCanSwingPatch
    {
        private static void Prefix(WateringCan __instance, Farmer who, out GearToolHelper.SwingState __state)
            => __state = GearToolHelper.Begin(__instance, who, false);
        private static void Finalizer(WateringCan __instance, Farmer who, GearToolHelper.SwingState __state)
            => GearToolHelper.End(__instance, who, __state);
    }

    [HarmonyPatch(typeof(FishingRod), nameof(FishingRod.DoFunction))]
    internal static class GearRodSwingPatch
    {
        private static void Prefix(FishingRod __instance, Farmer who, out GearToolHelper.SwingState __state)
            => __state = GearToolHelper.Begin(__instance, who, false);
        private static void Finalizer(FishingRod __instance, Farmer who, GearToolHelper.SwingState __state)
            => GearToolHelper.End(__instance, who, __state);
    }

    [HarmonyPatch(typeof(Pickaxe), nameof(Pickaxe.DoFunction))]
    internal static class GearPickaxeSwingPatch
    {
        private static void Prefix(Pickaxe __instance, Farmer who, out GearToolHelper.SwingState __state)
            => __state = GearToolHelper.Begin(__instance, who, true);
        private static void Finalizer(Pickaxe __instance, Farmer who, GearToolHelper.SwingState __state)
            => GearToolHelper.End(__instance, who, __state);
    }

    [HarmonyPatch(typeof(Axe), nameof(Axe.DoFunction))]
    internal static class GearAxeSwingPatch
    {
        private static void Prefix(Axe __instance, Farmer who, out GearToolHelper.SwingState __state)
            => __state = GearToolHelper.Begin(__instance, who, true);
        private static void Finalizer(Axe __instance, Farmer who, GearToolHelper.SwingState __state)
            => GearToolHelper.End(__instance, who, __state);
    }
}
