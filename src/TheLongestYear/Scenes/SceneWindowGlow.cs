using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Scenes
{
    /// <summary>Firelight behind a row of windows, and one dark figure at work in one of them (spec
    /// 2026-09-21, the hall; Jeff, 2026-10-07: one of the mines' own shadow monsters, completely
    /// blacked out, side-on and busy, replacing the row of men's silhouettes, which read like a
    /// washroom sign). The Community Center's front wears it and nothing else does yet, but it
    /// knows nothing about the Community Center: it is given a list of panes in world pixels and it
    /// lights them.
    ///
    /// TWO PARTS, AND THEY ARE DIFFERENT THINGS. The PAINTED glow is what the player reads as a lit
    /// room: a flat warm fill on the glass itself, flickering, drawn by the scene after the lightmap
    /// has already been composited, so it is never dimmed by the night. The LIGHT SOURCES are what
    /// the player reads as the light getting out: one warm pool per pane in the game's own lightmap,
    /// which lifts the wall, the path and the snow around each window the way a real window does.
    /// Neither one alone looks like a lit building.
    ///
    /// THE LIGHT COLOUR IS INVERTED ON PURPOSE. The lightmap is SUBTRACTED from the finished frame
    /// (<c>Game1.lightingBlend</c>), so a light source is given the complement of the colour it is
    /// meant to cast. Vanilla's torch is <c>new Color(0, 80, 160)</c> for exactly this reason
    /// (Object.cs:2756), and it burns orange.
    ///
    /// THE FIGURE NEVER LEAVES THE GLASS. It is cut down to the pane it is crossing by
    /// <see cref="SceneWindow.Clip"/>, which takes the matching slice of the texture rather than
    /// touching the device's scissor rectangle. See that class for why.</summary>
    internal sealed class SceneWindowGlow : IDisposable
    {
        /// <summary>Firelight, as the player sees it on the glass.</summary>
        private static readonly Color Firelight = new Color(255, 140, 40);

        /// <summary>How strong a window's pool of light is, as a share of full firelight. Jeff
        /// found the first version too bright (2026-09-23).</summary>
        private const float LightStrength = 0.6f;

        /// <summary>The same firelight as the lightmap wants it: the complement, because the
        /// lightmap is subtracted, scaled down to <see cref="LightStrength"/>.</summary>
        private static readonly Color FirelightSubtracted = new Color(
            (int)((255 - Firelight.R) * LightStrength),
            (int)((255 - Firelight.G) * LightStrength),
            (int)((255 - Firelight.B) * LightStrength));

        /// <summary>How far one window's light reaches, in the light texture's own units. One is
        /// about a two tile pool, which is a window and not a bonfire.</summary>
        private const float LightRadius = 1f;

        /// <summary>The sconce texture, which is the small round pool vanilla uses for a torch.
        /// The window light texture is a hard rectangle meant to be laid over a window tile from
        /// inside a house, and it does not read from the street.</summary>
        private const int LightTextureIndex = LightSource.sconceLight;

        /// <summary>What this class calls its lights in <see cref="Game1.currentLightSources"/>.
        /// Prefixed so a light left behind by a crash is obviously the mod's.</summary>
        private const string LightIdPrefix = "TLY_SceneWindow_";

        /// <summary>The game's art is drawn four pixels to a texel, and so is the figure.</summary>
        private const int Texel = 4;

        /// <summary>The figure: a Shadow Brute, the mines' own shadow monster (controller ruling,
        /// 2026-10-07). Its sheet is four columns of 16 by 32 frames in the villager layout: frames 4
        /// to 7 walk to the right, and frames 20 to 23 are the same side-on figure leaning in and
        /// reaching forward with one arm, which is what reads as hands busy at something.</summary>
        private const string FigureSheet = "Characters\\Monsters\\Shadow Brute";
        private const int FrameWidth = 16;
        private const int FrameHeight = 32;
        /// <summary>Its work, side-on and turned away from the street (Jeff, 2026-10-07: he is
        /// busy inside, undoing something, and is NOT looking out at Shane): upright, lean in and
        /// reach, lean in, reach again. The reversion is what he is doing.</summary>
        private static readonly int[] WorkFrames = { 21, 22, 23, 22 };
        /// <summary>How long each working pose is held. Slow, deliberate work.</summary>
        private const int WorkFrameMs = 320;
        private const int WalkRightFirstFrame = 4;
        private const int WalkFrames = 4;
        /// <summary>Vanilla's own walking frame interval.</summary>
        private const int WalkFrameMs = 175;

        /// <summary>A texel of the sheet counts as figure only when it is fully opaque. The
        /// see-through texels are its outline's soft edge and the shadow under its feet.</summary>
        private const byte OpaqueAlpha = 255;

        /// <summary>The top of the frame sits this many texels above the top of the glass, which
        /// puts the tip of its head two texels inside the pane and its arms at the sill. Its legs
        /// are below the sill, out of sight.</summary>
        private const int FrameAbovePaneTexels = 1;

        /// <summary>When it steps into the window from the left, and when it has stopped at its work.
        /// Both while the camera is still on the facade.</summary>
        private const int FigureEnterAtMs = 900;
        private const int FigureArriveAtMs = 2400;

        /// <summary>Where it stands to work: the frame's left edge this many texels left of the glass.
        /// Its body then fills the left of the pane and its reaching hand the right.</summary>
        private const int WorkLeftOfPaneTexels = 2;

        /// <summary>The figure in world pixels.</summary>
        private const int FigureWidth = FrameWidth * Texel;
        private const int FigureHeight = FrameHeight * Texel;

        /// <summary>The panes, in WORLD pixels.</summary>
        private readonly List<Rectangle> _panes = new List<Rectangle>();

        /// <summary>What this class put in <see cref="Game1.currentLightSources"/>, so it can take
        /// exactly those out again.</summary>
        private readonly List<string> _lightIds = new List<string>();

        /// <summary>Which pane the figure stands in.</summary>
        private readonly int _figurePane;

        /// <summary>Where a warning goes when the figure cannot be built.</summary>
        private readonly Action<string> _warn;

        /// <summary>The figure's sheet as a solid white mask, holes filled, built the first time a
        /// frame is painted, because a texture needs the graphics device.</summary>
        private Texture2D _silhouette;
        private bool _silhouetteFailed;

        /// <param name="originTile">The top left tile of the building the panes are measured from.</param>
        /// <param name="tileRelativePanes">Each pane in PIXELS, relative to the top left corner of
        /// <paramref name="originTile"/>.</param>
        /// <param name="figurePane">Which pane the figure stands in. Out of range means no figure.</param>
        /// <param name="warn">Where to say so when the figure cannot be built, and the windows then
        /// burn with nobody in them.</param>
        public SceneWindowGlow(Vector2 originTile, IReadOnlyList<Rectangle> tileRelativePanes, int figurePane, Action<string> warn = null)
        {
            if (tileRelativePanes == null) throw new ArgumentNullException(nameof(tileRelativePanes));
            _warn = warn;
            _figurePane = figurePane;
            var origin = new Point((int)originTile.X * SceneCamera.TileSize, (int)originTile.Y * SceneCamera.TileSize);
            foreach (Rectangle pane in tileRelativePanes)
            {
                if (pane.Width <= 0 || pane.Height <= 0) continue;
                _panes.Add(new Rectangle(origin.X + pane.X, origin.Y + pane.Y, pane.Width, pane.Height));
            }
        }

        /// <summary>How many panes are lit.</summary>
        public int Count => _panes.Count;

        /// <summary>Put one warm light in the game's own lightmap per pane. Call it AFTER the camera
        /// has cut to the map: <c>SceneCamera.CutTo</c> runs <c>resetForPlayerEntry</c>, which
        /// clears and rebuilds every light on the map, so a light added before it is thrown away.</summary>
        public void AddLights()
        {
            if (Game1.currentLightSources == null) return;
            for (int i = 0; i < _panes.Count; i++)
            {
                Rectangle pane = _panes[i];
                // An ordinary light and not a WindowLight one: the game fades a WindowLight out by
                // itself whenever it rains or the hour says the lights should be off
                // (LightSource.Draw), and this one has to burn through whatever tonight's weather is.
                string id = LightIdPrefix + i;
                Game1.currentLightSources[id] = new LightSource(
                    id,
                    LightTextureIndex,
                    new Vector2(pane.Center.X, pane.Center.Y),
                    LightRadius,
                    FirelightSubtracted);
                _lightIds.Add(id);
            }
        }

        /// <summary>Take back exactly the lights this class added. Safe to call twice and safe to
        /// call when none were ever added.</summary>
        public void RemoveLights()
        {
            if (Game1.currentLightSources != null)
                foreach (string id in _lightIds)
                    Game1.currentLightSources.Remove(id);
            _lightIds.Clear();
        }

        /// <summary>The glass this instant: the firelight on each pane, then the figure in its one.
        /// Drawn at the world layer, in screen pixels, and deliberately never tinted by the scene's
        /// night.</summary>
        public void Paint(SpriteBatch b, int elapsedMs)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (_panes.Count == 0 || Game1.staminaRect == null) return;
            for (int i = 0; i < _panes.Count; i++)
                b.Draw(Game1.staminaRect, ToScreen(_panes[i]), Firelight * SceneWindow.Flicker(elapsedMs, i));
            PaintFigure(b, elapsedMs);
        }

        /// <summary>The figure: it steps in from the left edge of its pane, walking, and then works
        /// there side-on for the rest of the scene, its reaching arm toward the right of the glass.
        /// Solid black, cut at the glass.</summary>
        private void PaintFigure(SpriteBatch b, int elapsedMs)
        {
            if (_figurePane < 0 || _figurePane >= _panes.Count) return;
            Texture2D mask = Silhouette();
            if (mask == null) return;
            Rectangle pane = _panes[_figurePane];

            // On the texel grid like the window art, so the cut edge never falls mid-texel, and a
            // little left of centre so the arm it reaches out with lands on the glass. It starts
            // wholly left of the glass, where the cut hides it.
            int standX = SceneWindow.SnapToTexel(pane.X - WorkLeftOfPaneTexels * Texel, Texel);
            int x = SceneWindow.SnapToTexel(
                SceneWindow.FigureX(elapsedMs, FigureEnterAtMs, FigureArriveAtMs, pane.X - FigureWidth, standX, out bool walking),
                Texel);
            int y = pane.Y - FrameAbovePaneTexels * Texel;
            int frame = walking
                ? WalkRightFirstFrame + SceneWindow.Cycle(elapsedMs, FigureEnterAtMs, WalkFrames, WalkFrameMs)
                : WorkFrames[SceneWindow.Cycle(elapsedMs, FigureArriveAtMs, WorkFrames.Length, WorkFrameMs)];
            int columns = Math.Max(1, mask.Width / FrameWidth);
            int frameX = frame % columns * FrameWidth;
            int frameY = frame / columns * FrameHeight;

            bool hit = SceneWindow.Clip(
                x, y, FigureWidth, FigureHeight,
                pane.X, pane.Y, pane.Width, pane.Height,
                FrameWidth, FrameHeight,
                out SceneWindow.ClippedDraw cut);
            if (!hit) return;
            b.Draw(
                mask,
                ToScreen(new Rectangle(cut.DestX, cut.DestY, cut.DestWidth, cut.DestHeight)),
                new Rectangle(frameX + cut.SourceX, frameY + cut.SourceY, cut.SourceWidth, cut.SourceHeight),
                Color.Black);
        }

        /// <summary>The figure's sheet as a solid mask: every fully opaque texel white, and every
        /// hole inside a frame's outline (its eyes, its mouth) filled, frame by frame, so it draws
        /// as one unbroken black shape. Null, with one warning, when the sheet will not load or the
        /// device will not give a texture, and the glass then burns with nobody in it.</summary>
        private Texture2D Silhouette()
        {
            if (_silhouette != null || _silhouetteFailed) return _silhouette;
            try
            {
                Texture2D sheet = Game1.content.Load<Texture2D>(FigureSheet);
                int w = sheet.Width, h = sheet.Height;
                var pixels = new Color[w * h];
                sheet.GetData(pixels);
                var data = new Color[w * h];
                for (int fy = 0; fy + FrameHeight <= h; fy += FrameHeight)
                    for (int fx = 0; fx + FrameWidth <= w; fx += FrameWidth)
                    {
                        var opaque = new bool[FrameWidth, FrameHeight];
                        for (int x = 0; x < FrameWidth; x++)
                            for (int y = 0; y < FrameHeight; y++)
                                opaque[x, y] = pixels[(fy + y) * w + fx + x].A >= OpaqueAlpha;
                        bool[,] solid = SceneWindow.FillHoles(opaque);
                        for (int x = 0; x < FrameWidth; x++)
                            for (int y = 0; y < FrameHeight; y++)
                                data[(fy + y) * w + fx + x] = solid[x, y] ? Color.White : Color.Transparent;
                    }
                _silhouette = new Texture2D(Game1.graphics.GraphicsDevice, w, h);
                _silhouette.SetData(data);
            }
            catch (Exception ex)
            {
                _silhouetteFailed = true;
                _silhouette = null;
                _warn?.Invoke($"the window figure could not be built from {FigureSheet}, so the windows burn with nobody in them ({ex.GetType().Name}: {ex.Message})");
            }
            return _silhouette;
        }

        /// <summary>Let go of the silhouette texture. Safe to call twice.</summary>
        public void Dispose()
        {
            _silhouette?.Dispose();
            _silhouette = null;
        }

        /// <summary>A world rectangle where it lands on the screen.</summary>
        private static Rectangle ToScreen(Rectangle world)
        {
            Vector2 corner = SceneCamera.ToScreen(new Vector2(world.X, world.Y));
            return new Rectangle((int)corner.X, (int)corner.Y, world.Width, world.Height);
        }

        /// <summary>The panes and their lights, for the log.</summary>
        public string Describe()
        {
            if (_panes.Count == 0) return "no windows";
            var said = new List<string>();
            foreach (Rectangle pane in _panes)
                said.Add($"({pane.X},{pane.Y} {pane.Width}x{pane.Height})");
            string figure = _figurePane >= 0 && _figurePane < _panes.Count ? $"a {FigureSheet} figure in window {_figurePane}" : "no figure";
            return $"{_panes.Count} window(s) in world pixels {string.Join(" ", said)}, {_lightIds.Count} light(s), {figure}";
        }
    }
}
