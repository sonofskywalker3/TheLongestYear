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
    /// witness: the world map in its Winter art, and a dark cloud drifts in over it from the
    /// mountain and mines side and settles sort of everywhere, evenly over the whole map image and
    /// never clustered on the town, a little thicker on the farm, which ends the darkest place on
    /// the map (Jeff, 2026-10-07). The map dims under it, and as the farm settles the low
    /// <c>shadowDie</c> sounds and the board is rewritten. The Community Center gets no special
    /// treatment: the strike is on the things the farmer was saving to donate, not on the hall.
    ///
    /// THE MAP IS THE MAP TAB'S ART, NOT A PICTURE OF IT. The texture and the area overlays (the
    /// farm type, the town's repairs and so on) come from the game's own <c>Data/WorldMap</c>
    /// through <see cref="WorldMapManager"/>, chosen the way <c>MapPage.drawMap</c> chooses them,
    /// always in their Winter art. The real menu is never opened. The FIT is not the map tab's: the
    /// map tab draws the art at four times its size times the UI scale, which left it tiny on a
    /// large screen (Jeff, 2026-10-07), so the scene scales the map to fill the screen instead
    /// (<see cref="SceneCloud.Fit"/>), black round it, and the cloud and the farm scale with it.
    ///
    /// The scene paints in the WORLD layer (<see cref="StrikeSceneBase.Paint"/>), so the base's fade
    /// lands over it, and fits the map to that layer's size (<c>Game1.viewport</c>, the screen over
    /// the zoom).
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. No Valley region, or no farm position on it, calls the
    /// scene off and the tamper lands with no scene, which is the popup and the Junimos' porch scene
    /// in the morning exactly as before.</summary>
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

        public CloudScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        // ---------------------------------------------------------------- staging

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
                Monitor.Log("Darkness: the world map has no position for the farm, so the tamper lands with no scene.", LogLevel.Info);
                return false;
            }
            if (!TakeTextures(region))
            {
                Monitor.Log($"Darkness: the world map region '{region.Id}' has no base texture to draw, so the tamper lands with no scene.", LogLevel.Info);
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

            _cloud = SceneCloud.Plan(SceneCloud.BlobCount, _mapWidth, _mapHeight, _farm.X, _farm.Y, _farm.Width, _farm.Height, new Random(CloudSeed));
            _blob = BuildBlob();

            Monitor.Log(
                $"Darkness: the cloud is staged on world map region '{region.Id}', {_mapWidth}x{_mapHeight} map pixels, "
                + $"base {_baseTexture.Name} plus {_overlays.Count} overlay(s), the farm at ({_farm.X},{_farm.Y} {_farm.Width}x{_farm.Height}), "
                + $"{_cloud.Count} blob(s), {SceneCloud.FarmCount(_cloud.Count)} of them over the farm, blob texture {(_blob == null ? "MISSING (no cloud will draw)" : "built")}.",
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
            (int x, int y, int w, int h) = SceneCloud.ArtToMap(art.X, art.Y, art.Width, art.Height, MapArtScale);
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

        /// <inheritdoc />
        protected override void Paint(SpriteBatch b)
        {
            if (Game1.fadeToBlackRect == null) return;
            b.Draw(Game1.fadeToBlackRect, WholeScreen(), Color.Black);

            // The world layer is the screen over the zoom, which is the size of Game1.viewport.
            (double scale, int ox, int oy) = SceneCloud.Fit(Game1.viewport.Width, Game1.viewport.Height, _mapWidth, _mapHeight, MapArtScale);
            Rectangle ToPaint(Rectangle r)
            {
                (int x, int y, int w, int h) = SceneCloud.MapToPaint(r.X, r.Y, r.Width, r.Height, ox, oy, scale);
                return new Rectangle(x, y, w, h);
            }

            b.Draw(_baseTexture, ToPaint(_baseArea), _baseSource, Color.White);
            foreach ((Texture2D texture, Rectangle source, Rectangle area) in _overlays)
                b.Draw(texture, ToPaint(area), source, Color.White);

            int elapsed = ElapsedMs;
            Rectangle map = ToPaint(new Rectangle(0, 0, _mapWidth, _mapHeight));
            b.Draw(Game1.fadeToBlackRect, map, Color.Black * SceneCloud.Dim(elapsed));

            if (_blob == null) return;
            foreach (SceneCloud.Blob blob in _cloud)
            {
                float alpha = SceneCloud.Alpha(blob, elapsed);
                if (alpha <= 0f) continue;
                (double x, double y) = SceneCloud.Position(blob, elapsed);
                int half = (int)(blob.Diameter / 2);
                Rectangle at = ToPaint(new Rectangle((int)x - half, (int)y - half, (int)blob.Diameter, (int)blob.Diameter));
                b.Draw(_blob, at, CloudTint * alpha);
            }
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
