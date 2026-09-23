using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Scenes
{
    /// <summary>Firelight behind a row of windows, with shapes moving about in front of it (spec
    /// 2026-09-21, the hall). The Community Center's front wears it and nothing else does yet, but
    /// it knows nothing about the Community Center: it is given a list of panes in world pixels and
    /// it lights them.
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
    /// THE SHAPES NEVER LEAVE THE GLASS. Each one is cut down to the pane it is crossing by
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

        /// <summary>The game's art is drawn four pixels to a texel, and so is the silhouette.</summary>
        private const int Texel = 4;

        /// <summary>The silhouette in world pixels.</summary>
        private static readonly int ShapeWidth = SceneWindow.SilhouetteWidth * Texel;
        private static readonly int ShapeHeight = SceneWindow.SilhouetteHeight * Texel;

        /// <summary>How far below the top of the pane the top of his head is, in texels, when he is
        /// not mid-stride. His body then runs on below the sill, which hides his legs.</summary>
        private const int HeadBelowPaneTexels = 5;

        /// <summary>How black a silhouette is against the firelight. Not fully black: a shape in
        /// front of a fire still catches a little of it round the edges.</summary>
        private const float ShapeDarkness = 0.88f;

        /// <summary>The panes, in WORLD pixels.</summary>
        private readonly List<Rectangle> _panes = new List<Rectangle>();

        /// <summary>What this class put in <see cref="Game1.currentLightSources"/>, so it can take
        /// exactly those out again.</summary>
        private readonly List<string> _lightIds = new List<string>();

        private readonly int _spanLeft;
        private readonly int _spanWidth;

        /// <summary>Where a warning goes when the silhouette cannot be built.</summary>
        private readonly Action<string> _warn;

        /// <summary>The silhouette, built from <see cref="SceneWindow.Silhouette"/> the first time a
        /// frame is painted, because a texture needs the graphics device.</summary>
        private Texture2D _silhouette;
        private bool _silhouetteFailed;

        /// <param name="originTile">The top left tile of the building the panes are measured from.</param>
        /// <param name="tileRelativePanes">Each pane in PIXELS, relative to the top left corner of
        /// <paramref name="originTile"/>.</param>
        /// <param name="warn">Where to say so when the silhouette cannot be built, and the windows
        /// then burn with nobody crossing them.</param>
        public SceneWindowGlow(Vector2 originTile, IReadOnlyList<Rectangle> tileRelativePanes, Action<string> warn = null)
        {
            if (tileRelativePanes == null) throw new ArgumentNullException(nameof(tileRelativePanes));
            _warn = warn;
            var origin = new Point((int)originTile.X * SceneCamera.TileSize, (int)originTile.Y * SceneCamera.TileSize);
            int left = int.MaxValue, right = int.MinValue;
            foreach (Rectangle pane in tileRelativePanes)
            {
                if (pane.Width <= 0 || pane.Height <= 0) continue;
                var world = new Rectangle(origin.X + pane.X, origin.Y + pane.Y, pane.Width, pane.Height);
                _panes.Add(world);
                left = Math.Min(left, world.Left);
                right = Math.Max(right, world.Right);
            }
            _spanLeft = _panes.Count > 0 ? left : 0;
            _spanWidth = _panes.Count > 0 ? right - left : 0;
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

        /// <summary>The glass this instant: the firelight on each pane, then the shapes crossing it.
        /// Drawn at the world layer, in screen pixels, and deliberately never tinted by the scene's
        /// night.</summary>
        public void Paint(SpriteBatch b, int elapsedMs)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (_panes.Count == 0 || Game1.staminaRect == null) return;
            for (int i = 0; i < _panes.Count; i++)
                b.Draw(Game1.staminaRect, ToScreen(_panes[i]), Firelight * SceneWindow.Flicker(elapsedMs, i));
            PaintShapes(b, elapsedMs);
        }

        private void PaintShapes(SpriteBatch b, int elapsedMs)
        {
            Texture2D shape = Silhouette();
            if (shape == null || _spanWidth <= 0) return;
            for (int s = 0; s < SceneWindow.ShapeCount; s++)
            {
                // On the texel grid, like the window art, so the cut edge never falls mid-texel.
                int x = SceneWindow.SnapToTexel(SceneWindow.SlideX(elapsedMs, s, _spanLeft, _spanWidth, ShapeWidth), Texel);
                int lift = SceneWindow.StrideBob(elapsedMs, s) * Texel;
                foreach (Rectangle pane in _panes)
                {
                    bool hit = SceneWindow.Clip(
                        x, pane.Y + HeadBelowPaneTexels * Texel - lift, ShapeWidth, ShapeHeight,
                        pane.X, pane.Y, pane.Width, pane.Height,
                        shape.Width, shape.Height,
                        out SceneWindow.ClippedDraw cut);
                    if (!hit) continue;
                    b.Draw(
                        shape,
                        ToScreen(new Rectangle(cut.DestX, cut.DestY, cut.DestWidth, cut.DestHeight)),
                        new Rectangle(cut.SourceX, cut.SourceY, cut.SourceWidth, cut.SourceHeight),
                        Color.Black * ShapeDarkness);
                }
            }
        }

        /// <summary>The silhouette texture, built once from the mask. Null, with one warning, when
        /// the device will not give one, and the glass then burns with nobody crossing it.</summary>
        private Texture2D Silhouette()
        {
            if (_silhouette != null || _silhouetteFailed) return _silhouette;
            try
            {
                int w = SceneWindow.SilhouetteWidth, h = SceneWindow.SilhouetteHeight;
                var data = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        data[y * w + x] = SceneWindow.Silhouette[y][x] == '#' ? Color.White : Color.Transparent;
                _silhouette = new Texture2D(Game1.graphics.GraphicsDevice, w, h);
                _silhouette.SetData(data);
            }
            catch (Exception ex)
            {
                _silhouetteFailed = true;
                _silhouette = null;
                _warn?.Invoke($"the window silhouette could not be built, so the windows burn with nobody crossing them ({ex.GetType().Name}: {ex.Message})");
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
            return $"{_panes.Count} window(s) in world pixels {string.Join(" ", said)}, {_lightIds.Count} light(s)";
        }
    }
}
