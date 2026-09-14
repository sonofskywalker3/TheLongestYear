using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace TheLongestYear.UI
{
    /// <summary>Grey cloud drifting round the rim of the bedroom's lit area while the darkness closes
    /// in: "I really do need some kind of effect around the edges to make it seem more sinister and
    /// pressing in ... even if it's just a high transparency grey cloud flowing by vanilla animation
    /// bounded into the fading zone at the edge of the circle of light" (Jeff, 2026-09-14).
    ///
    /// Each wisp is the game's own soft light sprite (<c>Game1.sconceLight</c>, the texture the scene's
    /// pools are drawn with), stretched along the rim and tinted grey. They orbit the bed slowly, some
    /// each way, and breathe in and out across a band that straddles the edge of the light, thickest in
    /// the middle of the band and gone at both ends, so the cloud stays inside the fading zone rather
    /// than drifting over the lit floor or out into the black where nobody could see it.
    ///
    /// Drawn in UI space by the scene, after the world and its lighting, because drawn in the world
    /// the lightmap would subtract it to nothing exactly where it is meant to show.</summary>
    internal sealed class RewindEdgeFog
    {
        private const int WispCount = 40;

        /// <summary>How much of a light sprite's half-width reads as lit before its falloff is lost in
        /// the dark. The rim is placed off this.</summary>
        private const float GlowVisibleFraction = 0.75f;

        // The band the cloud lives in, as fractions of the lit edge's distance from the bed.
        private const float BandInner = 0.72f;
        private const float BandOuter = 1.12f;

        // "High transparency": the most any one wisp is ever drawn at. Forty of them overlap, so the
        // thickest patches still read as cloud rather than a grey ring.
        private const float MaxAlpha = 0.24f;
        private const float MinEnvelopeAlpha = 0.2f;

        // A wisp's size, as fractions of the lit edge: long along the rim, shallow across it.
        private const float WispLengthOfEdge = 0.55f;
        private const float WispDepthOfEdge = 0.22f;
        private const float MinWispSize = 0.7f;
        private const float WispSizeSpread = 0.6f;

        // Orbit speed in radians per millisecond (a full lap takes about 35 to 125 seconds), and the
        // period of each wisp's drift in and out across the band.
        private const float MinAngularSpeed = 0.00005f;
        private const float MaxAngularSpeed = 0.00018f;
        private const float MinBandPeriodMs = 2600f;
        private const float MaxBandPeriodMs = 5200f;

        /// <summary>Only used if the light texture has not loaded, which it always has by now.</summary>
        private const int FallbackLightTextureWidth = 256;

        private static readonly Color FogGrey = new Color(150, 150, 160);

        private struct Wisp
        {
            public float Angle;
            public float AngularSpeed;
            public float Phase;
            public float BandPeriodMs;
            public float Size;
        }

        private readonly Wisp[] _wisps = new Wisp[WispCount];
        private float _clockMs;

        /// <summary>0 draws nothing, 1 is the full cloud. The scene drives it with the darkness.</summary>
        public float Strength { get; set; }

        public RewindEdgeFog()
        {
            var rng = new Random(Game1.random.Next());
            for (int i = 0; i < _wisps.Length; i++)
            {
                float direction = rng.Next(2) == 0 ? -1f : 1f;
                _wisps[i] = new Wisp
                {
                    Angle = (float)(rng.NextDouble() * MathHelper.TwoPi),
                    AngularSpeed = direction * MathHelper.Lerp(MinAngularSpeed, MaxAngularSpeed, (float)rng.NextDouble()),
                    Phase = (float)(rng.NextDouble() * MathHelper.TwoPi),
                    BandPeriodMs = MathHelper.Lerp(MinBandPeriodMs, MaxBandPeriodMs, (float)rng.NextDouble()),
                    Size = MinWispSize + WispSizeSpread * (float)rng.NextDouble(),
                };
            }
        }

        /// <summary>How far a light of <paramref name="radius"/> visibly reaches, in world pixels.
        /// <c>LightSource.Draw</c> draws its texture at <c>radius</c> times its own size.</summary>
        public static float GlowReach(float radius)
            => radius * (Game1.sconceLight?.Width ?? FallbackLightTextureWidth) / 2f * GlowVisibleFraction;

        public void Update(float elapsedMs)
        {
            _clockMs += elapsedMs;
            for (int i = 0; i < _wisps.Length; i++)
                _wisps[i].Angle += _wisps[i].AngularSpeed * elapsedMs;
        }

        /// <summary>Draws the cloud round <paramref name="worldCentre"/>, straddling a lit edge
        /// <paramref name="worldEdge"/> world pixels out.</summary>
        public void Draw(SpriteBatch b, Vector2 worldCentre, float worldEdge)
        {
            Texture2D texture = Game1.sconceLight;
            if (Strength <= 0f || texture == null || worldEdge <= 0f) return;

            float worldToUi = Game1.options.zoomLevel / Game1.options.uiScale;
            var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            foreach (Wisp w in _wisps)
            {
                float band = 0.5f + 0.5f * (float)Math.Sin(w.Phase + _clockMs * MathHelper.TwoPi / w.BandPeriodMs);
                float distance = worldEdge * MathHelper.Lerp(BandInner, BandOuter, band);
                Vector2 world = worldCentre + new Vector2((float)Math.Cos(w.Angle), (float)Math.Sin(w.Angle)) * distance;
                Vector2 onScreen = Utility.ModifyCoordinatesForUIScale(Game1.GlobalToLocal(Game1.viewport, world));

                float envelope = (float)Math.Sin(band * MathHelper.Pi);
                float alpha = Strength * MaxAlpha * (MinEnvelopeAlpha + (1f - MinEnvelopeAlpha) * envelope);
                var scale = new Vector2(WispLengthOfEdge, WispDepthOfEdge) * (worldEdge * w.Size * worldToUi / texture.Width);
                b.Draw(texture, onScreen, null, FogGrey * alpha, w.Angle + MathHelper.PiOver2, origin, scale,
                    SpriteEffects.None, 0f);
            }
        }
    }
}
