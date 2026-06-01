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

        private const string FarmEventsAsset = "Data/Events/Farm";
        private const string CcEventsAsset   = "Data/Events/CommunityCenter";

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
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Only act on a settled frame. Warping during the new-game load fade fights the game's
            // own player placement — the warp never sticks and the driver re-warps every cooldown
            // (the "disco" flicker). Waiting for the fade to clear costs a brief farmhouse-interior
            // glimpse but makes every warp land.
            if (Game1.fadeToBlackAlpha > 0f) return;
            if (Game1.ticks < _cooldownUntilTick) return;

            var p = Game1.player;
            if (p == null) return;

            GameLocation loc = Game1.currentLocation;

            // EventActive folds in eventOver: right after an event ends, GameLocation.startEvent
            // bails while Game1.eventOver is still true, which is exactly why the chained CC event
            // never fired before. Treat that window as "still busy" so we wait it out.
            var snap = new IntroSnapshot(
                HasSeenIntro: _meta.State.HasSeenIntro,
                Season: _meta.Run.Season,
                DayOfMonth: _meta.Run.DayOfMonth,
                PorchSeen: p.mailReceived.Contains(IntroEventKeys.PorchSeenMail),
                CcSeen:    p.mailReceived.Contains(IntroEventKeys.CcSeenMail),
                EventActive: Game1.eventUp || Game1.eventOver || loc?.currentEvent != null);

            switch (IntroSequenceDecider.Next(snap))
            {
                case IntroAction.StartPorch:
                    // Warp to the Farm first, then start the porch event once we're there.
                    if (loc?.Name != "Farm")
                    {
                        _monitor.Log("Intro: warping to Farm for the porch (Lewis) event.", LogLevel.Info);
                        Game1.warpFarmer("Farm", FarmTileX, FarmTileY, 2, false);
                        Bump();
                    }
                    else if (ReadyToStartEvent(loc))
                    {
                        _monitor.Log("Intro: starting the porch (Lewis) event.", LogLevel.Info);
                        loc.startEvent(new Event(IntroEventInjector.PorchEventScript(), FarmEventsAsset, IntroEventKeys.PorchEventId));
                        Bump();
                    }
                    break;

                case IntroAction.WarpToCc:
                    if (loc?.Name != "CommunityCenter")
                    {
                        _monitor.Log("Intro: porch done — warping to the Community Center for the Junimo event.", LogLevel.Info);
                        Game1.warpFarmer("CommunityCenter", CcTileX, CcTileY, 0, false);
                        Bump();
                    }
                    else if (ReadyToStartEvent(loc))
                    {
                        _monitor.Log("Intro: starting the Community Center (Junimo) event.", LogLevel.Info);
                        loc.startEvent(new Event(IntroEventInjector.CcEventScript(), CcEventsAsset, IntroEventKeys.CcEventId));
                        Bump();
                    }
                    break;

                case IntroAction.OpenPicker:
                    if (loc?.Name != "FarmHouse")
                    {
                        _monitor.Log("Intro: both cutscenes done — returning home.", LogLevel.Info);
                        Game1.warpFarmer("FarmHouse", HomeTileX, HomeTileY, 2, false);
                        Bump();
                        return; // open the picker next tick, after the warp settles
                    }
                    if (Game1.fadeToBlackAlpha == 0f && Game1.activeClickableMenu == null)
                    {
                        _monitor.Log("Intro: opening the theme picker.", LogLevel.Info);
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

        /// <summary>True when it's safe to call <c>startEvent</c>: no event running or finishing,
        /// no fade in progress, no menu open.</summary>
        private static bool ReadyToStartEvent(GameLocation loc)
            => loc != null
               && !Game1.eventUp && !Game1.eventOver && loc.currentEvent == null
               && Game1.fadeToBlackAlpha == 0f
               && Game1.activeClickableMenu == null;

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30; // ~0.5s at 60fps
    }
}
