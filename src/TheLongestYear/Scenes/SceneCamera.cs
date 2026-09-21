using System;
using Microsoft.Xna.Framework;
using StardewValley;

namespace TheLongestYear.Scenes
{
    /// <summary>The camera an overnight strike scene borrows (spec 2026-09-21), shared by the crows,
    /// the thief and the hall. It takes the view away from the farmer, puts it on a tile of the
    /// scene's choosing at night, and gives every last piece of it back.
    ///
    /// The save-and-set half is copied from <c>WitchEvent.setUp</c> and <c>FairyEvent.setUp</c>
    /// (PC 1.6 decompile): reassign <see cref="Game1.currentLocation"/> rather than warping,
    /// <c>resetForPlayerEntry</c> on the new one, clear the fade, freeze the viewport, hide the
    /// farmer, and move <see cref="Game1.viewport"/> by hand with both axes clamped to the map.
    ///
    /// The give-it-back half is NOT copied from them, because they do not have one: vanilla puts the
    /// world right after any farm event ends (Game1.cs:3801 to 3841, and the warp callback under it).
    /// A strike scene cannot rely on that. It also plays from the debug command in the middle of an
    /// ordinary day, where nothing vanilla ever runs, and it can end early by a skip or a throw. So
    /// every field this class touches is remembered on the way in and written back by
    /// <see cref="Restore"/>, which the scene calls from its cleanup hook on EVERY ending.
    ///
    /// Night is <see cref="NightClock"/> rather than a tint over the finished frame: the engine's own
    /// lighting pass derives <see cref="Game1.outdoorLight"/> from <see cref="Game1.timeOfDay"/>
    /// (<c>Game1.UpdateGameClock</c>, Game1.cs:5627), so driving the clock gives the real night look,
    /// light pools and all. 2400 and not 2600: vanilla passes the farmer out at 2600
    /// (Game1.cs:6021), and the debug command plays this while he is awake.</summary>
    internal static class SceneCamera
    {
        /// <summary>The clock the scene plays at. Past truly-dark, short of the pass-out hour.</summary>
        public const int NightClock = 2400;

        /// <summary>The night a scene plays in. This is <c>WitchEvent</c>'s own value
        /// (WitchEvent.cs:82), not the darker colour the clock's own ramp lands on at 2am: the
        /// engine subtracts the lightmap from the world, and the full 2am ramp takes the farm so far
        /// down that a row of crops cannot be told from bare soil. Vanilla picked a lighter night
        /// for its own farm events for exactly that reason. Caught on the first screenshot pass,
        /// 2026-09-21.</summary>
        private static readonly Color NightAmbient = new Color(200, 190, 40);

        /// <summary>What the scene's night does to a white surface, and so the tint a scene must put
        /// on anything it draws ITSELF.
        ///
        /// A farm event draws after the lightmap has been composited (Game1.cs:13697), so a sprite a
        /// scene paints is never lit and comes out looking like a daylight sticker on a night frame.
        /// The lightmap is subtracted with ColorSourceBlend SourceColor and ReverseSubtract
        /// (Game1.lightingBlend), so a world pixel loses ambient squared, and a white one lands on
        /// 255 minus ambient squared. Measured off a screenshot pair to be sure of it: the mailbox
        /// reads (255,250,228) by day and (98,108,222) inside this scene, against a predicted
        /// (98,113,249). A SpriteBatch tint multiplies rather than subtracts, so it cannot be exact
        /// for mid tones, but it puts white exactly where the lightmap would and never clips.</summary>
        public static Color NightTint { get; } = new Color(
            255 - NightAmbient.R * NightAmbient.R / 255,
            255 - NightAmbient.G * NightAmbient.G / 255,
            255 - NightAmbient.B * NightAmbient.B / 255);

        private const int TileSize = 64;

        private static bool _active;
        private static GameLocation _priorLocation;
        private static int _priorViewportX;
        private static int _priorViewportY;
        private static int _priorTimeOfDay;
        private static Color _priorAmbientLight;
        private static Color _priorOutdoorLight;
        private static bool _priorViewportFreeze;
        private static bool _priorDisplayFarmer;
        private static bool _priorNonWarpFade;
        private static bool _priorFadeToBlack;
        private static bool _priorFadeIn;
        private static float _priorFadeAlpha;
        private static int _priorGameTimeInterval;

        /// <summary>Is a scene holding the camera right now?</summary>
        public static bool Active => _active;

