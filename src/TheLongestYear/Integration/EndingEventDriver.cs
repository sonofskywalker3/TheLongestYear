using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using TheLongestYear.Loop;

namespace TheLongestYear.Integration
{
    /// <summary>Year One Ending (spec 2026-09-06 §2): starts the ending event the first time the player
    /// steps onto the farm on an armed morning, waits for the seen mail, then hands the continuation
    /// to RunController. Decision logic is EndingMorningDecider; this is the Game1 glue.</summary>
    internal sealed class EndingEventDriver
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly GameplayConfig _config;
        private Func<RunController> _runController;
        private bool _started;
        private bool _replayOnly;
        private int _cooldownUntilTick;

        public EndingEventDriver(IMonitor monitor, MetaStore meta, GameplayConfig config)
        {
            _monitor = monitor; _meta = meta; _config = config;
        }

        public void Attach(IModHelper helper, Func<RunController> runController)
        {
            _runController = runController;
            helper.Events.GameLoop.DayStarted += (s, e) => { _started = false; _replayOnly = false; };
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!RunActivation.IsActive || !_config.Enabled) return;
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            if (Game1.fadeToBlackAlpha > 0f || Game1.ticks < _cooldownUntilTick) return;
            Farmer p = Game1.player;
            if (p == null) return;

            bool busy = IsBusy();
            var snap = new EndingSnapshot(
                Armed: _meta.Run.EndingArmed || _replayOnly,
                WorldReady: true,
                OnFarm: Game1.currentLocation is Farm,
                Busy: busy,
                FestivalToday: Utility.isFestivalDay(),
                SeenMailPresent: p.mailReceived.Contains(EndingEventKeys.SeenMail),
                StartedThisMorning: _started);

            switch (EndingMorningDecider.Next(snap))
            {
                case EndingAction.Start:
                    Start(forcedSpeaker: null);
                    break;
                case EndingAction.Finish:
                    p.mailReceived.Remove(EndingEventKeys.SeenMail);   // transient signal, never persisted
                    _started = false;
                    if (_replayOnly)
                    {
                        _replayOnly = false;
                        _monitor.Log("Ending: replay finished (no continuation).", LogLevel.Info);
                        break;
                    }
                    _meta.Run.EndingArmed = false;
                    _meta.State.EndingSeen = true;
                    _meta.Save();
                    _monitor.Log("Ending: event finished, running the continuation.", LogLevel.Info);
                    _runController?.Invoke()?.OnEndingFinished();
                    break;
                case EndingAction.ReArm:
                    _monitor.Log("Ending: the event ended without its seen flag; it will play again on the next step outside.", LogLevel.Warn);
                    _started = false;
                    // An interrupted replay must not swallow a real armed morning: if it stayed
                    // true, the automatic re-fire below would finish through the no-continuation
                    // branch and skip EndingArmed=false/EndingSeen=true/Save/OnEndingFinished.
                    _replayOnly = false;
                    Bump();
                    break;
            }
        }

        /// <summary>Debug replay from tly_ending: plays the event now, in the current location, and
        /// runs no continuation. <paramref name="forcedSpeaker"/> overrides the pick.</summary>
        public void StartNow(string forcedSpeaker)
        {
            if (IsBusy())
            {
                _monitor.Log("tly_ending: the game is busy, try again outside with no menu open.", LogLevel.Warn);
                return;
            }
            _replayOnly = true;
            Start(forcedSpeaker);
        }

        private static bool IsBusy()
            => Game1.eventUp || Game1.eventOver || Game1.currentLocation?.currentEvent != null
               || Game1.farmEvent != null || Game1.locationRequest != null || Game1.activeClickableMenu != null
               || Game1.newDay;

        private void Start(string forcedSpeaker)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null) return;
            EndingCast cast = BuildCast(forcedSpeaker);
            _monitor.Log($"Ending: starting (speaker={cast.Speaker ?? "none"}, crowd={cast.Crowd.Count}, shrine={cast.ShrineX},{cast.ShrineY}).", LogLevel.Info);
            loc.startEvent(new Event(EndingEventInjector.Build(cast), null, EndingEventKeys.EventId));
            _started = true;
            Bump();
        }

        private EndingCast BuildCast(string forcedSpeaker)
        {
            Farmer p = Game1.player;
            Func<string, bool> eligible = name =>
            {
                NPC npc = Game1.getCharacterFromName(name);
                if (npc == null || npc is StardewValley.Characters.Child) return false;
                if (p.spouse != null && p.spouse == name) return false;
                return true;
            };
            string speaker = forcedSpeaker != null && eligible(forcedSpeaker)
                ? forcedSpeaker
                : EndingSpeaker.Pick(_meta.State, _config.DejaVuThreshold, eligible);
            string middleKey = null, sceneKey = null;
            if (speaker != null)
            {
                _meta.State.VillagerMemory.TryGetValue(speaker, out VillagerMemory mem);
                EndingLineTier tier = EndingLine.Tier(mem, out string sceneId);
                middleKey = EndingLine.MiddleKey(speaker, tier);
                sceneKey = sceneId != null ? EndingLine.SceneKey(sceneId) : null;
            }
            List<string> crowd = EndingCast.DefaultCrowd.Where(n => Game1.getCharacterFromName(n) != null).ToList();
            Microsoft.Xna.Framework.Point shrine = Game1.getFarm().GetGrandpaShrinePosition();
            return new EndingCast(speaker, middleKey, sceneKey, crowd, shrine.X, shrine.Y);
        }

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30;
    }
}
