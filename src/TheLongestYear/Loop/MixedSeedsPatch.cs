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
    }

    [HarmonyPatch(typeof(Crop), "getRandomWildCropForSeason")]
    internal static class MixedSeedsPatch
    {
        private static void Postfix(ref string __result)
        {
            if (UpgradeChecker.HasUpgrade == null) return;
            if (Game1.season != StardewValley.Season.Summer) return;

            // Try Starfruit first (requires Red Cabbage as prereq).
            if (UpgradeChecker.HasUpgrade("cult_starfruit")
                && UpgradeChecker.HasUpgrade("cult_red_cabbage")
                && Game1.random.NextDouble() < 0.10)
            {
                __result = "(O)398"; // Starfruit seed id
                return;
            }

            // Try Red Cabbage.
            if (UpgradeChecker.HasUpgrade("cult_red_cabbage")
                && Game1.random.NextDouble() < 0.10)
            {
                __result = "(O)266"; // Red Cabbage seed id
            }
        }
    }
}
