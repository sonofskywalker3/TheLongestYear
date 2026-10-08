using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheLongestYear.Scenes
{
    /// <summary>The crows scene's bird: the critter sheet's crow frames and its red eye.</summary>
    internal sealed partial class CrowsScene
    {
        // ---------------------------------------------------------------- the crow sheet

        private const string CritterSheet = "TileSheets\\critters";
        private const int CrowSpriteSize = 32;
        /// <summary>Crow.cs:22 passes 14 to <c>Critter</c>, so the crow's frames start here. From
        /// Crow.cs's own animations: +0 standing, +1 to +4 the peck, +5 asleep, +6 to +10 the flap.</summary>
        private const int CrowBaseFrame = 14;
        private const int CrowStandFrame = CrowBaseFrame;
        private const int CrowPeckDownFrame = CrowBaseFrame + 3;
        private const int CrowPeckStrikeFrame = CrowBaseFrame + 4;
        private const int CrowFlapFirstFrame = CrowBaseFrame + 6;
        private const int CrowFlapFrames = 5;
        private const int CrowFlapMs = 60;

        private const float DrawScale = 4f;
        /// <summary>A crow's drawn sprite is four tiles across and hangs two tiles above its feet,
        /// the offset <c>Critter.draw</c> uses (Critter.cs:69).</summary>
        private static readonly Vector2 CrowDrawOffset = new Vector2(-64f, -128f);

        /// <summary>The eye, in sheet pixels, drawn on top of the bird.
        ///
        /// It is PAINTED and not a <c>LightSource</c>. The spec asked for a small red light per crow
        /// as well. It was built, screenshotted and cut (2026-09-21): even at the smallest radius the
        /// sconce texture is about a hundred pixels across, so six of them turned the row of birds
        /// into one orange bonfire and hid the crows the scene is about.</summary>
        private const int EyeDotPixels = 2;

        /// <summary>WHERE THE EYE IS, PER FRAME, HAND AUTHORED OFF THE REAL SHEET.
        ///
        /// <c>TileSheets\critters</c> was dumped out of the running game (320x640, ten 32 px columns)
        /// and read pixel by pixel. The crow's eye is the only magenta pixel on the bird, so there is
        /// no guessing involved: these are its exact offsets inside each 32x32 frame, top left of the
        /// two pixel eye.
        ///
        /// The first pass looked the eye up at runtime as the brightest opaque pixel in the TOP HALF
        /// of the frame. That was wrong in two ways the screenshots showed: it is not stable frame to
        /// frame, and on the two pecking frames the bird's head is down at y 24 and 25, outside the
        /// half it searched, so the dot landed somewhere on the body instead.
        ///
        /// Frame 19 is the sleeping pose and its eye is shut, so it has no entry and the scene never
        /// draws it.</summary>
        private static readonly Dictionary<int, Point> EyeOffsets = new()
        {
            [CrowBaseFrame + 0] = new Point(11, 17),   // standing
            [CrowBaseFrame + 1] = new Point(10, 17),   // head dipping
            [CrowBaseFrame + 2] = new Point(8, 19),
            [CrowBaseFrame + 3] = new Point(8, 24),    // head down
            [CrowBaseFrame + 4] = new Point(8, 25),    // the peck itself
            [CrowBaseFrame + 6] = new Point(7, 19),    // the five flap frames
            [CrowBaseFrame + 7] = new Point(7, 19),
            [CrowBaseFrame + 8] = new Point(7, 18),
            [CrowBaseFrame + 9] = new Point(7, 17),
            [CrowBaseFrame + 10] = new Point(7, 17),
        };

        /// <summary>How wide the glow is drawn, in sheet pixels, so it scales with the bird. The
        /// glow itself lives in <see cref="SceneGlow"/>, shared with the thief.</summary>
        private const float EyeGlowSheetPixels = 9f;

        /// <summary>A glowing red eye: a soft radial pool, then two sheet pixels of solid red in the
        /// middle of it, at the eye's real place in this frame. Neither is tinted by the night, which
        /// is the point of it.</summary>
        private static void PaintEye(SpriteBatch b, SceneCrow crow, Vector2 corner, bool flip)
        {
            if (!EyeOffsets.TryGetValue(crow.Frame, out Point eye)) return;
            float x = flip ? (CrowSpriteSize - EyeDotPixels - eye.X) * DrawScale : eye.X * DrawScale;
            float core = EyeDotPixels * DrawScale;
            var centre = new Vector2(corner.X + x + core / 2f, corner.Y + eye.Y * DrawScale + core / 2f);
            SceneGlow.Draw(b, centre, EyeGlowSheetPixels * DrawScale, core, Color.Red);
        }
    }
}
