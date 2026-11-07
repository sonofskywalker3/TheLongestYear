using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Empties SpaceCore's custom equipment slots on the rewind, with the boots, rings and trinkets.
    /// Other mods register these slots through SpaceCore (Sword and Sorcery's offhand weapon and
    /// armor slots), and SpaceCore keeps their items on the Farmer itself, which the rewind keeps,
    /// so a dual-wielded weapon carried into every new loop (Celjhyn, Nexus posts, 2026-11-07).
    ///
    /// SpaceCore's API reads and writes one slot by id but cannot list the slots, and its
    /// SetItemInEquipmentSlot runs the slot's validator on the item, which may refuse null. So the
    /// slots are read from SpaceCore's own serializer hook, the public static
    /// <c>SpaceCore.FarmerExtData.get_equippables(Farmer)</c>, and emptied in place. The items'
    /// effects come back through SpaceCore's postfix on <c>Farmer.GetEquippedItems</c>, so once the
    /// slots are empty the next buff recompute drops them; the buffs are marked dirty for that.
    ///
    /// Soft integration and fail open, like <see cref="SpaceCoreSkillReset"/>: nothing happens
    /// without SpaceCore, and an error is logged rather than thrown out of the rewind.
    /// </summary>
    internal sealed class SpaceCoreEquipmentReset
    {
        private const string ExtDataTypeName = "SpaceCore.FarmerExtData";
        private const string GetterName = "get_equippables";

        private readonly IModRegistry _registry;
        private readonly IMonitor _monitor;

        public SpaceCoreEquipmentReset(IModRegistry registry, IMonitor monitor)
        {
            _registry = registry;
            _monitor = monitor;
        }

        /// <summary>Empty every SpaceCore custom equipment slot <paramref name="p"/> has.</summary>
        public void Apply(Farmer p)
        {
            try
            {
                if (p == null || !_registry.IsLoaded(CustomSkillReset.SpaceCoreModId))
                    return;

                MethodInfo getter = AccessTools.Method(AccessTools.TypeByName(ExtDataTypeName), GetterName, new[] { typeof(Farmer) });
                if (getter == null)
                {
                    _monitor.Log($"Rewind: SpaceCore is loaded but {ExtDataTypeName}.{GetterName} was not found; its custom equipment slots are left as they are.", LogLevel.Warn);
                    return;
                }
                if (getter.Invoke(null, new object[] { p }) is not NetStringDictionary<Item, NetRef<Item>> slots)
                {
                    _monitor.Log("Rewind: SpaceCore's custom equipment slots are not the expected type; they are left as they are.", LogLevel.Warn);
                    return;
                }

                var cleared = new List<string>();
                foreach (string slot in slots.Keys.ToList())
                {
                    Item item = slots[slot];
                    if (item == null)
                        continue;
                    slots[slot] = null;
                    cleared.Add($"{slot} ({item.DisplayName})");
                }
                if (cleared.Count == 0)
                    return;
                p.buffs.Dirty = true;
                _monitor.Log($"Rewind: emptied SpaceCore equipment slot(s): {string.Join(", ", cleared)}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Rewind: emptying SpaceCore's custom equipment slots failed; the rest of the rewind goes on. {ex}", LogLevel.Warn);
            }
        }
    }
}
