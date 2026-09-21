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

        /// <summary>The scene's beats. Called once, after <see cref="Stage"/> said yes.</summary>
        protected abstract void Build(Timeline t);

        /// <summary>Paint at the world layer.</summary>
        protected virtual void Paint(SpriteBatch b) { }

        /// <summary>Paint above everything, including the fade.</summary>
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
                return false;
            }
            catch (Exception ex)
            {
                Fail(ex);
                return true;
            }
        }

        /// <inheritdoc />
        public override void draw(SpriteBatch b)
        {
            if (_ended) return;
            try { Paint(b); }
            catch (Exception ex) { Fail(ex); }
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
