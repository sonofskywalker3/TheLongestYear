using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;

namespace TheLongestYear.Integration
{
    /// <summary>Event commands for the bad ending of Morris's offer (JojaBadEnding). Every command
    /// logs and skips on bad args and never throws, and everything it draws or changes is undone the
    /// first tick the bad ending is no longer the running event (and on return to title). Tiles are
    /// absolute map tiles: none of these apply the farm's event offset.
    ///
    /// <c>tlyHideFarmhouse</c> / <c>tlyShowFarmhouse</c>: the main farmhouse (and the mailbox, a
    /// draw layer of it) is not drawn while hidden; the floating new-mail flag moves off-map with it.
    /// <c>tlyJojaSign &lt;x&gt; &lt;y&gt; [delayMs]</c>: the Joja sign over the farmhouse door whose entry
    /// tile is (x, y), shown after delayMs (JojaBadEndingVisuals).
    /// <c>tlyDust &lt;x&gt; &lt;y&gt; &lt;w&gt; &lt;h&gt; &lt;ms&gt;</c>: smoke puffs over the tile rectangle for ms, then continues.
    /// <c>tlyDustView &lt;ms&gt; [x y w h]</c>: big puffs over the whole screen for ms, none centred on
    /// the optional tile rectangle (the kept farmhouse); continues at once, so the commands after it
    /// run under the dust.
    /// <c>tlyItemSprite &lt;itemId&gt; &lt;x&gt; &lt;y&gt; [degrees]</c>: an item lying on tile (x, y).
    /// <c>tlyWaterTint &lt;r&gt; &lt;g&gt; &lt;b&gt;</c>: the current location's water colour, restored at the end.
    /// <c>tlyMusic &lt;cue&gt;</c>: plays a music cue and keeps it playing until the scene ends.
    /// <c>tlyBoardPierre</c>: Pierre's shop boarded up (JojaPierreBoards), restored at the end.
    /// <c>tlyJojaWarehouse</c>: the Community Center drawn as the Joja warehouse (JojaWarehouse),
    /// restored at the end.
    /// <c>tlyDaylight</c>: the clock held at noon for the rest of the scene (Jeff, 2026-10-02: "it
    /// should be day, they're not literally magically doing this that night"). The engine derives
    /// outdoorLight, lamps, lit windows and night tiles from <c>Game1.timeOfDay</c>
    /// (Game1.UpdateGameClock, Game1.cs 5627; GameLocation.resetLocalState), and time does not pass
    /// in an event, so every location the scene warps to afterwards draws in daylight. The real
    /// clock is put back at the end; the scene ends unsaved either way.
    /// <c>tlyLitter</c>: see JojaLitter.cs. The farm commands are in JojaBadEndingFarmCommands.cs.
    /// <c>tlyGameOver</c>: ends the script and opens the Game Over screen (JojaGameOverMenu), which
    /// exits to the title without saving.</summary>
    internal static partial class JojaBadEndingCommands
    {
        public const string HideFarmhouseName = "tlyHideFarmhouse";
        public const string ShowFarmhouseName = "tlyShowFarmhouse";
        public const string DustName = "tlyDust";
        public const string DustViewName = "tlyDustView";
        public const string ItemSpriteName = "tlyItemSprite";
        public const string WaterTintName = "tlyWaterTint";
        public const string MusicName = "tlyMusic";
        public const string BoardPierreName = "tlyBoardPierre";
        public const string JojaWarehouseName = "tlyJojaWarehouse";
        public const string DaylightName = "tlyDaylight";
        private const int DaylightClock = 1200;   // short of every season's dusk (Winter's is 1500)
        public const string GameOverName = "tlyGameOver";

        private const float SpriteScale = 4f;
        private const float LongLife = 999999f;
        private const int PuffsPerTileSecond = 3, MinPuffs = 12, MaxPuffs = 400, PuffAnimMs = 450;
        private const float ViewPuffsPerTileSecond = 0.9f;
        private const int MaxViewPuffs = 900;
        private const float ViewPuffScaleMin = 3f, ViewPuffScaleSpread = 1.6f;
        private static readonly Color DustColour = new(190, 170, 150);

        private static IMonitor _monitor;
        private static bool _farmhouseHidden;
        private static float _dustElapsed = -1f;
        private static bool _gameOverSent;
        // Set once the bad ending has really started; cleared at the title. While it is set, the
        // scene must end at the title: if the event goes away any other way (a command threw and
        // vanilla skipped the event), OnTick exits to the title so no later sleep saves the world
        // the scene changed (the farm clear, above all).
        private static bool _running;
        private static int _missingTicks;
        // A tlyChangeLocation leaves the event parked (currentEvent null) until the new map loads;
        // only an absence longer than this, with no warp pending, counts as the scene gone.
        private const int FailClosedGraceTicks = 90;
        private static readonly List<(GameLocation Loc, TemporaryAnimatedSprite Sprite)> Sprites = new();
        private static GameLocation _tinted;
        private static Color _tintOriginal;
        private static string _music;
        private static GameLocation _musicCheckedIn;
        private static int _realClock = -1;   // the clock tlyDaylight took over, -1 = not held

