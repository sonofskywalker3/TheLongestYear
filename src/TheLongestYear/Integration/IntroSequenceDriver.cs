using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Plays the day-1 intro on a fresh run the vanilla way: it starts ONE event
    /// (<see cref="IntroEventInjector.BuildIntroEvent"/>) once the world is settled, and that event
    /// moves itself from the farm porch to the Community Center via the in-event
    /// <c>changeLocation</c> command. No mod-side <c>warpFarmer</c> and no per-tick loop — the
    /// earlier version warped the player every tick and fought the engine's own placement, which
    /// never stuck and re-fired (the "disco" flicker).
    ///
    /// When the event ends it has set the cc-seen flag; the driver then opens the theme picker.
    /// Decision logic is the pure <see cref="IntroSequenceDecider"/>; this is the Game1 glue.
    /// </summary>
    internal sealed class IntroSequenceDriver
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly GameplayConfig _config;
        private System.Func<MenuLauncher> _launcher;

        private bool _finished;
        private bool _introStartedThisMorning;
        private int _cooldownUntilTick;

        public IntroSequenceDriver(IMonitor monitor, MetaStore meta, GameplayConfig config)
        {
            _monitor = monitor;
            _meta = meta;
            _config = config;
        }

        public void Attach(IModHelper helper, System.Func<MenuLauncher> launcher)
        {
            _launcher = launcher;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!RunActivation.IsActive) return; // dormant on non-TLY saves — no intro
            // Re-arm for a (possibly replayed) fresh morning.
            if (IntroGate.IsFreshIntroMorning(_meta.State.HasSeenIntro, _meta.Run.Season, _meta.Run.DayOfMonth))
            {
                _finished = false;
                _introStartedThisMorning = false;
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!RunActivation.IsActive) return; // dormant on non-TLY saves — no intro cutscene
            if (!_config.Enabled || _finished) return;
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Only act on a settled frame — never during the load fade (acting then fights the
            // game's own player placement).
            if (Game1.fadeToBlackAlpha > 0f) return;
            if (Game1.ticks < _cooldownUntilTick) return;

            var p = Game1.player;
            if (p == null) return;

            // EventActive folds in eventOver: startEvent bails while eventOver is still true right
            // after an event ends, so treat that window as "still busy".
            var snap = new IntroSnapshot(
                HasSeenIntro: _meta.State.HasSeenIntro,
                Season: _meta.Run.Season,
                DayOfMonth: _meta.Run.DayOfMonth,
                CcSeen: p.mailReceived.Contains(IntroEventKeys.CcSeenMail),
                EventActive: Game1.eventUp || Game1.eventOver || Game1.currentLocation?.currentEvent != null);

            switch (IntroSequenceDecider.Next(snap))
            {
                case IntroAction.StartIntro:
                    var loc = Game1.currentLocation;
                    if (loc != null && Game1.activeClickableMenu == null)
                    {
                        if (_introStartedThisMorning)
                        {
                            // We already started the intro this morning, yet the decider is asking
                            // to start it again — meaning the event ended WITHOUT setting the cc-seen
                            // flag (an interrupted/edge-case end; a skip is no longer possible since
                            // the event isn't skippable). Force the flag so we proceed to the picker
                            // instead of re-firing forever (the 2026-06-01 dialog-loop guard).
                            _monitor.Log("Intro: event ended without the cc-seen flag — forcing it to avoid a re-fire loop.", LogLevel.Warn);
                            p.mailReceived.Add(IntroEventKeys.CcSeenMail);
                            Bump();
                            break;
                        }
                        _monitor.Log("Intro: starting the Lewis -> Junimo cutscene.", LogLevel.Info);
                        loc.startEvent(new Event(IntroEventInjector.BuildIntroEvent(), null, IntroEventKeys.IntroEventId));
                        _introStartedThisMorning = true;
                        Bump();
                    }
                    break;

                case IntroAction.OpenPicker:
                    if (Game1.activeClickableMenu == null)
                    {
                        _monitor.Log("Intro: cutscene done — opening the theme picker.", LogLevel.Info);
                        _launcher?.Invoke()?.OpenWeeklyHub();
                        _finished = true;
                    }
                    break;

                case IntroAction.Waiting:
                case IntroAction.None:
                default:
                    break;
            }
        }

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30; // ~0.5s at 60fps
    }
}
