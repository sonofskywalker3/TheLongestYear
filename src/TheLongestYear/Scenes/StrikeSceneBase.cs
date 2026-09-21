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
    /// the viewport and the farmer after any farm event ends (Game1.cs:3804 and 3826), so the scene
    /// sets those the way WitchEvent and FairyEvent do and never puts them back itself.</summary>
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
        private readonly Action _onFinished;
        private readonly Timeline _timeline = new();
        private int _next;
        private bool _ended;
        /// <summary>Starts true so a button already held when the scene begins is not a fresh press.</summary>
        private bool _skipHeldLastTick = true;

        /// <summary>Milliseconds since the scene's first tick.</summary>
        protected int ElapsedMs { get; private set; }

        protected StrikeSceneBase(PendingStrike strike, bool skippable, IMonitor monitor, Action onFinished)
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
                    End(EndedNotStaged, markPlayed: false);
                    return true;
                }
                Build(_timeline);
                _timeline.Cues.Sort((a, b) => a.AtMs.CompareTo(b.AtMs));
                Game1.displayHUD = false;
                Game1.freezeControls = true;
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
                    End(EndedSkipped);
                    return true;
                }
                while (_next < _timeline.Cues.Count && _timeline.Cues[_next].AtMs <= ElapsedMs)
                    _timeline.Cues[_next++].Do();
                if (ElapsedMs >= _timeline.EndMs)
                {
                    End(EndedFinished);
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

        /// <summary>Applies the strike, tells the run the scene played, and logs how it went. Runs
        /// once whatever the ending was. A scene that never got as far as staging is the one ending
        /// that does not count as played: the player has still not seen it.</summary>
        private void End(string how, bool markPlayed = true)
        {
            if (_ended) return;
            _ended = true;
            ApplyStrike();
            Monitor.Log($"Darkness: the {GetType().Name} scene ended ({how}) at tick {Game1.ticks}.", LogLevel.Trace);
            if (!markPlayed) return;
            try
            {
                _onFinished?.Invoke();
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: the {GetType().Name} scene could not record that it played. {ex}", LogLevel.Error);
            }
        }

        private void Fail(Exception ex)
        {
            Monitor.Log($"Darkness: the {GetType().Name} scene failed and was ended. The strike still lands. {ex}", LogLevel.Error);
            End(EndedFailed);
        }
    }
}
