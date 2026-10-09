using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Growing Herd (spec 2026-10-09, round 2): barn animals give birth more often.
    ///
    /// Vanilla's overnight barn birth is <c>QuestionEvent(2)</c>, which Utility.pickPersonalFarmEvent
    /// returns on half of the nights that have no other farm event (the other half is the dogs sound).
    /// Its setUp (QuestionEvent.cs:51) walks the player's buildings and, for the first Big or Deluxe Barn
    /// (Data/Buildings AllowAnimalPregnancy) that is not full and passes
    /// <c>Game1.random &lt; animalsThatLiveHere x 0.0055</c>, picks one animal living there at random. The birth
    /// happens only when that animal is an adult with reproduction allowed and a species that can get
    /// pregnant (cow, goat, sheep, pig); otherwise the night is wasted.
    ///
    /// With the power the prefix runs the same walk with <see cref="AnimalPowers.BirthChance"/> (four times
    /// vanilla's per-animal chance) and picks only among the animals that can give birth, then does what
    /// vanilla does on success (the birth dialogue, then tickUpdate's NamingMenu and
    /// AnimalHouse.addNewHatchedAnimal). The coin flip in pickPersonalFarmEvent is left alone, so the dogs
    /// night still happens. Never on a night whose morning rewinds (the birth would be rewound, and its
    /// menus would race the Fail scene like the owl did, see FarmEventSuppressionPatch).</summary>
    [HarmonyPatch(typeof(QuestionEvent), nameof(QuestionEvent.setUp))]
    internal static class GrowingHerdPatch
    {
        private static readonly AccessTools.FieldRef<QuestionEvent, int> WhichQuestion =
            AccessTools.FieldRefAccess<QuestionEvent, int>("whichQuestion");
        private static readonly AccessTools.FieldRef<QuestionEvent, AnimalHouse> BirthHouse =
            AccessTools.FieldRefAccess<QuestionEvent, AnimalHouse>("animalHouse");

        /// <summary>Debug only (tly_animalpowers birthnight): the next barn-birth setUp rolls a certain birth.</summary>
        internal static bool ForceNextBirth;

        private static bool Prefix(QuestionEvent __instance, ref bool __result)
        {
            if (__instance == null || WhichQuestion(__instance) != QuestionEvent.barnBirth || !Game1.IsMasterGame) return true;
            bool force = ForceNextBirth;
            ForceNextBirth = false;
            bool owned = UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade(AnimalPowers.GrowingHerd);
            if (!owned && !force) return true;
            if (FarmEventSuppressionPatch.SuppressTonight?.Invoke() == true) return true;
            try
            {
                FarmAnimal parent = null;
                AnimalHouse home = null;
                Utility.ForEachBuilding((Building b) =>
                {
                    if ((b.owner.Value != Game1.player.UniqueMultiplayerID && Game1.IsMultiplayer) || !b.AllowsAnimalPregnancy()
                        || b.GetIndoors() is not AnimalHouse house || house.isFull())
                        return true;
                    double chance = force ? 1.0 : AnimalPowers.BirthChance(house.animalsThatLiveHere.Count);
                    if (Game1.random.NextDouble() >= chance) return true;
                    var parents = new List<FarmAnimal>();
                    foreach (long id in house.animalsThatLiveHere)
                    {
                        FarmAnimal a = Utility.getAnimal(id);
                        if (a != null && AnimalPowers.CanGiveBirth(a.isBaby(), a.allowReproduction.Value, a.CanHavePregnancy()))
                            parents.Add(a);
                    }
                    if (parents.Count == 0) return true;   // no possible parent here, try the next barn
                    parent = parents[Game1.random.Next(parents.Count)];
                    home = house;
                    return false;
                });
                if (parent == null)
                {
                    __result = true;   // no event tonight, as vanilla when its roll fails
                    return false;
                }
                BirthHouse(__instance) = home;
                __instance.animal = parent;
                Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Events:AnimalBirth", parent.displayName, parent.shortDisplayType()));
                Game1.messagePause = true;
                PatchLog.Info($"{AnimalPowers.GrowingHerd}: {parent.displayName} ({parent.type.Value}) gives birth tonight" +
                              (force ? " (forced by tly_animalpowers birthnight)." : "."));
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"GrowingHerdPatch: threw {ex.GetType().Name}: {ex.Message}; vanilla's roll runs instead.");
                return true;
            }
        }
    }
}
