using System;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Warm Welcome and the arrival half of Morning Rounds (spec 2026-10-09 powers 9 and 10).
    /// Postfix on AnimalHouse.adoptAnimal (AnimalHouse.cs:143), which every arrival goes through:
    /// Marnie's shop, an incubator hatch, a barn birth, TLY's "Start with a" animals and vanilla's
    /// `debug animal`. Only a brand-new animal counts (<c>daysOwned &lt; 0</c>; an animal moved between
    /// buildings has lived a night already). Herd Book restores are skipped on purpose: they come back
    /// with their own hearts (<see cref="Restoring"/> is set around HerdBookService.Restore).</summary>
    [HarmonyPatch(typeof(AnimalHouse), nameof(AnimalHouse.adoptAnimal))]
    internal static class AnimalArrivalPatch
    {
        /// <summary>True while the Herd Book puts its animals back; Warm Welcome leaves them alone.</summary>
        internal static bool Restoring;

        private static void Postfix(FarmAnimal animal)
        {
            if (animal == null || Restoring || UpgradeChecker.HasUpgrade == null || !Game1.IsMasterGame) return;
            if (animal.daysOwned.Value >= 0) return;
            try
            {
                int floor = AnimalPowers.WarmWelcomeFloor(UpgradeChecker.HasUpgrade);
                int before = animal.friendshipTowardFarmer.Value;
                int after = AnimalPowers.WelcomedFriendship(before, floor);
                if (after != before)
                {
                    animal.friendshipTowardFarmer.Value = after;
                    PatchLog.Info($"Warm Welcome: {animal.displayName} ({animal.type.Value}) arrives with friendship {after} (was {before}).");
                }
                if (UpgradeChecker.HasUpgrade(AnimalPowers.MorningRounds) && !animal.wasAutoPet.Value && Game1.player != null)
                {
                    animal.pet(Game1.player, is_auto_pet: true);
                    PatchLog.Info($"{AnimalPowers.MorningRounds}: {animal.displayName} petted on arrival (friendship {animal.friendshipTowardFarmer.Value}).");
                }
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"AnimalArrivalPatch: threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
