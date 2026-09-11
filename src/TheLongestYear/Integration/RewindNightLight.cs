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

        /// <summary>Wires the per-tick write and the return-to-title safety net. Safe to call more
        /// than once; the subscriptions only happen on the first call. Called once from
        /// ModEntry.Entry, the same pattern <see cref="RewindSpringPaint.Register"/> uses.</summary>
        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            if (_registered) return;
            _registered = true;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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
