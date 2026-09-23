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

        // ---------------------------------------------------------------- Shane

        private const string ShaneSheet = "Characters\\Shane";
        /// <summary>His sheet is 64x416, four columns of 16x32 frames (dumped from the running game,
        /// task 7 report).</summary>
        private const int ShaneSpriteWidth = 16;
        private const int ShaneSpriteHeight = 32;

        /// <summary>How far WEST of the door his way into the frame has to be. The Saloon is south
        /// and west of the hall, so he should come up the path at an angle and leave the same way.
        /// Four tiles west and four south of the door is a plainly diagonal entry at the bottom left
        /// of the shot rather than a figure rising straight up the middle of it.</summary>
        private const int SaloonSideTiles = 4;

        /// <summary>How far a way into the frame may be and still be WALKED in from, and so how long
        /// the walk may be. This is the whole route now: nothing is trimmed off the far end any
        /// more, because trimming is what quietly threw the westward leg away and left him coming
        /// straight up the door's own column (caught by review, 2026-09-21). Eight tiles is what a
        /// bottom-left entry from <see cref="SaloonSideTiles"/> actually costs.</summary>
        private const int MaxWalkTiles = 8;

        /// <summary>How far down the path to put him when no way into the frame can be reached.</summary>
        private const int AlreadyInFrameTiles = 5;

        /// <summary>How many frames of his walk cycle are spent on one tile. Two reads as walking,
        /// and holding it fixed is what lets the route be longer without his feet sliding: the
        /// diagonal is covered in the same fifteen hundred milliseconds, so he is hurrying, and the
        /// cycle hurries with him.</summary>
        private const int WalkFramesPerTile = 2;

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

            SceneCamera.CutTo(_town, HallFacade.CameraTile);
            _windows = new SceneWindowGlow(HallFacade.OriginTile, HallFacade.FrontWindows);
            _windows.AddLights();

            StageShane();

            Monitor.Log(
                $"Darkness: the hall is staged on the Community Center front at ({HallFacade.Tiles.X},{HallFacade.Tiles.Y}) to ({HallFacade.Tiles.Right - 1},{HallFacade.Tiles.Bottom - 1}) in Town, "
                + $"facade {(HallFacade.IsRestored ? "RESTORED (the window rectangles were measured off the abandoned art)" : "abandoned")}, "
                + $"{_windows.Describe()}, {DescribeShane()}. {HallFacade.DescribeMapWindowLights(_town)}",
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
            // The route may be eight tiles or two, and the walk window is the same either way, so
            // the footfall rate is set from the pace rather than left at the class default.
            int msPerTile = Math.Max(1, (StopAtMs - WalkInAtMs) / Math.Max(1, _steps));
            _shane.StepMs = Math.Max(40, msPerTile / WalkFramesPerTile);
        }

        /// <summary>The walk up to the hall. The target is the front door tile itself, which is a
        /// building and so is never walked on, so the search stops on the clear tile in front of it,
        /// which is exactly where he should be standing when he looks up.
        ///
        /// THE WHOLE ROUTE PLAYS. The way in is capped at <see cref="MaxWalkTiles"/> by the search
        /// itself rather than found first and then cut down to size, because
        /// <c>ScenePath.Trim</c> keeps the tiles NEAREST the target, which is exactly the westward
        /// leg thrown away and the diagonal entry with it.</summary>
        private IReadOnlyList<(int X, int Y)> PlanWalk()
        {
            bool[,] ground = SceneGround.PassableGrid(_town);
            // An empty list is deliberately still handed over: the search's own fallback then puts
            // him AlreadyInFrameTiles from the door instead, which is the "stage him in view and
            // skip the walk in" the spec asks for when the frame has no way into it.
            return ScenePath.WalkTo(
                ground,
                ((int)HallFacade.DoorTile.X, (int)HallFacade.DoorTile.Y),
                WaysIntoTheFrame(),
                MaxWalkTiles,
                AlreadyInFrameTiles);
        }

        /// <summary>Where he may come into the shot from: the lowest rows of the frame, west of the
        /// door by at least <see cref="SaloonSideTiles"/>, nearest the bottom left first. The search
        /// takes the nearest of them it can reach inside <see cref="MaxWalkTiles"/>, so what plays
        /// is the shortest genuinely diagonal approach the ground allows.
        ///
        /// Town's own map edge is fifty tiles away and would have put him half a minute's walk out
        /// of shot, which is why the frame's own edge is the way in instead.</summary>
        private IReadOnlyList<(int X, int Y)> WaysIntoTheFrame()
        {
            Rectangle frame = SceneCamera.FrameInTiles();
            var ways = new List<(int X, int Y)>();
            if (frame.Width <= 0 || frame.Height <= 0) return ways;
            int west = (int)HallFacade.DoorTile.X - SaloonSideTiles;
            int south = (int)HallFacade.DoorTile.Y + SaloonSideTiles;
            for (int row = frame.Bottom - 1; row >= south; row--)
                for (int x = west; x >= frame.Left + 1; x--)
                    if (SceneGround.CanStandOn(_town, x, row))
                        ways.Add((x, row));
            return ways;
        }

        private string DescribeShane()
        {
            if (_shane == null) return "nobody on the path";
            return $"Shane walks {_steps} tile(s) in from ({_walk.Start.X},{_walk.Start.Y}) and stops at ({_walk.End.X},{_walk.End.Y}), drawn from a sheet of {_shane.Describe()}";
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
