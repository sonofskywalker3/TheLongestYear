using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Obtainability upgrade: inject Red Cabbage Seeds into the Summer Mixed Seeds roll when the
    /// player owns cult_red_cabbage. 10% substitution chance, applied only in Summer. (The
    /// Starfruit twin was removed 2026-08-21 — the desert is reachable without RNG.) See
    /// <see cref="MixedSeedsPatch"/>.
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

    /// <summary>
    /// Per-run purchasable Boost ownership, read via static Funcs wired by ModEntry.OnSaveLoaded
    /// (and cleared on return to title), mirroring <see cref="UpgradeChecker"/> so patches never
    /// import MetaStore/RunState directly.
    /// </summary>
    internal static class BoostChecker
    {
        /// <summary>Set by ModEntry.OnSaveLoaded: true if the Year-Two Seeds boost is active for
        /// the run's current week-of-year.</summary>
        public static System.Func<bool> YearTwoSeedsActive;

        /// <summary>Set by ModEntry.OnSaveLoaded: true if the Sneak Peek boost is active for the
        /// run's current season.</summary>
        public static System.Func<bool> SneakPeekActive;
    }

    /// <summary>
    /// Postfix on <c>Crop.ResolveSeedId(string, GameLocation)</c> — the ONLY path Mixed Seeds (770)
    /// take when planted (Crop ctor → ResolveSeedId → getRandomLowGradeCropForThisSeason). Returns
    /// UNQUALIFIED seed ids, matching what vanilla returns on that path ("485" Red Cabbage Seeds,
    /// "486" Starfruit Seeds — Data/Crops keys).
    ///
    /// History: 0.9.x–0.11.60 patched <c>Crop.getRandomWildCropForSeason(bool)</c> instead, which is
    /// the WILD-seeds (Spring/Summer/Fall/Winter Seeds) path — so Mixed Seeds never hit and Summer
    /// Seeds grew Red Cabbage (Nexus bug 1109718, four reporters).
    /// </summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.ResolveSeedId), new System.Type[] { typeof(string), typeof(GameLocation) })]
    internal static class MixedSeedsPatch
    {
        private const string MixedSeedsId = "770";
        private const string RedCabbageSeeds = "485";
        private const double SubstitutionChance = 0.10;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(string itemId, GameLocation location, ref string __result)
        {
            if (UpgradeChecker.HasUpgrade == null) return;
            if (itemId != MixedSeedsId) return;
            if (location == null || location.GetSeason() != StardewValley.Season.Summer) return;
            if (location is StardewValley.Locations.IslandLocation) return; // vanilla overrides island picks

            if (UpgradeChecker.HasUpgrade("cult_red_cabbage")
                && Game1.random.NextDouble() < SubstitutionChance)
            {
                __result = RedCabbageSeeds;
            }

            if (BoostChecker.YearTwoSeedsActive?.Invoke() == true)
            {
                string seed = TheLongestYear.Core.YearTwoSeeds.SeedIdFor((TheLongestYear.Core.Season)(int)location.GetSeason());
                if (seed != null && Game1.random.NextDouble() < TheLongestYear.Core.YearTwoSeeds.Chance)
                {
                    __result = seed;
                    PatchLog.Trace($"Year-Two Seeds: Mixed Seeds rolled {seed}");
                }
            }
        }
    }
}
