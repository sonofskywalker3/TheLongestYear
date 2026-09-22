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
        /// second, but a crow entering above the frame has ten or more tiles to fall and would still
        /// have it gliding when Linus arrives, so the glide is a fixed length instead and the speed
        /// falls out of it (about four tiles a second on a normal window).</summary>
        private const int GlideMs = 1600;
        private const int ScarecrowCrowLandMs = 2200;
        private const int LinusEnterMs = 2600;
        private const int LinusSeesMs = 3600;
        private const int LinusShakeMs = 300;
        /// <summary>How far the recoil shoves him, in SCREEN pixels. The spec says 2 px, and this is
        /// that 2 px measured the way the sprite is: two pixels of the sheet at the game's 4x draw
        /// scale. Two screen pixels is half a sheet pixel and did not show at all.</summary>
        private const float LinusShakePixels = 8f;
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
        /// <summary>How far inside the frame edge he starts. Three and not one: at one tile in he
        /// read as a figure pinned in the corner against the fence line (screenshots, 2026-09-21).</summary>
        private const int LinusEntryInset = 3;

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
            /// <summary>Pixels a millisecond upward once it lifts off, and sideways as it goes.</summary>
            public float RiseRate;
            public float DriftRate;
        }

        /// <summary>Only ever used to make the birds leave unalike. It takes no part in what dies,
        /// which was decided by the strike's own seeded roll long before the scene was built.</summary>
        private readonly Random _spread = new Random();

        private Farm _farm;
        private Vector2 _focus;
        private readonly List<SceneCrow> _crows = new();
        private SceneActor _linus;
        private Vector2 _linusEntry;
        private int _linusInward;
        private bool _linusInFrame;

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
                $"Darkness: the crows are staged on {_crows.Count} bird(s) at ({_focus.X},{_focus.Y}), "
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
            };
            // The scene is silent and every crow leaves on the same beat, so the only thing keeping
            // the lift-off from looking like one object is that no two birds climb alike.
            crow.RiseRate = 0.5f + _spread.Next(0, 5) * 0.05f;
            crow.DriftRate = (tile.X < _focus.X ? -1f : 1f) * (0.2f + _spread.Next(0, 5) * 0.06f);
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
            if (frame.Width < LinusWalkTiles + LinusEntryInset * 2 || frame.Height < 3) return;
            int mapWidth = _farm.map.Layers[0].LayerWidth;
            bool leftFirst = frame.Left <= mapWidth - frame.Right;
            foreach (bool fromLeft in leftFirst ? new[] { true, false } : new[] { false, true })
            {
                int inward = fromLeft ? 1 : -1;
                int entryX = fromLeft ? frame.Left + LinusEntryInset : frame.Right - LinusEntryInset - 1;
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
                        Monitor.Log($"Darkness: the crows scene draws Linus from a sheet of {_linus.Describe()}.", LogLevel.Trace);
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
        /// with nothing in the way. Grass and laid flooring are fine to walk on. Every other terrain
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
            Fade(FadeInMs, FadeOutMs, FadeOutLengthMs);
            t.At(CrowsEnterMs, () => Game1.playSound("crow", -600));
            t.At(StrikeMs, ApplyStrike);
            t.EndAt(SceneEndMs);
        }

        /// <summary>The base pumps this map for the scene on the real overnight path.</summary>
        protected override GameLocation SceneLocation => _farm;

        /// <summary>Moving the birds and Linus runs through the base's hook, not out of the override
        /// above, so a throw here goes down the base's failure path and the camera always comes back.</summary>
        protected override void Advance(int elapsed)
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
                // Each bird has its own rise and its own sideways drift, so they open out instead of
                // going up as one clump.
                float risen = (elapsed - LiftOffMs) * crow.RiseRate;
                crow.Position = crow.Landing + new Vector2(crow.DriftRate * risen, -risen);
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
            if (_linus == null) return;
            // He is not there until he walks in: without this he stands at his entry tile from the
            // scene's first frame, which reads as a bystander who was always watching.
            _linusInFrame = elapsed >= LinusEnterMs;
            if (!_linusInFrame) return;
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
            _linus.Shake = shaking ? (elapsed / 50 % 2 == 0 ? -LinusShakePixels : LinusShakePixels) : 0f;
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
                        SceneCamera.NightTint * 0.5f,
                        0f,
                        new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
                        3f,
                        SpriteEffects.None,
                        0.898f);
                }
                crow.Sprite.draw(b, corner, 0.9f, 0, 0, SceneCamera.NightTint, crow.Flip, DrawScale);
                PaintEye(b, crow, corner);
            }
            if (_linusInFrame) _linus?.Draw(b, SceneCamera.NightTint);
        }

        /// <summary>A glowing red eye: a soft radial pool, then two sheet pixels of solid red in the
        /// middle of it, at the eye's real place in this frame. Neither is tinted by the night, which
        /// is the point of it.</summary>
        private static void PaintEye(SpriteBatch b, SceneCrow crow, Vector2 corner)
        {
            if (!EyeOffsets.TryGetValue(crow.Frame, out Point eye)) return;
            float x = crow.Flip ? (CrowSpriteSize - EyeDotPixels - eye.X) * DrawScale : eye.X * DrawScale;
            float core = EyeDotPixels * DrawScale;
            var centre = new Vector2(corner.X + x + core / 2f, corner.Y + eye.Y * DrawScale + core / 2f);
            SceneGlow.Draw(b, centre, EyeGlowSheetPixels * DrawScale, core, Color.Red);
        }

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup() => SceneCamera.Restore();
    }
}
