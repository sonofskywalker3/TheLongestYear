using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;
using Season = StardewValley.Season;

namespace TheLongestYear.Loop
{
    /// <summary>A wild seed crop finished by <see cref="Crop.growCompletely"/> (the crop fairy, on a
    /// night a Farming week or Green Thumb boost had already pushed it past phase 0) stayed a crop:
    /// vanilla only converts wild seeds to forage inside Crop.newDay, which ran before the farm
    /// event. It drew a random mid-growth sprite and harvested the placeholder Wild Horseradish at
    /// normal quality. sigyn2002, Nexus bug 2026-10-03. The postfix does what newDay does
    /// (Crop.cs:869): a random wild crop of the seed's season as spawned forage, so pickup applies
    /// forage quality and foraging XP. A crop already stuck in a save converts on the next newDay.</summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.growCompletely))]
    internal static class WildSeedGrowCompletelyPatch
    {
        /// <summary>Vanilla's marker for forage grown from wild seeds (Crop.cs:902, GameLocation.cs:7635).</summary>
        private const int WildSeedForageMarker = 724519;

        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static void Postfix(Crop __instance)
        {
            GameLocation location = __instance.currentLocation;
            if (location == null) return;
            if (!WildSeedRipening.ShouldBecomeForage(__instance.isWildSeedCrop(), __instance.dead.Value,
                    __instance.currentPhase.Value, __instance.phaseDays.Count))
                return;

            Vector2 tile = __instance.tilePosition;
            Season seedSeason = __instance.whichForageCrop.Value switch
            {
                "495" => Season.Spring,
                "496" => Season.Summer,
                "497" => Season.Fall,
                "498" => Season.Winter,
                _ => location.GetSeason(),
            };
            if (location.objects.TryGetValue(tile, out Object obj))
            {
                if (obj is IndoorPot pot)
                {
                    pot.heldObject.Value = ItemRegistry.Create<Object>(__instance.getRandomWildCropForSeason(seedSeason));
                    pot.hoeDirt.Value.crop = null;
                }
                else
                {
                    location.objects.Remove(tile);
                }
            }
            if (!location.objects.ContainsKey(tile))
            {
                Object spawned = ItemRegistry.Create<Object>(__instance.getRandomWildCropForSeason(seedSeason));
                spawned.IsSpawnedObject = true;
                spawned.CanBeGrabbed = true;
                spawned.SpecialVariable = WildSeedForageMarker;
                location.objects.Add(tile, spawned);
            }
            if (location.terrainFeatures.TryGetValue(tile, out TerrainFeature feature) && feature is HoeDirt dirt)
                dirt.crop = null;
        }
    }
}
