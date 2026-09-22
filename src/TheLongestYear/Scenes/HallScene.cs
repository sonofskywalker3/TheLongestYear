using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core.Sabotage;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The reversion scene (spec 2026-09-21). About seven seconds, no text: the Community
    /// Center from OUTSIDE, in Town, at night. Its windows glow like firelight and shapes cross
    /// them. Shane comes up the path from the Saloon side on his way home, stops dead, gives a small
    /// jump of shock, backs away two tiles still facing the hall, then turns and runs out the way he
    /// came. The slot comes undone at the jump.
    ///
    /// THE PLAYER LEARNS NOTHING ABOUT WHICH ROOM OR SLOT WAS HIT, and that is the point of shooting
    /// it from the street. This scene never opens, enters or draws the Community Center's interior,
    /// and nothing it paints is tied to the slot the strike takes.
    ///
    /// WHAT IS REAL. The building, the street, the lamps and Town's own night are the game's. The
    /// firelight and the shapes behind the glass are painted (<see cref="SceneWindowGlow"/>), and
    /// Shane is a <see cref="SceneActor"/> drawn from his sheet, never the real NPC: the real one
    /// has a schedule that would put him back on the map at 6am wherever the scene left him.
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. No route for Shane means he simply stands in view, a sheet
    /// that will not load means the scene plays without him at all, and only a Town with no map
    /// loaded calls the scene off. The reversion lands either way.</summary>
    internal sealed class HallScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms

        private const int FadeInMs = 700;
        /// <summary>When he steps into the frame. The glow and the shapes are already running.</summary>
        private const int WalkInAtMs = 1500;
        /// <summary>When he stops dead and faces the hall.</summary>
        private const int StopAtMs = 3000;
        /// <summary>The jump of shock, and the beat the slot comes undone on.</summary>
        private const int JumpAtMs = 3300;
        private const int JumpLengthMs = 300;
        /// <summary>How far off the ground the jump takes him, in SCREEN pixels. The brief asked for
        /// sixteen, which is four pixels of his sheet at the game's 4x draw scale, and on the first
        /// overnight frames that was not readable at all against a figure sixteen sheet pixels wide.
        /// Twenty eight is seven sheet pixels, which reads as a start without reading as a leap.</summary>
        private const float JumpHeightPixels = 28f;
        /// <summary>When he starts backing away, and how long the two tiles take.</summary>
        private const int BackAwayAtMs = 3800;
        private const int BackAwayMs = 800;
        private const int BackAwayTiles = 2;
        /// <summary>When he turns and runs, and how fast.</summary>
        private const int RunOutAtMs = 4600;
        private const int RunMsPerTile = 150;
        /// <summary>The hold on the windows with nobody in the shot.</summary>
        private const int HoldAtMs = 5400;
        private const int FadeOutAtMs = 6200;
        private const int FadeOutLengthMs = 800;
        private const int SceneEndMs = 7000;

        // ---------------------------------------------------------------- the hall

        /// <summary>The Community Center's own tiles in Town. Vanilla's own bounds, from
        /// <c>Town.refurbishCommunityCenter</c> (Town.cs:377), which walks
        /// <c>new Rectangle(47, 11, 11, 9)</c> with <c>x &lt;= Right</c> and <c>y &lt;= Bottom</c>
        /// and so covers twelve tiles by ten.</summary>
        private static readonly Rectangle HallTiles = new Rectangle(47, 11, 12, 10);

        /// <summary>The front door, which is what the camera and the walk are hung on.</summary>
        private static readonly Vector2 HallDoorTile = new Vector2(52, 20);

        /// <summary>How far above the bottom of the building the camera sits, so the whole front and
        /// the path below it share the frame.</summary>
        private const int CameraAboveFootTiles = 3;

        /// <summary>The glass of the front windows, in PIXELS relative to the top left corner of
        /// <see cref="HallTiles"/>.
        ///
        /// MEASURED, NOT GUESSED, AND THERE ARE TWO OF THEM. The task asked for four to six, but the
        /// abandoned Community Center has exactly two windows on its front, one either side of the
        /// door, each with its shutters open on a boarded pane. They were read off a frame of this
        /// very scene at zoom 1 with the viewport at (2400,644) (see the task 9 report), and Town's
        /// own map has no <c>WindowLight</c> property on the building at all, so there was nothing
        /// in the map data to take them from. They are deliberately a little inside the glass rather
        /// than flush with the frame, because a pane that overshoots by a pixel reads as a glowing
        /// wall.</summary>
        private static readonly Rectangle[] FrontWindows =
        {
            new Rectangle(118, 414, 60, 92),
            new Rectangle(590, 414, 60, 92),
        };

        // ---------------------------------------------------------------- Shane

        private const string ShaneSheet = "Characters\\Shane";
        /// <summary>His sheet is 64x416, four columns of 16x32 frames (dumped from the running game,
        /// task 7 report).</summary>
        private const int ShaneSpriteWidth = 16;
        private const int ShaneSpriteHeight = 32;

        /// <summary>How far he walks in. Five tiles across the fifteen hundred milliseconds the walk
        /// has is a brisk walk home, and a longer route would have to be run.</summary>
        private const int MaxWalkTiles = 5;
        /// <summary>How far from the door a way into the frame may be and still be walked in from.</summary>
        private const int MaxWayInTiles = 20;
        /// <summary>How far down the path to put him when no way into the frame can be reached.</summary>
        private const int AlreadyInFrameTiles = 5;

        // ---------------------------------------------------------------- state

        private GameLocation _town;
        private SceneWindowGlow _windows;
        private SceneActor _shane;
        private SceneWalk _walk;
        private int _steps;

        public HallScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        // ---------------------------------------------------------------- staging

        /// <inheritdoc />
        protected override bool Stage()
        {
            _town = Game1.getLocationFromName("Town");
            if (_town?.map == null)
            {
                Monitor.Log("Darkness: Town has no map loaded, so the reversion lands with no scene.", LogLevel.Info);
                return false;
            }

            SceneCamera.CutTo(_town, new Vector2(HallDoorTile.X, HallTiles.Bottom - CameraAboveFootTiles));
            _windows = new SceneWindowGlow(new Vector2(HallTiles.X, HallTiles.Y), FrontWindows);
            _windows.AddLights();

            StageShane();

            Monitor.Log(
                $"Darkness: the hall is staged on the Community Center front at ({HallTiles.X},{HallTiles.Y}) to ({HallTiles.Right - 1},{HallTiles.Bottom - 1}) in Town, "
                + $"{_windows.Describe()}, {DescribeShane()}. {DescribeMapWindowLights()}",
                LogLevel.Trace);
            return true;
        }

        /// <summary>Put Shane on the path, if there is a path. He walks in from the frame's bottom
        /// left, which is the Saloon side of the hall, and stops on the clear tile below the front
        /// door. Nothing about him is allowed to stop the scene: a route that cannot be found leaves
        /// him standing in view, and a sheet that will not load leaves him out of it.</summary>
        private void StageShane()
        {
            try
            {
                _shane = new SceneActor(ShaneSheet, ShaneSpriteWidth, ShaneSpriteHeight);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Darkness: the hall scene could not load Shane, so it plays with nobody on the path. {ex}", LogLevel.Warn);
                _shane = null;
                return;
            }

            IReadOnlyList<(int X, int Y)> walk = PlanWalk();
            if (walk.Count == 0)
            {
                Monitor.Log("Darkness: the hall scene found no clear ground in front of the Community Center, so it plays with nobody on the path.", LogLevel.Info);
                _shane = null;
                return;
            }
            _walk = new SceneWalk(walk);
            _steps = _walk.Steps;
            _shane.Position = _walk.At(0f);
            _shane.Facing = SceneActor.FacingUp;
        }

        /// <summary>The walk up to the hall. The target is the front door tile itself, which is a
        /// building and so is never walked on, so the search stops on the clear tile in front of it,
        /// which is exactly where he should be standing when he looks up.
        ///
        /// The ways in are the bottom edge of the FRAME, west of the door, which is the direction the
        /// Saloon lies in. Town's own map edge is fifty tiles away and would have put him half a
        /// minute's walk out of shot.</summary>
        private IReadOnlyList<(int X, int Y)> PlanWalk()
        {
            bool[,] ground = SceneGround.PassableGrid(_town);
            // An empty list is deliberately still handed over: the search's own fallback then puts
            // him AlreadyInFrameTiles from the door instead, which is the "stage him in view and
            // skip the walk in" the spec asks for when the frame has no way into it.
            IReadOnlyList<(int X, int Y)> ways = WaysIntoTheFrame();
            IReadOnlyList<(int X, int Y)> walk = ScenePath.WalkTo(
                ground,
                ((int)HallDoorTile.X, (int)HallDoorTile.Y),
                ways,
                MaxWayInTiles,
                AlreadyInFrameTiles);
            return walk.Count == 0 ? walk : ScenePath.Trim(walk, MaxWalkTiles);
        }

        /// <summary>How far WEST of the door a way into the frame has to be. Without it the search
        /// finds the tile directly below the door and he walks in from straight off the bottom of
        /// the shot, which is nobody's way home: the Saloon is south and west of the hall, so he
        /// should come up the path at an angle (seen on the first overnight frames, 2026-09-21).</summary>
        private const int SaloonSideTiles = 4;

        /// <summary>The tiles along the bottom of the frame, west of the door by at least
        /// <see cref="SaloonSideTiles"/>. The search takes the nearest of them that can be reached,
        /// so the walk is the shortest one that still comes in from the Saloon side.</summary>
        private IReadOnlyList<(int X, int Y)> WaysIntoTheFrame()
        {
            Rectangle frame = SceneCamera.FrameInTiles();
            var ways = new List<(int X, int Y)>();
            if (frame.Width <= 0 || frame.Height <= 0) return ways;
            int row = frame.Bottom - 1;
            for (int x = (int)HallDoorTile.X - SaloonSideTiles; x >= frame.Left + 1; x--)
                if (SceneGround.CanStandOn(_town, x, row))
                    ways.Add((x, row));
            return ways;
        }

        private string DescribeShane()
        {
            if (_shane == null) return "nobody on the path";
            return $"Shane walks {_steps} tile(s) in from ({_walk.Start.X},{_walk.Start.Y}) and stops at ({_walk.End.X},{_walk.End.Y}), drawn from a sheet of {_shane.Describe()}";
        }

        /// <summary>What the map itself says about lit windows on the hall, for the record. The
        /// window rectangles above were measured off the facade, and this line is how a later reader
        /// can tell whether the map ever agreed with them.</summary>
        private string DescribeMapWindowLights()
        {
            try
            {
                string[] lights = _town.GetMapPropertySplitBySpaces("WindowLight");
                var mine = new List<string>();
                for (int i = 0; i + 1 < lights.Length; i += 3)
                {
                    if (!int.TryParse(lights[i], out int x) || !int.TryParse(lights[i + 1], out int y)) continue;
                    if (HallTiles.Contains(x, y)) mine.Add($"({x},{y})");
                }
                return mine.Count == 0
                    ? "Town's map declares no WindowLight on the hall."
                    : $"Town's map declares WindowLight on the hall at {string.Join(" ", mine)}.";
            }
            catch (Exception ex)
            {
                return $"Town's WindowLight property could not be read ({ex.GetType().Name}).";
            }
        }

        // ---------------------------------------------------------------- the beats

        /// <inheritdoc />
        protected override void Build(Timeline t)
        {
            Fade(FadeInMs, FadeOutAtMs, FadeOutLengthMs);
            t.At(JumpAtMs, () =>
            {
                Game1.playSound("dwop", -400);
                ApplyStrike();
            });
            t.At(HoldAtMs, () => Game1.playSound("shadowDie", -900));
            t.EndAt(SceneEndMs);
        }

        /// <summary>The base pumps Town for the scene on the real overnight path.</summary>
        protected override GameLocation SceneLocation => _town;

        /// <inheritdoc />
        protected override void Advance(int elapsed)
        {
            MoveShane(elapsed);
        }

        /// <summary>Where he is and what he is doing this instant. Before his cue and after he has
        /// run off he is simply not drawn.</summary>
        private void MoveShane(int elapsed)
        {
            if (_shane == null) return;
            _shane.Lift = 0f;
            float tilesIn;
            bool walking;
            bool backwards = false;

            if (elapsed < WalkInAtMs)
            {
                tilesIn = 0f;
                walking = false;
            }
            else if (elapsed < StopAtMs)
            {
                tilesIn = _steps * (elapsed - WalkInAtMs) / (float)(StopAtMs - WalkInAtMs);
                walking = true;
            }
            else if (elapsed < BackAwayAtMs)
            {
                tilesIn = _steps;
                walking = false;
                if (elapsed >= JumpAtMs && elapsed < JumpAtMs + JumpLengthMs) _shane.Lift = JumpLift(elapsed - JumpAtMs);
            }
            else if (elapsed < RunOutAtMs)
            {
                // Backing away: he moves down the walk but keeps looking at the hall.
                tilesIn = _steps - BackAwayTiles * (elapsed - BackAwayAtMs) / (float)BackAwayMs;
                walking = true;
            }
            else
            {
                tilesIn = _steps - BackAwayTiles - (elapsed - RunOutAtMs) / (float)RunMsPerTile;
                walking = true;
                backwards = true;
            }
            // Clamped only at the far end. Running out, he carries on past the start of the walk
            // and off the edge of the shot, which is what SceneWalk.At is built to answer.
            tilesIn = Math.Min(_steps, tilesIn);

            _shane.Position = _walk.At(tilesIn);
            _shane.Walking = walking;
            // He faces the hall from the moment he stops until he turns to run, the backing away
            // included. That is the whole read of the scene: he cannot take his eyes off it.
            if (elapsed >= StopAtMs && elapsed < RunOutAtMs) _shane.Facing = SceneActor.FacingUp;
            else _shane.Facing = _walk.FacingAt(tilesIn, backwards);
            _shane.Animate(elapsed);
        }

        /// <summary>The jump arc: up and back down again over <see cref="JumpLengthMs"/>, the way
        /// vanilla's own jump reads, nothing at either end.</summary>
        private static float JumpLift(int intoJumpMs)
        {
            float across = Math.Max(0f, Math.Min(1f, intoJumpMs / (float)JumpLengthMs));
            return JumpHeightPixels * (float)Math.Sin(across * Math.PI);
        }

        // ---------------------------------------------------------------- painting

        /// <inheritdoc />
        protected override void Paint(SpriteBatch b)
        {
            _windows?.Paint(b, ElapsedMs);
            if (ShaneIsInTheShot()) _shane.Draw(b, SceneCamera.NightTint);
        }

        /// <summary>He is drawn from his cue onward and never before it. Standing him at his entry
        /// tile from the first frame would read as a man who had been watching the hall all along.
        /// There is no far end to test: he runs clean off the shot and keeps going, so the frame
        /// simply stops containing him.</summary>
        private bool ShaneIsInTheShot() => _shane != null && ElapsedMs >= WalkInAtMs;

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup()
        {
            _windows?.RemoveLights();
            SceneCamera.Restore();
        }
    }
}
