using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Opens <see cref="CraftbookMenu"/> when the player presses the action button on the
    /// configured farmhouse table tile inside the FarmHouse. Only activates when the player
    /// owns at least <c>craftbook_1</c>. No in-run house-upgrade prereq (accessible from day 1).
    ///
    /// Tile coords configured via <see cref="GameplayConfig.CraftbookTileX"/> / <see cref="GameplayConfig.CraftbookTileY"/>.
    /// Default (0,0) = disabled; set via the <c>tly_setcraftbook</c> console command after locating
    /// the tile in-game with <c>tly_here</c>.
    /// </summary>
    internal static class CraftbookInteractable
    {
        private static CraftbookInteractableInstance _instance;

        public static void ConnectTo(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _instance = new CraftbookInteractableInstance(monitor, config, meta);
        }

        [HarmonyPatch(typeof(FarmHouse), nameof(FarmHouse.checkAction))]
        internal static class CheckActionPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            private static bool Prefix(FarmHouse __instance, xTile.Dimensions.Location tileLocation,
                xTile.Dimensions.Rectangle viewport, Farmer who, ref bool __result)
            {
                if (_instance == null) return true;
                return _instance.TryHandle(__instance, tileLocation, who, ref __result);
            }
        }
    }

    internal sealed class CraftbookInteractableInstance
    {
        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly MetaStore _meta;

        public CraftbookInteractableInstance(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _monitor = monitor;
            _config  = config;
            _meta    = meta;
        }

        public bool TryHandle(FarmHouse house, xTile.Dimensions.Location tile,
            Farmer who, ref bool result)
        {
            if (_config.CraftbookTileX == 0 && _config.CraftbookTileY == 0) return true;
            if (tile.X != _config.CraftbookTileX || tile.Y != _config.CraftbookTileY) return true;

            if (!_meta.State.HasUpgrade("craftbook_1")) return true;

            // Guard: don't open over an existing menu.
            if (Game1.activeClickableMenu != null) { result = true; return false; }

            Game1.activeClickableMenu = new CraftbookMenu(_monitor, _meta.State);
            _monitor.Log("CraftbookInteractable: opened CraftbookMenu.", LogLevel.Trace);
            result = true;
            return false;
        }
    }
}
