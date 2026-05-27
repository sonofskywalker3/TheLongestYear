using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Opens <see cref="CookbookMenu"/> when the player presses the action button on the
    /// configured kitchen counter tile inside the FarmHouse. Only activates when:
    /// - <c>HouseUpgradeLevel >= 1</c> (kitchen built), AND
    /// - the player owns at least <c>cookbook_1</c>.
    ///
    /// Tile coords configured via <see cref="GameplayConfig.CookbookTileX"/> / <see cref="GameplayConfig.CookbookTileY"/>.
    /// Default (0,0) = disabled; set via the <c>tly_setcookbook</c> console command after locating
    /// the tile in-game with <c>tly_here</c>.
    /// </summary>
    internal static class CookbookInteractable
    {
        private static CookbookInteractableInstance _instance;

        public static void ConnectTo(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _instance = new CookbookInteractableInstance(monitor, config, meta);
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

    internal sealed class CookbookInteractableInstance
    {
        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly MetaStore _meta;

        public CookbookInteractableInstance(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _monitor = monitor;
            _config  = config;
            _meta    = meta;
        }

        public bool TryHandle(FarmHouse house, xTile.Dimensions.Location tile,
            Farmer who, ref bool result)
        {
            // (0,0) means "not yet configured" — fall through to vanilla.
            if (_config.CookbookTileX == 0 && _config.CookbookTileY == 0) return true;
            if (tile.X != _config.CookbookTileX || tile.Y != _config.CookbookTileY) return true;

            // Kitchen must be built.
            if (who.HouseUpgradeLevel < 1) return true;

            // Must own at least Cookbook I.
            if (!_meta.State.HasUpgrade("cookbook_1")) return true;

            // Guard: don't open over an existing menu.
            if (Game1.activeClickableMenu != null) { result = true; return false; }

            Game1.activeClickableMenu = new CookbookMenu(_monitor, _meta.State);
            _monitor.Log("CookbookInteractable: opened CookbookMenu.", LogLevel.Trace);
            result = true;
            return false;
        }
    }
}
