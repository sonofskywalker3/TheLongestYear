using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.WorldMaps;
using StardewValley.WorldMaps;
using TheLongestYear.Core.Sabotage;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The tampering scene (spec 2026-09-21, Scene 4). About twelve seconds, no text, no
    /// witness: the world map in its Winter art, framed the way the map tab frames it, and a dark
    /// cloud that drifts in from above the top of the screen and settles sort of everywhere, evenly
    /// over the whole screen and never clustered on the town, a little thicker on the farm, which
    /// ends the darkest place on the map (Jeff, 2026-10-07). Everything dims under it, and as the
    /// farm settles the low <c>shadowDie</c> sounds and the board is rewritten. The Community Center
    /// gets no special treatment: the strike is on the things the farmer was saving to donate, not
    /// on the hall.
    ///
    /// THE MAP IS THE MAP TAB'S ART, NOT A PICTURE OF IT. The texture and the area overlays (the
    /// farm type, the town's repairs and so on) come from the game's own <c>Data/WorldMap</c>
    /// through <see cref="WorldMapManager"/>, chosen the way <c>MapPage.drawMap</c> chooses them,
    /// always in their Winter art. The real menu is never opened. It is drawn at the map tab's own
    /// size inside the map tab's own frame (<see cref="SceneMapFit.MapTab"/>, Jeff 2026-10-07: the
    /// map blown up to fill the screen "looks bad"), on a dark night backdrop. The filled version
    /// comes back by setting <see cref="FillScreen"/>.
    ///
    /// The scene paints in the WORLD layer (<see cref="StrikeSceneBase.Paint"/>), so the base's fade
    /// lands over it, and fits the map to that layer's size (<c>Game1.viewport</c>, the screen over
    /// the zoom).
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. No Valley region, or no farm position on it, calls the
    /// scene off, and the tamper lands without it (Jeff, 2026-10-08).</summary>
    internal sealed class CloudScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms

        // Jeff, 2026-10-07: the cloud drifts in slowly rather than darting, so the scene runs
        // longer than the first cut's 9.7 seconds. The blob beats themselves live in SceneCloud.

        private const int FadeInMs = 800;
        /// <summary>The farm's blobs are at rest, the dim is at full, the low <c>shadowDie</c>
        /// sounds and the board is rewritten, all on one beat (<see cref="SceneCloud.SettledAtMs"/>).</summary>
        private const int SettledAtMs = SceneCloud.SettledAtMs;
        /// <summary>The hold on the settled cloud before the fade.</summary>
        private const int HoldMs = 2000;
        private const int FadeOutAtMs = SettledAtMs + HoldMs;
        private const int FadeOutLengthMs = 1200;
        private const int SceneEndMs = FadeOutAtMs + FadeOutLengthMs;

        /// <summary>The low <c>shadowDie</c>, the same pitch the hall's hold uses.</summary>
        private const string SettleSound = "shadowDie";
        private const int SettlePitch = -900;

        // ---------------------------------------------------------------- the look

        /// <summary>The cloud's colour: a near-black violet, the darkness's own.</summary>
        private static readonly Color CloudTint = new Color(20, 0, 30);

        /// <summary>The backdrop round the frame: a very dark night blue rather than pure black, so
        /// the cloud rolling over it, which covers the whole screen, can still be seen.</summary>
        private static readonly Color NightBackdrop = new Color(14, 16, 32);

        /// <summary>False (the default since 2026-10-07): the map at the map tab's size in the map
        /// tab's frame. True: the earlier map scaled to fill the screen, no frame. One switch, so the
        /// designer can have either.</summary>
        private static readonly bool FillScreen = false;

        /// <summary>The map tab's frame is <c>Game1.menuTexture</c>'s dialogue box: these source
        /// rectangles, read off <c>Game1.drawDialogueBox</c> in the 1.6 decompile.</summary>
        private static readonly Rectangle FrameFill = new Rectangle(64, 128, 64, 64);
        private static readonly Rectangle FrameTopLeft = new Rectangle(0, 0, 64, 64);
        private static readonly Rectangle FrameTopRight = new Rectangle(192, 0, 64, 64);
        private static readonly Rectangle FrameBottomLeft = new Rectangle(0, 192, 64, 64);
        private static readonly Rectangle FrameBottomRight = new Rectangle(192, 192, 64, 64);
        private static readonly Rectangle FrameTop = new Rectangle(128, 0, 64, 64);
        private static readonly Rectangle FrameBottom = new Rectangle(128, 192, 64, 64);
        private static readonly Rectangle FrameLeft = new Rectangle(0, 128, 64, 64);
        private static readonly Rectangle FrameRight = new Rectangle(192, 128, 64, 64);

        /// <summary>Map pixels per world map art pixel (<c>MapRegion</c> multiplies every rectangle
        /// by four). The cloud is planned in map pixels; the screen fit is applied when painting.</summary>
        private const int MapArtScale = 4;

        /// <summary>The soft blob texture is this many pixels square. Stretched over a blob a couple
        /// of hundred pixels across, a texel comes out near the map art's own four-pixel grain.</summary>
        private const int BlobPixels = 64;

        /// <summary>How much the blob's edge is roughened, so it reads as cloud and not as a disc.</summary>
        private const float BlobGrain = 0.10f;

        /// <summary>The suffix the game puts on a map texture for its seasonal art
        /// (<c>LooseSprites/map_winter</c>).</summary>
        private const string WinterSuffix = "_winter";

        /// <summary>The overlay condition for a custom farm's own map art, and the width under which
        /// the game takes that art's whole texture as the source (<c>MapArea.GetTextures</c>).</summary>
        private const string CustomFarmCondition = "IS_CUSTOM_FARM_TYPE";
        private const int CustomFarmWholeTextureWidth = 200;

        /// <summary>The seed for the cloud's shape. Fixed, so every player sees the same cloud and a
        /// screenshot can be compared against the last one.</summary>
        private const int CloudSeed = 20260921;

        // ---------------------------------------------------------------- state

        private Texture2D _baseTexture;
        private Rectangle _baseSource;
        private Rectangle _baseArea;
        private readonly List<(Texture2D Texture, Rectangle Source, Rectangle Area)> _overlays = new();
        /// <summary>The map's size and the farm's rectangle, in map pixels at the map tab's scale.</summary>
        private int _mapWidth, _mapHeight;
        private Rectangle _farm;
        private IReadOnlyList<SceneCloud.Blob> _cloud;
        private Texture2D _blob;
        /// <summary>The fit the cloud was planned against: the screen size it was planned for, the
        /// scale from map pixels to paint pixels, and where the map's corner sits.</summary>
        private int _viewWidth, _viewHeight, _originX, _originY;
        private double _scale;

        public CloudScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        // ---------------------------------------------------------------- staging

        /// <summary>Can the cloud stage tonight at all? The farm has a position on a world map region
        /// with a base texture whose condition holds: the two checks <see cref="Stage"/> calls the
        /// scene off on, asked at pick time so a tamper whose scene cannot stage lands without it
        /// (Jeff, 2026-10-08) instead of failing in setUp.</summary>
        internal static bool CanStage()
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return false;
            WorldMapManager.ReloadData();
            MapRegion region = WorldMapManager.GetPositionData(farm, Point.Zero)?.Data?.Region;
            if (region?.Data?.BaseTexture == null) return false;
            foreach (WorldMapTextureData entry in region.Data.BaseTexture)
                if (GameStateQuery.CheckConditions(entry.Condition)) return true;
            return false;
        }

        /// <inheritdoc />
        protected override bool Stage()
        {
            Farm farm = Game1.getFarm();
            WorldMapManager.ReloadData();
            // The live 1.6 build wraps the position with its context; the PC decompile predates that.
            MapAreaPosition farmOnMap = farm == null ? null : WorldMapManager.GetPositionData(farm, Point.Zero)?.Data;
            MapRegion region = farmOnMap?.Region;
            if (region == null)
            {
                Monitor.Log("Darkness: the world map has no position for the farm, so the strike lands without its scene.", LogLevel.Info);
                return false;
            }
            if (!TakeTextures(region))
            {
                Monitor.Log($"Darkness: the world map region '{region.Id}' has no base texture to draw, so the strike lands without its scene.", LogLevel.Info);
                return false;
            }

            // The map's size the way GetMapPixelBounds measures it: the biggest of the base and the
            // overlays, in map pixels at the map tab's scale.
            _mapWidth = _baseArea.Width;
            _mapHeight = _baseArea.Height;
            foreach ((_, _, Rectangle area) in _overlays)
            {
                _mapWidth = Math.Max(_mapWidth, area.Width);
                _mapHeight = Math.Max(_mapHeight, area.Height);
            }
            // The farm's whole area on the map, not the single point its position may pin.
            _farm = ArtToMap(farmOnMap.Area.Data.PixelArea);
            if (_farm.Width <= 0 || _farm.Height <= 0)
                _farm = farmOnMap.GetPixelArea();

            FitToScreen();
            (double sx, double sy, double sw, double sh) = SceneMapFit.ScreenInMap(_viewWidth, _viewHeight, _originX, _originY, _scale);
            _cloud = SceneCloud.Plan(_mapWidth, _mapHeight, _farm.X, _farm.Y, _farm.Width, _farm.Height, sx, sy, sw, sh, new Random(CloudSeed));
            _blob = BuildBlob();

            int onFarm = 0;
            foreach (SceneCloud.Blob blob in _cloud)
                if (blob.OnFarm) onFarm++;
            Monitor.Log(
                $"Darkness: the cloud is staged on world map region '{region.Id}', {_mapWidth}x{_mapHeight} map pixels, "
                + $"base {_baseTexture.Name} plus {_overlays.Count} overlay(s), the farm at ({_farm.X},{_farm.Y} {_farm.Width}x{_farm.Height}), "
                + $"{(FillScreen ? "filled to the screen" : "framed at the map tab's size")} on a {_viewWidth}x{_viewHeight} screen at {_scale:0.###} paint px per map px, map corner ({_originX},{_originY}), "
                + $"{_cloud.Count} blob(s), {onFarm} of them over the farm, blob texture {(_blob == null ? "MISSING (no cloud will draw)" : "built")}.",
                LogLevel.Trace);
            return true;
        }

        /// <summary>The base texture and the area overlays, chosen exactly as <c>MapRegion.GetBaseTexture</c>
        /// and <c>MapArea.GetTextures</c> choose them (the first base entry whose condition holds,
        /// every overlay whose condition holds, the custom farm's own art), except for the season:
        /// the game loads <c>name_season</c> when that asset exists, and the scene always asks for
        /// <c>name_winter</c>. False when the region has no base texture to draw.</summary>
        private bool TakeTextures(MapRegion region)
        {
            foreach (WorldMapTextureData entry in region.Data.BaseTexture)
            {
                if (!GameStateQuery.CheckConditions(entry.Condition)) continue;
                Texture2D texture = WinterTexture(entry.Texture);
                Rectangle source = entry.SourceRect.IsEmpty ? texture.Bounds : entry.SourceRect;
                Rectangle art = entry.MapPixelArea.IsEmpty ? source : entry.MapPixelArea;
                _baseTexture = texture;
                _baseSource = source;
                _baseArea = ArtToMap(art);
                break;
            }
            if (_baseTexture == null) return false;

            foreach (MapArea area in region.GetAreas())
            {
                foreach (WorldMapTextureData entry in area.Data.Textures)
                {
                    if (!GameStateQuery.CheckConditions(entry.Condition)) continue;
                    Texture2D texture;
                    Rectangle source = entry.SourceRect;
                    if (entry.Condition == CustomFarmCondition)
                    {
                        string custom = Game1.whichModFarm?.WorldMapTexture;
                        if (custom == null) continue;
                        texture = WinterTexture(custom);
                        if (texture.Width <= CustomFarmWholeTextureWidth) source = texture.Bounds;
                    }
                    else
                    {
                        texture = WinterTexture(entry.Texture);
                    }
                    if (source.IsEmpty) source = texture.Bounds;
                    Rectangle art = entry.MapPixelArea.IsEmpty ? area.Data.PixelArea : entry.MapPixelArea;
                    _overlays.Add((texture, source, ArtToMap(art)));
                }
            }
            return true;
        }

        /// <summary>The Winter version of a map texture when the game has one, else the texture
        /// itself, the same suffix rule as <c>MapRegion.GetTexture</c>.</summary>
        private static Texture2D WinterTexture(string assetName)
        {
            string winter = assetName + WinterSuffix;
            return Game1.content.DoesAssetExist<Texture2D>(winter)
                ? Game1.content.Load<Texture2D>(winter)
                : Game1.content.Load<Texture2D>(assetName);
        }

        private static Rectangle ArtToMap(Rectangle art)
        {
            (int x, int y, int w, int h) = SceneMapFit.ArtToMap(art.X, art.Y, art.Width, art.Height, MapArtScale);
            return new Rectangle(x, y, w, h);
        }

        /// <summary>One soft blob: alpha falls off from the middle as (1 - r squared) squared, with a fixed
        /// grain on top, so stacked blobs read as cloud. Premultiplied, as the game's batches want.
        /// Null when the device will not make it, and then the map and the dim still play.</summary>
        private Texture2D BuildBlob()
        {
            try
            {
                var pixels = new Color[BlobPixels * BlobPixels];
                float middle = (BlobPixels - 1) / 2f;
                var grain = new Random(CloudSeed);
                for (int y = 0; y < BlobPixels; y++)
                {
                    for (int x = 0; x < BlobPixels; x++)
                    {
                        float dx = (x - middle) / middle, dy = (y - middle) / middle;
                        float r = Math.Min(1f, (float)Math.Sqrt(dx * dx + dy * dy));
                        // A broad, soft profile: dense through the middle, feathered at the edge,
                        // so neighbours overlap into one cloud. A plain (1 - r) squared was
                        // tried and read as a scatter of small dark spots on the first frames.
                        float body = 1f - r * r;
                        float soft = body * body;
                        float rough = 1f - BlobGrain + BlobGrain * 2f * (float)grain.NextDouble();
                        float alpha = Math.Max(0f, Math.Min(1f, soft * rough));
                        pixels[y * BlobPixels + x] = Color.White * alpha;
                    }
                }
                var made = new Texture2D(Game1.graphics.GraphicsDevice, BlobPixels, BlobPixels);
                made.SetData(pixels);
                return made;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: the cloud's blob texture could not be built, so the map plays without it. {ex}", LogLevel.Warn);
                return null;
            }
        }

        // ---------------------------------------------------------------- the beats

        /// <inheritdoc />
        protected override void Build(Timeline t)
        {
            Fade(FadeInMs, FadeOutAtMs, FadeOutLengthMs);
            t.At(SettledAtMs, () =>
            {
                Game1.playSound(SettleSound, SettlePitch);
                ApplyStrike();
            });
            t.EndAt(SceneEndMs);
        }

        // ---------------------------------------------------------------- painting

        /// <summary>Where the map goes on this screen. The world layer is the screen over the zoom,
        /// which is the size of Game1.viewport; one UI pixel there is the UI scale over the zoom.</summary>
        private void FitToScreen()
        {
            _viewWidth = Game1.viewport.Width;
            _viewHeight = Game1.viewport.Height;
            double uiToPaint = Game1.options.uiScale / Game1.options.zoomLevel;
            (_scale, _originX, _originY) = FillScreen
                ? SceneMapFit.Fill(_viewWidth, _viewHeight, _mapWidth, _mapHeight, MapArtScale)
                : SceneMapFit.MapTab(_viewWidth, _viewHeight, _mapWidth, _mapHeight, MapArtScale, uiToPaint);
        }

        /// <inheritdoc />
        protected override void Paint(SpriteBatch b)
        {
            if (Game1.fadeToBlackRect == null) return;
            Rectangle screen = WholeScreen();
            b.Draw(Game1.fadeToBlackRect, screen, FillScreen ? Color.Black : NightBackdrop);

            int ox = _originX, oy = _originY;
            double scale = _scale;
            Rectangle ToPaint(Rectangle r)
            {
                (int x, int y, int w, int h) = SceneMapFit.MapToPaint(r.X, r.Y, r.Width, r.Height, ox, oy, scale);
                return new Rectangle(x, y, w, h);
            }

            Rectangle map = ToPaint(new Rectangle(0, 0, _mapWidth, _mapHeight));
            if (!FillScreen) PaintFrame(b, map);
            b.Draw(_baseTexture, ToPaint(_baseArea), _baseSource, Color.White);
            foreach ((Texture2D texture, Rectangle source, Rectangle area) in _overlays)
                b.Draw(texture, ToPaint(area), source, Color.White);

            int elapsed = ElapsedMs;
            b.Draw(Game1.fadeToBlackRect, screen, Color.Black * SceneCloud.Dim(elapsed));

            // The cloud covers the whole screen, frame and backdrop included, and comes in from
            // above its top edge, so nothing is clipped.
            if (_blob == null) return;
            // Indexed: foreach over an IReadOnlyList boxes its enumerator, once a frame.
            for (int i = 0; i < _cloud.Count; i++)
            {
                SceneCloud.Blob blob = _cloud[i];
                float alpha = SceneCloud.Alpha(blob, elapsed);
                if (alpha <= 0f) continue;
                (double x, double y) = SceneCloud.Position(blob, elapsed);
                int half = (int)(blob.Diameter / 2);
                Rectangle at = ToPaint(new Rectangle((int)x - half, (int)y - half, (int)blob.Diameter, (int)blob.Diameter));
                b.Draw(_blob, at, CloudTint * alpha);
            }
        }

        /// <summary>The map tab's frame round the map: the dialogue box <c>MapPage.drawMap</c> asks
        /// <c>Game1.drawDialogueBox</c> for, drawn here piece by piece rather than through that
        /// method, which reads the dialogue system's global state (question choices, the current
        /// speaker) and only knows UI pixels. Same texture, same pieces, same place: 32 UI pixels
        /// outside the map on every side, corners and edges 64 UI pixels thick.</summary>
        private static void PaintFrame(SpriteBatch b, Rectangle map)
        {
            Texture2D menu = Game1.menuTexture;
            if (menu == null) return;
            double uiToPaint = Game1.options.uiScale / Game1.options.zoomLevel;
            (int fx, int fy, int fw, int fh, int piece) = SceneMapFit.Frame(map.X, map.Y, map.Width, map.Height, uiToPaint);
            int inset = (int)Math.Round(SceneMapFit.FrameFillInset * uiToPaint);
            b.Draw(menu, new Rectangle(fx + inset, fy + inset, fw - 2 * inset, fh - 2 * inset), FrameFill, Color.White);
            b.Draw(menu, new Rectangle(fx + piece, fy, fw - 2 * piece, piece), FrameTop, Color.White);
            b.Draw(menu, new Rectangle(fx + piece, fy + fh - piece, fw - 2 * piece, piece), FrameBottom, Color.White);
            b.Draw(menu, new Rectangle(fx, fy + piece, piece, fh - 2 * piece), FrameLeft, Color.White);
            b.Draw(menu, new Rectangle(fx + fw - piece, fy + piece, piece, fh - 2 * piece), FrameRight, Color.White);
            b.Draw(menu, new Rectangle(fx, fy, piece, piece), FrameTopLeft, Color.White);
            b.Draw(menu, new Rectangle(fx + fw - piece, fy, piece, piece), FrameTopRight, Color.White);
            b.Draw(menu, new Rectangle(fx, fy + fh - piece, piece, piece), FrameBottomLeft, Color.White);
            b.Draw(menu, new Rectangle(fx + fw - piece, fy + fh - piece, piece, piece), FrameBottomRight, Color.White);
        }

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup()
        {
            Texture2D blob = _blob;
            _blob = null;
            blob?.Dispose();
        }
    }
}
