using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
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
    /// Dismisses the "tly.stash" indicator the first time the player opens the stash chest.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.ShowMenu))]
    internal static class JunimoStashShowMenuPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance)
        {
            if (__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                IndicatorRegistry.Dismiss("tly.stash");
        }
    }
}
