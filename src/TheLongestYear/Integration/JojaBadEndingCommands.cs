using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.ItemTypeDefinitions;

namespace TheLongestYear.Integration
{
    /// <summary>Event commands for the bad ending of Morris's offer (JojaBadEnding). Every command
    /// logs and skips on bad args, and everything it draws or changes is undone the first tick the
    /// bad ending is no longer the running event (and on return to title). Tiles are absolute map
    /// tiles: none of these apply the farm's event offset.
    ///
    /// <c>tlyHideFarmhouse</c> / <c>tlyShowFarmhouse</c>: the main farmhouse (and the mailbox, a
    /// draw layer of it) is not drawn while hidden; the floating new-mail flag moves off-map with it.
    /// <c>tlyDust &lt;x&gt; &lt;y&gt; &lt;w&gt; &lt;h&gt; &lt;ms&gt;</c>: smoke puffs over the tile rectangle for ms, then continues.
    /// <c>tlyBuildingSprite &lt;type&gt; &lt;x&gt; &lt;y&gt;</c>: Data/Buildings[type]'s sprite, bottom-left on tile (x, y+1).
    /// <c>tlyItemSprite &lt;itemId&gt; &lt;x&gt; &lt;y&gt; [degrees]</c>: an item lying on tile (x, y).
    /// <c>tlyWaterTint &lt;r&gt; &lt;g&gt; &lt;b&gt;</c>: the current location's water colour, restored at the end.
    /// <c>tlyClosedSign &lt;x&gt; &lt;y&gt;</c>: two brown planks nailed in an X across the two-tile door whose
    /// bottom-left tile is (x, y). LooseSprites/Cursors has no "closed" sign, so they are drawn from
    /// Game1.staminaRect.
    /// <c>tlyGameOver</c>: ends the script and opens the Game Over screen (JojaGameOverMenu), which
    /// exits to the title without saving.</summary>
    internal static partial class JojaBadEndingCommands
    {
        public const string HideFarmhouseName = "tlyHideFarmhouse";
        public const string ShowFarmhouseName = "tlyShowFarmhouse";
        public const string DustName = "tlyDust";
        public const string BuildingSpriteName = "tlyBuildingSprite";
        public const string ItemSpriteName = "tlyItemSprite";
        public const string WaterTintName = "tlyWaterTint";
        public const string ClosedSignName = "tlyClosedSign";
        public const string GameOverName = "tlyGameOver";
        public const string ClearName = "tlyClear";

        private const string AnimalTexturePrefix = "Animals";
        private const int FaceLeft = 3;
        private const int LeftFramesStart = 12, RightFramesStart = 4, FramesPerRow = 4;
        private const float SpriteScale = 4f;
        private const float LongLife = 999999f;
        private const int PuffsPerTileSecond = 3, MinPuffs = 12, MaxPuffs = 400, PuffAnimMs = 450;
        private static readonly Color DustColour = new(190, 170, 150);
        private static readonly Color PlankColour = new(110, 72, 40);
        private static readonly Color PlankEdgeColour = new(70, 44, 24);
        private const int PlankLength = 128, PlankThickness = 24, PlankEdge = 4;
        private const float PlankAngle = 0.42f;

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
        private static readonly List<(GameLocation Loc, Rectangle Door)> Planks = new();
        private static GameLocation _tinted;
        private static Color _tintOriginal;

        internal static bool IsBadEnding(Event ev) => ev != null && ev.id == JojaEventKeys.BadEndingId;

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            helper.Events.GameLoop.UpdateTicked += (_, _) => OnTick();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => { Cleanup(); _gameOverSent = false; _running = false; _missingTicks = 0; };
            helper.Events.Display.RenderedWorld += (_, e) => DrawPlanks(e.SpriteBatch);

            Event.RegisterCommand(HideFarmhouseName, (evt, args, context) => Guarded(evt, HideFarmhouseName, () => _farmhouseHidden = true));
            Event.RegisterCommand(ShowFarmhouseName, (evt, args, context) => Guarded(evt, ShowFarmhouseName, () => _farmhouseHidden = false));
            Event.RegisterCommand(ClearName, Clear);
            Event.RegisterCommand(DustName, Dust);
            Event.RegisterCommand(BuildingSpriteName, BuildingSprite);
            Event.RegisterCommand(ItemSpriteName, ItemSprite);
            Event.RegisterCommand(WaterTintName, WaterTint);
            Event.RegisterCommand(ClosedSignName, ClosedSign);
            Event.RegisterCommand(GameOverName, GameOver);
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
                    SpawnDust(Game1.currentLocation, x, y, Math.Max(1, w), Math.Max(1, h), Math.Max(1, ms));
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

