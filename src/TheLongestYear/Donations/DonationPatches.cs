using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Harmony postfixes on the vanilla Community Center bundle/room completion path. Per-item
    /// donations are NOT patched here — the 2026-05-26 playtest showed that path never fired
    /// against real CC deposits, so <see cref="DonationObserver"/> watches each
    /// <see cref="JunimoNoteMenu"/> session and awards JP via diff-on-completed instead.
    /// We keep these completion-bonus patches because they sit on different methods that
    /// weren't observed misbehaving; if either turns out to break the same way, move the
    /// detection into the observer.
    /// </summary>
    internal static class DonationPatches
    {
        /// <summary>After the menu confirms a bundle is complete: award the bundle bonus.</summary>
        [HarmonyPatch(typeof(JunimoNoteMenu), "checkIfBundleIsComplete")]
        internal static class BundleCompletePatch
        {
            private static void Postfix(JunimoNoteMenu __instance)
            {
                if (DonationService.Active == null)
                    return;

                Bundle bundle = __instance.currentPageBundle;
                if (bundle != null && bundle.complete)
                    DonationService.Active.OnBundleCompleted(bundle.bundleIndex);
            }
        }

        /// <summary>After a room/area is marked complete: award the room bonus.</summary>
        [HarmonyPatch(typeof(CommunityCenter), nameof(CommunityCenter.markAreaAsComplete))]
        internal static class AreaCompletePatch
        {
            private static void Postfix(int area)
                => DonationService.Active?.OnRoomCompleted(area);
        }
    }
}
