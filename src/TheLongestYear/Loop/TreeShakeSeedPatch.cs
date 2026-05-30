using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Guarantees a seed drop the first time a mature wild tree is shaken each day.
    ///
    /// Vanilla arms <c>Tree.hasSeed</c> once per day via
    /// <c>Game1.random.NextBool(data.SeedOnShakeChance)</c> — ~5% for oak/maple/pine — which
    /// is the "shake dozens of trees trying to find 10 that drop seeds" grind the player
    /// flagged (2026-05-30). This prefix re-arms <c>hasSeed</c> to <c>true</c> before the shake
    /// runs, so the vanilla seed-drop block (Tree.shake) fires every time, lifting the
    /// effective per-shake seed rate to 100%.
    ///
    /// Scope decisions:
    /// - <b>Always on, not theme-gated.</b> The player scoped only the all_drops_up *bonus* to
    ///   the Mixed week; the shake rate itself was requested flat. Easy to wrap in an
    ///   <c>ActiveEffectsProvider.ActiveBonus("forage_yield_up")</c> check here later if they
    ///   want it Foraging-only.
    /// - <b>Once per tree per day.</b> Gated on <c>!wasShakenToday</c>, matching vanilla's
    ///   one-seed-per-tree-per-day cadence — so a player can't farm infinite seeds by
    ///   re-shaking a single tree. After the first shake of the day, vanilla sets
    ///   <c>wasShakenToday</c> and this prefix stops re-arming; subsequent shakes that day
    ///   behave exactly like vanilla.
    /// - Only mature trees (<c>growthStage &gt;= 5</c>) of a type that actually has a seed
    ///   (<c>GetData().SeedItemId != null</c>) — mirrors vanilla's own hasSeed condition, so
    ///   we never feed a null seed id into the drop block.
    ///
    /// The all_drops_up / forage_yield_up shake bonus (TreeShakeAllDropsBonusPatch, a postfix
    /// on the same method) still rolls on top of the now-guaranteed base seed — that's the
    /// intended stacking: a reliable seed plus the themed chance for a second.
    /// </summary>
    [HarmonyPatch(typeof(Tree), nameof(Tree.shake), new[] { typeof(Vector2), typeof(bool) })]
    internal static class TreeShakeSeedPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Prefix(Tree __instance)
        {
            if (__instance == null) return;
            if (__instance.growthStage.Value < 5) return;
            if (__instance.wasShakenToday.Value) return;

            var data = __instance.GetData();
            if (string.IsNullOrEmpty(data?.SeedItemId)) return;

            __instance.hasSeed.Value = true;
        }
    }
}
