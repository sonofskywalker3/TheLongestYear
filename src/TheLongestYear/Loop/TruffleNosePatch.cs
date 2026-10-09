using System;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Truffle Hog (spec 2026-10-09 power 4): a pig's dig that turned up a truffle has a 25%
    /// chance to turn up a second one. FarmAnimal.DigUpProduce (FarmAnimal.cs:1658) counts
    /// <c>Game1.stats.TrufflesFound</c> only when the truffle actually spawned (not a Truffle Crab, not a
    /// failed spawn), so the prefix records it and the postfix acts only when it went up. Separate from
    /// DigUpDoublePatch (Kitchen / Double Yolk), which restores currentProduce for an extra dig; the two
    /// stack.</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.DigUpProduce))]
    internal static class TruffleNosePatch
    {
        private const int TruffleSalt = 4302;

        private static void Prefix(out uint __state) => __state = Game1.stats?.TrufflesFound ?? 0;

        private static void Postfix(FarmAnimal __instance, GameLocation location, StardewValley.Object produce, uint __state)
        {
            if (__instance == null || produce == null || UpgradeChecker.HasUpgrade == null || !Game1.IsMasterGame) return;
            if (!UpgradeChecker.HasUpgrade(AnimalPowers.TruffleNose)) return;
            if (produce.QualifiedItemId != AnimalPowers.TruffleQid || Game1.stats.TrufflesFound <= __state) return;
            try
            {
                Random r = Utility.CreateRandom(__instance.myID.Value, Game1.stats.DaysPlayed, Game1.timeOfDay, TruffleSalt);
                if (r.NextDouble() >= AnimalPowers.TruffleDoubleChance) return;
                var second = (StardewValley.Object)produce.getOne();
                GameLocation where = __instance.currentLocation ?? location;
                if (!Utility.spawnObjectAround(Utility.getTranslatedVector2(__instance.Tile, __instance.FacingDirection, 1f), second, where))
                    return;
                Game1.stats.TrufflesFound++;
                PatchLog.Info($"{AnimalPowers.TruffleNose}: {__instance.displayName} dug up a second truffle.");
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"TruffleNosePatch: threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
