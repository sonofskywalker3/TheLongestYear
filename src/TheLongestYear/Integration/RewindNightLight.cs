using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>The rewind bedroom's darkness, driven through the engine's own lighting pass instead
    /// of a translucent sheet painted over the finished frame (playtest 2026-09-11: the sheet dimmed
    /// the HUD along with the world, produced no light pools, and read as someone turning the
    /// brightness down).
    ///
    /// HOW THE ENGINE DRAWS DARK ROOMS. <c>Game1.UpdateOther</c> (Game1.cs:6131 in the PC 1.6
    /// decompile) sets <c>Game1.drawLighting</c> whenever <c>Game1.ambientLight</c> is not white; the
    /// draw pass then fills a lightmap with the ambient colour, composites every entry in
    /// <c>Game1.currentLightSources</c> into it with real radial falloff, and subtracts the finished
    /// lightmap from the world (<c>lightingBlend</c> is a ReverseSubtract). Two consequences this
    /// class is built around:
    ///
    /// - The lightmap is subtracted from the WORLD only, never the interface, which is exactly why
    ///   moving off the overlay stops the HUD dimming.
    /// - Because it subtracts, a light source's colour is the colour it REMOVES: <c>Color.Black</c>
    ///   is a fully bright pool (vanilla's own colourless LightSource constructor uses Black), and
    ///   <c>Color.White</c> would be a black hole. A Junimo's palette colour therefore cannot be
    ///   handed straight to its light; <see cref="PoolTint"/> converts it.
    ///
    /// WHY A PER-TICK WRITE. <c>GameLocation.UpdateWhenCurrentLocation</c> calls
    /// <c>_updateAmbientLighting</c> every tick the player is in the room, menu or no menu, and
    /// FarmHouse's override sets ambient back to white at 3am (its night branch only engages past
    /// <c>getStartingToGetDarkTime</c>). That runs AFTER a menu's own update and BEFORE
    /// <c>UpdateOther</c>, so a scene writing ambient from its <c>update</c> would be overwritten and
    /// <c>drawLighting</c> computed from the overwrite. SMAPI's <c>UpdateTicked</c> fires after the
    /// whole of <c>Game1.Update</c> and before the draw, so this writes ambient there and sets
    /// <c>drawLighting</c> itself rather than waiting for the engine to recompute it.
    ///
    /// It also strips every light source that is not one of the scene's own, every tick: the
    /// farmhouse lamps and fireplace, and the farmer's own glow ring and lantern, which is the
    /// suppression the brief asks for. Ring lights live in <c>GameLocation.sharedLights</c> and are
    /// union-ed into <c>currentLightSources</c> on location entry, so removing them here holds.</summary>
    internal static class RewindNightLight
    {
        /// <summary>Where the scene opens. This is <c>MineShaft</c>'s own dark-area lighting colour
        /// (MineShaft.cs:656), the floors that need a lantern: the "cave darkness" the designer asked
        /// for. The first pass used vanilla's ordinary indoor night ambient
        /// (<c>indoorLightingNightColor</c>, 150/150/30) and the room stayed far too bright for the
        /// Junimo pools to read against at all (playtest 2026-09-11: "I still can't see the light
        /// until you turn on the cave darkness"). The pools have to be visible from the first
        /// frame, so the room starts at cave dark rather than arriving there.</summary>
        public static readonly Color NightAmbient = new Color(230, 200, 90);

        /// <summary>The darkness at its deepest, past even the deepest mine floor
        /// (MineShaft.cs:688 is 237/212/185). Not a full 255 subtraction, so the room goes very
        /// dark rather than mathematically black and the Junimo pools still have something to sit
        /// on.</summary>
        public static readonly Color DeepAmbient = new Color(245, 238, 225);

        private static IMonitor _monitor;
        private static bool _registered;
        private static bool _holding;
        private static Color _ambient = NightAmbient;
        private static ICollection<string> _ownedLightIds;
        private static GameLocation _nightTiles;
        private static double _heartbeatMs;

        /// <summary>Wires the per-tick write and the return-to-title safety net. Safe to call more
        /// than once; the subscriptions only happen on the first call. Called once from
        /// ModEntry.Entry, the same pattern <see cref="RewindSpringPaint.Register"/> uses.</summary>
        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            if (_registered) return;
            _registered = true;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => Release();
        }

        /// <summary>Starts driving the room's lighting. <paramref name="ownedLightIds"/> is held by
        /// reference, not copied, so a scene can keep adding its lights after this call and they are
        /// still recognised as its own.</summary>
        public static void Begin(ICollection<string> ownedLightIds)
        {
            _ownedLightIds = ownedLightIds;
            _ambient = NightAmbient;
            _holding = true;
            _heartbeatMs = 0.0;
            _nightTiles = Game1.currentLocation;
            SwitchTiles(_nightTiles, night: true);
            Paint();
        }

        /// <summary>The ambient colour to subtract from the world this beat. Between
        /// <see cref="NightAmbient"/> and <see cref="DeepAmbient"/> while the darkness closes in, and
        /// back toward black (subtract nothing) as the Junimos flare.</summary>
        public static Color Ambient
        {
            get => _ambient;
            set => _ambient = value;
        }

        /// <summary>Hands the room's lighting back to the engine. Idempotent. The next tick's
        /// <c>_updateAmbientLighting</c> recomputes ambient from the real clock, so nothing has to be
        /// restored by hand.</summary>
        public static void Release()
        {
            if (!_holding) return;
            _holding = false;
            _ownedLightIds = null;
            SwitchTiles(_nightTiles, night: false);
            _nightTiles = null;
            _monitor?.Log("RewindNightLight: released the room's lighting back to the engine.", LogLevel.Info);
        }

        /// <summary>The light colour that makes a pool READ as <paramref name="palette"/>. The
        /// lightmap is subtracted, so a pool's tint is the complement of what its light removes: a
        /// fully neutral (black) light leaves the room's own colours, and leaning the light toward
        /// the complement of the palette entry pushes the pool toward the palette entry itself.
        /// <paramref name="strength"/> keeps that lean gentle; at 1 the pool would be a flat colour
        /// wash rather than a lit patch of floor.</summary>
        public static Color PoolTint(Color palette, float strength)
        {
            var complement = new Color(255 - palette.R, 255 - palette.G, 255 - palette.B);
            return Color.Lerp(Color.Black, complement, MathHelper.Clamp(strength, 0f, 1f));
        }

        /// <summary>A pool's finished light colour: its tint, held back from full brightness.
        ///
        /// <paramref name="brightness"/> is the piece the first version was missing. A light source
        /// that subtracts nothing does not make its pool bright, it makes the pool the brightness
        /// the world was ALREADY drawn at, which is full daylight; that is why six of them turned a
        /// cave-dark room into an ordinary afternoon. Interpolating from the ambient instead means a
        /// pool takes back only part of what the darkness removed: lit, and still night.</summary>
        public static Color PoolColour(Color palette, float tintStrength, float brightness)
            => Color.Lerp(
                NightAmbient,
                PoolTint(palette, tintStrength),
                MathHelper.Clamp(brightness, 0f, 1f));


        /// <summary>Swaps the room's windows between their day and night art.
        ///
        /// The ambient can take the room down to a cave and the windows will still be painted with
        /// daylight coming through them, because the window tiles are MAP DATA, not lighting: the
        /// game swaps them from the <c>NightTiles</c> and <c>DayTiles</c> map properties, and only
        /// ever at a location entry or on the ten-minute clock tick that crosses dusk. This scene
        /// arrives on a 6am wake frame and paints two in the morning over it, so neither of those
        /// ever ran and the sun stayed in the window (playtest 2026-09-11: "the sun is shining in
        /// the window, what's going on?").
        ///
        /// Night is vanilla's own <c>switchOutNightTiles</c>. Day has no public counterpart -- it is
        /// inline in <c>GameLocation.resetLocalState</c> -- so the same <c>DayTiles</c> property is
        /// read back here. Both are wrapped: a map without either property, or with a malformed
        /// entry, must not take the cutscene down with it.</summary>
        private static void SwitchTiles(GameLocation loc, bool night)
        {
            if (loc?.map == null) return;
            try
            {
                if (night)
                {
                    int entries = loc.GetMapPropertySplitBySpaces("NightTiles").Length / 4;
                    _monitor?.Log(
                        $"RewindNightLight: swapping in {entries} night tile(s) for '{loc.Name}'.",
                        LogLevel.Info);
                    loc.switchOutNightTiles();
                    return;
                }

                string[] dayTiles = loc.GetMapPropertySplitBySpaces("DayTiles");
                for (int i = 0; i + 3 < dayTiles.Length; i += 4)
                {
                    if (!ArgUtility.TryGet(dayTiles, i, out string layerId, out string _)
                        || !ArgUtility.TryGetPoint(dayTiles, i + 1, out Point position, out string _)
                        || !ArgUtility.TryGetInt(dayTiles, i + 3, out int tileIndex, out string _))
                        continue;
                    xTile.Layers.Layer layer = loc.map.GetLayer(layerId);
                    xTile.Tiles.Tile tile = layer?.Tiles[position.X, position.Y];
                    if (tile != null) tile.TileIndex = tileIndex;
                }
            }
            catch (System.Exception ex)
            {
                _monitor?.Log($"RewindNightLight: could not swap the {(night ? "night" : "day")} tiles: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>What the finished frame ACTUALLY had, once a second, after everything that
        /// could have overwritten it has run. This scene cannot be read off a screenshot from here
        /// and its whole job is three globals, so it says out loud what they were at draw time: the
        /// difference between "the room is not dark" meaning the ambient never landed and it meaning
        /// the Junimo pools are washing it out is one line of log rather than another playtest.</summary>
        private static void OnRendered(object sender, RenderedEventArgs e)
        {
            if (!_holding) return;
            _heartbeatMs += Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? 0.0;
            if (_heartbeatMs < 1000.0) return;
            _heartbeatMs = 0.0;
            _monitor?.Log(
                $"RewindNightLight: drawLighting={Game1.drawLighting}, ambient={Game1.ambientLight}, " +
                $"lights={Game1.currentLightSources.Count}, fadeToBlack={Game1.fadeToBlackAlpha:0.00}, " +
                $"timeOfDay={Game1.timeOfDay}.",
                LogLevel.Trace);
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_holding) return;
            // Dormancy (project rule): _holding is static and outlives the save it was set on.
            if (!RunActivation.IsActive) { Release(); return; }
            Paint();
        }

        private static void Paint()
        {
            StripForeignLights();
            Game1.ambientLight = _ambient;
            // UpdateOther already recomputed this from the ambient the location put back, so setting
            // the colour alone is not enough: say so directly. See the class comment.
            Game1.drawLighting = true;
        }

        /// <summary>Everything in the light table that is not one of the scene's own goes: the
        /// farmhouse's lamps and fireplace, and anything the farmer carries or wears.</summary>
        private static void StripForeignLights()
        {
            List<string> foreign = null;
            foreach (string key in Game1.currentLightSources.Keys)
            {
                if (_ownedLightIds != null && _ownedLightIds.Contains(key)) continue;
                (foreign ??= new List<string>()).Add(key);
            }
            if (foreign == null) return;
            foreach (string key in foreign)
                Game1.currentLightSources.Remove(key);
        }
    }
}