        /// <summary>Puffs scattered over the rectangle, their starts spread over the whole duration,
        /// drawn above everything so the building under them is hidden.</summary>
        private static void SpawnDust(GameLocation loc, int x, int y, int w, int h, int ms)
        {
            if (loc == null) return;
            int count = Math.Clamp(w * h * PuffsPerTileSecond * ms / 1000, MinPuffs, MaxPuffs);
            int lastStart = Math.Max(0, ms - PuffAnimMs);
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(x + (float)Game1.random.NextDouble() * w - 0.5f, y + (float)Game1.random.NextDouble() * h - 0.5f) * 64f;
                var puff = new TemporaryAnimatedSprite(5, pos, DustColour, animationLength: 8,
                    flipped: Game1.random.Next(2) == 0, animationInterval: 60f, layerDepth: 1f, delay: Game1.random.Next(lastStart + 1))
                {
                    scale = 1.4f + (float)Game1.random.NextDouble() * 0.8f,
                    motion = new Vector2(0f, -0.3f),
                };
                loc.temporarySprites.Add(puff);
                Sprites.Add((loc, puff));
            }
        }

        private static void BuildingSprite(Event evt, string[] args, EventContext context)
        {
            try
            {
                if (!ArgUtility.TryGet(args, 1, out string type, out string error)
                    || !ArgUtility.TryGetInt(args, 2, out int x, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int y, out error))
                {
                    Skip(evt, BuildingSpriteName, error);
                    return;
                }
                if (Game1.buildingData == null || !Game1.buildingData.TryGetValue(type, out BuildingData data) || string.IsNullOrEmpty(data.Texture))
                {
                    Skip(evt, BuildingSpriteName, $"no building type '{type}'");
                    return;
                }
                Texture2D tex = Game1.content.Load<Texture2D>(data.Texture);
                Rectangle src = data.SourceRect.IsEmpty ? tex.Bounds : data.SourceRect;
                float bottom = (y + 1) * 64f;
                Vector2 pos = new Vector2(x * 64f, bottom - src.Height * SpriteScale) + data.DrawOffset * SpriteScale;
                Add(new TemporaryAnimatedSprite(data.Texture, src, LongLife, 1, 0, pos, flicker: false, flipped: false,
                    bottom / 10000f, 0f, Color.White, SpriteScale, 0f, 0f, 0f));
            }
            catch (Exception ex)
            {
                _monitor.Log($"{BuildingSpriteName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
            }
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
                ParsedItemData data = ItemRegistry.GetDataOrErrorItem(itemId);
                Rectangle src = data.GetSourceRect();
                // Centred on the tile; lying on the ground, just above anything flat drawn there.
                Vector2 pos = new Vector2(x * 64f + (64f - src.Width * SpriteScale) / 2f, y * 64f + (64f - src.Height * SpriteScale) / 2f);
                Add(new TemporaryAnimatedSprite(data.TextureName, src, LongLife, 1, 0, pos, flicker: false, flipped: false,
                    (y * 64f + 1f) / 10000f, 0f, Color.White, SpriteScale, 0f, MathHelper.ToRadians(degrees), 0f));
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

        private static void ClosedSign(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGetInt(args, 1, out int x, out string error) || !ArgUtility.TryGetInt(args, 2, out int y, out error))
            {
                Skip(evt, ClosedSignName, error);
                return;
            }
            // The two tiles of the door, from the top of the tile above (x, y) to the bottom of (x, y).
            Guarded(evt, ClosedSignName, () => Planks.Add((Game1.currentLocation, new Rectangle(x * 64, (y - 1) * 64, 128, 128))));
        }

        /// <summary>tlyClear &lt;x&gt; &lt;y&gt; &lt;w&gt; &lt;h&gt;: empties that farm rectangle for the scene (see
        /// JojaFarmClear). Runs only inside the bad ending, which always ends at the title unsaved.</summary>
        private static void Clear(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGetInt(args, 1, out int x, out string error)
                || !ArgUtility.TryGetInt(args, 2, out int y, out error)
                || !ArgUtility.TryGetInt(args, 3, out int w, out error)
                || !ArgUtility.TryGetInt(args, 4, out int h, out error))
            {
                Skip(evt, ClearName, error);
                return;
            }
            Guarded(evt, ClearName, () =>
            {
                if (!_running || !IsBadEnding(evt) || !IsBadEnding(Game1.CurrentEvent) || Game1.currentLocation is not Farm farm)
                {
                    _monitor.Log($"{ClearName}: only runs on the farm inside the bad ending; skipping.", LogLevel.Warn);
                    return;
                }
                string cleared = JojaFarmClear.Clear(farm, new Rectangle(x, y, w, h));
                _monitor.Log($"{ClearName} {x},{y} {w}x{h}: cleared {cleared} (in memory; the scene ends unsaved).", LogLevel.Info);
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
                FlipLeftWalkers(ev);
                return;
            }
            if (_farmhouseHidden || Sprites.Count > 0 || Planks.Count > 0 || _tinted != null || _dustElapsed >= 0f)
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
            Planks.Clear();
            RestoreWater();
        }
    }
}
