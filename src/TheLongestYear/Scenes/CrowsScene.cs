using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core.Sabotage;
using TheLongestYear.Loop;
using SObject = StardewValley.Object;

namespace TheLongestYear.Scenes
{
    /// <summary>The crop blight scene (spec 2026-09-21). Eight seconds on the night farm, no text and
    /// no dialogue: crows with red eyes drop onto the exact crops that are about to die, one settles
    /// beside a scarecrow and is not troubled by it, Linus wanders in, sees them, backs off and
    /// hurries away, the crows peck, the crops die on that beat, the crows lift off, black.
    ///
    /// WHAT IS REAL AND WHAT IS PAINTED. The crops are real: the scene never draws one. The world
    /// keeps drawing underneath for the whole scene (<c>farmEvent.draw</c> is called after the map,
    /// the terrain features and the lightmap, Game1.cs:13697), which is why the withered sprites are
    /// simply there once <see cref="StrikeSceneBase.ApplyStrike"/> has run. The crows and Linus are
    /// painted by this class from their own sheets and own nothing in the world. Linus in particular
    /// is a <see cref="SceneActor"/> and never the villager: see that class for why.
    ///
    /// WHY IT PAINTS ABOVE THE LIGHT. Everything a farm event draws lands after the lightmap has been
    /// subtracted, so the crows read at full brightness against a dark field. That is how the witch
    /// and the fairy are drawn too, and it is the only reason a black bird is visible at 2am.
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. Every stage decision degrades instead of failing: no
    /// scarecrow in range simply means no scarecrow crow, and nowhere clear for Linus to stand means
    /// no Linus (the scene still counts as played, which is all the later witness line asks of it).
    /// Only an empty crop pick calls the scene off altogether.</summary>
    internal sealed class CrowsScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms

        private const int FadeInMs = 600;
        private const int CrowsEnterMs = 600;
        private const int CrowGapMs = 120;
        /// <summary>How long a crow is in the air on its way down. The spec asks for 3.5 tiles a
        /// second; a crow entering above the frame has ten or more tiles to fall, which would still
        /// have it gliding when Linus arrives, so the glide is a fixed length instead and the speed
        /// falls out of it (about four tiles a second on a normal window).</summary>
        private const int GlideMs = 1600;
        private const int ScarecrowCrowLandMs = 2200;
        private const int LinusEnterMs = 2600;
        private const int LinusSeesMs = 3600;
        private const int LinusShakeMs = 300;
        private const int LinusStepsBackMs = 4200;
        private const int LinusStepBackLengthMs = 400;
        private const int PeckMs = 4600;
        private const int PeckLengthMs = 250;
        private const int Pecks = 3;
        private const int StrikeMs = 5400;
        private const int LinusLeavesMs = 5600;
        private const int LiftOffMs = 6400;
        private const int FadeOutMs = 7200;
        private const int FadeOutLengthMs = 800;
        private const int SceneEndMs = 8000;

        // ---------------------------------------------------------------- staging

        /// <summary>A crop tile joins the patch when it is this near the tile that opened it.</summary>
        private const int ClusterJoinTiles = 6;
        /// <summary>How many crops get a crow of their own. The rest of the pick dies off screen.</summary>
        private const int MaxCrows = 6;
        /// <summary>The fewest crops the debug preview asks for, so every crow has a crop even when
        /// tonight's real count would be smaller.</summary>
        public const int PreviewCrops = MaxCrows;
        /// <summary>How far from the patch a scarecrow may be and still share the frame.</summary>
        private const int ScarecrowReachTiles = 12;
        /// <summary>How far up or down the frame edge Linus's path may be shifted to find clear
        /// ground before the scene gives up on him.</summary>
        private const int LinusRowSearch = 8;
        private const int LinusWalkTiles = 2;

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

        private const int TileSize = 64;
        private const float DrawScale = 4f;
        /// <summary>A crow's drawn sprite is four tiles across and hangs two tiles above its feet,
        /// the offset <c>Critter.draw</c> uses (Critter.cs:69).</summary>
        private static readonly Vector2 CrowDrawOffset = new Vector2(-64f, -128f);

        private const string EyeLightIdPrefix = "TLY.CrowEye.";
        /// <summary>The lightmap is subtracted from the world, so a light's colour is the colour it
        /// REMOVES. Taking green and blue away leaves red, which is why a red glow is cyan here.</summary>
        private static readonly Color EyeLight = Color.Cyan;
        private const float EyeLightRadius = 0.3f;
        /// <summary>The eye pixel itself, in sheet pixels, drawn on top of the bird.</summary>
        private const int EyeDotPixels = 2;

