using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Orchestrates the day-1 intro on a fresh run: warps the farmer to the Farm so the porch
    /// (Lewis) event auto-fires, then into the Community Center so the Junimo event fires, then
    /// back to the farmhouse where it opens the theme picker. The events position their own
    /// actors and warp targets; this driver only sequences them and detects completion via the
    /// per-run mail flags + <see cref="Game1.eventUp"/>. Decision logic lives in the pure
    /// <see cref="IntroSequenceDecider"/>; this class is the Game1 glue.
    ///
    /// Activates only when <see cref="IntroGate.IsFreshIntroMorning"/> holds and the world is
    /// interactive (no fade/minigame/menu), so it never runs on the black save-creation screen.
    /// </summary>
    internal sealed class IntroSequenceDriver
    {
        private const int FarmTileX = 64, FarmTileY = 15;   // just outside the farmhouse door; porch event repositions the farmer
        private const int CcTileX = 32,  CcTileY = 22;      // CC south door; CC event repositions the farmer
        private const int HomeTileX = 9, HomeTileY = 9;     // farmhouse bed area

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly GameplayConfig _config;
        private System.Func<MenuLauncher> _launcher;

        private bool _finished;
        private int _cooldownUntilTick;   // debounce warps so we don't re-fire mid-fade

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
            // Re-arm for a (possibly replayed) fresh morning.
            if (IntroGate.IsFreshIntroMorning(_meta.State.HasSeenIntro, _meta.Run.Season, _meta.Run.DayOfMonth))
                _finished = false;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_config.Enabled || _finished) return;
            if (!Context.IsWorldReady) return;
            // Wait for an interactive frame so we never act on the black load screen.
            if (Game1.fadeToBlackAlpha > 0f || Game1.currentMinigame != null) return;
            if (Game1.ticks < _cooldownUntilTick) return;

            var p = Game1.player;
            if (p == null) return;

            var snap = new IntroSnapshot(
                HasSeenIntro: _meta.State.HasSeenIntro,
                Season: _meta.Run.Season,
                DayOfMonth: _meta.Run.DayOfMonth,
                PorchSeen: p.mailReceived.Contains(IntroEventKeys.PorchSeenMail),
                CcSeen:    p.mailReceived.Contains(IntroEventKeys.CcSeenMail),
                EventActive: Game1.eventUp || Game1.currentLocation?.currentEvent != null);

            switch (IntroSequenceDecider.Next(snap))
            {
                case IntroAction.StartPorch:
                    if (Game1.currentLocation?.Name != "Farm")
                    {
                        _monitor.Log("Intro: warping to Farm for the porch (Lewis) event.", LogLevel.Info);
                        Game1.warpFarmer("Farm", FarmTileX, FarmTileY, 2, false);
                        Bump();
                    }
                    break;

                case IntroAction.WarpToCc:
                    if (Game1.currentLocation?.Name != "CommunityCenter")
                    {
                        _monitor.Log("Intro: porch done — warping to the Community Center for the Junimo event.", LogLevel.Info);
                        Game1.warpFarmer("CommunityCenter", CcTileX, CcTileY, 0, false);
                        Bump();
                    }
                    break;

                case IntroAction.OpenPicker:
                    _monitor.Log("Intro: both cutscenes done — returning home and opening the theme picker.", LogLevel.Info);
                    if (Game1.currentLocation?.Name != "FarmHouse")
                    {
                        Game1.warpFarmer("FarmHouse", HomeTileX, HomeTileY, 2, false);
                        Bump();
                        return; // open the picker next tick, after the warp settles
                    }
                    _launcher?.Invoke()?.OpenWeeklyHub();
                    _finished = true;
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
