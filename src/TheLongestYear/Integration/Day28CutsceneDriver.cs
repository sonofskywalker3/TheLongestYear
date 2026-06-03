using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core.Day28;
using TheLongestYear.Loop;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Plays the day-28 bedtime Junimo cutscene the vanilla way, mirroring
    /// <see cref="IntroSequenceDriver"/>: on a settled morning frame, if
    /// <see cref="RunController.PendingCutscene"/> is set, it starts ONE forced event
    /// (<see cref="Day28CutsceneInjector.BuildEvent"/>); when that event ends it calls
    /// <see cref="RunController.OnCutsceneEnded"/> (FAIL → shop+reset, CONTINUE → next season).
    ///
    /// Re-arm is implicit: OnCutsceneEnded clears PendingCutscene to None, which resets
    /// <c>_started</c> on the next tick — so this also drives the mid-day <c>tly_failreset</c>
    /// debug path, not just real morning resets.
    /// </summary>
    internal sealed class Day28CutsceneDriver
    {
        private readonly IMonitor _monitor;
        private Func<RunController> _runController;

        private bool _started;
        private int _cooldownUntilTick;

        public Day28CutsceneDriver(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Subscribe once (from ModEntry.Entry). The RunController is built later on save
        /// load, so it's resolved through a thunk — same pattern as the intro driver's launcher.</summary>
        public void Attach(IModHelper helper, Func<RunController> runController)
        {
            _runController = runController;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
        }

        /// <summary>Paint the whole screen black while OUR cutscene event is on, so it reads as an
        /// in-bed black-background scene with neither the room, the farmer, nor a Junimo sprite
        /// visible — only the dialogue box (drawn later, on top). Done here rather than with a
        /// vanilla fade command because <c>fade</c> reveals the room and <c>globalFade</c> blinks
        /// back (2026-06-03 playtests). RenderedWorld runs after the world is drawn (screen-space
        /// SpriteBatch) and before the dialogue overlay, so the text stays readable on top.</summary>
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation?.currentEvent?.id != Day28CutsceneContent.EventId)
                return;

            Rectangle full = Game1.graphics.GraphicsDevice.Viewport.Bounds;
            e.SpriteBatch.Draw(Game1.fadeToBlackRect, full, Color.Black);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            RunController rc = _runController?.Invoke();
            if (rc == null) return;

            Day28Branch branch = rc.PendingCutscene;
            if (branch == Day28Branch.None)
            {
                _started = false; // idle / re-arm for the next pending episode
                return;
            }

            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Before we start, wait for the morning wake-fade to settle (starting during a fade
            // fights the engine's player placement — the intro lesson). After we start, the
            // event's own `fade` is expected, so don't bail on it.
            if (!_started && Game1.fadeToBlackAlpha > 0f) return;
            if (Game1.ticks < _cooldownUntilTick) return;

            bool eventActive = Game1.eventUp || Game1.eventOver
                || Game1.currentLocation?.currentEvent != null;

            switch (Day28CutsceneDecider.Next(new Day28Snapshot(branch, _started, eventActive)))
            {
                case Day28Action.StartCutscene:
                    GameLocation loc = Game1.currentLocation;
                    if (loc != null && Game1.activeClickableMenu == null)
                    {
                        _monitor.Log($"Day-28 cutscene: starting the {branch} Junimo scene.", LogLevel.Info);
                        loc.startEvent(new Event(
                            Day28CutsceneInjector.BuildEvent(branch),
                            null,
                            Day28CutsceneContent.EventId));
                        _started = true;
                        Bump();
                    }
                    break;

                case Day28Action.RunContinuation:
                    _monitor.Log("Day-28 cutscene: scene ended — running the continuation.", LogLevel.Info);
                    rc.OnCutsceneEnded(); // clears PendingCutscene → _started resets next tick
                    break;

                case Day28Action.Waiting:
                case Day28Action.None:
                default:
                    break;
            }
        }

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30; // ~0.5s at 60fps
    }
}