        /// <summary>Where the eye sits inside each crow frame, found once by reading the sheet.</summary>
        private static readonly Dictionary<int, Point> EyeOffsets = new();

        // ---------------------------------------------------------------- state

        private sealed class SceneCrow
        {
            public AnimatedSprite Sprite;
            /// <summary>The tile it comes down on.</summary>
            public Vector2 Tile;
            /// <summary>Feet, in world pixels, where it lands.</summary>
            public Vector2 Landing;
            public Vector2 Start;
            public int EnterAtMs;
            public int LandAtMs;
            public bool Flip;
            public Vector2 Position;
            public int Frame = CrowStandFrame;
            public bool Visible;
            /// <summary>On the ground: between its landing and the lift-off.</summary>
            public bool Perched;
            public string LightId;
        }

        private Farm _farm;
        private Vector2 _focus;
        private readonly List<SceneCrow> _crows = new();
        private SceneActor _linus;
        private Vector2 _linusEntry;
        private int _linusInward;
        private bool _lightsOn;

        public CrowsScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        // ---------------------------------------------------------------- staging

        /// <inheritdoc />
        protected override bool Stage()
        {
            _farm = Game1.getFarm();
            if (_farm == null || Strike.CropTiles == null || Strike.CropTiles.Count == 0) return false;

            var tiles = new List<(int X, int Y)>();
            foreach (Vector2 tile in Strike.CropTiles) tiles.Add(((int)tile.X, (int)tile.Y));
            IReadOnlyList<(int X, int Y)> patch = CropCluster.Largest(tiles, ClusterJoinTiles);
            if (patch.Count == 0) return false;
            (int X, int Y) centre = CropCluster.Centroid(patch);
            var centroid = new Vector2(centre.X, centre.Y);

            Vector2? scarecrow = NearestScarecrow(centroid);
            _focus = scarecrow.HasValue
                ? new Vector2((float)Math.Round((centroid.X + scarecrow.Value.X) / 2f), (float)Math.Round((centroid.Y + scarecrow.Value.Y) / 2f))
                : centroid;

            SceneCamera.CutTo(_farm, _focus);

            foreach ((int X, int Y) tile in CropCluster.Nearest(patch, centre, MaxCrows))
                _crows.Add(MakeCrow(new Vector2(tile.X, tile.Y), CrowsEnterMs + _crows.Count * CrowGapMs));
            if (scarecrow.HasValue)
            {
                SceneCrow perched = MakeCrow(TileBesideScarecrow(scarecrow.Value, centroid), ScarecrowCrowLandMs - GlideMs);
                perched.Flip = scarecrow.Value.X > perched.Tile.X;
                _crows.Add(perched);
            }
            if (_crows.Count == 0) return false;

            StageLinus();
            Monitor.Log(
                $"Darkness: the crows are staged on {_crows.Count} bird(s) at ({_focus.X},{_focus.Y}); "
                + $"scarecrow {(scarecrow.HasValue ? "in frame" : "none in range")}, Linus {(_linus != null ? "walking in" : "left out, nowhere clear to stand")}.",
                LogLevel.Trace);
            return true;
        }

        private SceneCrow MakeCrow(Vector2 tile, int enterAtMs)
        {
            Vector2 landing = SceneCamera.TileCentre(tile);
            var crow = new SceneCrow
            {
                Sprite = new AnimatedSprite(CritterSheet, CrowBaseFrame, CrowSpriteSize, CrowSpriteSize),
                Tile = tile,
                Landing = landing,
                Start = new Vector2(landing.X, Game1.viewport.Y - TileSize * 2),
                EnterAtMs = Math.Max(0, enterAtMs),
                Flip = tile.X < _focus.X,
                LightId = EyeLightIdPrefix + Guid.NewGuid().ToString("N"),
            };
            crow.LandAtMs = crow.EnterAtMs + GlideMs;
            crow.Position = crow.Start;
            return crow;
        }

        /// <summary>The nearest thing on the farm that scares crows, within reach of the patch.
        /// Null when there is none, and then the scene simply has no scarecrow crow.</summary>
        private Vector2? NearestScarecrow(Vector2 centroid)
        {
            Vector2? best = null;
            float bestDistance = float.MaxValue;
            foreach (KeyValuePair<Vector2, SObject> pair in _farm.objects.Pairs)
            {
                if (pair.Value == null || !pair.Value.IsScarecrow()) continue;
                float distance = Vector2.Distance(pair.Key, centroid);
                if (distance > ScarecrowReachTiles || distance >= bestDistance) continue;
                bestDistance = distance;
                best = pair.Key;
            }
            return best;
        }

