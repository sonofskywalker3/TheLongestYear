using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Scenes
{
    /// <summary>Shane in the hall scene (Jeff, 2026-09-23). He is NOT going to the Community Center.
    /// It is late, he has left the Saloon and is walking past the hall to clear his head before he
    /// heads home to Marnie's. He comes along the town's own paths, sees the lit windows, stops,
    /// jumps, backs away two tiles still looking at it, then turns and hurries off toward home.
    ///
    /// BOTH HALVES OF HIS WALK ARE THE GAME'S OWN ROUTES, from
    /// <c>PathFindController.findPathForNPCSchedules</c>, the pathing his schedule uses, which
    /// prefers stone, wood and dirt over grass. He comes along the dirt road below the hall from the
    /// east, up the short dirt path to its front and onto the cobbles, and after the fright he goes
    /// west and down the dirt path that runs south toward the square and the road to Marnie's.
    /// Each route is cut to the part in shot, plus a few tiles out of it at the open end
    /// (<see cref="SceneRoute"/>).
    ///
    /// WHY NOT FROM THE SALOON DOOR TO THE FOREST EXIT. That was tried first (2026-09-23). Neither
    /// real route goes near the hall, so both came up and went down the same column of grass under
    /// the door, which reads as a man visiting the hall rather than passing it. The two ends below
    /// are points on Town's own paths, read off the map's Back layer (the Type property), so the
    /// routes between them are still the pathfinder's.
    ///
    /// He is a <see cref="SceneActor"/> drawn from his sheet, never the real NPC. Nothing about him
    /// can stop the scene: no route in leaves him standing on the path when his cue comes, no route
    /// out sends him straight down out of the shot, and a sheet that will not load leaves him out
    /// of it altogether.</summary>
    internal sealed class HallWalker
    {
        private const string ShaneSheet = "Characters\\Shane";
        private const int SpriteWidth = 16;
        private const int SpriteHeight = 32;

        /// <summary>The same search budget the rewind's town walkers are given.</summary>
        private const int PathfinderLimit = 30000;

        /// <summary>Where he stops: on the path this many tiles below the front door, well short of
        /// the steps. He is passing, not calling.</summary>
        private const int NoticeBelowDoorTiles = 3;
        /// <summary>How far round that spot to look for ground he can stand on, if it is not.</summary>
        private const int NoticeSearchTiles = 2;

        /// <summary>How many tiles of each route are kept out of shot, so he walks in over the edge
        /// of the frame and out over it. He is two tiles tall, so three puts all of him outside.</summary>
        private const int OutOfShotTiles = 3;

        /// <summary>A walking pace, in ms a tile. Faster than vanilla's stroll, because a scene has
        /// seven seconds, but still a walk. A long way in starts earlier rather than going faster.</summary>
        private const int WalkMsPerTile = 250;

        /// <summary>His pace when he hurries off, in ms a tile, bounded both ways. The real pace is
        /// set so he is out of the shot by the hold on the windows when the route allows it.</summary>
        private const int SlowestRunMsPerTile = 150;
        private const int FastestRunMsPerTile = 75;

        /// <summary>Walk frames a tile, so the feet keep up with the ground at any pace.</summary>
        private const int FramesPerTile = 2;
        private const int FastestFrameMs = 40;

        /// <summary>Where his way in starts: the dirt road that runs east and west below the hall,
        /// out of shot to the east. From here the pathfinder brings him up the short dirt path at
        /// (54,25) to (56,27) onto the cobbles in front of the door.</summary>
        private static readonly Point ComesAlongFrom = new Point(66, 29);

        /// <summary>Where his way out is aimed: the dirt path west of the hall, out of shot below the
        /// frame, which runs south toward the square and the road to Marnie's.</summary>
        private static readonly Point LeavesBy = new Point(40, 32);

        /// <summary>Where he joins that path, in shot, west of the hall. Without it the pathfinder
        /// takes him straight down from where he backed away to, which is the way he came and reads
        /// as a man retreating from a visit rather than carrying on past (seen 2026-09-23).</summary>
        private static readonly Point JoinsThePathAt = new Point(40, 23);

        private readonly GameLocation _town;
        private readonly IMonitor _monitor;

        private SceneActor _shane;
        private SceneWalk _in;
        private SceneWalk _out;
        private Point _notice;
        private Point _backed;
        private int _walkFromMs;
        private int _walkFrameMs;
        private int _runMsPerTile = SlowestRunMsPerTile;
        private string _inSaid = "no route in";
        private string _outSaid = "no route out";

        public HallWalker(GameLocation town, IMonitor monitor)
        {
            _town = town ?? throw new ArgumentNullException(nameof(town));
            _monitor = monitor;
        }

        // ---------------------------------------------------------------- staging

        /// <summary>Load his sheet and plan both routes. Call it after the camera has cut to the
        /// hall, because the routes are cut to the frame. False only when he cannot be drawn at all.</summary>
        public bool Stage()
        {
            try
            {
                _shane = new SceneActor(ShaneSheet, SpriteWidth, SpriteHeight);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Darkness: the hall scene could not load Shane, so it plays with nobody passing. {ex}", LogLevel.Warn);
                _shane = null;
                return false;
            }

            Point? notice = FindNotice();
            if (notice == null)
            {
                _monitor.Log("Darkness: the hall scene found no clear ground on the path in front of the Community Center, so it plays with nobody passing.", LogLevel.Info);
                _shane = null;
                return false;
            }
            _notice = notice.Value;
            _backed = BackAwayFrom(_notice);

            Rectangle frame = SceneCamera.FrameInTiles();
            PlanWayIn(frame);
            PlanWayOut(frame);

            _shane.Position = _in != null ? _in.At(0f) : TilePixels(_notice);
            _shane.Facing = SceneActor.FacingUp;
            return true;
        }

        /// <summary>The spot on the path where he stops, a few tiles below the door, or the nearest
        /// ground he can stand on round it.</summary>
        private Point? FindNotice()
        {
            var aim = new Point((int)HallFacade.DoorTile.X, (int)HallFacade.DoorTile.Y + NoticeBelowDoorTiles);
            for (int r = 0; r <= NoticeSearchTiles; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        if (SceneGround.CanStandOn(_town, aim.X + dx, aim.Y + dy))
                            return new Point(aim.X + dx, aim.Y + dy);
                    }
            return null;
        }

        /// <summary>Straight back down, away from the hall, as far as the ground allows up to
        /// <see cref="HallScene.BackAwayTiles"/>.</summary>
        private Point BackAwayFrom(Point from)
        {
            int tiles = 0;
            while (tiles < HallScene.BackAwayTiles && SceneGround.CanStandOn(_town, from.X, from.Y + tiles + 1)) tiles++;
            return new Point(from.X, from.Y + tiles);
        }

        private void PlanWayIn(Rectangle frame)
        {
            List<(int X, int Y)> route = Route(ComesAlongFrom, _notice);
            if (route == null)
            {
                _inSaid = $"no route in from the road at ({ComesAlongFrom.X},{ComesAlongFrom.Y}), so he is standing on the path at his cue";
                return;
            }
            if (route[route.Count - 1] != (_notice.X, _notice.Y)) route.Add((_notice.X, _notice.Y));
            IReadOnlyList<(int X, int Y)> inShot = SceneRoute.IntoFrame(route, frame.X, frame.Y, frame.Width, frame.Height, OutOfShotTiles);
            if (inShot.Count < 2)
            {
                _inSaid = "a route in with nothing of it in shot, so he is standing on the path at his cue";
                return;
            }
            _in = new SceneWalk(inShot);
            _walkFromMs = Math.Max(0, Math.Min(HallScene.WalkInAtMs, HallScene.StopAtMs - _in.Steps * WalkMsPerTile));
            int msPerTile = (HallScene.StopAtMs - _walkFromMs) / Math.Max(1, _in.Steps);
            _walkFrameMs = Math.Max(FastestFrameMs, msPerTile / FramesPerTile);
            _inSaid = $"walks in {_in.Steps} tile(s) from ({_in.Start.X},{_in.Start.Y}) at {msPerTile} ms a tile from {_walkFromMs} ms";
        }

        private void PlanWayOut(Rectangle frame)
        {
            List<(int X, int Y)> route = Route(_backed, JoinsThePathAt);
            List<(int X, int Y)> onward = route == null ? null : Route(JoinsThePathAt, LeavesBy);
            if (onward != null && route.Count > 0)
                route.AddRange(onward.SkipWhile(t => t == route[route.Count - 1]).ToList());
            if (route != null && route.Count > 0 && route[0] != (_backed.X, _backed.Y)) route.Insert(0, (_backed.X, _backed.Y));
            IReadOnlyList<(int X, int Y)> inShot = route == null
                ? null
                : SceneRoute.OutOfFrame(route, frame.X, frame.Y, frame.Width, frame.Height, OutOfShotTiles);
            if (inShot == null || inShot.Count < 2)
            {
                // Straight down and out of the bottom of the shot, which is still away from the hall.
                var down = new List<(int X, int Y)>();
                for (int y = _backed.Y; y <= frame.Bottom + OutOfShotTiles; y++) down.Add((_backed.X, y));
                inShot = down;
                _outSaid = $"no route toward the path at ({LeavesBy.X},{LeavesBy.Y}), so he runs straight down out of the shot";
            }
            _out = new SceneWalk(inShot);
            _runMsPerTile = Math.Max(FastestRunMsPerTile, Math.Min(SlowestRunMsPerTile, (HallScene.HoldAtMs - HallScene.RunOutAtMs) / Math.Max(1, _out.Steps)));
            if (_outSaid == "no route out")
                _outSaid = $"hurries off toward home {_out.Steps} tile(s) to ({_out.End.X},{_out.End.Y}) at {_runMsPerTile} ms a tile";
        }

        /// <summary>The game's own schedule route between two tiles in Town, first tile first, or
        /// null when the pathfinder finds none or refuses.</summary>
        private List<(int X, int Y)> Route(Point from, Point to)
        {
            try
            {
                Stack<Point> path = PathFindController.findPathForNPCSchedules(from, to, _town, PathfinderLimit);
                if (path == null || path.Count == 0) return null;
                return path.Select(p => (p.X, p.Y)).ToList();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Darkness: the pathfinder refused Shane's route from ({from.X},{from.Y}) to ({to.X},{to.Y}): {ex.Message}", LogLevel.Trace);
                return null;
            }
        }

        // ---------------------------------------------------------------- moving

        /// <summary>Where he is and what he is doing this instant.</summary>
        public void Move(int elapsed)
        {
            if (_shane == null) return;
            _shane.Lift = 0f;
            _shane.StepMs = _walkFrameMs > 0 ? _walkFrameMs : _shane.StepMs;

            if (elapsed < HallScene.StopAtMs)
            {
                if (_in == null)
                {
                    Stand(SceneActor.FacingUp);
                }
                else
                {
                    float tiles = _in.Steps * Math.Max(0, elapsed - _walkFromMs) / (float)Math.Max(1, HallScene.StopAtMs - _walkFromMs);
                    _shane.Position = _in.At(tiles);
                    _shane.Facing = _in.FacingAt(tiles, false);
                    _shane.Walking = elapsed >= _walkFromMs;
                }
            }
            else if (elapsed < HallScene.BackAwayAtMs)
            {
                Stand(SceneActor.FacingUp);
                int intoJump = elapsed - HallScene.JumpAtMs;
                if (intoJump >= 0 && intoJump < HallScene.JumpLengthMs) _shane.Lift = HallScene.JumpLift(intoJump);
            }
            else if (elapsed < HallScene.RunOutAtMs)
            {
                // Backing away: down the path, eyes still on the hall.
                float across = (elapsed - HallScene.BackAwayAtMs) / (float)HallScene.BackAwayMs;
                _shane.Position = Vector2.Lerp(TilePixels(_notice), TilePixels(_backed), Math.Min(1f, across));
                _shane.Facing = SceneActor.FacingUp;
                _shane.Walking = _backed != _notice;
            }
            else
            {
                float tiles = (elapsed - HallScene.RunOutAtMs) / (float)_runMsPerTile;
                _shane.Position = _out.At(tiles);
                _shane.Facing = _out.FacingAt(Math.Min(tiles, _out.Steps), false);
                _shane.Walking = true;
                _shane.StepMs = Math.Max(FastestFrameMs, _runMsPerTile / FramesPerTile);
            }
            _shane.Animate(elapsed);
        }

        private void Stand(int facing)
        {
            _shane.Position = TilePixels(_notice);
            _shane.Facing = facing;
            _shane.Walking = false;
        }

        /// <summary>He is drawn from the moment he starts walking in, or from his cue when he has no
        /// way in. He is never drawn before, which would read as a man who had been watching the hall
        /// all along. He runs clean off the shot at the end, so there is no far cut-off to test.</summary>
        public void Draw(SpriteBatch b, int elapsed)
        {
            if (_shane == null) return;
            int from = _in != null ? _walkFromMs : HallScene.WalkInAtMs;
            if (elapsed < from) return;
            _shane.Draw(b, SceneCamera.NightTint);
        }

        private static Vector2 TilePixels(Point tile) => new Vector2(tile.X, tile.Y) * SceneCamera.TileSize;

        /// <summary>For the log.</summary>
        public string Describe()
        {
            if (_shane == null) return "nobody passing";
            return $"Shane {_inSaid}, stops on the path at ({_notice.X},{_notice.Y}), backs away to ({_backed.X},{_backed.Y}), {_outSaid}, drawn from a sheet of {_shane.Describe()}";
        }
    }
}
