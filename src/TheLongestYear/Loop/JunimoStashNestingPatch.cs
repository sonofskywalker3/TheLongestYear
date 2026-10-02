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
    /// onto this menu's cursor (PC: the held item) goes to an empty slot, or drops at the player's
    /// feet when there is none (spec Addendum 2). The cursor item is read and written through
    /// MenuHeldItem: a property on PC, a field on Android.
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

            // Only an item lifted onto this menu's cursor is moved. Anything else (still in its own
            // slot, or from a caller we do not know) is refused where it is, never copied.
            if (!ContainsInstance(who.Items, item)
                && Game1.activeClickableMenu is MenuWithInventory menu
                && ReferenceEquals(MenuHeldItem.Get(menu), item))
                HandBackFromCursor(menu, who, item);
            StashNestingRefusal.ShowHud();
            PatchLog.Trace($"JunimoStashGrabPatch: refused '{item.QualifiedItemId}', holds {string.Join(", ", blocked)}.");
            return false;
        }

        /// <summary>The refused item is on the cursor: put it in an empty inventory slot, or at the
        /// player's feet when there is none, and only then take it off the cursor. If anything
        /// fails, it goes back on the cursor (and out of the slot it was put in), so it is never
        /// lost and never doubled.</summary>
        private static void HandBackFromCursor(MenuWithInventory menu, Farmer who, Item item)
        {
            bool dropped = false;
            try
            {
                if (!StashNestingRefusal.TryPutInEmptySlot(who, item))
                {
                    dropped = GroundDrop.AtFeet(who, item, StashItemCodec.Monitor,
                        "a refused container could not go back in a full inventory");
                    if (!dropped)
                        return; // Still on the cursor: nothing moved, nothing lost.
                }
                if (!MenuHeldItem.TrySet(menu, null))
                    throw new System.InvalidOperationException("the cursor item could not be cleared");
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: this runs inside the game's own menu click. Undo the move.
                for (int i = 0; i < who.Items.Count; i++)
                    if (ReferenceEquals(who.Items[i], item))
                        who.Items[i] = null;
                bool back = !dropped && SafeSetCursor(menu, item);
                PatchLog.Warn($"JunimoStashGrabPatch: handing back '{item.QualifiedItemId}' threw ({ex.GetType().Name}: {ex.Message}); " +
                              (dropped ? "it was dropped at the player's feet and may still be on the cursor." : back ? "it stays on the cursor." : "it could not be put back on the cursor."));
            }
        }

        private static bool SafeSetCursor(MenuWithInventory menu, Item item)
        {
            try
            {
                return MenuHeldItem.TrySet(menu, item);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: the restore inside a catch must not throw again.
                PatchLog.Warn($"JunimoStashGrabPatch: restoring the cursor item threw. {ex.GetType().Name}: {ex.Message}");
                return false;
            }
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