        /// <summary>The tile a crow perches on beside the scarecrow: the one on the crops' side of
        /// it, so both are in the same half of the frame.</summary>
        private static Vector2 TileBesideScarecrow(Vector2 scarecrow, Vector2 centroid)
        {
            int step = centroid.X >= scarecrow.X ? 1 : -1;
            return new Vector2(scarecrow.X + step, scarecrow.Y);
        }

        /// <summary>Find Linus a clear line to walk. He comes in from whichever side of the frame is
        /// nearer the edge of the map, on the patch's row if that row is clear, and the search shifts
        /// the row up and down the frame edge until three clear tiles in a line turn up. Nothing
        /// clear inside the frame leaves him out: a villager walking through a fence is worse than no
        /// villager at all.</summary>
        private void StageLinus()
        {
            Rectangle frame = SceneCamera.FrameInTiles();
            if (frame.Width < LinusWalkTiles + 3 || frame.Height < 3) return;
            int mapWidth = _farm.map.Layers[0].LayerWidth;
            bool leftFirst = frame.Left <= mapWidth - frame.Right;
            foreach (bool fromLeft in leftFirst ? new[] { true, false } : new[] { false, true })
            {
                int inward = fromLeft ? 1 : -1;
                int entryX = fromLeft ? frame.Left + 1 : frame.Right - 2;
                for (int shift = 0; shift <= LinusRowSearch; shift++)
                {
                    foreach (int row in shift == 0 ? new[] { (int)_focus.Y } : new[] { (int)_focus.Y + shift, (int)_focus.Y - shift })
                    {
                        if (row <= frame.Top || row >= frame.Bottom - 1) continue;
                        if (!LineIsClear(entryX, row, inward)) continue;
                        _linusEntry = new Vector2(entryX, row);
                        _linusInward = inward;
                        try
                        {
                            _linus = new SceneActor("Characters\\Linus");
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Darkness: the crows scene could not load Linus, so it plays without him. {ex}", LogLevel.Warn);
                            return;
                        }
                        _linus.Position = _linusEntry * TileSize;
                        _linus.Facing = fromLeft ? SceneActor.FacingRight : SceneActor.FacingLeft;
                        return;
                    }
                }
            }
        }

        private bool LineIsClear(int entryX, int row, int inward)
        {
            for (int step = 0; step <= LinusWalkTiles; step++)
                if (!ClearToStandOn(entryX + step * inward, row)) return false;
            return true;
        }

        /// <summary>Somewhere a person could plausibly be standing: on the map, walkable, dry, and
        /// with nothing in the way. Grass and laid flooring are fine to walk on; every other terrain
        /// feature is refused, which is what keeps him off the crops, and off the ones about to die
        /// in particular.</summary>
        private bool ClearToStandOn(int x, int y)
        {
            var tile = new Vector2(x, y);
            if (!_farm.isTileOnMap(tile)) return false;
            if (_farm.terrainFeatures.TryGetValue(tile, out StardewValley.TerrainFeatures.TerrainFeature feature)
                && !(feature is StardewValley.TerrainFeatures.Grass)
                && !(feature is StardewValley.TerrainFeatures.Flooring))
                return false;
            if (_farm.objects.ContainsKey(tile)) return false;
            if (_farm.isWaterTile(x, y)) return false;
            if (_farm.getBuildingAt(tile) != null) return false;
            foreach (SceneCrow crow in _crows)
                if ((int)crow.Tile.X == x && (int)crow.Tile.Y == y) return false;
            return _farm.isTilePassable(new xTile.Dimensions.Location(x, y), Game1.viewport);
        }

        // ---------------------------------------------------------------- the beats

        /// <inheritdoc />
        protected override void Build(Timeline t)
        {
            t.At(CrowsEnterMs, () => Game1.playSound("crow", -600));
            t.At(PeckMs, LightTheEyes);
            t.At(StrikeMs, ApplyStrike);
            t.At(LiftOffMs, DouseTheEyes);
            t.EndAt(SceneEndMs);
        }

        /// <summary>The eyes catch at the peck, one small red pool a crow. They are lights rather
        /// than only painted dots so the field around each bird reddens as well.</summary>
        private void LightTheEyes()
        {
            _lightsOn = true;
            foreach (SceneCrow crow in _crows)
                Game1.currentLightSources[crow.LightId] =
                    new LightSource(crow.LightId, LightSource.sconceLight, crow.Landing + new Vector2(0f, -48f), EyeLightRadius, EyeLight);
        }

        private void DouseTheEyes()
        {
            _lightsOn = false;
            foreach (SceneCrow crow in _crows) Game1.currentLightSources.Remove(crow.LightId);
        }

