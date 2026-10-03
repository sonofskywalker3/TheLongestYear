using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's dust: tlyDust and tlyDustView (documented with the other commands
    /// in JojaBadEndingCommands.cs). Every puff is a temporary sprite in Sprites, removed by Cleanup.</summary>
    internal static partial class JojaBadEndingCommands
    {
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

        /// <summary>Dust over everything on screen (one tile past each edge), without waiting. An
        /// optional tile rectangle (the kept farmhouse) gets no puffs centred on it.</summary>
        private static void DustView(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGetInt(args, 1, out int ms, out string error)
                || !ArgUtility.TryGetOptionalInt(args, 2, out int ex, out error)
                || !ArgUtility.TryGetOptionalInt(args, 3, out int ey, out error)
                || !ArgUtility.TryGetOptionalInt(args, 4, out int ew, out error)
                || !ArgUtility.TryGetOptionalInt(args, 5, out int eh, out error))
            {
                Skip(evt, DustViewName, error);
                return;
            }
            Guarded(evt, DustViewName, () =>
            {
                int x = Game1.viewport.X / Game1.tileSize - 1, y = Game1.viewport.Y / Game1.tileSize - 1;
                int w = Game1.viewport.Width / Game1.tileSize + 3, h = Game1.viewport.Height / Game1.tileSize + 3;
                SpawnDust(Game1.currentLocation, x, y, w, h, Math.Max(1, ms), ViewPuffsPerTileSecond, MaxViewPuffs, ViewPuffScaleMin, ViewPuffScaleSpread,
                    new Rectangle(ex, ey, ew, eh));
            });
        }

        /// <summary>Puffs scattered over the rectangle, their starts spread over the whole duration,
        /// drawn above everything so what changes under them is hidden. A puff whose centre would land
        /// in <paramref name="spare"/> (tiles; empty = none) is not spawned.</summary>
        private static void SpawnDust(GameLocation loc, int x, int y, int w, int h, int ms, float perTileSecond, int max, float scaleMin, float scaleSpread,
            Rectangle spare = default)
        {
            if (loc == null) return;
            int count = Math.Clamp((int)(w * h * perTileSecond * ms / 1000f), MinPuffs, max);
            int lastStart = Math.Max(0, ms - PuffAnimMs);
            var spared = new Rectangle(spare.X * 64, spare.Y * 64, spare.Width * 64, spare.Height * 64);
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(x + (float)Game1.random.NextDouble() * w - 0.5f, y + (float)Game1.random.NextDouble() * h - 0.5f) * 64f;
                float scale = scaleMin + (float)Game1.random.NextDouble() * scaleSpread;
                Vector2 centre = pos + new Vector2(32f * scale);   // the puff grows right and down from pos
                if (!spared.IsEmpty && spared.Contains((int)centre.X, (int)centre.Y)) continue;
                var puff = new TemporaryAnimatedSprite(5, pos, DustColour, animationLength: 8,
                    flipped: Game1.random.Next(2) == 0, animationInterval: 60f, layerDepth: 1f, delay: Game1.random.Next(lastStart + 1))
                {
                    scale = scale,
                    motion = new Vector2(0f, -0.3f),
                };
                loc.temporarySprites.Add(puff);
                Sprites.Add((loc, puff));
            }
        }
    }
}
