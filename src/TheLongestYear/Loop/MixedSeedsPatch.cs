using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Obtainability upgrades: inject Red Cabbage (266) and Starfruit (398) into the Summer
    /// Mixed Seeds pool when the player owns cult_red_cabbage / cult_starfruit respectively.
    ///
    /// Hooks <see cref="Crop.getRandomWildCropForSeason"/> (or the wild-seed crop resolution
    /// path in Crop.newDay if the method is not directly patchable). 10% substitution chance
    /// per upgrade, applied only in Summer.
    ///
    /// Upgrade ownership is read via <see cref="UpgradeChecker"/>, a static Func wired by
    /// ModEntry.OnSaveLoaded to avoid importing MetaStore into the patch.
    /// </summary>
    internal static class UpgradeChecker
    {
        /// <summary>Set by ModEntry.OnSaveLoaded: returns true if the given upgrade id is owned.</summary>
        public static System.Func<string, bool> HasUpgrade;

        /// <summary>Highest owned tier in a "<prefix>_<n>" chain (e.g. "green_thumb_1".."green_thumb_5").
        /// Returns 0 if no tier is owned. Walks top-down so newest tier wins when chained linearly.</summary>
        public static int GetTier(string prefix, int maxTier)
        {
            if (HasUpgrade == null) return 0;
            for (int t = maxTier; t >= 1; t--)
            {
                if (HasUpgrade(prefix + "_" + t)) return t;
            }
            return 0;
        }
    }

    [HarmonyPatch(typeof(Crop), "getRandomWildCropForSeason", new System.Type[] { typeof(bool) })]
    internal static class MixedSeedsPatch
    {
        private static void Postfix(ref string __result)
        {
            if (UpgradeChecker.HasUpgrade == null) return;
            if (Game1.season != StardewValley.Season.Summer) return;

            // 2026-05-29 user spec: starfruit no longer requires cult_red_cabbage — each
            // cultivation upgrade is independent. Either-or order preserved (Starfruit first
            // when both owned) so a player who bought both doesn't see Starfruit roll get
            // suppressed by an earlier Red Cabbage hit.
            if (UpgradeChecker.HasUpgrade("cult_starfruit")
                && Game1.random.NextDouble() < 0.10)
            {
                __result = "(O)398"; // Starfruit item id (produce — passed to ItemRegistry.Create<Object> for the harvested crop)
                return;
            }

            // Try Red Cabbage.
            if (UpgradeChecker.HasUpgrade("cult_red_cabbage")
                && Game1.random.NextDouble() < 0.10)
            {
                __result = "(O)266"; // Red Cabbage item id (produce — same path as Starfruit above)
            }
        }
    }
}
