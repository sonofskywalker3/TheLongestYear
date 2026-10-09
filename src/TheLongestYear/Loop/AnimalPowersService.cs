using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Pets;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The animal powers that are not Harmony patches (spec 2026-10-09):
    ///  - Morning Rounds (power 10): at day start the host auto-pets every farm animal, the same call an
    ///    Auto-Petter makes overnight (FarmAnimal.pet with is_auto_pet). A real Auto-Petter adds nothing
    ///    on top (vanilla no-op for a second auto-pet).
    ///  - Loyal Pet (power 12): Data/Pets edit, GiftChance at least 0.4 and every gift's friendship
    ///    threshold at most 600 (3 hearts). Data/Pets is read by nothing else in TLY. The asset is
    ///    reloaded when the power is bought, on save load and at the title screen.</summary>
    internal sealed class AnimalPowersService
    {
        private const string PetsAsset = "Data/Pets";

        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;

        public AnimalPowersService(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
        }

        private static bool Has(string id) => UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade(id);

        /// <summary>Morning Rounds. Wired to GameLoop.DayStarted.</summary>
        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Context.IsMainPlayer || !Has(AnimalPowers.MorningRounds)) return;
            Farm farm = Game1.getFarm();
            if (farm == null || Game1.player == null) return;
            int petted = 0, already = 0;
            foreach (FarmAnimal animal in farm.getAllFarmAnimals())
            {
                if (animal == null) continue;
                if (animal.wasAutoPet.Value) { already++; continue; }
                try
                {
                    animal.pet(Game1.player, is_auto_pet: true);
                    petted++;
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Morning Rounds: petting {animal.displayName} threw {ex.GetType().Name}: {ex.Message}", LogLevel.Trace);
                }
            }
            _monitor.Log($"Morning Rounds: petted {petted} animal(s) ({already} already petted).", LogLevel.Info);
        }

        /// <summary>Reload Data/Pets so the Loyal Pet edit matches what is owned now.</summary>
        public void RefreshPets(string reason)
        {
            _helper.GameContent.InvalidateCache(PetsAsset);
            _monitor.Log($"Loyal Pet: Data/Pets reloaded ({reason}; power {(Has(AnimalPowers.LoyalPet) ? "owned" : "not owned")}).", LogLevel.Trace);
        }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(PetsAsset) || !Has(AnimalPowers.LoyalPet)) return;
            e.Edit(asset =>
            {
                IDictionary<string, PetData> pets = asset.AsDictionary<string, PetData>().Data;
                foreach (PetData pet in pets.Values)
                {
                    if (pet == null) continue;
                    pet.GiftChance = AnimalPowers.GiftChance(pet.GiftChance);
                    if (pet.Gifts == null) continue;
                    foreach (PetGift gift in pet.Gifts)
                        if (gift != null)
                            gift.MinimumFriendshipThreshold = AnimalPowers.GiftThreshold(gift.MinimumFriendshipThreshold);
                }
            }, AssetEditPriority.Late);
        }
    }
}
