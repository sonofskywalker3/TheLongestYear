using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// In-world interactable that opens <see cref="SeasonGoalsMenu"/> when the player presses the
    /// action button on a chosen tile inside the Community Center. Uses vanilla's tile-action
    /// registry (<c>GameLocation.RegisterTileAction</c>) — same extension hook the base game uses
    /// for "Billboard", "MasteryCave_Pedestal", "Bookseller", etc. The action key is namespaced so
    /// it can't collide with any other mod's action.
    ///
    /// Default tile coords: (45, 11) — one tile west of the Bulletin Board room's Junimo Note
    /// (which sits at (46, 11) per <c>CommunityCenter.getNotePosition(5)</c>). The player can move
    /// the board by editing <c>SeasonGoalsBoardTileX/Y</c> in config.json — useful if the default
    /// tile overlaps map furniture, or to put it closer to the entrance.
    ///
    /// Visual feedback: a small parchment overlay drawn via <see cref="IDisplayEvents.RenderedWorld"/>
    /// so the player can find the tile. Vanilla doesn't draw anything for Action tile properties
    /// alone, so without this overlay the board would be invisible.
    /// </summary>
    internal sealed class SeasonGoalsBoard
    {
        /// <summary>Namespaced action key. The leading author segment guards against any other
        /// mod registering a key called "SeasonGoals".</summary>
        public const string ActionKey = "sonofskywalker3.TheLongestYear_SeasonGoals";

        /// <summary>Hardcoded map name where the board lives. Always the (community-center
        /// instance's) interior — the Refurbished / Joja variants share the same location name.</summary>
        private const string TargetLocationName = "CommunityCenter";

        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly Func<MenuLauncher> _launcherAccessor;

        /// <param name="launcherAccessor">Resolved lazily — MenuLauncher isn't constructed until
        /// OnSaveLoaded, but this board's events fire from Entry onward. Returns null until the
        /// save is loaded; we guard for that in the action handler.</param>
        public SeasonGoalsBoard(IModHelper helper, IMonitor monitor, GameplayConfig config,
            Func<MenuLauncher> launcherAccessor)
        {
            _monitor = monitor;
            _config = config;
            _launcherAccessor = launcherAccessor;

            // RegisterTileAction is idempotent (set with non-null overwrites; null removes), so
            // re-running this on save-load reruns is fine. Done once at Entry to keep the lifecycle
            // simple.
            GameLocation.RegisterTileAction(ActionKey, OnTileAction);

            helper.Events.Player.Warped += OnWarped;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        // ---------- placement ----------

        /// <summary>On save load, paint the action onto the target tile in the (already-loaded) CC
        /// location so the player can interact with it without first warping there + back.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            ApplyActionTile(Game1.getLocationFromName(TargetLocationName) as CommunityCenter);
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.NewLocation is CommunityCenter cc)
                ApplyActionTile(cc);
        }

        /// <summary>Set the Action tile property on the configured tile so vanilla's
        /// <c>GameLocation.checkAction</c> dispatches to our handler when the player interacts.
        /// We do this every CC warp (and every save load) because re-entering the CC can reload
        /// the map (Refurbished/Joja variants swap maps mid-game), and a re-load would drop any
        /// property we painted on a previous instance.</summary>
        private void ApplyActionTile(CommunityCenter cc)
        {
            if (cc == null) return;
            int x = _config.SeasonGoalsBoardTileX;
            int y = _config.SeasonGoalsBoardTileY;

            var map = cc.map;
            if (map == null)
            {
                _monitor.Log("Season Goals board: CC map is null; can't set Action property.", LogLevel.Warn);
                return;
            }

            var layer = map.GetLayer("Buildings");
            if (layer == null)
            {
                _monitor.Log("Season Goals board: CC map has no 'Buildings' layer.", LogLevel.Warn);
                return;
            }

            if (x < 0 || y < 0 || x >= layer.LayerWidth || y >= layer.LayerHeight)
            {
                _monitor.Log(
                    $"Season Goals board: configured tile ({x},{y}) is outside the CC map " +
                    $"({layer.LayerWidth}x{layer.LayerHeight}). Edit SeasonGoalsBoardTileX/Y in config.json.",
                    LogLevel.Warn);
                return;
            }

            // The vanilla checkAction path first reads tile.Properties["Action"], then falls back to
            // doesTileHaveProperty(...) which scans tile-property layers. We need the action to
            // resolve regardless of whether a Tile instance exists at the coord, so we set the
            // property on both: the Tile instance (if any) for the fast path, and the location-
            // level property registry for the fallback.
            var tile = layer.Tiles[x, y];
            if (tile != null)
                tile.Properties["Action"] = ActionKey;

            // location-level property registry. setTileProperty is on Map, not GameLocation,
            // but xTile exposes it via Layer.Properties or per-tile property bags. The most
            // portable option is to assign to tile.Properties (above) when a tile exists; when
            // the tile slot is empty (no building drawn there), checkAction falls through to
            // doesTileHaveProperty which can't find anything we'd set without modifying the
            // map's TilesheetProperties — so empty slots aren't supported. The default coords
            // (45,11 — west of the Bulletin Board Junimo Note) sit on a real tile.

            _monitor.Log(
                tile != null
                    ? $"Season Goals board placed at CC ({x},{y})."
                    : $"Season Goals board: CC tile ({x},{y}) has no Buildings tile to paint. " +
                      "Pick a coord with a real wall/decoration tile via SeasonGoalsBoardTileX/Y.",
                tile != null ? LogLevel.Trace : LogLevel.Warn);
        }

        // ---------- vanilla action callback ----------

        /// <summary>Invoked by vanilla's tile-action dispatch when the player presses Action on
        /// a tile whose Action property is <see cref="ActionKey"/>. Returns true to mark the
        /// action consumed (no further fallthrough).</summary>
        private bool OnTileAction(GameLocation _, string[] _args, Farmer _who, Point _tile)
        {
            // CanOpen guards (world ready, no other menu, not in a cutscene) live in MenuLauncher.
            MenuLauncher launcher = _launcherAccessor();
            if (launcher == null)
            {
                _monitor.Log("Season Goals tile pressed before launcher was ready.", LogLevel.Warn);
                return false;
            }
            launcher.OpenSeasonGoals();
            return true;
        }

        // ---------- visual hint ----------

        /// <summary>Draw a small scroll/parchment icon at the board's tile so the player can find
        /// it. Without this, an Action-property tile is invisible in vanilla — the cursor doesn't
        /// change colour, the player has no clue something is interactable here.</summary>
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.currentLocation is not CommunityCenter) return;

            int tileX = _config.SeasonGoalsBoardTileX;
            int tileY = _config.SeasonGoalsBoardTileY;

            // World → screen. Stardew renders tiles at (tile*64 - viewport offset) at zoom 1.
            // Game1.GlobalToLocal converts world-pixel coords to viewport-local pixel coords.
            Vector2 worldPos = new Vector2(tileX * Game1.tileSize, tileY * Game1.tileSize);
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, worldPos);

            // Source rect for "?" cursor icon from LooseSprites\Cursors. Same one we use for
            // missing-item placeholders so the visual style stays consistent across the mod.
            const int iconScale = 4;
            var sourceRect = new Rectangle(403, 496, 5, 7);
            int w = sourceRect.Width * iconScale;
            int h = sourceRect.Height * iconScale;

            // Center the icon horizontally over the tile; hover it slightly above so it doesn't
            // bury the underlying decoration.
            float floatBob = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalSeconds * 3.0) * 4f;
            var dest = new Rectangle(
                (int)screenPos.X + (Game1.tileSize - w) / 2,
                (int)screenPos.Y - h - 8 + (int)floatBob,
                w, h);

            e.SpriteBatch.Draw(Game1.mouseCursors, dest, sourceRect, Color.White);
        }
    }
}
