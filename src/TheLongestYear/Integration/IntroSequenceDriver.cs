using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Opens the theme picker on the first morning of a fresh run once the cc-seen flag is present.
    /// The flag is planted by the arrival event's end (OpeningEventInjector) or by OnSaveLoaded when
    /// Skip intro was ticked. This class never starts an event.
    /// </summary>
    internal sealed class IntroSequenceDriver
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly GameplayConfig _config;
        private System.Func<MenuLauncher> _launcher;

        private bool _finished;

        // Vanilla's own arrival event id (without the "/u 0" precondition suffix), as it lands in
        // player.eventsSeen once the event finishes. Used only by the WaitForOpening fallback below.
        private const string VanillaArrivalEventId = "60367";
        private bool _plantedMissingCcSeenFlag;

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
                _finished = false;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!RunActivation.IsActive) return; // dormant on non-TLY saves — no intro cutscene
            if (!_config.Enabled || _finished) return;
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Only act on a settled frame — never during the load fade (acting then fights the
            // game's own player placement).
            if (Game1.fadeToBlackAlpha > 0f) return;

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
                case IntroAction.OpenPicker:
                    if (Game1.activeClickableMenu == null)
                    {
                        _monitor.Log("Intro: cutscene done — opening the theme picker.", LogLevel.Info);
                        _launcher?.Invoke()?.OpenWeeklyHub();
                        _finished = true;
                    }
                    break;

                case IntroAction.WaitForOpening:
                    // Glue-level fallback, not a decider change: on a save from an older build (or
                    // one where the arrival somehow got skipped) the event already ran and is in
                    // player.eventsSeen, but the cc-seen flag it plants was never carried over, so
                    // the decider would otherwise wait for an event that will never happen again.
                    // Plant the flag once so the next tick's WaitForOpening resolves to OpenPicker.
                    if (!_plantedMissingCcSeenFlag
                        && Context.IsWorldReady && Game1.dayOfMonth >= 1
                        && p.eventsSeen.Contains(VanillaArrivalEventId))
                    {
                        p.mailReceived.Add(IntroEventKeys.CcSeenMail);
                        _plantedMissingCcSeenFlag = true;
                        _monitor.Log(
                            "Opening: arrival already seen but the cc-seen flag is missing; planting it so the picker opens.",
                            LogLevel.Warn);
                    }
                    break;

                case IntroAction.Waiting:
                case IntroAction.None:
                default:
                    break;
            }
        }
    }
}