        /// <inheritdoc />
        public override bool tickUpdate(GameTime time)
        {
            // Nothing in the world updates itself during a farm event: vanilla's own night events
            // pump the clock, the location and the rest by hand (WitchEvent.tickUpdate). The debug
            // command plays the scene during an ordinary update, where the game is already doing all
            // of this, so the pump only runs when this really is tonight's farm event.
            if (ReferenceEquals(Game1.farmEvent, this))
            {
                try
                {
                    Game1.UpdateGameClock(time);
                    _farm.UpdateWhenCurrentLocation(time);
                    _farm.updateEvenIfFarmerIsntHere(time);
                    Game1.UpdateOther(time);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Darkness: the crows scene could not pump the farm this tick. {ex}", LogLevel.Trace);
                }
            }
            bool done = base.tickUpdate(time);
            if (!done) MoveEverything(ElapsedMs);
            return done;
        }

        private void MoveEverything(int elapsed)
        {
            foreach (SceneCrow crow in _crows) MoveCrow(crow, elapsed);
            MoveLinus(elapsed);
        }

        private static void MoveCrow(SceneCrow crow, int elapsed)
        {
            crow.Visible = elapsed >= crow.EnterAtMs;
            crow.Perched = elapsed >= crow.LandAtMs && elapsed < LiftOffMs;
            if (elapsed < crow.EnterAtMs)
            {
                crow.Position = crow.Start;
                crow.Frame = FlapFrame(elapsed);
            }
            else if (elapsed < crow.LandAtMs)
            {
                float travelled = (elapsed - crow.EnterAtMs) / (float)GlideMs;
                crow.Position = Vector2.Lerp(crow.Start, crow.Landing, travelled);
                crow.Frame = FlapFrame(elapsed);
            }
            else if (elapsed < LiftOffMs)
            {
                crow.Position = crow.Landing;
                crow.Frame = PeckFrame(elapsed);
            }
            else
            {
                float risen = (elapsed - LiftOffMs) * 0.6f;
                crow.Position = crow.Landing + new Vector2((crow.Flip ? 1f : -1f) * risen * 0.35f, -risen);
                crow.Frame = FlapFrame(elapsed);
            }
        }

        private static int FlapFrame(int elapsed) => CrowFlapFirstFrame + Math.Abs(elapsed / CrowFlapMs) % CrowFlapFrames;

        private static int PeckFrame(int elapsed)
        {
            int since = elapsed - PeckMs;
            if (since < 0 || since >= Pecks * PeckLengthMs) return CrowStandFrame;
            return since % PeckLengthMs < PeckLengthMs / 2 ? CrowPeckDownFrame : CrowPeckStrikeFrame;
        }

        private void MoveLinus(int elapsed)
        {
            if (_linus == null || elapsed < LinusEnterMs) return;
            float tilesIn;
            if (elapsed < LinusSeesMs)
            {
                tilesIn = LinusWalkTiles * ((elapsed - LinusEnterMs) / (float)(LinusSeesMs - LinusEnterMs));
                _linus.Walking = true;
            }
            else if (elapsed < LinusStepsBackMs)
            {
                tilesIn = LinusWalkTiles;
                _linus.Walking = false;
            }
            else if (elapsed < LinusStepsBackMs + LinusStepBackLengthMs)
            {
                tilesIn = LinusWalkTiles - (elapsed - LinusStepsBackMs) / (float)LinusStepBackLengthMs;
                _linus.Walking = true;
            }
            else if (elapsed < LinusLeavesMs)
            {
                tilesIn = LinusWalkTiles - 1;
                _linus.Walking = false;
            }
            else
            {
                // Away at a run: four tiles a second, twice a walk, until he is off the frame.
                tilesIn = LinusWalkTiles - 1 - (elapsed - LinusLeavesMs) * 0.004f;
                _linus.Walking = true;
                _linus.Facing = _linusInward > 0 ? SceneActor.FacingLeft : SceneActor.FacingRight;
            }
            if (elapsed < LinusLeavesMs)
                _linus.Facing = _linusInward > 0 ? SceneActor.FacingRight : SceneActor.FacingLeft;
            _linus.Position = new Vector2((_linusEntry.X + tilesIn * _linusInward) * TileSize, _linusEntry.Y * TileSize);
            // The recoil: a shudder on the spot the moment he takes the birds in.
            bool shaking = elapsed >= LinusSeesMs && elapsed < LinusSeesMs + LinusShakeMs;
            _linus.Shake = shaking ? (elapsed / 50 % 2 == 0 ? -6f : 6f) : 0f;
            _linus.Animate(elapsed);
        }

