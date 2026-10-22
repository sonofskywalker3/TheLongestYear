using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using TheLongestYear.Core.Joja;
using xTile.Layers;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's litter (Jeff, 2026-09-25): dead fish, driftwood and trash washed up
    /// along a shore, and dead fish and trash floating in the water with a gentle bob. Sparse and
    /// irregular ("neglected, not a junkyard"): the tiles come from the map itself, the picks from a
    /// seeded <see cref="JojaScatter"/>, so the layout is the same every run.
    ///
    /// <c>tlyLitter &lt;seed&gt; &lt;shore|water&gt; &lt;x&gt; &lt;y&gt; &lt;w&gt; &lt;h&gt; &lt;count&gt; &lt;spacing&gt; &lt;id,id,...&gt;</c>
    /// - shore: dry, open ground (no water, passable, nothing on it, nothing drawn over it) within
    ///   <see cref="ShoreBand"/> tiles of water.
    /// - water: open water (the tile and its four neighbours are water, nothing drawn over it, so
    ///   never under a bridge). These bob.</summary>
    internal static partial class JojaBadEndingCommands
    {
        public const string LitterName = "tlyLitter";
        private const int ShoreBand = 4;
        private const float BobPixels = 3f, BobPeriodMs = 2600f, SwayRadians = 0.08f;
        private static readonly Color FloatTint = new(215, 225, 205);
        private static readonly string[] OverLayers = { "Buildings", "Front", "AlwaysFront" };
        private static readonly List<(TemporaryAnimatedSprite Sprite, Vector2 Base, float Rotation, float Phase)> Floating = new();
        private static float _bobClock;

        private static void Litter(Event evt, string[] args, EventContext context)
        {
            try
            {
                if (!ArgUtility.TryGetInt(args, 1, out int seed, out string error)
                    || !ArgUtility.TryGet(args, 2, out string mode, out error)
                    || !ArgUtility.TryGetInt(args, 3, out int x, out error)
                    || !ArgUtility.TryGetInt(args, 4, out int y, out error)
                    || !ArgUtility.TryGetInt(args, 5, out int w, out error)
                    || !ArgUtility.TryGetInt(args, 6, out int h, out error)
                    || !ArgUtility.TryGetInt(args, 7, out int count, out error)
                    || !ArgUtility.TryGetFloat(args, 8, out float spacing, out error)
                    || !ArgUtility.TryGet(args, 9, out string ids, out error))
                {
                    Skip(evt, LitterName, error);
                    return;
                }
                GameLocation loc = Game1.currentLocation;
                bool water = mode == "water";
                var candidates = new List<(int X, int Y)>();
                for (int ty = y; ty < y + h; ty++)
                    for (int tx = x; tx < x + w; tx++)
                        if (water ? OpenWater(loc, tx, ty) : Shore(loc, tx, ty))
                            candidates.Add((tx, ty));
                IReadOnlyList<(int X, int Y)> picks = JojaScatter.Pick(candidates, count, spacing, seed);
                string[] items = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var rng = new Random(seed + 1);
                foreach (var (px, py) in picks)
                {
                    string id = items[rng.Next(items.Length)];
                    float degrees = rng.Next(360);
                    TemporaryAnimatedSprite sprite = LitterSprite(id, px, py, degrees, water ? FloatTint : Color.White);
                    if (water)
                        Floating.Add((sprite, sprite.Position, sprite.rotation, (float)(rng.NextDouble() * MathHelper.TwoPi)));
                }
                _monitor.Log($"{LitterName} {mode} seed {seed}: {picks.Count} of {count} placed from {candidates.Count} candidate tiles in {x},{y} {w}x{h}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"{LitterName}: {ex.GetType().Name}: {ex.Message}; skipping.", LogLevel.Warn);
            }
            evt.CurrentCommand++;
        }

        /// <summary>An item lying on (or floating on) tile (x, y), centred, just above the ground.</summary>
        private static TemporaryAnimatedSprite LitterSprite(string itemId, int x, int y, float degrees, Color tint)
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(itemId);
            Rectangle src = data.GetSourceRect();
            var pos = new Vector2(x * 64f + (64f - src.Width * SpriteScale) / 2f, y * 64f + (64f - src.Height * SpriteScale) / 2f);
            var sprite = new TemporaryAnimatedSprite(data.TextureName, src, LongLife, 1, 0, pos, flicker: false, flipped: false,
                (y * 64f + 1f) / 10000f, 0f, tint, SpriteScale, 0f, MathHelper.ToRadians(degrees), 0f);
            Add(sprite);
            return sprite;
        }

        /// <summary>The bob: a slow rise and fall and a slight roll, each piece on its own phase.</summary>
        private static void BobFloating()
        {
            if (Floating.Count == 0) return;
            _bobClock += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            float t = _bobClock / BobPeriodMs * MathHelper.TwoPi;
            foreach (var (sprite, basePos, rotation, phase) in Floating)
            {
                sprite.Position = basePos + new Vector2(0f, (float)Math.Sin(t + phase) * BobPixels);
                sprite.rotation = rotation + (float)Math.Sin(t * 0.7f + phase) * SwayRadians;
            }
        }

        private static bool OpenWater(GameLocation loc, int x, int y)
            => loc.isWaterTile(x, y) && loc.isWaterTile(x - 1, y) && loc.isWaterTile(x + 1, y)
               && loc.isWaterTile(x, y - 1) && loc.isWaterTile(x, y + 1) && !DrawnOver(loc, x, y);

        private static bool Shore(GameLocation loc, int x, int y)
        {
            var tile = new Vector2(x, y);
            if (!loc.isTileOnMap(tile) || loc.isWaterTile(x, y) || loc.getTileIndexAt(x, y, "Back") < 0) return false;
            if (!loc.isTilePassable(tile) || loc.objects.ContainsKey(tile) || loc.terrainFeatures.ContainsKey(tile) || DrawnOver(loc, x, y)) return false;
            for (int dy = -ShoreBand; dy <= ShoreBand; dy++)
                for (int dx = -ShoreBand; dx <= ShoreBand; dx++)
                    if (loc.isWaterTile(x + dx, y + dy)) return true;
            return false;
        }

        private static bool DrawnOver(GameLocation loc, int x, int y)
        {
            foreach (string name in OverLayers)
            {
                Layer layer = loc.Map.GetLayer(name);
                if (layer != null && x >= 0 && y >= 0 && x < layer.LayerWidth && y < layer.LayerHeight && layer.Tiles[x, y] != null)
                    return true;
            }
            return false;
        }
    }
}
