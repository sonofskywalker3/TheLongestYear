using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's draw-side pieces: the Harmony patches that hide the farmhouse and its
    /// mail flag while the scene has torn the house down.</summary>
    internal static partial class JojaBadEndingCommands
    {
        private static bool IsHiddenFarmhouse(Building building)
            => _farmhouseHidden && building != null && building == Game1.getFarm()?.GetMainFarmHouse();

        [HarmonyPatch(typeof(Building), nameof(Building.draw))]
        internal static class HideFarmhouseDraw
        {
            private static bool Prefix(Building __instance) => !IsHiddenFarmhouse(__instance);
        }

        /// <summary>The farm's new-mail flag floats over the mailbox; with the house gone it would
        /// hang in the air. While hidden, the mailbox is off the map.</summary>
        [HarmonyPatch(typeof(Farmer), nameof(Farmer.getMailboxPosition))]
        internal static class HideMailFlag
        {
            private static readonly Point OffMap = new(-100, -100);

            private static void Postfix(ref Point __result)
            {
                if (_farmhouseHidden) __result = OffMap;
            }
        }
    }
}
