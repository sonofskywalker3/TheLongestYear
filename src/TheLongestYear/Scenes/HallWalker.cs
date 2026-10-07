using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Scenes
{
    /// <summary>Shane in the hall scene (Jeff, 2026-09-23 and 2026-09-25). He is NOT going to the
    /// Community Center. He has left the Saloon and taken the long way round to clear his head, and
    /// he walks PAST the hall along the dirt road below it. Level with it he sees the lit windows,
    /// stops dead, jumps, backs off two tiles still looking at it, then hurries home down the path
    /// toward the square and Marnie's ranch, where he lives.
    ///
    /// EVERY TILE HE STEPS ON IS A PATH TILE ON TOWN'S MAP. The route is designed in
    /// <see cref="HallRoute"/>, read off the map's own Back layer (Type Dirt or Wood) and tested
    /// against an export of it. A scripted walk ignores collision, so before the scene uses the
    /// route it checks every tile again on the LIVE map: the Back tile's Type must be Dirt, Stone or
    /// Wood, and <see cref="SceneGround.CanStandOn"/> must agree. A part that fails (a map mod, say)
    /// is re-routed between the same two ends with <see cref="TileRoute.Between"/>, which searches
    /// path tiles ONLY. If even that finds nothing he is left out of the scene altogether, which is
    /// better than a man walking through a fence.
    ///
    /// He is a <see cref="SceneActor"/> drawn from his sheet, never the real NPC. Nothing about him
    /// can stop the scene.</summary>
    internal sealed class HallWalker
    {
        private const string ShaneSheet = "Characters\\Shane";
        private const int SpriteWidth = 16;
        private const int SpriteHeight = 32;

        /// <summary>Back layer Types that are path, not lawn. Wood is the bridge.</summary>
        private static readonly HashSet<string> PathTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dirt", "Stone", "Wood",
        };

        /// <summary>How many tiles of the way in are kept out of shot, so he walks in over the edge
        /// of the frame. (The way home is cut by <see cref="HallRoute.WayOutInShot"/>.)</summary>
        private const int OutOfShotTiles = 3;

        /// <summary>Vanilla's walking pace, in ms a tile: a villager walks at speed 2, two pixels a
        /// tick at sixty ticks a second, so a 64 pixel tile takes 32 ticks.</summary>
        internal const int WalkMsPerTile = 533;

        /// <summary>His pace when he hurries off home, in ms a tile. Quicker than a walk, short of
        /// the farmer's run.</summary>
        internal const int HurryMsPerTile = 320;

        /// <summary>Walk frames a tile. At a walking pace this is vanilla's own 175 ms a frame.</summary>
        private const int FramesPerTile = 3;

        private readonly GameLocation _town;
        private readonly IMonitor _monitor;

        private SceneActor _shane;
        private SceneWalk _in;
        private SceneWalk _back;
        /// <summary>The way home, as tiles, played back clamped (<see cref="TileRoute.At"/>) so he
        /// can never walk past its last checked tile, whatever the screen size.</summary>
        private IReadOnlyList<(int X, int Y)> _out;
        private SceneWalk _outFacing;
        private int _walkFromMs;
        private bool[,] _pathGrid;
        private readonly List<string> _notes = new List<string>();

        public HallWalker(GameLocation town, IMonitor monitor)
        {
            _town = town ?? throw new ArgumentNullException(nameof(town));
            _monitor = monitor;
        }

        // ---------------------------------------------------------------- staging

        /// <summary>Load his sheet and check and cut his routes. <paramref name="roadFrame"/> is the
        /// tiles in shot once the camera has come down to the road, which is where he walks.
        /// False when he is left out of the scene.</summary>
        public bool Stage(Rectangle roadFrame)
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

            IReadOnlyList<(int X, int Y)> wayIn = OnPath("way in", HallRoute.WayIn);
            IReadOnlyList<(int X, int Y)> backAway = OnPath("back away", HallRoute.BackAway);
            IReadOnlyList<(int X, int Y)> wayOut = OnPath("way out", HallRoute.WayOut);
            if (wayIn == null || backAway == null || wayOut == null)
            {
                _monitor.Log($"Darkness: the hall scene found no path route for Shane on this Town ({string.Join("; ", _notes)}), so it plays with nobody passing.", LogLevel.Info);
                _shane = null;
                return false;
            }

            // The way in: what is in shot plus a few tiles out of it, and no more than he can walk
            // at a walking pace before his stop. A longer way in starts further along, never faster.
            IReadOnlyList<(int X, int Y)> inShot = SceneRoute.IntoFrame(wayIn, roadFrame.X, roadFrame.Y, roadFrame.Width, roadFrame.Height, OutOfShotTiles);
            inShot = TileRoute.LastSteps(inShot, HallScene.StopAtMs / WalkMsPerTile);
            _in = new SceneWalk(inShot);
            _walkFromMs = HallScene.StopAtMs - _in.Steps * WalkMsPerTile;

            _back = new SceneWalk(backAway);
            _out = HallRoute.WayOutInShot(wayOut, roadFrame.X, roadFrame.Y, roadFrame.Width, roadFrame.Height);
            _outFacing = new SceneWalk(_out);

            _shane.Position = _in.At(0f);
            _shane.Facing = _in.FacingAt(0f, false);
            return true;
        }

        /// <summary>The route if every tile of it is path on the live map, else the shortest walk
        /// between its two ends over path tiles only, else null.</summary>
        private IReadOnlyList<(int X, int Y)> OnPath(string part, IReadOnlyList<(int X, int Y)> route)
        {
            int off = TileRoute.FirstOffPath(route, IsTownPath);
            if (off < 0) return route;

            (int X, int Y) bad = route[off];
            _pathGrid ??= PathGrid();
            IReadOnlyList<(int X, int Y)> detour = TileRoute.Between(_pathGrid, route[0], route[route.Count - 1]);
            if (detour.Count == 0)
            {
                _notes.Add($"{part}: ({bad.X},{bad.Y}) is not path and no path joins ({route[0].X},{route[0].Y}) to ({route[route.Count - 1].X},{route[route.Count - 1].Y})");
                return null;
            }
            _notes.Add($"{part}: ({bad.X},{bad.Y}) is not path, re-routed over {detour.Count} path tile(s)");
            _monitor.Log($"Darkness: the hall scene's {part} for Shane crosses ({bad.X},{bad.Y}), which is not a path tile on this Town, so it is re-routed over path tiles only ({detour.Count} tiles).", LogLevel.Info);
            return detour;
        }

        /// <summary>A path tile on the live map: its Back tile is Dirt, Stone or Wood, and nothing
        /// stands on it.</summary>
        private bool IsTownPath(int x, int y)
        {
            string type = _town.doesTileHaveProperty(x, y, "Type", "Back");
            return type != null && PathTypes.Contains(type) && SceneGround.CanStandOn(_town, x, y);
        }

        private bool[,] PathGrid()
        {
            int width = _town.map.Layers[0].LayerWidth;
            int height = _town.map.Layers[0].LayerHeight;
            var grid = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    grid[x, y] = IsTownPath(x, y);
            return grid;
        }

        // ---------------------------------------------------------------- moving

        /// <summary>Where he is and what he is doing this instant.</summary>
        public void Move(int elapsed)
        {
            if (_shane == null) return;
            _shane.Lift = 0f;
            _shane.StepMs = WalkMsPerTile / FramesPerTile;

            if (elapsed < HallScene.StopAtMs)
            {
                float tiles = Math.Max(0, elapsed - _walkFromMs) / (float)WalkMsPerTile;
                _shane.Position = _in.At(tiles);
                _shane.Facing = _in.FacingAt(tiles, false);
                _shane.Walking = elapsed >= _walkFromMs;
            }
            else if (elapsed < HallScene.BackAwayAtMs)
            {
                // Stopped dead on the road, turned to the hall.
                _shane.Position = _in.At(_in.Steps);
                _shane.Facing = SceneActor.FacingUp;
                _shane.Walking = false;
                int intoJump = elapsed - HallScene.JumpAtMs;
                if (intoJump >= 0 && intoJump < HallScene.JumpLengthMs) _shane.Lift = HallScene.JumpLift(intoJump);
            }
            else if (elapsed < HallScene.RunOutAtMs)
            {
                // Backing off down the road, eyes still on the hall.
                float across = Math.Min(1f, (elapsed - HallScene.BackAwayAtMs) / (float)HallScene.BackAwayMs);
                _shane.Position = _back.At(across * _back.Steps);
                _shane.Facing = SceneActor.FacingUp;
                _shane.Walking = _back.Steps > 0;
                _shane.StepMs = Math.Max(1, HallScene.BackAwayMs / Math.Max(1, _back.Steps) / FramesPerTile);
            }
            else
            {
                // Clamped to the checked tiles. On any screen the scene supports he is out of shot
                // long before the end; on one tall enough to show the whole way home he is still
                // walking it when the scene fades (20 tiles at 320 ms a tile outlasts it), and even
                // then he can only ever stop on its last checked tile, never walk past it.
                float tiles = (elapsed - HallScene.RunOutAtMs) / (float)HurryMsPerTile;
                (float x, float y) = TileRoute.At(_out, tiles);
                _shane.Position = new Vector2(x, y) * SceneCamera.TileSize;
                _shane.Facing = _outFacing.FacingAt(Math.Min(tiles, _outFacing.Steps), false);
                _shane.Walking = tiles < _outFacing.Steps;
                _shane.StepMs = HurryMsPerTile / FramesPerTile;
            }
            _shane.Animate(elapsed);
        }

        /// <summary>He is drawn from the moment he starts walking. Before that he is somewhere out
        /// of shot on his way, not standing waiting. He walks clean off the shot at the end.</summary>
        public void Draw(SpriteBatch b, int elapsed)
        {
            if (_shane == null || elapsed < _walkFromMs) return;
            _shane.Draw(b, SceneCamera.NightTint);
        }

        /// <summary>For the log.</summary>
        public string Describe()
        {
            if (_shane == null) return "nobody passing";
            string notes = _notes.Count == 0 ? "every tile checked as path on this Town" : string.Join("; ", _notes);
            return $"Shane walks in {_in.Steps} tile(s) from ({_in.Start.X},{_in.Start.Y}) at {WalkMsPerTile} ms a tile from {_walkFromMs} ms, "
                + $"stops on the road at ({_in.End.X},{_in.End.Y}), backs off to ({_back.End.X},{_back.End.Y}), "
                + $"hurries home {_outFacing.Steps} tile(s) to ({_outFacing.End.X},{_outFacing.End.Y}) and no further at {HurryMsPerTile} ms a tile ({notes}), "
                + $"drawn from a sheet of {_shane.Describe()}";
        }
    }
}