        // ---------------------------------------------------------------- painting

        /// <inheritdoc />
        protected override void Paint(SpriteBatch b)
        {
            foreach (SceneCrow crow in _crows)
            {
                if (!crow.Visible) continue;
                crow.Sprite.currentFrame = crow.Frame;
                crow.Sprite.UpdateSourceRect();
                Vector2 corner = SceneCamera.ToScreen(crow.Position + CrowDrawOffset);
                if (Game1.shadowTexture != null && crow.Perched)
                {
                    b.Draw(
                        Game1.shadowTexture,
                        SceneCamera.ToScreen(crow.Position + new Vector2(0f, -4f)),
                        Game1.shadowTexture.Bounds,
                        Color.White * 0.5f,
                        0f,
                        new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
                        3f,
                        SpriteEffects.None,
                        0.898f);
                }
                crow.Sprite.draw(b, corner, 0.9f, 0, 0, Color.White, crow.Flip, DrawScale);
                PaintEye(b, crow, corner);
            }
            _linus?.Draw(b, Color.White);
        }

        /// <summary>Two sheet pixels of red where the bird's eye is, with a softer square around it
        /// so the glow reads at normal zoom without hiding the head.</summary>
        private void PaintEye(SpriteBatch b, SceneCrow crow, Vector2 corner)
        {
            Point eye = EyeOffset(crow.Sprite, crow.Frame);
            if (eye.X < 0) return;
            float x = crow.Flip ? (CrowSpriteSize - EyeDotPixels - eye.X) * DrawScale : eye.X * DrawScale;
            var dot = new Rectangle(
                (int)(corner.X + x),
                (int)(corner.Y + eye.Y * DrawScale),
                (int)(EyeDotPixels * DrawScale),
                (int)(EyeDotPixels * DrawScale));
            var glow = new Rectangle(dot.X - dot.Width, dot.Y - dot.Height, dot.Width * 3, dot.Height * 3);
            b.Draw(Game1.staminaRect, glow, Color.Red * 0.35f);
            b.Draw(Game1.staminaRect, dot, Color.Red);
        }

        /// <summary>Where the eye sits in one crow frame, read off the real sheet once: the brightest
        /// opaque pixel in the top half of the frame, which on the crow is the eye. A frame that
        /// cannot be read gets no dot rather than a dot in the wrong place.</summary>
        private static Point EyeOffset(AnimatedSprite sprite, int frame)
        {
            if (EyeOffsets.TryGetValue(frame, out Point cached)) return cached;
            var found = new Point(-1, -1);
            try
            {
                Texture2D sheet = sprite?.Texture;
                if (sheet != null)
                {
                    Rectangle source = AnimatedSprite.GetSourceRect(sheet.Width, CrowSpriteSize, CrowSpriteSize, frame);
                    var pixels = new Color[CrowSpriteSize * CrowSpriteSize];
                    sheet.GetData(0, source, pixels, 0, pixels.Length);
                    int brightest = -1;
                    for (int y = 0; y < CrowSpriteSize / 2; y++)
                    {
                        for (int x = 0; x < CrowSpriteSize; x++)
                        {
                            Color pixel = pixels[y * CrowSpriteSize + x];
                            if (pixel.A < 200) continue;
                            int brightness = pixel.R + pixel.G + pixel.B;
                            if (brightness <= brightest) continue;
                            brightest = brightness;
                            found = new Point(x, y);
                        }
                    }
                }
            }
            catch (Exception)
            {
                found = new Point(-1, -1);
            }
            EyeOffsets[frame] = found;
            return found;
        }

        /// <inheritdoc />
        protected override void PaintAbove(SpriteBatch b)
        {
            float black = BlackAt(ElapsedMs);
            if (black <= 0f) return;
            // drawAboveEverything runs in the zoomed backbuffer, the debug preview draws in UI space:
            // cover whichever is bigger, since over-covering a full-screen black costs nothing.
            Viewport screen = Game1.graphics.GraphicsDevice.Viewport;
            int width = Math.Max(screen.Width, Game1.uiViewport.Width);
            int height = Math.Max(screen.Height, Game1.uiViewport.Height);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * black);
        }

        private static float BlackAt(int elapsed)
        {
            if (elapsed < FadeInMs) return 1f - elapsed / (float)FadeInMs;
            if (elapsed >= FadeOutMs) return Math.Min(1f, (elapsed - FadeOutMs) / (float)FadeOutLengthMs);
            return 0f;
        }

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup()
        {
            if (_lightsOn || _crows.Count > 0) DouseTheEyes();
            SceneCamera.Restore();
        }
    }
}