        internal static bool IsBadEnding(Event ev) => ev != null && ev.id == JojaEventKeys.BadEndingId;

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            JojaPierreBoards.Register(monitor, helper);
            JojaWarehouse.Register(monitor);
            helper.Events.GameLoop.UpdateTicked += (_, _) => OnTick();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => { Cleanup(); _gameOverSent = false; _running = false; _missingTicks = 0; };

            Event.RegisterCommand(HideFarmhouseName, (evt, args, context) => Guarded(evt, HideFarmhouseName, () => _farmhouseHidden = true));
            Event.RegisterCommand(ShowFarmhouseName, (evt, args, context) => Guarded(evt, ShowFarmhouseName, () => _farmhouseHidden = false));
            Event.RegisterCommand(DustName, Dust);
            Event.RegisterCommand(DustViewName, DustView);
            Event.RegisterCommand(ItemSpriteName, ItemSprite);
            Event.RegisterCommand(WaterTintName, WaterTint);
            Event.RegisterCommand(MusicName, Music);
            Event.RegisterCommand(BoardPierreName, (evt, args, context) => Guarded(evt, BoardPierreName,
                () => _monitor.Log($"{BoardPierreName}: {JojaPierreBoards.Apply(Game1.currentLocation)}.", LogLevel.Info)));
            Event.RegisterCommand(JojaWarehouseName, (evt, args, context) => Guarded(evt, JojaWarehouseName,
                () => _monitor.Log($"{JojaWarehouseName}: {JojaWarehouse.Apply(Game1.currentLocation)} (in memory).", LogLevel.Info)));
            Event.RegisterCommand(DaylightName, (evt, args, context) => Guarded(evt, DaylightName, () =>
            {
                if (_realClock < 0) _realClock = Game1.timeOfDay;
                Game1.timeOfDay = DaylightClock;
                _monitor.Log($"{DaylightName}: clock {_realClock} held at {DaylightClock} for the scene (in memory; no save follows).", LogLevel.Info);
            }));
            Event.RegisterCommand(LitterName, Litter);
            Event.RegisterCommand(GameOverName, GameOver);
            Event.RegisterCommand(JojaSignName, JojaSign);
            RegisterFarmCommands();
        }

        /// <summary>The bad ending has started: from now on it can only end at the title.</summary>
        internal static void MarkRunning()
        {
            _running = true;
            _gameOverSent = false;
            _missingTicks = 0;
        }

        /// <summary>A Yes that lost its scene goes straight to the title, no save.</summary>
        internal static void FailClosed(string why)
        {
            if (_gameOverSent) return;
            _gameOverSent = true;
            _monitor.Log($"Joja: {why}; exiting to the title without saving.", LogLevel.Warn);
            Cleanup();
            // Straight to the title, no Game Over screen: an abnormal end is not the scene's ending.
            Game1.ExitToTitle();
        }

        private static void Guarded(Event evt, string name, Action action)
        {
            try { action(); }
            catch (Exception ex) { _monitor.Log($"{name}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn); }
            evt.CurrentCommand++;
        }

        private static void Skip(Event evt, string name, string error)
        {
            _monitor.Log($"{name}: {error}; skipping.", LogLevel.Warn);
            evt.CurrentCommand++;
        }

        private static void ItemSprite(Event evt, string[] args, EventContext context)
        {
            try
            {
                if (!ArgUtility.TryGet(args, 1, out string itemId, out string error)
                    || !ArgUtility.TryGetInt(args, 2, out int x, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int y, out error)
                    || !ArgUtility.TryGetOptionalFloat(args, 4, out float degrees, out error))
                {
                    Skip(evt, ItemSpriteName, error);
                    return;
                }
                LitterSprite(itemId, x, y, degrees, Color.White);
            }
            catch (Exception ex)
            {
                _monitor.Log($"{ItemSpriteName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
            }
            evt.CurrentCommand++;
        }

        private static void WaterTint(Event evt, string[] args, EventContext context)
        {
            try
            {
                if (!ArgUtility.TryGetInt(args, 1, out int r, out string error)
                    || !ArgUtility.TryGetInt(args, 2, out int g, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int b, out error))
                {
                    Skip(evt, WaterTintName, error);
                    return;
                }
                GameLocation loc = Game1.currentLocation;
                RestoreWater();
                _tinted = loc;
                _tintOriginal = loc.waterColor.Value;
                loc.waterColor.Value = new Color(r, g, b);
            }
            catch (Exception ex)
            {
                _monitor.Log($"{WaterTintName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
            }
            evt.CurrentCommand++;
        }

        /// <summary>tlyMusic &lt;cue&gt;: the scene's music, held (OnTick puts it back if a location
        /// change swaps it) until the scene is over.</summary>
        private static void Music(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string cue, out string error))
            {
                Skip(evt, MusicName, error);
                return;
            }
            Guarded(evt, MusicName, () =>
            {
                _music = cue;
                PlayMusic();
                _monitor.Log($"{MusicName}: playing '{cue}'.", LogLevel.Trace);
            });
        }

        /// <summary>The Event music context, the one vanilla's own playMusic event command uses
        /// (Event.cs 1329): the Default context is refused on a green-rain day (Game1.cs 10064), which
        /// left the cue re-requested every tick and never playing.</summary>
        private static void PlayMusic() => Game1.changeMusicTrack(_music, track_interruptable: false, MusicContext.Event);

        private static bool MusicHeld => Game1.getMusicTrackName(MusicContext.Event) == _music;

        private static void GameOver(Event evt, string[] args, EventContext context)
        {
            evt.CurrentCommand++;   // past the last command: the script idles under the black overlay
            if (_gameOverSent) return;
            _gameOverSent = true;
            try
            {
                _monitor.Log("Joja: bad ending over; showing the Game Over screen (no save).", LogLevel.Info);
                Game1.activeClickableMenu = new UI.JojaGameOverMenu(_monitor);   // its Exit() goes to the title
            }
            catch (Exception ex)
            {
                _monitor.Log($"{GameOverName}: {ex.GetType().Name}: {ex.Message}; exiting to the title without saving.", LogLevel.Error);
                Game1.ExitToTitle();
            }
        }

        private static void Add(TemporaryAnimatedSprite sprite)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null) return;
            loc.temporarySprites.Add(sprite);
            Sprites.Add((loc, sprite));
        }

