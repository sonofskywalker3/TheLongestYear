using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace TheLongestYear.Scenes
{
    /// <summary>The soft light an overnight strike scene paints on a pair of eyes (spec 2026-09-21):
    /// a small radial pool with a solid core in the middle of it. The crows wear it, and so does the
    /// thief when he turns to face the camera.
    ///
    /// It is PAINTED and never a <c>LightSource</c>. The spec asked for a real light per eye at
    /// first. That was built, screenshotted and cut: even at the smallest radius the sconce texture
    /// is about a hundred screen pixels across, so a handful of them turn whatever is wearing them
    /// into one orange blob and hide the thing the scene is about.
    ///
    /// It is also never tinted by the scene's night. That is the whole point of it: everything else
    /// a scene draws takes <see cref="SceneCamera.NightTint"/>, so the eyes come out as the only lit
    /// thing in the frame.</summary>
    internal static class SceneGlow
    {
        /// <summary>The radial texture is this many pixels square. Small on purpose: it is always
        /// drawn stretched, and the falloff is what reads, not the resolution.</summary>
        private const int GlowPixels = 16;

        private static Texture2D _glow;
        /// <summary>The device the texture belongs to. A graphics device reset throws every texture
        /// made on the old one away, so the next draw has to notice and build a fresh one.</summary>
        private static GraphicsDevice _builtOn;
        /// <summary>True once building has failed. Without it a device that will not make a texture
        /// is asked again on every single frame for the rest of the scene.</summary>
        private static bool _gaveUp;

        /// <summary>Draw a glowing point: the soft pool, then a solid core in the middle of it.</summary>
        /// <param name="centre">Where the light is, in SCREEN pixels.</param>
        /// <param name="glowWidth">How wide the soft pool is drawn, in screen pixels.</param>
        /// <param name="coreWidth">How wide the solid core is drawn, in screen pixels. Zero draws
        /// the pool alone.</param>
        public static void Draw(SpriteBatch b, Vector2 centre, float glowWidth, float coreWidth, Color colour)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            Texture2D glow = Texture();
            if (glow != null && glowWidth > 0f)
            {
                b.Draw(
                    glow,
                    new Rectangle(
                        (int)(centre.X - glowWidth / 2f),
                        (int)(centre.Y - glowWidth / 2f),
                        (int)glowWidth,
                        (int)glowWidth),
                    colour);
            }
            if (coreWidth <= 0f || Game1.staminaRect == null) return;
            b.Draw(
                Game1.staminaRect,
                new Rectangle(
                    (int)(centre.X - coreWidth / 2f),
                    (int)(centre.Y - coreWidth / 2f),
                    (int)coreWidth,
                    (int)coreWidth),
                colour);
        }

        /// <summary>The radial falloff texture, built once and kept for the session. Alpha falls off
        /// with the square of the distance from the middle, which reads as a glow rather than as a
        /// disc with an edge. Rebuilt when the graphics device has changed under it, and never
        /// retried once it has failed.</summary>
        private static Texture2D Texture()
        {
            GraphicsDevice device = Game1.graphics?.GraphicsDevice;
            if (device == null) return null;
            if (_glow != null && !_glow.IsDisposed && ReferenceEquals(_builtOn, device)) return _glow;
            if (_gaveUp && ReferenceEquals(_builtOn, device)) return null;
            Forget();
            try
            {
                var pixels = new Color[GlowPixels * GlowPixels];
                float middle = (GlowPixels - 1) / 2f;
                for (int y = 0; y < GlowPixels; y++)
                {
                    for (int x = 0; x < GlowPixels; x++)
                    {
                        float dx = (x - middle) / middle;
                        float dy = (y - middle) / middle;
                        float reach = 1f - Math.Min(1f, (float)Math.Sqrt(dx * dx + dy * dy));
                        pixels[y * GlowPixels + x] = Color.White * (reach * reach);
                    }
                }
                var made = new Texture2D(device, GlowPixels, GlowPixels);
                made.SetData(pixels);
                _glow = made;
                _builtOn = device;
                _gaveUp = false;
            }
            catch (Exception)
            {
                _glow = null;
                _builtOn = device;
                _gaveUp = true;
            }
            return _glow;
        }

        /// <summary>Throw the old texture away before a rebuild, so a device reset does not leak one
        /// per reset.</summary>
        private static void Forget()
        {
            Texture2D old = _glow;
            _glow = null;
            _builtOn = null;
            _gaveUp = false;
            if (old == null || old.IsDisposed) return;
            try { old.Dispose(); }
            catch (Exception) { /* a texture that will not let go is not worth failing a scene for. */ }
        }
    }
}
