using System;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Interactables;
using TheLongestYear.Loop;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug: drive the Herd Book headlessly (its menu cannot be clicked through the bridge).
    /// register and remove run the same HerdBookService calls as the menu. In memory only; persists
    /// on the next save like every other MetaState edit.</summary>
    internal static class HerdBookDebugCommand
    {
        public const string Name = "tly_herdbook";
        public const string Description =
            "Debug: the Herd Book. Usage: tly_herdbook list | record | register <slot> <animal name> | remove <slot> | friend <animal name> <0-1000>";

        private const int MinArgsRegister = 3;
        private const int MinArgsFriend = 3;
        private const int MinArgsRemove = 2;

        public static void Run(IMonitor monitor, MetaState meta, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (meta == null || args.Length < 1) { monitor.Log(Description, LogLevel.Warn); return; }

            switch (args[0].ToLowerInvariant())
            {
                case "list":
                    List(monitor, meta);
                    break;
                case "record":
                    AnimalSpeciesRecorder.Record(meta, monitor);
                    monitor.Log($"tly_herdbook: species ever owned: {string.Join(", ", meta.AnimalSpeciesEverOwned)}.", LogLevel.Info);
                    break;
                case "register" when args.Length >= MinArgsRegister && int.TryParse(args[1], out int slot):
                {
                    FarmAnimal animal = FindByName(string.Join(" ", args.Skip(2)));
                    if (animal == null) { monitor.Log("tly_herdbook: no farm animal with that name.", LogLevel.Warn); break; }
                    HerdBookService.Register(meta, slot, animal, monitor);
                    break;
                }
                case "remove" when args.Length >= MinArgsRemove && int.TryParse(args[1], out int removeSlot):
                    HerdBookService.Remove(meta, removeSlot, monitor);
                    break;
                case "friend" when args.Length >= MinArgsFriend && int.TryParse(args[^1], out int friendship):
                {
                    FarmAnimal animal = FindByName(string.Join(" ", args.Skip(1).Take(args.Length - 2)));
                    if (animal == null) { monitor.Log("tly_herdbook: no farm animal with that name.", LogLevel.Warn); break; }
                    animal.friendshipTowardFarmer.Value = HerdBookRules.ClampFriendship(friendship);
                    monitor.Log($"tly_herdbook: '{animal.Name}' friendship set to {animal.friendshipTowardFarmer.Value}.", LogLevel.Info);
                    break;
                }
                default:
                    monitor.Log(Description, LogLevel.Warn);
                    break;
            }
        }

        private static FarmAnimal FindByName(string name)
            => HerdBookService.LiveAnimals().FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        private static void List(IMonitor monitor, MetaState meta)
        {
            var slots = HerdBookRules.SlotsFor(meta);
            int held = Game1.player.Items.Count(i => i?.ItemId == BookKit.HerdBookId);
            int placed = 0;
            Utility.ForEachLocation(loc => { placed += loc.furniture.Count(f => f.ItemId == BookKit.HerdBookId); return true; });
            monitor.Log(
                $"tly_herdbook: {slots.Count} slots, {HerdBookRules.Used(meta.HerdBook, slots.Count)} registered; " +
                $"book held={held} placed={placed}.",
                LogLevel.Info);
            for (int i = 0; i < slots.Count; i++)
            {
                HerdEntry e = HerdBookRules.EntryAt(meta.HerdBook, i);
                string keepId = HerdSlotRules.RequiredKeepId(slots[i]);
                string keep = meta.HasUpgrade(keepId) ? "" : $" (needs {keepId})";
                monitor.Log(e == null
                    ? $"  slot {i} {slots[i]}: empty{keep}"
                    : $"  slot {i} {slots[i]}: '{e.Name}' {e.Type} id {e.AnimalId} friendship {e.Friendship} age {e.Age}{keep}",
                    LogLevel.Info);
            }
            foreach (FarmAnimal a in HerdBookService.LiveAnimals())
                monitor.Log(
                    $"  farm: '{a.Name}' {a.type.Value} id {a.myID.Value} friendship {a.friendshipTowardFarmer.Value} " +
                    $"age {a.age.Value} adult={a.isAdult()} home={a.home?.buildingType.Value ?? "none"}",
                    LogLevel.Info);
        }
    }
}