        /// <summary>Take the view to this map and centre it on this tile, at night. The first call
        /// remembers what it is replacing. A later call while the camera is still held only moves
        /// it, so a scene can follow something without losing the way home.</summary>
        public static void CutTo(GameLocation where, Vector2 tile)
        {
            if (where == null) throw new ArgumentNullException(nameof(where));
            if (!_active)
            {
                _priorLocation = Game1.currentLocation;
                _priorViewportX = Game1.viewport.X;
                _priorViewportY = Game1.viewport.Y;
                _priorTimeOfDay = Game1.timeOfDay;
                _priorAmbientLight = Game1.ambientLight;
                _priorOutdoorLight = Game1.outdoorLight;
                _priorViewportFreeze = Game1.viewportFreeze;
                _priorDisplayFarmer = Game1.displayFarmer;
                _priorNonWarpFade = Game1.nonWarpFade;
                // fadeClear() below wipes all three of these, so they are remembered with the rest.
                _priorFadeToBlack = Game1.fadeToBlack;
                _priorFadeIn = Game1.fadeIn;
                _priorFadeAlpha = Game1.fadeToBlackAlpha;
                // The clock is held at NightClock for the whole scene, so whatever part of a ten
                // minute tick had already gone by must come back with it.
                _priorGameTimeInterval = Game1.gameTimeInterval;
                _active = true;
            }
            Game1.currentLocation = where;
            where.resetForPlayerEntry();
            Game1.fadeClear();
            Game1.nonWarpFade = true;
            Game1.viewportFreeze = true;
            Game1.displayFarmer = false;
            HoldNight();
            CenterOn(where, tile);
        }

        /// <summary>Write the night again. The engine recomputes <see cref="Game1.outdoorLight"/>
        /// from the clock and then copies it into <see cref="Game1.ambientLight"/> on every tick the
        /// player is on the map (<c>GameLocation._updateAmbientLighting</c>), so a scene that wants
        /// its own light has to say so every tick. That is the same per-tick write
        /// <c>RewindNightLight</c> makes, and for the same reason.</summary>
        public static void HoldNight()
        {
            if (!_active) return;
            Game1.timeOfDay = NightClock;
            Game1.ambientLight = NightAmbient;
            Game1.outdoorLight = NightAmbient;
            Game1.drawLighting = true;
        }

        /// <summary>Put the viewport's middle on this tile, clamped so the frame never runs off the
        /// map. A map smaller than the frame on an axis is pinned to 0 on that axis, exactly as
        /// <c>WitchEvent</c> does it.</summary>
        public static void CenterOn(GameLocation where, Vector2 tile)
        {
            if (where?.map == null) return;
            Game1.viewport.X = Math.Max(0, Math.Min(where.map.DisplayWidth - Game1.viewport.Width, (int)tile.X * TileSize + TileSize / 2 - Game1.viewport.Width / 2));
            Game1.viewport.Y = Math.Max(0, Math.Min(where.map.DisplayHeight - Game1.viewport.Height, (int)tile.Y * TileSize + TileSize / 2 - Game1.viewport.Height / 2));
        }

        /// <summary>Give everything back. Safe to call when no scene ever took the camera, and safe
        /// to call twice.</summary>
        public static void Restore()
        {
            if (!_active) return;
            _active = false;
            Game1.timeOfDay = _priorTimeOfDay;
            Game1.ambientLight = _priorAmbientLight;
            Game1.outdoorLight = _priorOutdoorLight;
            Game1.viewportFreeze = _priorViewportFreeze;
            Game1.displayFarmer = _priorDisplayFarmer;
            Game1.nonWarpFade = _priorNonWarpFade;
            Game1.fadeToBlack = _priorFadeToBlack;
            Game1.fadeIn = _priorFadeIn;
            Game1.fadeToBlackAlpha = _priorFadeAlpha;
            Game1.gameTimeInterval = _priorGameTimeInterval;
            Game1.viewport.X = _priorViewportX;
            Game1.viewport.Y = _priorViewportY;
            GameLocation back = Game1.player?.currentLocation ?? _priorLocation;
            _priorLocation = null;
            if (back == null) return;
            Game1.currentLocation = back;
            // Puts the map's own light sources and local state back the way entering it would. On
            // the overnight path vanilla warps the farmer a moment later and does it again, which
            // costs nothing, and without it the debug preview would keep the night's lamp lights.
            try { back.resetForPlayerEntry(); }
            catch (Exception) { /* a map that will not reset is still better than a held camera. */ }
        }

        /// <summary>Where a world pixel lands on the screen.</summary>
        public static Vector2 ToScreen(Vector2 worldPixels) => Game1.GlobalToLocal(Game1.viewport, worldPixels);

        /// <summary>The tiles the frame currently shows, whole ones only.</summary>
        public static Rectangle FrameInTiles()
        {
            int left = (int)Math.Ceiling(Game1.viewport.X / (double)TileSize);
            int top = (int)Math.Ceiling(Game1.viewport.Y / (double)TileSize);
            int right = (Game1.viewport.X + Game1.viewport.Width) / TileSize;
            int bottom = (Game1.viewport.Y + Game1.viewport.Height) / TileSize;
            return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        /// <summary>The middle of a tile, in world pixels.</summary>
        public static Vector2 TileCentre(Vector2 tile) => new Vector2(tile.X * TileSize + TileSize / 2f, tile.Y * TileSize + TileSize / 2f);
    }
}
