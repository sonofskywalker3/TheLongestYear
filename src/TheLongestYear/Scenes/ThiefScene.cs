using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core.Sabotage;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The chest blight scene (spec 2026-09-21). About eight seconds, no text: a Shadow
    /// Brute walks in to the chest the darkness picked, the lid comes up, a beat, the units go, the
    /// lid drops, he turns to face the camera with red eyes for half a second, then runs back out
    /// the way he came. Black.
    ///
    /// WHERE IT PLAYS. Wherever the night's chest is, so long as the scene can show it: the Farm,
    /// a Shed, the Cellar or the FarmHouse (<see cref="SceneTargetOnFarm"/>). In the farmhouse the
    /// camera frames the bed with the chest where the room allows it, the farmer is lying in it,
    /// and the spouse and children are in theirs (<see cref="SceneSleepers"/>).
    ///
    /// WHAT IS REAL. The chest is real and its lid is the real lid
    /// (<see cref="SceneChestLid"/>). The items are removed by the strike itself at its beat, never
    /// by this class. Only the Brute is painted, and he is a <see cref="SceneActor"/> rather than a
    /// real monster: a real one would path, fight and survive the scene.
    ///
    /// A MACHINE TARGET. When the darkness took no chest at all the scene is staged on one of the
    /// machines it took instead (Jeff, 2026-09-21). He walks up to it, a beat, and it vanishes with
    /// everything else at the strike. No lid, and nothing else changes.
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. Every staging decision degrades: no way to walk in means
    /// he simply starts nearer, an unreadable lid means the lid does not move, a household that
    /// cannot be posed is left where it is. Only a target with no ground at all beside it calls the
    /// scene off, and then the take lands with no scene.</summary>
    internal sealed class ThiefScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms

        private const int FadeInMs = 600;
        /// <summary>When he starts walking. The fade is still coming up under him.</summary>
        private const int WalkStartMs = FadeInMs;
        /// <summary>Two and a half tiles a second in, five back out.</summary>
        private const int WalkMsPerTile = 400;
        private const int RunMsPerTile = 200;
        /// <summary>The beat between arriving and the lid moving.</summary>
        private const int BeatMs = 400;
        /// <summary>From the lid opening to the units going.</summary>
        private const int TakeAfterLidMs = 600;
        /// <summary>From the units going to the lid dropping.</summary>
        private const int CloseAfterTakeMs = 400;
        /// <summary>From the lid dropping to him turning to the camera.</summary>
        private const int TurnAfterCloseMs = 300;
        /// <summary>How long he holds the look.</summary>
        private const int LookMs = 500;
        private const int FadeOutLengthMs = 800;
        /// <summary>A breath of black after he is gone.</summary>
        private const int TailMs = 200;
        /// <summary>The whole scene is clamped to this, which is what fixes
        /// <see cref="MaxWalkTiles"/>: 600 in, 400 a tile out, 2200 at the chest, 200 a tile back,
        /// and 200 of tail comes to exactly 9000 at ten tiles.</summary>
        private const int MaxSceneMs = 9000;

        // ---------------------------------------------------------------- staging

        /// <summary>How long a walk in may be. See <see cref="MaxSceneMs"/>: any longer and he
        /// starts further along the path instead.</summary>
        private const int MaxWalkTiles = 10;
        /// <summary>How far a door or a map edge may be and still be walked in from. Further than
        /// this and he is simply already inside when the scene opens.</summary>
        private const int MaxWayInTiles = 14;
        /// <summary>How far from the target to start him when there is no way in near enough.</summary>
        private const int AlreadyInsideTiles = 8;
        /// <summary>How far apart the chest and the bed may be and still share the frame.</summary>
        private const int BedInFrameTiles = 10;

        private const int TileSize = 64;
        private const float DrawScale = 4f;

        // ---------------------------------------------------------------- the brute

        private const string BruteSheet = "Characters\\Monsters\\Shadow Brute";
        /// <summary>His sheet is 16 wide and 32 tall a frame, from <c>ShadowBrute.reloadSprite</c>
        /// (ShadowBrute.cs:21), which sets <c>Sprite.SpriteHeight = 32</c> on the default 16 wide
        /// <c>AnimatedSprite</c>. Task 7's report guessed 24 from the sheet's height alone, which
        /// does not divide into 256 by four columns.</summary>
        private const int BruteSpriteWidth = 16;
        private const int BruteSpriteHeight = 32;

        /// <summary>Where his eyes are in the down-facing standing frame, in sheet pixels from the
        /// frame's top left, and how wide each one is drawn.</summary>
        private static readonly Point[] BruteEyes = { new Point(5, 7), new Point(9, 7) };
        private const float EyeCoreSheetPixels = 2f;
        private const float EyeGlowSheetPixels = 7f;

        // ---------------------------------------------------------------- state

        private SpoilagePass.Hit _target;
        private GameLocation _where;
        private Vector2 _targetTile;
        private SceneActor _brute;
        private SceneChestLid _lid;
        private SceneSleepers _sleepers;
        /// <summary>The walk in, the tile he starts on first and the tile beside the target last.
        /// The way out is this, backwards.</summary>
        private IReadOnlyList<(int X, int Y)> _walk = Array.Empty<(int X, int Y)>();
        private int _steps;
        private int _arriveMs;
        private int _lidOpenMs;
        private int _takeMs;
        private int _lidCloseMs;
        private int _lookMs;
        private int _leaveMs;
        private int _fadeOutMs;
        private int _endMs;
        private bool _looking;

        public ThiefScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        /// <summary>Where the thief is staged: the night's chest, else the first machine it takes,
        /// but only on a map the scene can actually show. Null when nothing tonight can be filmed,
        /// and then the take lands with no scene.</summary>
        internal static SpoilagePass.Hit SceneTargetOnFarm(PendingStrike strike)
        {
            SpoilagePass.Hit hit = strike?.SceneTarget;
            if (hit?.Location == null) return null;
            bool onFarm = hit.Location is Farm
                       || hit.Location is FarmHouse
                       || hit.Location is Cellar
                       || hit.Location is Shed;
            return onFarm ? hit : null;
        }

        // ---------------------------------------------------------------- staging

        /// <inheritdoc />
        protected override bool Stage()
        {
            _target = SceneTargetOnFarm(Strike);
            if (_target == null) return false;
            _where = _target.Location;
            _targetTile = _target.Tile;
            if (_where?.map == null) return false;

            _walk = PlanWalk();
            if (_walk.Count == 0)
            {
                Monitor.Log($"Darkness: the thief has nowhere to stand beside the target at ({_targetTile.X},{_targetTile.Y}) on {_where.NameOrUniqueName}, so the take lands with no scene.", LogLevel.Info);
                return false;
            }
            _steps = _walk.Count - 1;
            BuildClock();

            try
            {
                _brute = new SceneActor(BruteSheet, BruteSpriteWidth, BruteSpriteHeight);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: the thief scene could not load the Shadow Brute, so the take lands with no scene. {ex}", LogLevel.Warn);
                return false;
            }
            _brute.Position = new Vector2(_walk[0].X, _walk[0].Y) * TileSize;
            _brute.Face(SceneCamera.TileCentre(_targetTile));

            var house = _where as FarmHouse;
            SceneCamera.CutTo(_where, Focus(house), showFarmer: house != null);
            if (house != null)
            {
                _sleepers = new SceneSleepers();
                _sleepers.Pose(house, Monitor);
            }
            if (!_target.Machine && _target.Chest != null) _lid = new SceneChestLid(_target.Chest);

            Monitor.Log(
                $"Darkness: the thief is staged on {(_target.Machine ? "a machine" : "a chest")} at ({_targetTile.X},{_targetTile.Y}) on {_where.NameOrUniqueName}, "
                + $"walking {_steps} tile(s) in from ({_walk[0].X},{_walk[0].Y}), lid {(_lid == null ? "none" : _lid.Available ? "driven" : "unreadable")}, "
                + $"asleep in bed: {(_sleepers == null ? "not a house" : _sleepers.Describe())}. He is drawn from a sheet of {_brute.Describe()}.",
                LogLevel.Trace);
            return true;
        }

        /// <summary>The walk in, through the passable-tile search in Core. Indoors the ways in are
        /// the door warps, outdoors the map's own edge and its gates as well.</summary>
        private IReadOnlyList<(int X, int Y)> PlanWalk()
        {
            bool[,] ground = SceneGround.PassableGrid(_where);
            IReadOnlyList<(int X, int Y)> ways = SceneGround.WaysIn(_where);
            IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(
                ground,
                ((int)_targetTile.X, (int)_targetTile.Y),
                ways,
                MaxWayInTiles,
                AlreadyInsideTiles);
            return ScenePath.Trim(walk, MaxWalkTiles);
        }

        private void BuildClock()
        {
            _arriveMs = WalkStartMs + _steps * WalkMsPerTile;
            _lidOpenMs = _arriveMs + BeatMs;
            _takeMs = _lidOpenMs + TakeAfterLidMs;
            _lidCloseMs = _takeMs + CloseAfterTakeMs;
            _lookMs = _lidCloseMs + TurnAfterCloseMs;
            _leaveMs = _lookMs + LookMs;
            _endMs = Math.Min(MaxSceneMs, _leaveMs + _steps * RunMsPerTile + TailMs);
            _fadeOutMs = Math.Max(_leaveMs, _endMs - FadeOutLengthMs);
        }

        /// <summary>What the camera sits on. The target, except in the farmhouse, where the bed
        /// shares the frame when it is near enough for both to fit.</summary>
        private Vector2 Focus(FarmHouse house)
        {
            if (house == null) return _targetTile;
            Point bed = house.GetPlayerBedSpot();
            if (bed.X <= 0 && bed.Y <= 0) return _targetTile;
            var bedTile = new Vector2(bed.X, bed.Y);
            if (Vector2.Distance(bedTile, _targetTile) > BedInFrameTiles) return _targetTile;
            return new Vector2(
                (float)Math.Round((_targetTile.X + bedTile.X) / 2f),
                (float)Math.Round((_targetTile.Y + bedTile.Y) / 2f));
        }

        // ---------------------------------------------------------------- the beats

        /// <inheritdoc />
        protected override void Build(Timeline t)
        {
            t.At(WalkStartMs, () => Game1.playSound("shadowpeep", -800));
            if (_lid != null)
            {
                t.At(_lidOpenMs, () => { _lid.Open(); Game1.playSound("openChest"); });
                t.At(_lidCloseMs, () => { _lid.Close(); Game1.playSound("doorCreakReverse"); });
            }
            t.At(_takeMs, ApplyStrike);
            t.EndAt(_endMs);
        }

        /// <inheritdoc />
        public override bool tickUpdate(GameTime time)
        {
            // Nothing in the world updates itself during a farm event: vanilla's own night events
            // pump the clock, the location and the rest by hand (WitchEvent.tickUpdate). The debug
            // preview plays the scene during an ordinary update, where the game is already doing all
            // of this, so the pump only runs when this really is tonight's farm event.
            if (ReferenceEquals(Game1.farmEvent, this))
            {
                try
                {
                    Game1.UpdateGameClock(time);
                    _where.UpdateWhenCurrentLocation(time);
                    _where.updateEvenIfFarmerIsntHere(time);
                    Game1.UpdateOther(time);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Darkness: the thief scene could not pump {_where?.NameOrUniqueName} this tick. {ex}", LogLevel.Trace);
                }
            }
            // AFTER the pump, never before it: the pump recomputes the outdoor light from the clock
            // and copies it into the ambient light, so holding the night first means the pump throws
            // it away again (Task 7, caught on the first real overnight screenshots).
            SceneCamera.HoldNight();
            return base.tickUpdate(time);
        }

        /// <summary>Moving him runs through the base's hook, so a throw here goes down the base's
        /// failure path and the camera, the lid and the household all come back.</summary>
        protected override void Advance(int elapsed)
        {
            // Same reason as the night above: Chest.fixLidFrame runs at the top of the chest's own
            // update and snaps an unlocked chest shut, so the wanted frame is written again here,
            // after the pump.
            _lid?.Hold();
            MoveBrute(elapsed);
        }

        private void MoveBrute(int elapsed)
        {
            if (_brute == null) return;
            float tilesIn;
            bool walking;
            if (elapsed < WalkStartMs)
            {
                tilesIn = 0f;
                walking = false;
            }
            else if (elapsed < _arriveMs)
            {
                tilesIn = (elapsed - WalkStartMs) / (float)WalkMsPerTile;
                walking = true;
            }
            else if (elapsed < _leaveMs)
            {
                tilesIn = _steps;
                walking = false;
            }
            else
            {
                tilesIn = _steps - (elapsed - _leaveMs) / (float)RunMsPerTile;
                walking = true;
            }
            tilesIn = Math.Max(0f, Math.Min(_steps, tilesIn));
            _brute.Position = AlongWalk(tilesIn);
            _brute.Walking = walking;

            // He looks at the camera for the length of the beat, and that is the only time the eyes
            // are lit: they belong to the down-facing frame.
            _looking = elapsed >= _lookMs && elapsed < _leaveMs;
            if (_looking) _brute.Facing = SceneActor.FacingDown;
            else if (elapsed >= _arriveMs && elapsed < _leaveMs) _brute.Face(SceneCamera.TileCentre(_targetTile));
            else _brute.Facing = StepFacing(tilesIn, elapsed >= _leaveMs);
            _brute.Animate(elapsed);
        }

        /// <summary>Where he is this instant, in world pixels, <paramref name="tilesIn"/> tiles
        /// along the walk.</summary>
        private Vector2 AlongWalk(float tilesIn)
        {
            if (_steps == 0) return new Vector2(_walk[0].X, _walk[0].Y) * TileSize;
            int leg = Math.Max(0, Math.Min(_steps - 1, (int)tilesIn));
            float across = Math.Max(0f, Math.Min(1f, tilesIn - leg));
            var from = new Vector2(_walk[leg].X, _walk[leg].Y);
            var to = new Vector2(_walk[leg + 1].X, _walk[leg + 1].Y);
            return Vector2.Lerp(from, to, across) * TileSize;
        }

        /// <summary>Which way he is facing for the leg he is on. Leaving, he faces the way he is
        /// going, which is the walk read backwards.</summary>
        private int StepFacing(float tilesIn, bool leaving)
        {
            if (_steps == 0) return SceneActor.FacingDown;
            int leg = Math.Max(0, Math.Min(_steps - 1, (int)tilesIn));
            (int X, int Y) from = _walk[leg];
            (int X, int Y) to = _walk[leg + 1];
            int dx = leaving ? from.X - to.X : to.X - from.X;
            int dy = leaving ? from.Y - to.Y : to.Y - from.Y;
            if (dx != 0) return dx > 0 ? SceneActor.FacingRight : SceneActor.FacingLeft;
            if (dy != 0) return dy > 0 ? SceneActor.FacingDown : SceneActor.FacingUp;
            return SceneActor.FacingDown;
        }

        // ---------------------------------------------------------------- painting

        /// <inheritdoc />
        protected override void Paint(SpriteBatch b)
        {
            _brute?.Draw(b, SceneCamera.NightTint);
            if (_looking) PaintEyes(b);
            PaintFade(b);
        }

        /// <summary>Two red points where his eyes are. The glow is shared with the crows and is
        /// deliberately not tinted by the night, which is what makes the eyes the only lit thing in
        /// the frame.</summary>
        private void PaintEyes(SpriteBatch b)
        {
            if (_brute == null) return;
            // SceneActor lifts the sprite so the feet land on the tile, and the eyes are measured
            // from the same top-left corner.
            float lift = BruteSpriteHeight * DrawScale - TileSize;
            Vector2 corner = SceneCamera.ToScreen(_brute.Position + new Vector2(0f, -lift));
            foreach (Point eye in BruteEyes)
            {
                var centre = new Vector2(
                    corner.X + (eye.X + EyeCoreSheetPixels / 2f) * DrawScale,
                    corner.Y + (eye.Y + EyeCoreSheetPixels / 2f) * DrawScale);
                SceneGlow.Draw(b, centre, EyeGlowSheetPixels * DrawScale, EyeCoreSheetPixels * DrawScale, Color.Red);
            }
        }

        /// <summary>The fade in and the fade out, drawn LAST in the world layer and never from
        /// <see cref="StrikeSceneBase.PaintAbove"/>, which has no open SpriteBatch on the real
        /// overnight path. See the same method on the crows for the whole story.</summary>
        private void PaintFade(SpriteBatch b)
        {
            float black = BlackAt(ElapsedMs);
            if (black <= 0f) return;
            Viewport screen = Game1.graphics.GraphicsDevice.Viewport;
            int width = Math.Max(screen.Width, Game1.uiViewport.Width);
            int height = Math.Max(screen.Height, Game1.uiViewport.Height);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * black);
        }

        private float BlackAt(int elapsed)
        {
            if (elapsed < FadeInMs) return 1f - elapsed / (float)FadeInMs;
            if (elapsed >= _fadeOutMs) return Math.Min(1f, (elapsed - _fadeOutMs) / (float)FadeOutLengthMs);
            return 0f;
        }

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup()
        {
            _lid?.Restore();
            _sleepers?.Restore();
            SceneCamera.Restore();
        }
    }
}
