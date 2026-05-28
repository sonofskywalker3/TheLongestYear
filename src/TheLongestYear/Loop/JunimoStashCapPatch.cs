using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Enforces the Junimo Stash slot cap on <see cref="Chest.addItem"/>.
    ///
    /// The patch is a postfix: vanilla runs first and handles stacking (stacking onto an
    /// existing stack is always allowed because it doesn't consume a new slot). After vanilla
    /// returns null (item accepted), we check whether the stash's non-null item count now
    /// exceeds the cap. If it does, we remove the just-added item, restore it to the return
    /// value, and show a HUD message.
    ///
    /// Gate: only fires when <c>chest.modData["tly.junimo.stash"] == "1"</c>.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.addItem))]
    internal static class JunimoStashCapPatch
    {
        private static IMonitor _monitor;
        private static MetaState _meta;

        public static void Connect(IMonitor monitor, MetaState meta)
        {
            _monitor = monitor;
            _meta    = meta;
        }

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance, Item item, ref Item __result)
        {
            // Only act on the stash chest, and only when vanilla accepted the item (result null).
            if (__result != null)
                return;
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return;
            if (_meta == null)
                return;

            int cap = _meta.StashSlotCount;
            if (cap == 0)
                return;

            // Count non-null items in the player's inventory for this chest.
            // GetItemsForPlayer() returns the right IInventory (single-player = Items).
            var inv = __instance.GetItemsForPlayer();
            int nonNullCount = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                if (inv[i] != null) nonNullCount++;
            }

            if (nonNullCount <= cap)
                return;  // within cap — accept as-is

            // Slot cap exceeded. Undo vanilla's acceptance: remove the item from the inventory.
            // The item landed in one of: a null slot (set by reference), or was added via .Add().
            // We find it by scanning for the item reference itself.
            for (int i = inv.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(inv[i], item))
                {
                    inv[i] = null;
                    break;
                }
            }

            // Return the item (rejected).
            __result = item;

            // HUD message.
            Game1.showRedMessage(
                $"Junimo Stash is full! ({cap} slot{(cap == 1 ? "" : "s")} maximum)");

            _monitor?.Log(
                $"JunimoStashCapPatch: rejected item '{item.QualifiedItemId}' — stash at cap ({cap}).",
                LogLevel.Trace);
        }
    }

    /// <summary>
    /// Dismisses the "tly.stash" indicator the first time the player opens the stash chest,
    /// and strips the chest-tint color picker from the opened ItemGrabMenu. Vanilla ItemGrabMenu
    /// shows the color picker for any Chest whose <c>SpecialChestType</c> is None or BigChest —
    /// our stash chest is forced to SpecialChestType.None (to keep local per-save inventory
    /// instead of the team-shared Junimo Chest pool), which would normally surface the picker.
    /// The 2026-05-28 playtest explicitly asked for it to be hidden.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.ShowMenu))]
    internal static class JunimoStashShowMenuPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance)
        {
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return;

            IndicatorRegistry.Dismiss("tly.stash");

            // Strip the color picker from the freshly-opened menu. ShowMenu sets
            // Game1.activeClickableMenu = new ItemGrabMenu(...); at this postfix point the
            // menu is fully constructed (the constructor finished allocating chestColorPicker
            // + colorPickerToggleButton). Nulling the fields hides the swatch row and toggle
            // button — Draw and click handlers no-op on null.
            if (Game1.activeClickableMenu is ItemGrabMenu igm)
            {
                igm.chestColorPicker = null;
                igm.colorPickerToggleButton = null;
                igm.discreteColorPickerCC = null;
                Game1.player.showChestColorPicker = false;
            }
        }
    }

    /// <summary>
    /// Caps the stash chest's <see cref="Chest.GetActualCapacity"/> at the current
    /// <see cref="MetaState.StashSlotCount"/> (4 base + 4 per stash_1/2/3 upgrade owned). This is
    /// what controls how many slots ItemGrabMenu draws on open — a non-upgraded stash shows 4
    /// slots, fully upgraded shows 16. Without this patch the chest opens with the full vanilla
    /// 36-slot grid (the JunimoStashCapPatch still REJECTS over-cap deposits, but the UI lies
    /// about the available space).
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.GetActualCapacity))]
    internal static class JunimoStashCapacityPatch
    {
        private static MetaState _meta;

        public static void Connect(MetaState meta) => _meta = meta;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance, ref int __result)
        {
            if (_meta == null) return;
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey)) return;

            int cap = _meta.StashSlotCount;
            if (cap > 0)
                __result = cap;
        }
    }

    /// <summary>
    /// Prevents the player from picking up the stash chest with a pickaxe or axe. The stash is
    /// part of the run loop (placed on every save load, populated from MetaState) — letting the
    /// player pick it up would put a regular-chest copy in their inventory that wouldn't sync
    /// with the meta layer + would leak the modData tag onto a portable chest. Returning false
    /// from performToolAction matches the vanilla semantics for "the tool didn't break this."
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.performToolAction))]
    internal static class JunimoStashImmovablePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(Chest __instance, ref bool __result)
        {
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return true; // run original

            __result = false;
            return false; // skip original
        }
    }
}
