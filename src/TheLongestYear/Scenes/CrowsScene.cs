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
    /// no dialogue: crows with red eyes swoop in from both sides of the frame onto the exact crops
    /// that are about to die (designer, 2026-10-08: not straight down; the curve is
    /// <see cref="CrowSwoop"/>), one settles
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
    internal sealed partial class CrowsScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms

        private const int FadeInMs = 600;
        private const int CrowsEnterMs = 600;
        private const int CrowGapMs = 120;
        /// <summary>How long a crow is in the air on its way in. A crow entering off the side of the
        /// frame has ten or more tiles to cover and would still be gliding when Linus arrives at a
        /// fixed speed, so the glide is a fixed length instead and the speed falls out of it, fast
        /// on entry and braking to land (<see cref="CrowSwoop.Progress"/>).</summary>
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

        private const int TileSize = 64;
        /// <summary>How far past the frame's side edge a crow starts, so it flies in from off screen
        /// (its sprite hangs a tile either side of its feet).</summary>
        private const int SwoopStartOutsideTiles = 2;
        /// <summary>How high above its crop a crow starts its swoop: this many tiles, plus up to
        /// <see cref="SwoopExtraRiseTiles"/> more so no two come in at one height.</summary>
        private const int SwoopRiseTiles = 3;
        private const int SwoopExtraRiseTiles = 3;

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
            /// <summary>Facing on the way in: the way it flies (the sheet's bird faces left).</summary>
            public bool FlyFlip;
            public Vector2 Position;
            public int Frame = CrowStandFrame;
            public bool Visible;
            /// <summary>On the ground: between its landing and the lift-off.</summary>
            public bool Perched;
            /// <summary>Has come down (it keeps its landed facing through the lift-off too).</summary>
            public bool Landed;
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

        /// <summary>Can the crows stage tonight? The Farm is there; the crops they land on are the
        /// picked live crops themselves, so a non-empty pick always has a patch. Asked at pick time
        /// (review I1).</summary>
        internal static bool CanStage() => Game1.getFarm()?.map != null;

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

            IReadOnlyList<(int X, int Y)> landings = CropCluster.Nearest(patch, centre, MaxCrows);
            // The flock comes in from both edges in turn; the first from the side its crop is on.
            bool firstFromLeft = landings.Count > 0 && landings[0].X <= _focus.X;
            foreach ((int X, int Y) tile in landings)
                _crows.Add(MakeCrow(new Vector2(tile.X, tile.Y), CrowsEnterMs + _crows.Count * CrowGapMs, CrowSwoop.FromLeft(_crows.Count, firstFromLeft)));
            if (scarecrow.HasValue)
            {
                Vector2 beside = TileBesideScarecrow(scarecrow.Value, centroid);
                // It flies in toward the scarecrow and lands facing it.
                SceneCrow perched = MakeCrow(beside, ScarecrowCrowLandMs - GlideMs, fromLeft: scarecrow.Value.X > beside.X);
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

        private SceneCrow MakeCrow(Vector2 tile, int enterAtMs, bool fromLeft)
        {
            Vector2 landing = SceneCamera.TileCentre(tile);
            // Off the frame's side edge, a few tiles above the crop: the swoop's dive starts there.
            float startX = fromLeft
                ? Game1.viewport.X - TileSize * SwoopStartOutsideTiles
                : Game1.viewport.X + Game1.viewport.Width + TileSize * SwoopStartOutsideTiles;
            float startY = landing.Y - TileSize * (SwoopRiseTiles + _spread.Next(0, SwoopExtraRiseTiles + 1));
            var crow = new SceneCrow
            {
                Sprite = new AnimatedSprite(CritterSheet, CrowBaseFrame, CrowSpriteSize, CrowSpriteSize),
                Tile = tile,
                Landing = landing,
                Start = new Vector2(startX, startY),
                EnterAtMs = Math.Max(0, enterAtMs),
                Flip = tile.X < _focus.X,
                FlyFlip = fromLeft,
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
            crow.Landed = elapsed >= crow.LandAtMs;
            if (elapsed < crow.EnterAtMs)
            {
                crow.Position = crow.Start;
                crow.Frame = FlapFrame(elapsed);
            }
            else if (elapsed < crow.LandAtMs)
            {
                // The swoop: a dive from the side that levels out and brakes onto the crop.
                (float x, float y) = CrowSwoop.At((crow.Start.X, crow.Start.Y), (crow.Landing.X, crow.Landing.Y), (elapsed - crow.EnterAtMs) / (float)GlideMs);
                crow.Position = new Vector2(x, y);
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
                // On the way in it faces the way it flies; once down it turns to the field as before.
                bool flip = crow.Landed ? crow.Flip : crow.FlyFlip;
                crow.Sprite.draw(b, corner, 0.9f, 0, 0, SceneCamera.NightTint, flip, DrawScale);
                PaintEye(b, crow, corner, flip);
            }
            if (_linusInFrame) _linus?.Draw(b, SceneCamera.NightTint);
        }

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup() => SceneCamera.Restore();
    }
}
