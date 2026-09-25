using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Game-side glue for the Herd Book (spec docs/superpowers/specs/2026-09-25-herd-book-design.md):
    /// reads the farm's animals, snapshots one into a <see cref="HerdEntry"/> and registers it. The rules
    /// (ladder, which animal fits which slot) live in Core: <see cref="HerdSlotRules"/>, <see cref="HerdBookRules"/>.</summary>
    internal static class HerdBookService
    {
        /// <summary>Every farm animal on the Farm, outdoors and inside its buildings.</summary>
        public static List<FarmAnimal> LiveAnimals()
            => Game1.getFarm()?.getAllFarmAnimals() ?? new List<FarmAnimal>();

        public static HerdAnimal ToHerdAnimal(FarmAnimal animal)
            => new HerdAnimal(animal.myID.Value, animal.type.Value, animal.Name, animal.friendshipTowardFarmer.Value);

        public static List<HerdAnimal> LiveHerdAnimals() => LiveAnimals().Select(ToHerdAnimal).ToList();

        /// <summary>The animal as it is right now. See <see cref="HerdEntry"/> for why these fields.</summary>
        public static HerdEntry Snapshot(FarmAnimal animal, int slotIndex) => new HerdEntry(
            SlotIndex: slotIndex,
            AnimalId: animal.myID.Value,
            Type: animal.type.Value,
            Name: animal.Name,
            SkinId: animal.skinID.Value,
            Friendship: animal.friendshipTowardFarmer.Value,
            Happiness: animal.happiness.Value,
            Age: animal.age.Value,
            DaysOwned: animal.daysOwned.Value,
            HasEatenAnimalCracker: animal.hasEatenAnimalCracker.Value,
            AllowReproduction: animal.allowReproduction.Value);

        /// <summary>The farm animals a slot of <paramref name="kind"/> can take right now.</summary>
        public static List<FarmAnimal> Candidates(HerdSlotKind kind, MetaState meta)
        {
            List<FarmAnimal> live = LiveAnimals();
            var byId = new Dictionary<long, FarmAnimal>();
            foreach (FarmAnimal animal in live)
                byId[animal.myID.Value] = animal;
            return HerdBookRules.Candidates(kind, live.Select(ToHerdAnimal), meta.HerdBook)
                .Select(c => byId[c.Id])
                .ToList();
        }

        /// <summary>Register <paramref name="animal"/> in <paramref name="slotIndex"/> with its current
        /// snapshot. Used by the menu and by <c>tly_herdbook register</c>, so both run the same rules.</summary>
        public static HerdRegisterResult Register(MetaState meta, int slotIndex, FarmAnimal animal, IMonitor monitor)
        {
            HerdEntry entry = Snapshot(animal, slotIndex);
            HerdRegisterResult result = HerdBookRules.Register(meta.HerdBook, HerdBookRules.SlotsFor(meta), entry);
            monitor.Log(
                $"HerdBook: register '{entry.Name}' ({entry.Type}, id {entry.AnimalId}) in slot {slotIndex}: {result} " +
                $"(friendship {entry.Friendship}, age {entry.Age}).",
                LogLevel.Info);
            return result;
        }

        public static bool Remove(MetaState meta, int slotIndex, IMonitor monitor)
        {
            HerdEntry gone = HerdBookRules.EntryAt(meta.HerdBook, slotIndex);
            bool removed = HerdBookRules.Remove(meta.HerdBook, slotIndex);
            monitor.Log(
                removed ? $"HerdBook: removed '{gone?.Name}' from slot {slotIndex}." : $"HerdBook: slot {slotIndex} was already empty.",
                LogLevel.Info);
            return removed;
        }
    }
}
