using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Live refresh for Gear upgrades whose effect sits on items that already exist. Runs after every
    /// successful purchase (shrine, debug command, focus card) and once on save load.
    /// </summary>
    internal static class GearRefresh
    {
        private const string GearPrefix = "gear_";

        internal static void AfterPurchase(string upgradeId)
        {
            if (upgradeId == null || !upgradeId.StartsWith(GearPrefix, StringComparison.Ordinal)) return;
            if (upgradeId.StartsWith(GearPowers.Can + "_", StringComparison.Ordinal)) RefreshCans();
            if (IsWeaponChain(upgradeId)) MarkBuffsDirty();
        }

        internal static void OnSaveLoaded()
        {
            RefreshCans();
            MarkBuffsDirty();
        }

        private static bool IsWeaponChain(string upgradeId)
            => upgradeId.StartsWith(GearPowers.Edge + "_", StringComparison.Ordinal)
               || upgradeId.StartsWith(GearPowers.Quick + "_", StringComparison.Ordinal)
               || upgradeId.StartsWith(GearPowers.Guard + "_", StringComparison.Ordinal);

        /// <summary>Weapon bonuses live in the buff total, which is cached until marked dirty.</summary>
        private static void MarkBuffsDirty()
        {
            if (Game1.player?.buffs != null) Game1.player.buffs.Dirty = true;
        }

        private static void RefreshCans()
        {
            int count = 0;
            Utility.ForEachItem(item =>
            {
                if (item is WateringCan can)
                {
                    GearReach.RefreshCan(can);
                    count++;
                }
                return true;
            });
            GearReach.Monitor?.Log($"Gear: refreshed {count} watering can(s).", LogLevel.Trace);
        }
    }
}
