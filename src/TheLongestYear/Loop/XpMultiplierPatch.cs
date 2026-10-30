using System;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Applies the XP upgrades (Exp Boost, Mastery Boost, and the retired per-skill chains and
    /// Junimo Insight until their hand-over): prefix on Farmer.gainExperience scaling howMuch by
    /// XpMultiplierRules.PercentFor. At vanilla Level >= 25 the game adds the same howMuch to MasteryExp and
    /// to the skill, so the prefix scales howMuch for the skill part (Exp Boost, only while that
    /// skill is below 10) and the postfix corrects MasteryExp to the Mastery Boost amount.
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

        /// <summary>Carried from prefix to postfix: the mastery XP vanilla will add for the
        /// (skill-scaled) howMuch, and the mastery XP we want instead.</summary>
        internal readonly struct MasteryFix
        {
            public readonly int Vanilla;
            public readonly int Wanted;
            public MasteryFix(int vanilla, int wanted) { Vanilla = vanilla; Wanted = wanted; }
        }

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Prefix(Farmer __instance, int which, ref int howMuch, out MasteryFix? __state)
        {
            __state = null;
            if (!RunActivation.IsActive) return;
            if (!__instance.IsLocalPlayer) return;
            if (howMuch <= 0 || which < 0 || which > 4) return;

            var meta = _meta;
            if (meta == null) return;

            var plan = XpMultiplierRules.PlanGain(
                meta, which, howMuch, __instance.GetUnmodifiedSkillLevel(which), __instance.Level);
            if (plan.Mastery > 0)
                __state = new MasteryFix(XpMultiplierRules.VanillaMasteryGain(which, plan.Skill), plan.Mastery);
            howMuch = plan.Skill;
        }

        // Vanilla has just added VanillaMasteryGain(skill-scaled howMuch) to MasteryExp; move the
        // total to the Mastery-Boost-scaled amount.
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(MasteryFix? __state)
        {
            if (__state == null) return;
            int diff = __state.Value.Wanted - __state.Value.Vanilla;
            if (diff == 0) return;

            const string key = "MasteryExp";
            uint current = Game1.stats.Get(key);
            long target = System.Math.Max(0L, (long)current + diff);
            int before = StardewValley.Menus.MasteryTrackerMenu.getCurrentMasteryLevel();
            Game1.stats.Set(key, (uint)target);
            if (diff > 0 && StardewValley.Menus.MasteryTrackerMenu.getCurrentMasteryLevel() > before)
            {
                Game1.showGlobalMessage(Game1.content.LoadString("Strings\\1_6_Strings:Mastery_newlevel"));
                Game1.playSound("newArtifact");
            }
        }
    }
}
