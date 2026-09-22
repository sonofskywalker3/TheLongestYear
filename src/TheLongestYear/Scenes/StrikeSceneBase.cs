using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Events;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>An overnight strike scene (spec 2026-09-21): silent, a few seconds, and it lands the
    /// night's damage at its own beat. A timeline of timed actions, a click to skip once the kind has
    /// been seen on the save, and one rule above all: however the scene ends, the strike is applied
    /// exactly once and the night goes on.
    ///
    /// It derives from vanilla's own <see cref="BaseFarmEvent"/>, so it is a farm event of exactly the
    /// shape the witch and the fairy are, NetFields included. Vanilla restores the HUD, the controls,
    /// the viewport and the farmer after any farm event ends (Game1.cs:3804 and 3826), and the scene
    /// sets those the way WitchEvent and FairyEvent do.
    ///
    /// It does NOT lean on that restore, though. <see cref="Cleanup"/> runs on every ending, and the
    /// HUD and control flags are put back beside it, because two paths reach an ending where vanilla
    /// never cleans up after it: a <see cref="Stage"/> that says no (vanilla nulls the event and
    /// walks away) and the debug command, which plays a scene in the middle of an ordinary day with
    /// no farm-event machinery running at all.</summary>
    internal abstract class StrikeSceneBase : BaseFarmEvent
    {
        /// <summary>How long a click is ignored at the start, so the button that put the farmer to
        /// bed cannot skip the scene before it has drawn a frame.</summary>
        private const int SkipDeadZoneMs = 500;

        private const string EndedFinished = "finished";
        private const string EndedSkipped = "skipped";
        private const string EndedNotStaged = "not staged";
        private const string EndedFailed = "failed";

        /// <summary>The beats of a scene: what happens when, and when it is over.</summary>
        protected sealed class Timeline
        {
            internal readonly List<(int AtMs, Action Do)> Cues = new();

            /// <summary>When the scene ends, in milliseconds from its first tick.</summary>
            public int EndMs { get; private set; }

            /// <summary>Do something at this many milliseconds in. The scene never ends before its
            /// last cue.</summary>
            public void At(int ms, Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                Cues.Add((ms, action));
                if (ms > EndMs) EndMs = ms;
            }

            /// <summary>End the scene at this many milliseconds in.</summary>
            public void EndAt(int ms) => EndMs = ms;
        }

        /// <summary>Tonight's strike. A subclass reads its target from here and lands it with
        /// <see cref="ApplyStrike"/> at whatever beat it likes.</summary>
        protected readonly PendingStrike Strike;

        protected readonly IMonitor Monitor;

        private readonly bool _skippable;
        private readonly Action<bool> _onFinished;
        private readonly Timeline _timeline = new();
        private int _next;
        private bool _ended;
        /// <summary>True once the scene is staged and running, so an ending knows whether the player
        /// was ever shown anything.</summary>
        private bool _staged;
        /// <summary>Starts true so a button already held when the scene begins is not a fresh press.</summary>
        private bool _skipHeldLastTick = true;
        /// <summary>True once the HUD and control flags have been taken, with what they were.</summary>
        private bool _flagsTaken;
        private bool _priorDisplayHud;
        private bool _priorFreezeControls;

        private int _fadeInMs;
        private int _fadeOutAtMs = int.MaxValue;
        private int _fadeOutLengthMs;

        /// <summary>Milliseconds since the scene's first tick.</summary>
        protected int ElapsedMs { get; private set; }

        /// <param name="onFinished">Told once, however the scene ended. True when the scene was
        /// actually shown (it finished, was skipped, or failed after it had been staged), false when
        /// it never got that far.</param>
        protected StrikeSceneBase(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
        {
            Strike = strike ?? throw new ArgumentNullException(nameof(strike));
            Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _skippable = skippable;
            _onFinished = onFinished;
        }

        /// <summary>Where the scene plays and what it needs. Return false to call the scene off: the
        /// strike still lands, the night still goes on.</summary>
        protected abstract bool Stage();

        /// <summary>The map the scene is showing, so the base can pump it. Null (the default) means
        /// the scene does not need the world ticked at all. Set from <see cref="Stage"/>.</summary>
        protected virtual GameLocation SceneLocation => null;

        /// <summary>Declare the scene's fade in and fade out, and the base paints them itself at the
        /// end of every <see cref="draw"/>. A scene that never calls this has no fade.
        ///
        /// It lives here and not in each scene because the fade has to be painted in the WORLD
        /// layer, and that is not obvious: vanilla calls <c>drawAboveEverything</c> from
        /// Game1.cs:13409, after DrawMenu has closed its batch, so there is no open SpriteBatch
        /// there and the first Draw call throws (caught live, 2026-09-21). <c>farmEvent.draw</c> at
        /// Game1.cs:13698 is wrapped in its own Begin and End, so the world layer is the one place a
        /// scene can paint without opening a batch of its own.</summary>
        protected void Fade(int fadeInMs, int fadeOutAtMs, int fadeOutLengthMs)
        {
            _fadeInMs = Math.Max(0, fadeInMs);
            _fadeOutAtMs = fadeOutAtMs;
            _fadeOutLengthMs = Math.Max(1, fadeOutLengthMs);
        }

        /// <summary>The scene's beats. Called once, after <see cref="Stage"/> said yes.</summary>
        protected abstract void Build(Timeline t);

        /// <summary>Move whatever the scene is animating, once a tick, after this tick's cues have
        /// fired. <paramref name="elapsedMs"/> is <see cref="ElapsedMs"/>, passed in so a subclass
        /// cannot read it at the wrong moment.
        ///
        /// It exists so per-tick movement runs INSIDE the base's own try/catch. A subclass that
        /// moved things from its own <c>tickUpdate</c> override would throw straight past the
        /// failure path, and the scene would stop without <see cref="Cleanup"/> ever running: a
        /// frozen night camera, no HUD and frozen controls.</summary>
        protected virtual void Advance(int elapsedMs) { }

        /// <summary>Paint at the world layer.</summary>
        protected virtual void Paint(SpriteBatch b) { }

        /// <summary>Paint above everything, including any menu.
        ///
        /// MIND THE BATCH. Vanilla calls this from Game1.cs:13409, after DrawMenu has closed its
        /// own, so on the real overnight path there is NO open SpriteBatch and the first Draw call
        /// throws. An override has to Begin and End one itself. <see cref="Paint"/> has no such
        /// problem: Game1.cs:13698 wraps it in a Begin and an End. Tasks 8 to 10 should paint in
        /// <see cref="Paint"/> unless they really must cover a menu.</summary>
        protected virtual void PaintAbove(SpriteBatch b) { }

        /// <summary>Put back whatever the scene borrowed from <c>Game1</c>: the camera, the clock,
        /// the lighting, any light source it added. Called exactly once on EVERY ending, inside its
        /// own try/catch, including a skip, a failure, and a scene that never staged. A scene that
        /// changes nothing global does not need it.</summary>
        protected virtual void Cleanup() { }

        /// <summary>Land tonight's damage. Safe to call more than once and from anywhere: the strike
        /// itself runs its effect at most once, and a strike that throws is logged and swallowed so it
        /// can never strand the night.</summary>
        protected void ApplyStrike()
        {
            try
            {
                Strike.Apply();
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: tonight's {Strike.Event} threw while it was being applied. The night goes on. {ex}", LogLevel.Error);
            }
        }

        /// <inheritdoc />
        public override bool setUp()
        {
            try
            {
                if (!Stage())
                {
                    Monitor.Log($"Darkness: the {GetType().Name} scene found nothing to play against, so tonight's {Strike.Event} lands with no scene.", LogLevel.Info);
                    End(EndedNotStaged, shown: false);
                    return true;
                }
                Build(_timeline);
                _timeline.Cues.Sort((a, b) => a.AtMs.CompareTo(b.AtMs));
                _priorDisplayHud = Game1.displayHUD;
                _priorFreezeControls = Game1.freezeControls;
                _flagsTaken = true;
                Game1.displayHUD = false;
                Game1.freezeControls = true;
                _staged = true;
                Monitor.Log($"Darkness: the {GetType().Name} scene takes tonight's overnight slot for {Strike.Event} ({(_skippable ? "skippable" : "not skippable")}).", LogLevel.Info);
                return false;
            }
            catch (Exception ex)
            {
                Fail(ex);
                return true;
            }
        }

        /// <inheritdoc />
        public override bool tickUpdate(GameTime time)
        {
            if (_ended) return true;
            try
            {
                PumpWorld(time);
                ElapsedMs += time.ElapsedGameTime.Milliseconds;
                if (SkipPressed())
                {
                    End(EndedSkipped, shown: true);
                    return true;
                }
                while (_next < _timeline.Cues.Count && _timeline.Cues[_next].AtMs <= ElapsedMs)
                    _timeline.Cues[_next++].Do();
                if (ElapsedMs >= _timeline.EndMs)
                {
                    End(EndedFinished, shown: true);
                    return true;
                }
                Advance(ElapsedMs);
                return false;
            }
            catch (Exception ex)
            {
                Fail(ex);
                return true;
            }
        }

        /// <summary>Tick the world by hand, the way vanilla's own night events do
        /// (<c>WitchEvent.tickUpdate</c>), and then write the scene's night again.
        ///
        /// ONLY ON THE REAL OVERNIGHT PATH. The debug preview plays the scene during an ordinary
        /// update, where the engine is already doing all of this, so the pump would do it twice.
        ///
        /// THE NIGHT IS WRITTEN AFTER, NEVER BEFORE. <c>UpdateGameClock</c> recomputes the outdoor
        /// light from the clock and <c>UpdateWhenCurrentLocation</c> copies that into the ambient
        /// light, so holding the night first meant the pump threw it away again and the farm came
        /// out at the full 2am dark (caught by the first real overnight screenshots, 2026-09-21).
        ///
        /// A CAVEAT FOR ANY NEW SCENE. Vanilla runs its OWN <c>UpdateCharacters</c>,
        /// <c>UpdateLocations</c> and <c>UpdateOther</c> after this event's tick as well
        /// (Game1.cs:3799 falls through to Game1.cs:3842), so this pump is a duplicate on the
        /// overnight path and, more to the point, anything a scene writes from
        /// <see cref="Advance"/> that the location's own update also writes will be overwritten
        /// before the frame is drawn. That is why the thief's chest lid needs a Harmony postfix
        /// rather than a per-tick write. The night survives only because the clock is frozen under
        /// <c>freezeControls</c> and both light colours are set to the same value.</summary>
        private void PumpWorld(GameTime time)
        {
            GameLocation where = SceneLocation;
            if (where != null && ReferenceEquals(Game1.farmEvent, this))
            {
                try
                {
                    Game1.UpdateGameClock(time);
                    where.UpdateWhenCurrentLocation(time);
                    where.updateEvenIfFarmerIsntHere(time);
                    Game1.UpdateOther(time);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Darkness: the {GetType().Name} scene could not pump {where.NameOrUniqueName} this tick. {ex}", LogLevel.Trace);
                }
            }
            SceneCamera.HoldNight();
        }

        /// <inheritdoc />
        public override void draw(SpriteBatch b)
        {
            if (_ended) return;
            try
            {
                Paint(b);
                PaintFade(b);
            }
            catch (Exception ex) { Fail(ex); }
        }

        /// <summary>The declared fade, painted last in the world layer.</summary>
        private void PaintFade(SpriteBatch b)
        {
            float black = BlackAt(ElapsedMs);
            if (black <= 0f || Game1.fadeToBlackRect == null) return;
            // The world layer draws in the zoomed backbuffer, the debug preview in UI space. Cover
            // whichever is bigger, since over-covering a full screen black costs nothing.
            Viewport screen = Game1.graphics.GraphicsDevice.Viewport;
            int width = Math.Max(screen.Width, Game1.uiViewport.Width);
            int height = Math.Max(screen.Height, Game1.uiViewport.Height);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * black);
        }

        private float BlackAt(int elapsed)
        {
            if (elapsed < _fadeInMs) return 1f - elapsed / (float)_fadeInMs;
            if (elapsed >= _fadeOutAtMs) return Math.Min(1f, (elapsed - _fadeOutAtMs) / (float)_fadeOutLengthMs);
            return 0f;
        }

        /// <inheritdoc />
        public override void drawAboveEverything(SpriteBatch b)
        {
            if (_ended) return;
            try { PaintAbove(b); }
            catch (Exception ex) { Fail(ex); }
        }

        /// <summary>The last net. Vanilla calls this the moment the event ends, so even a scene that
        /// somehow left without ending itself lands its strike here.</summary>
        public override void makeChangesToLocation() => ApplyStrike();

        /// <summary>Stop a scene from outside, because whoever was driving it cannot carry on. It
        /// goes through the same single ending as every other path, so the strike still lands and
        /// <see cref="Cleanup"/> still runs. Does nothing once the scene has ended.</summary>
        public void Abort(string why)
        {
            if (_ended) return;
            Monitor.Log($"Darkness: the {GetType().Name} scene was stopped from outside ({why}).", LogLevel.Warn);
            End(EndedFailed, shown: _staged);
        }

        /// <summary>A fresh press, never a button held down from before the scene began.</summary>
        private bool SkipPressed()
        {
            if (!_skippable) return false;
            bool pressed = Game1.input.GetMouseState().LeftButton == ButtonState.Pressed
                        || Game1.input.GetGamePadState().IsButtonDown(Buttons.A);
            bool fresh = pressed && !_skipHeldLastTick && ElapsedMs >= SkipDeadZoneMs;
            _skipHeldLastTick = pressed;
            return fresh;
        }

        /// <summary>Applies the strike, tells the owner how it went, and logs it. Runs once whatever
        /// the ending was. <paramref name="shown"/> is false only for a scene that never got as far
        /// as being staged: the player has still not seen that one.</summary>
        private void End(string how, bool shown)
        {
            if (_ended) return;
            _ended = true;
            // The world goes back to how it was BEFORE the damage lands, so a strike that throws
            // cannot leave the player holding a frozen night camera.
            try
            {
                if (_flagsTaken)
                {
                    Game1.displayHUD = _priorDisplayHud;
                    Game1.freezeControls = _priorFreezeControls;
                    _flagsTaken = false;
                }
                Cleanup();
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: the {GetType().Name} scene could not put the world back. {ex}", LogLevel.Error);
            }
            ApplyStrike();
            Monitor.Log($"Darkness: the {GetType().Name} scene ended ({how}) at tick {Game1.ticks}.", LogLevel.Trace);
            try
            {
                _onFinished?.Invoke(shown);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: the {GetType().Name} scene could not record how it ended. {ex}", LogLevel.Error);
            }
        }

        private void Fail(Exception ex)
        {
            Monitor.Log($"Darkness: the {GetType().Name} scene failed and was ended. The strike still lands. {ex}", LogLevel.Error);
            End(EndedFailed, shown: _staged);
        }
    }
}
