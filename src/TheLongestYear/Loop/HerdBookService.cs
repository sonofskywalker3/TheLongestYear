using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
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

        /// <summary>Step 0f of the reset, before loadForNewGame: every registered animal still on the
        /// farm takes its current snapshot, so it comes back with this loop's hearts. One that is gone
        /// keeps its last snapshot.</summary>
        public static void RefreshBeforeReset(MetaState meta, IMonitor monitor)
        {
            if (meta == null || meta.HerdBook.Count == 0) return;
            var liveById = new Dictionary<long, HerdEntry>();
            foreach (FarmAnimal animal in LiveAnimals())
                liveById[animal.myID.Value] = Snapshot(animal, -1);
            List<HerdEntry> refreshed = HerdBookRules.Refresh(meta.HerdBook, liveById);
            foreach (HerdEntry e in refreshed)
                monitor.Log(liveById.ContainsKey(e.AnimalId)
                    ? $"HerdBook: refreshed '{e.Name}' ({e.Type}) in slot {e.SlotIndex}: friendship {e.Friendship}, age {e.Age}."
                    : $"HerdBook: '{e.Name}' (slot {e.SlotIndex}) is not on the farm; keeping its last snapshot (friendship {e.Friendship}).",
                    LogLevel.Info);
            meta.HerdBook = refreshed;
        }

        /// <summary>Step 10a of the reset, right after the Start-with animals: rebuild every registered
        /// animal as an adult in a kept building with room (see <see cref="HerdPlacement"/>). An entry
        /// that cannot come back is logged, gets a HUD line, and stays in the book.</summary>
        public static void Restore(MetaState meta, IMonitor monitor)
        {
            if (meta == null || meta.HerdBook.Count == 0) return;
            Farm farm = Game1.getFarm();
            if (farm == null)
            {
                monitor.Log("HerdBook: no farm after the reset; nothing restored, entries kept.", LogLevel.Warn);
                return;
            }
            List<Building> homes = farm.buildings
                .Where(b => b.GetIndoors() is AnimalHouse && AnimalHousing.Chain(b.buildingType.Value).Tier > 0)
                .ToList();
            var houses = homes
                .Select(b =>
                {
                    var house = (AnimalHouse)b.GetIndoors();
                    return new HerdHouse(b.buildingType.Value, house.animalLimit.Value - house.animalsThatLiveHere.Count);
                })
                .ToList();

            int slotCount = HerdBookRules.SlotsFor(meta).Count;
            foreach (HerdAssignment a in HerdPlacement.Assign(meta.HerdBook, slotCount, houses))
            {
                if (a.Skip != HerdSkip.None) { ReportWaiting(a, monitor); continue; }
                Building home = homes[a.HouseIndex];
                FarmAnimal animal = Rebuild(a.Entry, monitor);
                if (animal == null) continue;
                animal.home = home;
                ((AnimalHouse)home.GetIndoors()).adoptAnimal(animal);
                if (animal.myID.Value != a.Entry.AnimalId)
                {
                    int index = meta.HerdBook.FindIndex(e => e.SlotIndex == a.Entry.SlotIndex);
                    meta.HerdBook[index] = a.Entry with { AnimalId = animal.myID.Value };
                }
                monitor.Log(
                    $"HerdBook: restored '{animal.Name}' ({animal.type.Value}) into {home.buildingType.Value} as adult " +
                    $"(age {animal.age.Value}), friendship {animal.friendshipTowardFarmer.Value}, id {animal.myID.Value}.",
                    LogLevel.Info);
            }
        }

        /// <summary>A fresh FarmAnimal carrying the entry's identity. Reuses the saved id (keeps the
        /// gender of MaleOrFemale animals) unless the fresh farm already has an animal with it.</summary>
        private static FarmAnimal Rebuild(HerdEntry entry, IMonitor monitor)
        {
            long id = Utility.getAnimal(entry.AnimalId) == null ? entry.AnimalId : (long)Utility.RandomLong();
            var animal = new FarmAnimal(entry.Type, id, Game1.player.UniqueMultiplayerID);
            StardewValley.GameData.FarmAnimals.FarmAnimalData data = animal.GetAnimalData();
            if (data == null)
            {
                monitor.Log($"HerdBook: '{entry.Name}' is a '{entry.Type}', which Data/FarmAnimals no longer has; entry kept.", LogLevel.Warn);
                return null;
            }
            animal.Name = entry.Name;
            animal.displayName = entry.Name;
            if (!string.IsNullOrEmpty(entry.SkinId))
                animal.skinID.Value = entry.SkinId;
            animal.friendshipTowardFarmer.Value = HerdBookRules.ClampFriendship(entry.Friendship);
            animal.happiness.Value = HerdBookRules.ClampHappiness(entry.Happiness);
            animal.age.Value = HerdBookRules.AdultAge(entry.Age, data.DaysToMature);
            animal.daysOwned.Value = entry.DaysOwned;
            animal.hasEatenAnimalCracker.Value = entry.HasEatenAnimalCracker;
            animal.allowReproduction.Value = entry.AllowReproduction;
            animal.ReloadTextureIfNeeded();   // baby sprite to adult sprite
            return animal;
        }

        private static void ReportWaiting(HerdAssignment a, IMonitor monitor)
        {
            if (a.Skip == HerdSkip.SlotNotOwned)
            {
                monitor.Log($"HerdBook: '{a.Entry.Name}' sits in slot {a.Entry.SlotIndex}, which this save does not own; entry kept.", LogLevel.Warn);
                return;
            }
            HerdSlotKind kind = HerdSlotRules.KindAt(a.Entry.SlotIndex);
            string keep = UpgradeCatalog.TryGet(HerdSlotRules.RequiredKeepId(kind))?.DisplayName ?? HerdSlotRules.RequiredHousing(kind);
            monitor.Log(
                $"HerdBook: '{a.Entry.Name}' ({a.Entry.Type}, slot {a.Entry.SlotIndex}) waits: {a.Skip} " +
                $"(needs a {HerdSlotRules.RequiredHousing(kind)} or better with room). Entry kept.",
                LogLevel.Info);
            Game1.addHUDMessage(new HUDMessage(
                Strings.Get(a.Skip == HerdSkip.NoRoom ? "hud.herdbook.no-room" : "hud.herdbook.no-building",
                    new Dictionary<string, string> { ["name"] = a.Entry.Name, ["keep"] = keep }),
                HUDMessage.newQuest_type));
        }
    }
}
