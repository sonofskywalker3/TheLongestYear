using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Adds every farm animal's species to <see cref="MetaState.AnimalSpeciesEverOwned"/>,
    /// the list the Start-with keeps gate on. Before this nothing wrote the list except the reset's
    /// own starting animals, so no Start-with keep could ever be bought (spec 2026-09-25).
    /// Called from ModEntry on DayStarted and Saving, and by <c>tly_herdbook record</c>.</summary>
    internal static class AnimalSpeciesRecorder
    {
        public static void Record(MetaState meta, IMonitor monitor)
        {
            Farm farm = Game1.getFarm();
            if (meta == null || farm == null) return;
            foreach (FarmAnimal animal in farm.getAllFarmAnimals())
            {
                if (AnimalSpecies.Record(meta.AnimalSpeciesEverOwned, animal.type.Value))
                    monitor.Log(
                        $"Species recorded: {AnimalSpecies.Normalize(animal.type.Value)} (from '{animal.type.Value}').",
                        LogLevel.Info);
            }
        }
    }
}
