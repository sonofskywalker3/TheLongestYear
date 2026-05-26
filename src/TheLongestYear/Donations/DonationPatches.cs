using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Harmony postfixes on the vanilla Community Center donation path. We only observe — vanilla still
    /// consumes the item, marks the bundle, and grants the in-run room reward.
    /// </summary>
    internal static class DonationPatches
    {
        /// <summary>After an item is deposited into a bundle slot: award per-item JP + ledger.</summary>
        [HarmonyPatch(typeof(Bundle), nameof(Bundle.tryToDepositThisItem))]
        internal static class DepositPatch
        {
            // Capture the stack before deposit so the postfix can compute how many were consumed.
            private static void Prefix(Item item, out int __state) => __state = item?.Stack ?? 0;

            private static void Postfix(Item item, int __state)
            {
                if (DonationService.Active == null || item == null)
                    return;

                int consumed = __state - item.Stack;
                if (consumed > 0)
                    DonationService.Active.OnItemDonated(item.QualifiedItemId, consumed);
            }
        }

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
