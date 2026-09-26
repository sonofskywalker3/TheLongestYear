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
    /// <c>tlyGameOver</c>: ends the script and exits to the title without saving.</summary>
    internal static class JojaBadEndingCommands
    {
        public const string HideFarmhouseName = "tlyHideFarmhouse";
        public const string ShowFarmhouseName = "tlyShowFarmhouse";
        public const string DustName = "tlyDust";
        public const string BuildingSpriteName = "tlyBuildingSprite";
        public const string ItemSpriteName = "tlyItemSprite";
        public const string WaterTintName = "tlyWaterTint";
        public const string ClosedSignName = "tlyClosedSign";
        public const string GameOverName = "tlyGameOver";

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
        private static readonly List<(GameLocation Loc, TemporaryAnimatedSprite Sprite)> Sprites = new();
        private static readonly List<(GameLocation Loc, Rectangle Door)> Planks = new();
        private static GameLocation _tinted;
        private static Color _tintOriginal;

        internal static bool IsBadEnding(Event ev) => ev != null && ev.id == JojaEventKeys.BadEndingId;

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            helper.Events.GameLoop.UpdateTicked += (_, _) => OnTick();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => { Cleanup(); _gameOverSent = false; };
            helper.Events.Display.RenderedWorld += (_, e) => DrawPlanks(e.SpriteBatch);

            Event.RegisterCommand(HideFarmhouseName, (evt, args, context) => { _farmhouseHidden = true; evt.CurrentCommand++; });
            Event.RegisterCommand(ShowFarmhouseName, (evt, args, context) => { _farmhouseHidden = false; evt.CurrentCommand++; });
            Event.RegisterCommand(DustName, Dust);
            Event.RegisterCommand(BuildingSpriteName, BuildingSprite);
            Event.RegisterCommand(ItemSpriteName, ItemSprite);
            Event.RegisterCommand(WaterTintName, WaterTint);
            Event.RegisterCommand(ClosedSignName, ClosedSign);
            Event.RegisterCommand(GameOverName, GameOver);
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
            Planks.Add((Game1.currentLocation, new Rectangle(x * 64, (y - 1) * 64, 128, 128)));
            evt.CurrentCommand++;
        }

        private static void GameOver(Event evt, string[] args, EventContext context)
        {
            // Task 7 replaces this with JojaGameOverMenu (which exits to the title itself).
            evt.CurrentCommand++;   // past the last command: the script idles under the black overlay
            if (_gameOverSent) return;
            _gameOverSent = true;
            _monitor.Log("Joja: bad ending over; exiting to the title without saving.", LogLevel.Info);
            Game1.ExitToTitle();
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
            if (!IsBadEnding(ev))
            {
                if (_farmhouseHidden || Sprites.Count > 0 || Planks.Count > 0 || _tinted != null || _dustElapsed >= 0f)
                    Cleanup();
                return;
            }
            FlipLeftWalkers(ev);
        }

        /// <summary>A farm animal's sheet has no left-facing row (FarmAnimal draws its right-facing
        /// frames flipped); an NPC walking left shows row 3, which is the eating frames. Swap them
        /// to the right-walk frames, mirrored, after the game's update and before the draw.</summary>
        private static void FlipLeftWalkers(Event ev)
        {
            foreach (NPC actor in ev.actors)
            {
                AnimatedSprite sprite = actor?.Sprite;
                if (sprite?.textureName.Value == null || !sprite.textureName.Value.StartsWith(AnimalTexturePrefix, StringComparison.Ordinal))
                    continue;
                if (actor.FacingDirection != FaceLeft)
                {
                    actor.flip = false;
                    continue;
                }
                if (sprite.currentFrame >= LeftFramesStart && sprite.currentFrame < LeftFramesStart + FramesPerRow)
                    sprite.CurrentFrame = RightFramesStart + sprite.currentFrame - LeftFramesStart;
                actor.flip = true;
            }
        }

        private static void DrawPlanks(SpriteBatch b)
        {
            if (Planks.Count == 0 || !IsBadEnding(Game1.CurrentEvent)) return;
            foreach (var (loc, door) in Planks)
            {
                if (loc != Game1.currentLocation) continue;
                Vector2 centre = Game1.GlobalToLocal(Game1.viewport, new Vector2(door.Center.X, door.Center.Y));
                DrawPlank(b, centre, PlankAngle);
                DrawPlank(b, centre, -PlankAngle);
            }
        }

        private static void DrawPlank(SpriteBatch b, Vector2 centre, float angle)
        {
            var origin = new Vector2(0.5f, 0.5f);
            b.Draw(Game1.staminaRect, centre, null, PlankEdgeColour, angle, origin,
                new Vector2(PlankLength + PlankEdge, PlankThickness + PlankEdge), SpriteEffects.None, 1f);
            b.Draw(Game1.staminaRect, centre, null, PlankColour, angle, origin,
                new Vector2(PlankLength, PlankThickness), SpriteEffects.None, 1f);
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

        private static bool IsHiddenFarmhouse(Building building)
            => _farmhouseHidden && building != null && building == Game1.getFarm()?.GetMainFarmHouse();

        [HarmonyPatch(typeof(Building), nameof(Building.draw))]
        internal static class HideFarmhouseDraw
        {
            private static bool Prefix(Building __instance) => !IsHiddenFarmhouse(__instance);
        }

        /// <summary>The farm's new-mail flag floats over the mailbox; with the house gone it would
        /// hang in the air. While hidden, the mailbox is off the map.</summary>
        [HarmonyPatch(typeof(Farmer), nameof(Farmer.getMailboxPosition))]
        internal static class HideMailFlag
        {
            private static readonly Point OffMap = new(-100, -100);

            private static void Postfix(ref Point __result)
            {
                if (_farmhouseHidden) __result = OffMap;
            }
        }
    }
}
