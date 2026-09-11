using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>The black the rewind ends on, and the HUD it hides while it runs.
    ///
    /// THE BLACK. The Junimos used to vanish the instant the hold question opened: the morning beat
    /// finished, tore its actors down and handed straight to the question, so the circle blinked out
    /// from under the player mid-frame (playtest 2026-09-11: "it's weird that the Junimo circle just
    /// blinks out when the keep box shows up"). Jeff's call was to end the scene properly instead:
    /// fade to black after the last Junimo line, hold the black through the hold question, the
    /// shrine and the theme picker, and fade up only once the player actually has the day, "like a
    /// normal morning".
    ///
    /// So this holds a full-screen black from the end of the morning beat until the surface has been
    /// clear for <see cref="QuietMs"/>, then fades it off. It is painted from
    /// <c>Display.RenderedWorld</c>, which is after the world and BEFORE the HUD and any menu, so
    /// every menu in that stretch reads normally on top of the black.
    ///
    /// WHY A QUIET TIMER AND NOT A HOOK ON THE THEME PICKER. The menus in this window hand over with
    /// a tick or two of nothing in between (the hold question closes, the shrine opens on a later
    /// tick through TickShrineWatchdog; the shrine closes, the reset runs, the planning hub opens on
    /// a free tick), so "no menu is up" on its own lifts the black in the first gap. Waiting for a
    /// continuous quiet stretch rides over those gaps, and it also covers the path where no theme
    /// picker opens at all (an empty offer is skipped, see RunController.PresentOffer), which a hook
    /// on the picker would leave black forever.
    ///
    /// THE HUD. <see cref="HudSuppressed"/> hides the mod's JP box for the whole rewind, from the
    /// first bedroom frame. Vanilla's own clock/date box needs nothing during the pan, which sets
    /// <c>Game1.freezeControls</c>, and Game1 skips drawHUD entirely while that is set; but it does
    /// draw in the bedroom (where the failed night's date is the point) and would draw over the
    /// black here, so the black window switches <c>Game1.displayHUD</c> off and puts it back with
    /// the fade.</summary>
    internal static class RewindBlackout
    {
        /// <summary>How long the surface has to stay clear before the black lifts. Long enough to
        /// ride over the one-and-two-tick gaps between the question, the shrine and the hub; short
        /// enough that the player is not left looking at black once they really do have control.</summary>
        private const float QuietMs = 750f;

        private const float FadeInMs = 900f;

        /// <summary>Backstop. Nothing should hold the black this long with the surface never going
        /// quiet, but a black screen with no way out is the worst failure this could have, so it
        /// gives up and says so rather than stranding the player.</summary>
        private const float MaxBlackMs = 180000f;

        private enum State { Idle, Black, FadingIn }

        private static IMonitor _monitor;
        private static bool _registered;

        private static State _state;
        private static float _quiet;
        private static float _held;
        private static float _fade;
        private static bool _hudSuppressed;
        private static bool _displayHudWasOn;

        /// <summary>True while the mod's own HUD should stay off screen. Read by ModEntry's JP box.</summary>
        public static bool HudSuppressed => _hudSuppressed;

        /// <summary>Wires the tick, the paint and the return-to-title safety net. Safe to call more
        /// than once; the subscriptions only happen on the first call.</summary>
        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            if (_registered) return;
            _registered = true;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => Release("returned to title");
        }

        /// <summary>Hides the mod's JP box for the rest of the sequence. Called as the bedroom
        /// opens; the black window below takes vanilla's HUD as well when it starts.</summary>
        public static void SuppressHud()
        {
            _hudSuppressed = true;
        }

        /// <summary>Takes the screen at full black. Called by the morning beat once its own fade-out
        /// has reached black, in the same synchronous step, so the two never show a lit frame between
        /// them. Idempotent.</summary>
        public static void Begin()
        {
            if (_state == State.Black) return;
            _state = State.Black;
            _quiet = 0f;
            _held = 0f;
            _fade = 1f;
            _hudSuppressed = true;
            _displayHudWasOn = Game1.displayHUD;
            Game1.displayHUD = false;
            _monitor?.Log("RewindBlackout: holding black until the player has the morning.", LogLevel.Info);
        }

        /// <summary>Drops the black and gives the HUD back immediately, whatever state it was in.
        /// Idempotent.</summary>
        public static void Release(string reason)
        {
            if (_state == State.Idle && !_hudSuppressed) return;
            if (_state != State.Idle)
                _monitor?.Log($"RewindBlackout: released ({reason}).", LogLevel.Info);
            if (_state == State.Black) Game1.displayHUD = _displayHudWasOn;
            _state = State.Idle;
            _fade = 0f;
            _hudSuppressed = false;
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_state == State.Idle) return;
            // Dormancy (project rule): this state is static and outlives the save it was set on.
            if (!RunActivation.IsActive) { Release("save unloaded"); return; }

            float ms = (float)(Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? 0.0);

            if (_state == State.FadingIn)
            {
                _fade -= ms / FadeInMs;
                if (_fade <= 0f) Release("faded up");
                return;
            }

            _held += ms;
            if (_held >= MaxBlackMs)
            {
                _monitor?.Log(
                    "RewindBlackout: the screen never went quiet; lifting the black rather than leaving " +
                    "the player looking at nothing.", LogLevel.Error);
                StartFadingIn();
                return;
            }

            if (!SurfaceIsQuiet()) { _quiet = 0f; return; }
            _quiet += ms;
            if (_quiet >= QuietMs) StartFadingIn();
        }

        private static void StartFadingIn()
        {
            Game1.displayHUD = _displayHudWasOn;
            _state = State.FadingIn;
            _fade = 1f;
        }

        /// <summary>Nothing on screen and nobody else driving: the player has the day. Deliberately
        /// strict, because every one of these is a thing that can be up in the stretch this covers
        /// and none of them should lift the black.</summary>
        private static bool SurfaceIsQuiet()
            => Game1.activeClickableMenu == null
               && !Game1.eventUp
               && Game1.farmEvent == null
               && Game1.currentMinigame == null
               && Game1.locationRequest == null
               && !Game1.newDay
               && !Game1.freezeControls;

        private static void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (_state == State.Idle || _fade <= 0f) return;
            e.SpriteBatch.Draw(
                Game1.fadeToBlackRect,
                Game1.graphics.GraphicsDevice.Viewport.Bounds,
                Color.Black * MathHelper.Clamp(_fade, 0f, 1f));
        }
    }
}