        private static void OnTick()
        {
            Event ev = Game1.CurrentEvent;
            if (IsBadEnding(ev))
            {
                _missingTicks = 0;
                try
                {
                    BobFloating();
                    if (Game1.currentLocation is Farm farm) JojaFarmAnimals.Tick(farm);
                    if (_music != null && Game1.currentLocation != _musicCheckedIn)
                    {
                        _musicCheckedIn = Game1.currentLocation;
                        _monitor.Log($"{MusicName}: now in {_musicCheckedIn?.Name}, scene music '{Game1.getMusicTrackName(MusicContext.Event)}'.", LogLevel.Info);
                    }
                    if (_music != null && !MusicHeld)
                    {
                        _monitor.Log($"{MusicName}: '{Game1.getMusicTrackName(MusicContext.Event)}' was requested in {Game1.currentLocation?.Name}; '{_music}' put back.", LogLevel.Info);
                        PlayMusic();
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Joja bad ending tick: {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
                return;
            }
            if (_running && !_gameOverSent)
            {
                // tlyChangeLocation leaves currentEvent null until the new map loads (Event.cs
                // 4867-4879): the scene is still on, so nothing is restored and the music is held.
                // Only a real end cleans up: the title, the Game Over screen, or FailClosed below.
                if (Game1.locationRequest != null || Game1.isWarping)
                {
                    _missingTicks = 0;
                    return;
                }
                if (++_missingTicks < FailClosedGraceTicks) return;
                FailClosed("the bad ending stopped before its end (a command failed or it was skipped)");
                return;
            }
            if (NeedsCleanup) Cleanup();
        }

        private static bool NeedsCleanup
            => _farmhouseHidden || Sprites.Count > 0 || _tinted != null || _dustElapsed >= 0f || JojaPierreBoards.Active
               || JojaWarehouse.Active || _realClock >= 0 || JojaFarmAnimals.Count > 0 || _music != null || JojaFarmClear.HasHeld;

        private static void RestoreWater()
        {
            if (_tinted == null) return;
            _tinted.waterColor.Value = _tintOriginal;
            _tinted = null;
        }

        private static void Cleanup()
        {
            if (NeedsCleanup)
                _monitor.Log($"Joja bad ending: restoring (Pierre's tiles {JojaPierreBoards.Active}, Joja warehouse {JojaWarehouse.Active}, mod objects set aside {JojaFarmClear.HasHeld}, "
                             + $"scene animals {JojaFarmAnimals.Count}, water tint {_tinted != null}, music '{_music}', clock {(_realClock >= 0 ? _realClock.ToString() : "not held")}).", LogLevel.Info);
            if (_music != null) Game1.stopMusicTrack(MusicContext.Event);
            _farmhouseHidden = false;
            _dustElapsed = -1f;
            foreach (var (loc, sprite) in Sprites)
                loc.temporarySprites.Remove(sprite);
            Sprites.Clear();
            Floating.Clear();
            _bobClock = 0f;
            RestoreWater();
            JojaPierreBoards.Restore();
            JojaWarehouse.Restore();
            if (_realClock >= 0) Game1.timeOfDay = _realClock;
            _realClock = -1;
            JojaFarmAnimals.Reset();
            JojaFarmClear.RestoreHeld();
            ResetFarmCommands();
            _music = null;
            _musicCheckedIn = null;
        }
    }
}
