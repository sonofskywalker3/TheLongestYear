using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace TheLongestYear.Integration
{
    /// <summary>Event commands for the bad ending of Morris's offer (JojaBadEnding). Every command
    /// logs and skips on bad args and never throws, and everything it draws or changes is undone the
    /// first tick the bad ending is no longer the running event (and on return to title). Tiles are
    /// absolute map tiles: none of these apply the farm's event offset.
    ///
    /// <c>tlyHideFarmhouse</c> / <c>tlyShowFarmhouse</c>: the main farmhouse (and the mailbox, a
    /// draw layer of it) is not drawn while hidden; the floating new-mail flag moves off-map with it.
    /// <c>tlyDust &lt;x&gt; &lt;y&gt; &lt;w&gt; &lt;h&gt; &lt;ms&gt;</c>: smoke puffs over the tile rectangle for ms, then continues.
    /// <c>tlyDustView &lt;ms&gt;</c>: big puffs over the whole screen for ms; continues at once, so the
    /// commands after it run under the dust.
    /// <c>tlyItemSprite &lt;itemId&gt; &lt;x&gt; &lt;y&gt; [degrees]</c>: an item lying on tile (x, y).
    /// <c>tlyWaterTint &lt;r&gt; &lt;g&gt; &lt;b&gt;</c>: the current location's water colour, restored at the end.
    /// <c>tlyMusic &lt;cue&gt;</c>: plays a music cue and keeps it playing until the scene ends.
    /// <c>tlyBoardPierre</c>: Pierre's shop boarded up (JojaPierreBoards), restored at the end.
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

        internal static bool IsBadEnding(Event ev) => ev != null && ev.id == JojaEventKeys.BadEndingId;

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            JojaPierreBoards.Register(monitor, helper);
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
            Event.RegisterCommand(LitterName, Litter);
            Event.RegisterCommand(GameOverName, GameOver);
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

        private static void Dust(Event evt, string[] args, EventContext context)
        {
            try
            {
                if (_dustElapsed < 0f)
                {
                    if (!ArgUtility.TryGetInt(args, 1, out int x, out string error)
                        || !ArgUtility.TryGetInt(args, 2, out int y, out error)
                        || !ArgUtility.TryGetInt(args, 3, out int w, out error)
                        || !ArgUtility.TryGetInt(args, 4, out int h, out error)
                        || !ArgUtility.TryGetInt(args, 5, out int ms, out error))
                    {
                        Skip(evt, DustName, error);
                        return;
                    }
                    SpawnDust(Game1.currentLocation, x, y, Math.Max(1, w), Math.Max(1, h), Math.Max(1, ms), PuffsPerTileSecond, MaxPuffs, 1.4f, 0.8f);
                    _dustElapsed = 0f;
                    return;
                }
                ArgUtility.TryGetInt(args, 5, out int total, out _);
                _dustElapsed += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                if (_dustElapsed < total) return;   // called every tick until the dust has run
            }
            catch (Exception ex)
            {
                _monitor.Log($"{DustName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
            }
            _dustElapsed = -1f;
            evt.CurrentCommand++;
        }

        /// <summary>Dust over everything on screen (one tile past each edge), without waiting.</summary>
        private static void DustView(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGetInt(args, 1, out int ms, out string error))
            {
                Skip(evt, DustViewName, error);
                return;
            }
            Guarded(evt, DustViewName, () =>
            {
                int x = Game1.viewport.X / Game1.tileSize - 1, y = Game1.viewport.Y / Game1.tileSize - 1;
                int w = Game1.viewport.Width / Game1.tileSize + 3, h = Game1.viewport.Height / Game1.tileSize + 3;
                SpawnDust(Game1.currentLocation, x, y, w, h, Math.Max(1, ms), ViewPuffsPerTileSecond, MaxViewPuffs, ViewPuffScaleMin, ViewPuffScaleSpread);
            });
        }

        /// <summary>Puffs scattered over the rectangle, their starts spread over the whole duration,
        /// drawn above everything so what changes under them is hidden.</summary>
        private static void SpawnDust(GameLocation loc, int x, int y, int w, int h, int ms, float perTileSecond, int max, float scaleMin, float scaleSpread)
        {
            if (loc == null) return;
            int count = Math.Clamp((int)(w * h * perTileSecond * ms / 1000f), MinPuffs, max);
            int lastStart = Math.Max(0, ms - PuffAnimMs);
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(x + (float)Game1.random.NextDouble() * w - 0.5f, y + (float)Game1.random.NextDouble() * h - 0.5f) * 64f;
                var puff = new TemporaryAnimatedSprite(5, pos, DustColour, animationLength: 8,
                    flipped: Game1.random.Next(2) == 0, animationInterval: 60f, layerDepth: 1f, delay: Game1.random.Next(lastStart + 1))
                {
                    scale = scaleMin + (float)Game1.random.NextDouble() * scaleSpread,
                    motion = new Vector2(0f, -0.3f),
                };
                loc.temporarySprites.Add(puff);
                Sprites.Add((loc, puff));
            }
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
                Game1.changeMusicTrack(cue);
                _monitor.Log($"{MusicName}: playing '{cue}'.", LogLevel.Trace);
            });
        }

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
                    if (_music != null && Game1.getMusicTrackName() != _music) Game1.changeMusicTrack(_music);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Joja bad ending tick: {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
                return;
            }
            if (_farmhouseHidden || Sprites.Count > 0 || _tinted != null || _dustElapsed >= 0f || JojaPierreBoards.Active
                || JojaFarmAnimals.Count > 0 || _music != null || JojaFarmClear.HasHeld)
                Cleanup();
            if (!_running || _gameOverSent) return;
            if (Game1.locationRequest != null || Game1.isWarping)
            {
                _missingTicks = 0;   // a scene change in flight: the event comes back on the load
                return;
            }
            if (++_missingTicks < FailClosedGraceTicks) return;
            FailClosed("the bad ending stopped before its end (a command failed or it was skipped)");
        }

        private static void RestoreWater()
        {
            if (_tinted == null) return;
            _tinted.waterColor.Value = _tintOriginal;
            _tinted = null;
        }

        private static void Cleanup()
        {
            _farmhouseHidden = false;
            _dustElapsed = -1f;
            foreach (var (loc, sprite) in Sprites)
                loc.temporarySprites.Remove(sprite);
            Sprites.Clear();
            Floating.Clear();
            _bobClock = 0f;
            RestoreWater();
            JojaPierreBoards.Restore();
            JojaFarmAnimals.Reset();
            JojaFarmClear.RestoreHeld();
            ResetFarmCommands();
            _music = null;
        }
    }
}
