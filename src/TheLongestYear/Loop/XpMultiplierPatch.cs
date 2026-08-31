using System;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Applies the xp_mult upgrade family (spec 2026-07-14, rebalanced 2026-08-30):
    /// prefix on Farmer.gainExperience scaling howMuch by XpMultiplierRules.PercentFor.
    /// Below the all-skills-10 line the per-skill tiers apply (+25% each, up to
    /// +100%, plus the capstone's +50%). At Level >= 25 vanilla routes the SAME
    /// howMuch into MasteryExp and skill levels are capped, so only the capstone's
    /// +50% applies there (user ruling: per-skill tiers never touch Mastery).
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

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Prefix(Farmer __instance, int which, ref int howMuch)
        {
            if (!RunActivation.IsActive) return;
            if (!__instance.IsLocalPlayer) return;
            if (howMuch <= 0 || which < 0 || which > 4) return;

            var meta = _meta;
            if (meta == null) return;

            int percent = XpMultiplierRules.PercentFor(meta, which, __instance.Level >= 25);
            if (percent <= 100) return;

            howMuch = XpMultiplierRules.Apply(howMuch, percent);
        }
    }
}
