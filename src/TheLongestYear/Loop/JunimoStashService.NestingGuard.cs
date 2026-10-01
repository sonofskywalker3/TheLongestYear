using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;

namespace TheLongestYear.Loop
{
    internal sealed partial class JunimoStashService
    {
        // The stash inventory currently watched, and our own removal in flight (so taking the
        // refused container back out does not re-enter the watcher).
        private Inventory _guardedItems;
        private bool _ejecting;

        /// <summary>Watch the stash chest's slots for a refused container. Chest.addItem (patched by
        /// JunimoStashNestingPatch) is only the shift-click path: a held item clicked onto a slot on
        /// PC, and every drag deposit on Android (InventoryMenu.tryToAddItemAt, then
        /// Utility.addItemToInventory), write the slot directly. Inventory.OnSlotChanged sees all of
        /// them. Idempotent: called on every PlaceChest and FindStashChest.</summary>
        private void GuardNesting(Chest chest)
        {
            Inventory items = chest?.Items;
            if (items == null || ReferenceEquals(items, _guardedItems))
                return;
            if (_guardedItems != null)
                _guardedItems.OnSlotChanged -= OnStashSlotChanged;
            _guardedItems = items;
            items.OnSlotChanged += OnStashSlotChanged;
        }

        private void OnStashSlotChanged(Inventory inventory, int index, Item before, Item after)
        {
            // Only a deposit this player is making through the stash's own menu. Restores
            // (PopulateFromMeta, TryDeposit) run with no menu open, and another player's deposit is
            // refused on that player's machine.
            if (_ejecting || after == null || !IsStashMenuOpenFor(inventory))
                return;
            IReadOnlyList<string> blocked = StashNestingRefusal.Blocked(after);
            if (blocked.Count == 0)
                return;

            _ejecting = true;
            bool removed = false;
            try
            {
                if (index < 0 || index >= inventory.Count || !ReferenceEquals(inventory[index], after))
                    return;
                inventory[index] = null;
                removed = true;
                // An empty slot, never Farmer.addItemToInventory: that stacks, and on Android the
                // dragged stack is still in its own slot, so it would stack onto itself.
                if (!StashNestingRefusal.TryPutInEmptySlot(Game1.player, after))
                {
                    // Inventory full: never delete, the overflow chest beside the stash takes it.
                    StoreInOverflowChest(after);
                    _monitor.Log($"JunimoStashService: refused '{after.QualifiedItemId}' and the inventory was full; it went to the overflow chest.", LogLevel.Info);
                }
                removed = false;
                StashNestingRefusal.ShowHud();
                _monitor.Log($"JunimoStashService: refused '{after.QualifiedItemId}' at slot {index}, holds {string.Join(", ", blocked)}.", LogLevel.Trace);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: this runs inside the game's own menu click. If the container
                // was taken out but never reached the inventory, it goes back in its slot: refused
                // or not, it is never lost.
                if (removed && inventory[index] == null)
                    inventory[index] = after;
                _monitor.Log($"JunimoStashService: refusing '{after.QualifiedItemId}' threw ({ex.GetType().Name}: {ex.Message})" +
                             (removed ? "; it stays in the stash." : "."), LogLevel.Error);
            }
            finally
            {
                _ejecting = false;
            }
        }

        private static bool IsStashMenuOpenFor(Inventory inventory)
            => Game1.activeClickableMenu is ItemGrabMenu menu
               && menu.context is Chest chest
               && ReferenceEquals(chest.Items, inventory);
    }
}
