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
    /// skill is below 10) and a finalizer sets MasteryExp to the Mastery Boost amount and announces once.
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

        /// <summary>Above the top mastery threshold (100000): while vanilla runs, MasteryExp is
        /// parked here so its own before/after level comparison can never announce.</summary>
        private const uint ParkedMasteryExp = 1_000_000;

        private const string MasteryExpKey = "MasteryExp";

        /// <summary>Carried from prefix to finalizer: MasteryExp before the call and the amount
        /// we want it to gain.</summary>
        internal readonly struct MasteryFix
        {
            public readonly uint Before;
            public readonly int Wanted;
            public MasteryFix(uint before, int wanted) { Before = before; Wanted = wanted; }
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
            howMuch = plan.Skill;

            // Vanilla's own mastery increment already equals what we want: leave it alone, its
            // announcement is then correct.
            if (plan.Mastery <= 0 || plan.Mastery == XpMultiplierRules.VanillaMasteryGain(which, plan.Skill))
                return;

            // Otherwise take over mastery for this call: park the stat above the top level so
            // vanilla's increment cannot announce, and let the finalizer set the real total and
            // announce once from before-vs-after.
            __state = new MasteryFix(Game1.stats.Get(MasteryExpKey), plan.Mastery);
            Game1.stats.Set(MasteryExpKey, ParkedMasteryExp);
        }

        // Finalizer, not Postfix, so the stat is restored even if vanilla throws.
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static Exception Finalizer(Exception __exception, MasteryFix? __state)
        {
            if (__state == null) return __exception;

            var fix = __state.Value;
            long after = __exception == null ? (long)fix.Before + fix.Wanted : fix.Before;
            Game1.stats.Set(MasteryExpKey, (uint)System.Math.Min(after, uint.MaxValue));

            int gained = XpMultiplierRules.MasteryLevelsGained(fix.Before, after);
            for (int i = 0; i < gained; i++)
            {
                Game1.showGlobalMessage(Game1.content.LoadString("Strings\\1_6_Strings:Mastery_newlevel"));
                Game1.playSound("newArtifact");
            }
            return __exception;
        }
    }
}
