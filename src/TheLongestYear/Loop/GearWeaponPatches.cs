using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Enchantments;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Gear tab: Keen Edge (damage, patched below), Quick Draw (swing speed) and Iron Guard (defense) for the held
    /// weapon. The bonus joins the player's buff total while the weapon is held; nothing is written
    /// onto the weapon. A weapon with the real gem gets the better of the two, never both.
    /// </summary>
    [HarmonyPatch(typeof(MeleeWeapon), nameof(MeleeWeapon.AddEquipmentEffects))]
    internal static class GearWeaponEffectsPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(MeleeWeapon __instance, BuffEffects effects)
        {
            if (GearToolKind.KindOf(__instance) != ToolKind.MeleeWeapon) return;

            int quick = UpgradeChecker.GetTier(GearPowers.Quick, GearPowers.QuickTiers);
            int guard = UpgradeChecker.GetTier(GearPowers.Guard, GearPowers.GuardTiers);

            if (quick > 0)
                effects.WeaponSpeedMultiplier.Value += GearPowers.ExtraWeaponSpeed(quick, GemLevel<EmeraldEnchantment>(__instance));
            if (guard > 0)
                effects.Defense.Value += GearPowers.ExtraDefense(guard, GemLevel<TopazEnchantment>(__instance));
        }

        internal static int GemLevel<T>(MeleeWeapon weapon) where T : BaseEnchantment
            => weapon.enchantments.OfType<T>().Select(e => e.GetLevel()).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Keen Edge: Ruby's rule (10% of base damage per level, at least 1) for the levels the weapon does
    /// not already have from a real Ruby. The range is raised for one damage call and put back in the
    /// finalizer, so nothing is saved onto the weapon. Covers the normal swing (<c>DoDamage</c>) and the
    /// club special smash (<c>triggerClubFunction</c>, which damages monsters directly).
    /// </summary>
    internal static class GearKeenEdge
    {
        private static bool _active;

        internal static (int Min, int Max) Begin(MeleeWeapon weapon)
        {
            if (_active || GearToolKind.KindOf(weapon) != ToolKind.MeleeWeapon) return (0, 0);
            int tier = UpgradeChecker.GetTier(GearPowers.Edge, GearPowers.EdgeTiers);
            var data = weapon.GetData();
            if (tier <= 0 || data == null) return (0, 0);

            int ruby = GearWeaponEffectsPatch.GemLevel<RubyEnchantment>(weapon);
            var extra = (GearPowers.ExtraDamage(tier, ruby, data.MinDamage), GearPowers.ExtraDamage(tier, ruby, data.MaxDamage));
            weapon.minDamage.Value += extra.Item1;
            weapon.maxDamage.Value += extra.Item2;
            _active = true;
            return extra;
        }

        internal static void End(MeleeWeapon weapon, (int Min, int Max) state)
        {
            if (state == (0, 0)) return;
            weapon.minDamage.Value -= state.Min;
            weapon.maxDamage.Value -= state.Max;
            _active = false;
        }
    }

    [HarmonyPatch(typeof(MeleeWeapon), nameof(MeleeWeapon.DoDamage))]
    internal static class GearWeaponDamagePatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Prefix(MeleeWeapon __instance, out (int Min, int Max) __state) => __state = GearKeenEdge.Begin(__instance);

        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Finalizer(MeleeWeapon __instance, (int Min, int Max) __state) => GearKeenEdge.End(__instance, __state);
    }

    [HarmonyPatch(typeof(MeleeWeapon), nameof(MeleeWeapon.triggerClubFunction))]
    internal static class GearClubSpecialPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Prefix(MeleeWeapon __instance, out (int Min, int Max) __state) => __state = GearKeenEdge.Begin(__instance);

        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Finalizer(MeleeWeapon __instance, (int Min, int Max) __state) => GearKeenEdge.End(__instance, __state);
    }
}
