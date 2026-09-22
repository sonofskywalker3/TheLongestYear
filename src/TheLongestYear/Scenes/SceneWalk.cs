using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheLongestYear.Scenes
{
    /// <summary>A walk a strike scene plays back (spec 2026-09-21): a list of tiles from
    /// <see cref="TheLongestYear.Core.Sabotage.ScenePath"/>, and where along it a figure is at a
    /// given moment. The thief walks it forwards and then backwards, the hall's Shane walks it,
    /// backs up two tiles along it and then runs the whole of it out.
    ///
    /// It lives on its own because both scenes need exactly the same two answers and neither wants
    /// the other's copy of them.</summary>
    internal sealed class SceneWalk
    {
        private const int TileSize = 64;

        private readonly IReadOnlyList<(int X, int Y)> _tiles;

        public SceneWalk(IReadOnlyList<(int X, int Y)> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (tiles.Count == 0) throw new ArgumentException("A walk needs at least one tile.", nameof(tiles));
            _tiles = tiles;
        }

        /// <summary>How many tiles are walked, which is one less than the number of tiles in it.</summary>
        public int Steps => _tiles.Count - 1;

        /// <summary>The tile the walk starts on.</summary>
        public (int X, int Y) Start => _tiles[0];

        /// <summary>The tile the walk ends on.</summary>
        public (int X, int Y) End => _tiles[_tiles.Count - 1];

        /// <summary>Where a figure <paramref name="tilesIn"/> tiles along the walk is, in world
        /// pixels (the top left corner of the tile it is standing on).
        ///
        /// It is deliberately NOT clamped at the near end. A figure who runs out of the shot carries
        /// on past the first tile along the walk's OVERALL direction, end to start, which is the way
        /// he came. Following the first leg alone instead would send him along whatever the last
        /// step happened to be, and a walk whose first step is sideways would leave him running
        /// across the bottom of the frame for the rest of the scene (caught on the hall's first
        /// overnight frames, 2026-09-21).</summary>
        public Vector2 At(float tilesIn)
        {
            if (Steps == 0) return new Vector2(_tiles[0].X, _tiles[0].Y) * TileSize;
            if (tilesIn < 0f) return PastTheStart(-tilesIn);
            int leg = Math.Max(0, Math.Min(Steps - 1, (int)Math.Floor(tilesIn)));
            float across = tilesIn - leg;
            var from = new Vector2(_tiles[leg].X, _tiles[leg].Y);
            var to = new Vector2(_tiles[leg + 1].X, _tiles[leg + 1].Y);
            return (from + (to - from) * across) * TileSize;
        }

        /// <summary>Where a figure is <paramref name="tiles"/> tiles beyond the start of the walk,
        /// carrying on away from its end.</summary>
        private Vector2 PastTheStart(float tiles)
        {
            var start = new Vector2(Start.X, Start.Y);
            Vector2 away = start - new Vector2(End.X, End.Y);
            if (away == Vector2.Zero) return start * TileSize;
            away.Normalize();
            return (start + away * tiles) * TileSize;
        }

        /// <summary>Which way a figure on this leg of the walk is facing, as a
        /// <see cref="SceneActor"/> facing constant. <paramref name="backwards"/> is for a figure
        /// travelling the walk the other way, who faces where he is going.</summary>
        public int FacingAt(float tilesIn, bool backwards)
        {
            if (Steps == 0) return SceneActor.FacingDown;
            int leg = Math.Max(0, Math.Min(Steps - 1, (int)Math.Floor(tilesIn)));
            (int X, int Y) from = _tiles[leg];
            (int X, int Y) to = _tiles[leg + 1];
            int dx = backwards ? from.X - to.X : to.X - from.X;
            int dy = backwards ? from.Y - to.Y : to.Y - from.Y;
            if (dx != 0) return dx > 0 ? SceneActor.FacingRight : SceneActor.FacingLeft;
            if (dy != 0) return dy > 0 ? SceneActor.FacingDown : SceneActor.FacingUp;
            return SceneActor.FacingDown;
        }
    }
}
