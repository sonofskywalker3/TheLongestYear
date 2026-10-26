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
    /// Gear tab: Keen Edge (damage), Quick Draw (swing speed) and Iron Guard (defense) for the held
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

            int edge = UpgradeChecker.GetTier(GearPowers.Edge, GearPowers.EdgeTiers);
            int quick = UpgradeChecker.GetTier(GearPowers.Quick, GearPowers.QuickTiers);
            int guard = UpgradeChecker.GetTier(GearPowers.Guard, GearPowers.GuardTiers);

            if (edge > 0)
                effects.AttackMultiplier.Value += GearPowers.ExtraAttack(edge, GemLevel<RubyEnchantment>(__instance));
            if (quick > 0)
                effects.WeaponSpeedMultiplier.Value += GearPowers.ExtraWeaponSpeed(quick, GemLevel<EmeraldEnchantment>(__instance));
            if (guard > 0)
                effects.Defense.Value += GearPowers.ExtraDefense(guard, GemLevel<TopazEnchantment>(__instance));
        }

        private static int GemLevel<T>(MeleeWeapon weapon) where T : BaseEnchantment
            => weapon.enchantments.OfType<T>().Select(e => e.GetLevel()).DefaultIfEmpty(0).Max();
    }
}
