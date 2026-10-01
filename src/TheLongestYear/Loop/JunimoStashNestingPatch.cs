using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Objects;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// The stash nesting rule at the moment of deposit (spec 2026-10-01): a container whose contents,
    /// at any depth, include anything that is not a hat, shirt, pants, furniture, wallpaper or
    /// flooring is handed back unchanged with a HUD line. Nothing is trimmed or deleted later.
    /// Prefix on Chest.addItem, the PC shift-click path (Chest.grabItemFromInventory). Returning
    /// the item as the result makes vanilla put it back in the inventory. Deposits that write a
    /// slot directly (a held item clicked onto a slot, Android drag) are caught by the stash
    /// inventory's slot watcher in JunimoStashService.NestingGuard.
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
}
