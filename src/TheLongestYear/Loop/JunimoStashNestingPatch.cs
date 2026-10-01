using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// The stash nesting rule at the moment of deposit (spec 2026-10-01): a container whose contents,
    /// at any depth, include anything that is not a hat, shirt, pants, furniture, wallpaper or
    /// flooring is handed back unchanged with a HUD line. Nothing is trimmed or deleted later.
    /// Prefix on Chest.addItem, for callers outside the menu (a mod's hopper or automation):
    /// returning the item as the result hands it back to the caller. The menu deposit
    /// (Chest.grabItemFromInventory) is refused one step earlier by JunimoStashGrabPatch, and
    /// deposits that write a slot directly (a held item clicked onto a slot, Android drag) by the
    /// stash inventory's slot watcher in JunimoStashService.NestingGuard.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.addItem))]
    internal static class JunimoStashNestingPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static bool Prefix(Chest __instance, Item item, ref Item __result)
        {
            if (item == null || !__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return true;

            IReadOnlyList<string> blocked = StashNestingRefusal.Blocked(item);
            if (blocked.Count == 0)
                return true;

            __result = item;
            StashNestingRefusal.ShowHud();
            PatchLog.Trace($"JunimoStashNestingPatch: refused '{item.QualifiedItemId}', holds {string.Join(", ", blocked)}.");
            return false;
        }
    }

    /// <summary>
    /// The menu deposit, refused before vanilla runs. Vanilla grabItemFromInventory hands a refused
    /// item back with Farmer.addItemToInventory, which stacks. A chest stacks to 999 and
    /// canStackWith ignores contents, and on Android the item is still in its own inventory slot
    /// when this runs (ItemGrabMenu passes inventory.actualInventory[dragItem]), so a filled chest
    /// would stack onto itself: its stack doubles and is then set to zero. Letting a stackable
    /// item through to the slot watcher instead is no fix: addItem would then report success and
    /// vanilla would call removeItemFromInventory on the instance the watcher just handed back.
    /// So for the stash: an item still in the inventory stays where it is; one already lifted out
    /// (PC: the menu's held item) goes to an empty slot, or the overflow chest when there is none.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.grabItemFromInventory))]
    internal static class JunimoStashGrabPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static bool Prefix(Chest __instance, Item item, Farmer who)
        {
            if (item == null || who == null || !__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return true;

            IReadOnlyList<string> blocked = StashNestingRefusal.Blocked(item);
            if (blocked.Count == 0)
                return true;

            if (!ContainsInstance(who.Items, item))
            {
                if (Game1.activeClickableMenu is ItemGrabMenu menu && ReferenceEquals(menu.heldItem, item))
                    menu.heldItem = null;
                if (!StashNestingRefusal.TryPutInEmptySlot(who, item))
                    JunimoStashService.StoreInOverflowChest(Game1.getFarm(), __instance.TileLocation, item, StashItemCodec.Monitor);
            }
            StashNestingRefusal.ShowHud();
            PatchLog.Trace($"JunimoStashGrabPatch: refused '{item.QualifiedItemId}', holds {string.Join(", ", blocked)}.");
            return false;
        }

        private static bool ContainsInstance(IList<Item> items, Item item)
        {
            foreach (Item slot in items)
                if (ReferenceEquals(slot, item))
                    return true;
            return false;
        }
    }
}
