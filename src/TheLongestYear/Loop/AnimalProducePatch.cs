using System;
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Lucky Rabbits and Fine Feathers (spec 2026-10-09, powers 1 and 2). Postfix on
    /// FarmAnimal.GetProduceID (decompile FarmAnimal.cs:880), the regular-produce pick used by
    /// dayUpdate and growFully. The deluxe pick (deluxe: true) is left alone, so vanilla's own deluxe
    /// roll still runs after this. Data/FarmAnimals is never edited (the effort model reads it).
    ///  - Lucky Rabbits: a Rabbit's regular pick becomes its deluxe pick (Rabbit's Foot), through
    ///    GetProduceID(r, true) so data conditions still apply.
    ///  - Fine Feathers: a Duck's Duck Egg becomes a Duck Feather on a seeded 25% roll, no heart gate.</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.GetProduceID))]
    internal static class AnimalProducePatch
    {
        /// <summary>Salt for Fine Feathers' roll so it never lines up with another seeded roll.</summary>
        private const int FeatherSalt = 4421;

        private static void Postfix(FarmAnimal __instance, Random r, bool deluxe, ref string __result)
        {
            if (deluxe || __result == null || __instance == null || UpgradeChecker.HasUpgrade == null) return;
            try
            {
                string type = __instance.type.Value;
                if (type == AnimalPowers.RabbitType && UpgradeChecker.HasUpgrade(AnimalPowers.LuckyRabbits))
                {
                    string foot = __instance.GetProduceID(r, deluxe: true);
                    if (foot != null && foot != __result)
                    {
                        PatchLog.Trace($"{AnimalPowers.LuckyRabbits}: {__instance.displayName} gives {foot} instead of {__result}.");
                        __result = foot;
                    }
                    return;
                }
                if (type == AnimalPowers.DuckType && __result == AnimalPowers.DuckEggId
                    && UpgradeChecker.HasUpgrade(AnimalPowers.FineFeathers)
                    && Utility.CreateRandom(__instance.myID.Value, Game1.stats.DaysPlayed, FeatherSalt).NextDouble()
                        < AnimalPowers.FineFeatherChance)
                {
                    PatchLog.Trace($"{AnimalPowers.FineFeathers}: {__instance.displayName} gives a Duck Feather instead of an egg.");
                    __result = AnimalPowers.DuckFeatherId;
                }
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"AnimalProducePatch: threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
