using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// In-world interactable that opens <see cref="SeasonGoalsMenu"/> when the player presses
    /// the action button on a configured tile inside the Community Center. The user wants this
    /// anchored to the CC's fireplace (or similarly fixed landmark) — no visual cue here, the
    /// player just knows from in-story dialogue (Plan 06+) that the board is above the fireplace.
    ///
    /// Patching strategy: <see cref="CommunityCenter.checkAction"/> overrides the base virtual
    /// method and dispatches on tile-index FIRST (Bulletin Board note = 1799 → vanilla 3-bundle
    /// gate; Junimo Notes 1824-1833 → checkBundle; default → base). A Harmony prefix on the
    /// override runs before the switch, so we get a chance to handle the configured tile
    /// regardless of what's underneath it. Same prefix also bypasses the 3-bundle gate on
    /// tile 1799 (it would otherwise leave the Bulletin Board Junimo Note un-interactable on a
    /// fresh run, which the 2026-05-26 playtest surfaced).
    /// </summary>
    internal static class SeasonGoalsBoard
    {
        private static SeasonGoalsBoardInstance _instance;

        /// <summary>Tile index of the Bulletin Board Junimo Note per the decompile
        /// (CommunityCenter.checkAction:375). Vanilla gates its interaction on
        /// numberOfCompleteBundles() > 2; we bypass that since our run model can have an
        /// empty CC at any point and the player still needs to read the bundle.</summary>
        private const int BulletinBoardNoteTileIndex = 1799;

        /// <summary>Half-extents of the rectangular hit area centered on the configured Season
        /// Goals tile. The CC fireplace is WIDE (and short), so a symmetric 3x3 (radius 1) left
        /// the far-right hearth tile un-interactable (2026-06-01 playtest). Widen horizontally to
        /// half-width 2 (a 5-tile-wide span) while keeping half-height 1 (3 tiles tall), so any
        /// tile along the hearth opens the tracker without reaching into unrelated tiles above.</summary>
        private const int SeasonGoalsHitHalfWidth  = 2;
        private const int SeasonGoalsHitHalfHeight = 1;

        public static void ConnectTo(IModHelper helper, IMonitor monitor, GameplayConfig config,
            Func<MenuLauncher> launcherAccessor)
        {
            _instance = new SeasonGoalsBoardInstance(monitor, config, launcherAccessor);
        }

        [HarmonyPatch(typeof(CommunityCenter), nameof(CommunityCenter.checkAction))]
        internal static class CheckActionPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            private static bool Prefix(CommunityCenter __instance, xTile.Dimensions.Location tileLocation,
                xTile.Dimensions.Rectangle viewport, Farmer who, ref bool __result)
            {
                if (_instance == null) return true;

                // 1. Configured Season Goals tile (e.g. above the fireplace) → open the menu.
                // 3x3 hit area centered on the configured tile so any part of the fireplace
                // sprite triggers the menu (player can stand anywhere along the hearth and
                // press action). See SeasonGoalsHitRadius for the rationale.
                int dx = tileLocation.X - _instance.Config.SeasonGoalsBoardTileX;
                int dy = tileLocation.Y - _instance.Config.SeasonGoalsBoardTileY;
                if (System.Math.Abs(dx) <= SeasonGoalsHitHalfWidth
                    && System.Math.Abs(dy) <= SeasonGoalsHitHalfHeight)
                {
                    _instance.OpenGoals();
                    __result = true;
                    return false;
                }

                // 2. Bulletin Board Junimo Note (vanilla gates on >2 complete bundles). Bypass
                // the gate so a fresh-run player can read the bundle even before completing
                // other rooms.
                int tileIndex = __instance.getTileIndexAt(
                    new xTile.Dimensions.Location(tileLocation.X, tileLocation.Y),
                    "Buildings", "indoors");
                if (tileIndex == BulletinBoardNoteTileIndex)
                {
                    __instance.checkBundle(5);
                    __result = true;
                    return false;
                }

                return true;   // fall through to vanilla
            }
        }
    }

    /// <summary>Instance state for the board: config/monitor/launcher accessor. Separate class so
    /// <see cref="SeasonGoalsBoard"/> can stay a pure static type for Harmony's PatchAll discovery.</summary>
    internal sealed class SeasonGoalsBoardInstance
    {
        public GameplayConfig Config { get; }

        private readonly IMonitor _monitor;
        private readonly Func<MenuLauncher> _launcherAccessor;

        public SeasonGoalsBoardInstance(IMonitor monitor, GameplayConfig config,
            Func<MenuLauncher> launcherAccessor)
        {
            _monitor = monitor;
            Config = config;
            _launcherAccessor = launcherAccessor;
        }

        public void OpenGoals()
        {
            MenuLauncher launcher = _launcherAccessor();
            if (launcher == null)
            {
                _monitor.Log("Season Goals tile pressed before launcher was ready.", LogLevel.Warn);
                return;
            }
            launcher.OpenSeasonGoals();
        }
    }
}
