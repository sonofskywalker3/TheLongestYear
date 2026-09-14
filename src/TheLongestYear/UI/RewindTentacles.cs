using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace TheLongestYear.UI
{
    /// <summary>Black tentacles reaching in from the dark toward the bedroom's lit area while the
    /// darkness closes in, and breaking up when the Junimos' light swells at the end.
    ///
    /// The first pass was grey cloud drifting round the rim. Jeff, 2026-09-14: "they should be pure
    /// black instead of the light grey, and they should be coming in from the outer darkness, thicker
    /// at the back and tapering to a point, more like tentacles". So each one is rooted out in the
    /// black past the light, thick there, and tapers to a point that reaches just into the fading
    /// edge, swaying more toward the tip. They creep in as <see cref="Strength"/> rises.
    ///
    /// DISINTEGRATING. "It was weird seeing the drawing grow as it moved outward with the expanding
    /// light, I'd probably like the expanding junimo light to disintegrate the tentacles as it
    /// expands." While the light swells, the scene draws them against the edge they had when it began,
    /// so they stay put, and passes the live lit edge as the dissolve radius: every piece the light
    /// has reached shrinks, scatters outward and fades, tips first, with a ragged front rather than a
    /// clean circle.
    ///
    /// Each tentacle is a chain of overlapping filled discs from a texture made here once, so the body
    /// is solid and the taper is smooth. Drawn in UI space by the scene, after the world's lighting,
    /// because drawn in the world the lightmap would take them down to the same black as the dark
    /// they come out of.</summary>
    internal sealed class RewindTentacles
    {
        private const int TentacleCount = 13;

        // Reach, as fractions of the lit edge's distance from the bed: rooted out in the dark, the
        // tip just inside the fading edge once fully in, and breathing a little in and out of it.
        private const float RootOut = 1.6f;
        private const float TipReach = 0.82f;
        private const float BreathOfEdge = 0.05f;
        private const float MinBreathPeriodMs = 3000f;
        private const float MaxBreathPeriodMs = 5500f;

        // Shape: the root's radius as a fraction of the lit edge, how fast it tapers, and the tip.
        private const float RootWidthOfEdge = 0.13f;
        private const float MinThickness = 0.75f;
        private const float ThicknessSpread = 0.5f;
        private const float TaperPower = 1.2f;
        private const float TipRadiusPx = 1.5f;

        // The sway: a travelling wave along the body, strongest at the tip, still at the root.
        private const float WaveCount = 1.25f;
        private const float WaveSpeedPerMs = 0.0025f;
        private const float SwayOfEdge = 0.09f;

        // How far round the bed each one slowly wanders, in radians per millisecond, and how far its
        // angle may start from an even spacing (as a fraction of that spacing).
        private const float MaxDriftPerMs = 0.00002f;
        private const float AngleJitter = 0.6f;

        // Disc spacing along the body, as a fraction of the local radius, with a floor so the tip is
        // not drawn a thousand times.
        private const float SpacingOfRadius = 0.35f;
        private const float MinStep = 0.004f;

        // Breaking up: how deep the ragged front is, and how far a crumbling piece is thrown.
        private const float CrumbleWidthOfEdge = 0.18f;
        private const float CrumbleDrift = 0.6f;

        /// <summary>How much of a light sprite's half-width reads as lit before its falloff is lost in
        /// the dark.</summary>
        private const float GlowVisibleFraction = 0.75f;

        /// <summary>Only used if the light texture has not loaded, which it always has by now.</summary>
        private const int FallbackLightTextureWidth = 256;

        private const int DiscSize = 64;
        private static Texture2D _disc;

        private struct Tentacle
        {
            public float Angle;
            public float DriftPerMs;
            public float Phase;
            public float BreathPeriodMs;
            public float Thickness;
        }

        private readonly Tentacle[] _tentacles = new Tentacle[TentacleCount];
        private float _clockMs;

        /// <summary>How far in they have crept: 0 is still in the dark (nothing drawn), 1 is fully in.</summary>
        public float Strength { get; set; }

        /// <summary>Overall opacity, for fading whatever is left out under the white.</summary>
        public float Opacity { get; set; } = 1f;

        public RewindTentacles()
        {
            var rng = new Random(Game1.random.Next());
            float spacing = MathHelper.TwoPi / TentacleCount;
            for (int i = 0; i < _tentacles.Length; i++)
            {
                _tentacles[i] = new Tentacle
                {
                    Angle = i * spacing + ((float)rng.NextDouble() - 0.5f) * AngleJitter * spacing,
                    DriftPerMs = ((float)rng.NextDouble() * 2f - 1f) * MaxDriftPerMs,
                    Phase = (float)(rng.NextDouble() * MathHelper.TwoPi),
                    BreathPeriodMs = MathHelper.Lerp(MinBreathPeriodMs, MaxBreathPeriodMs, (float)rng.NextDouble()),
                    Thickness = MinThickness + ThicknessSpread * (float)rng.NextDouble(),
                };
            }
        }

        /// <summary>How far a light of <paramref name="radius"/> visibly reaches, in world pixels.
        /// <c>LightSource.Draw</c> draws its texture at <c>radius</c> times its own size.</summary>
        public static float GlowReach(float radius)
            => radius * (Game1.sconceLight?.Width ?? FallbackLightTextureWidth) / 2f * GlowVisibleFraction;

        /// <summary>How far from the bed the roots sit against a lit edge of <paramref name="worldEdge"/>.
        /// The light has to pass this to break every tentacle up.</summary>
        public static float RootDistance(float worldEdge) => worldEdge * RootOut;

        public void Update(float elapsedMs)
        {
            _clockMs += elapsedMs;
            for (int i = 0; i < _tentacles.Length; i++)
                _tentacles[i].Angle += _tentacles[i].DriftPerMs * elapsedMs;
        }

        /// <summary>Draws every tentacle round <paramref name="worldCentre"/> against a lit edge
        /// <paramref name="worldEdge"/> world pixels out. Anything closer to the centre than
        /// <paramref name="dissolveRadius"/> is breaking up; pass 0 for none.</summary>
        public void Draw(SpriteBatch b, Vector2 worldCentre, float worldEdge, float dissolveRadius)
        {
            if (Strength <= 0f || Opacity <= 0f || worldEdge <= 0f) return;
            Texture2D disc = Disc();
            float worldToUi = Game1.options.zoomLevel / Game1.options.uiScale;
            var origin = new Vector2(DiscSize / 2f, DiscSize / 2f);
            float rootDistance = RootDistance(worldEdge);
            float crumbleWidth = worldEdge * CrumbleWidthOfEdge;

            for (int i = 0; i < _tentacles.Length; i++)
            {
                Tentacle t = _tentacles[i];
                float breath = BreathOfEdge * (float)Math.Sin(t.Phase + _clockMs * MathHelper.TwoPi / t.BreathPeriodMs);
                float tipDistance = worldEdge * MathHelper.Lerp(RootOut, TipReach + breath, Strength);
                float length = rootDistance - tipDistance;
                if (length <= 1f) continue;

                var dir = new Vector2((float)Math.Cos(t.Angle), (float)Math.Sin(t.Angle));
                var across = new Vector2(-dir.Y, dir.X);
                float rootRadius = worldEdge * RootWidthOfEdge * t.Thickness;

                int piece = 0;
                float s = 0f;
                while (s <= 1f)
                {
                    float radius = TipRadiusPx + (rootRadius - TipRadiusPx) * (float)Math.Pow(1f - s, TaperPower);
                    float step = Math.Max(MinStep, SpacingOfRadius * radius / length);
                    float sway = (float)Math.Sin(s * WaveCount * MathHelper.TwoPi - _clockMs * WaveSpeedPerMs + t.Phase)
                                 * worldEdge * SwayOfEdge * s;
                    Vector2 world = worldCentre + dir * (rootDistance - length * s) + across * sway;
                    float alpha = Opacity;

                    if (dissolveRadius > 0f)
                    {
                        float distance = Vector2.Distance(world, worldCentre);
                        float ragged = (Hash(i, piece) - 0.5f) * crumbleWidth;
                        float crumble = MathHelper.Clamp((dissolveRadius - distance + ragged) / crumbleWidth, 0f, 1f);
                        if (crumble >= 1f) { s += step; piece++; continue; }
                        radius *= 1f - crumble;
                        world += dir * crumble * crumbleWidth * CrumbleDrift
                                 + across * (Hash(piece, i) - 0.5f) * crumble * crumbleWidth;
                        alpha *= 1f - crumble * crumble;
                    }

                    Vector2 onScreen = Utility.ModifyCoordinatesForUIScale(Game1.GlobalToLocal(Game1.viewport, world));
                    float scale = radius * 2f * worldToUi / DiscSize;
                    b.Draw(disc, onScreen, null, Color.Black * alpha, 0f, origin, scale, SpriteEffects.None, 0f);

                    s += step;
                    piece++;
                }
            }
        }

        /// <summary>A stable 0-to-1 value per piece, so the ragged front and the scatter do not flicker
        /// from frame to frame.</summary>
        private static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>A filled white disc with a one-pixel soft rim, premultiplied, made once.</summary>
        private static Texture2D Disc()
        {
            if (_disc != null && !_disc.IsDisposed) return _disc;
            var data = new Color[DiscSize * DiscSize];
            float centre = DiscSize / 2f;
            for (int y = 0; y < DiscSize; y++)
            {
                for (int x = 0; x < DiscSize; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centre, centre));
                    data[y * DiscSize + x] = Color.White * MathHelper.Clamp(centre - d, 0f, 1f);
                }
            }
            _disc = new Texture2D(Game1.graphics.GraphicsDevice, DiscSize, DiscSize);
            _disc.SetData(data);
            return _disc;
        }
    }
}
