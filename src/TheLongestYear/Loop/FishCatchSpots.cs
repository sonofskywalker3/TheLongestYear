using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using TheLongestYear.Core;
using xTile;

namespace TheLongestYear.Loop
{
    /// <summary>Reads a location's live map for <see cref="FishCastReach"/>: which tiles are water,
    /// which the rod can fish, where the farmer can stand and where he walks in from. Gives each
    /// Data/Locations fish row the deepest clear water a cast reaches where that row bites, so the
    /// pool builder can tell a fish that only bites in shallow water (the Forest waterfall pool's
    /// Goby) from one that can roll a star (<see cref="FishStarDepth"/>). Read from the map at run
    /// time, so a modded map or a modded fish row gets the same answer as vanilla.
    ///
    /// Null (no change: the fish keeps its star) whenever the answer cannot be read: no world
    /// loaded, a key that is not a live location (farm-type rows, mine floors), no way in.</summary>
    internal static class FishCatchSpots
    {
        /// <summary>One analysed map, rebuilt when the location's Map object changes.</summary>
        private sealed class Analysis
        {
            public Map Map;
            public LiveMap Tiles;
            /// <summary>Null when no warp leads in: nothing on the map can be cast to.</summary>
            public IReadOnlyList<CastLanding> Landings;
            public ClearWaterMemo Depths;
            /// <summary>Each row's answer, by the row object: every GameDataPools build in a load
            /// reads the same Data/Locations rows, and a re-loaded asset brings new row objects.</summary>
            public readonly ConditionalWeakTable<SpawnFishData, StrongBox<int>> Rows = new();
        }

        private static readonly ConditionalWeakTable<GameLocation, Analysis> Cache = new();

        /// <summary>Time spent in <see cref="MaxClearWater"/> since <see cref="ResetTiming"/>.</summary>
        private static readonly Stopwatch Elapsed = new();
        private static int _rowsAsked;

        /// <summary>Above this, the load's fish depth time is worth an Info line.</summary>
        private const long SlowLoadMs = 250;

        /// <summary>The deepest clear-water distance a year-one cast reaches where this row
        /// bites, <see cref="FishCastReach.NoCastableSpot"/> when no cast does, or null when
        /// the map cannot be read.</summary>
        public static int? MaxClearWater(string locationKey, SpawnFishData row, IMonitor monitor)
        {
            if (row == null || Game1.locations == null || Game1.locations.Count == 0) return null;
            _rowsAsked++;
            Elapsed.Start();
            try
            {
                GameLocation loc = Game1.getLocationFromName(locationKey);
                if (loc?.Map == null || !string.Equals(loc.Name, locationKey, StringComparison.OrdinalIgnoreCase))
                    return null;
                Analysis analysis = AnalysisFor(loc);
                if (analysis.Landings == null) return null;
                if (analysis.Rows.TryGetValue(row, out StrongBox<int> known)) return known.Value;

                Rectangle? bobber = row.BobberPosition;
                Rectangle? player = row.PlayerPosition;
                int depth = FishCastReach.MaxClearWater(
                    analysis.Depths, analysis.Landings,
                    FishCastReach.RowBitesAt(
                        bobber is Rectangle b ? new TileRect(b.X, b.Y, b.Width, b.Height) : null,
                        row.FishAreaId, (x, y) => AreaAt(loc, x, y)),
                    row.MinDistanceFromShore, row.MaxDistanceFromShore,
                    player is Rectangle p ? new TileRect(p.X, p.Y, p.Width, p.Height) : null);
                analysis.Rows.AddOrUpdate(row, new StrongBox<int>(depth));
                return depth;
            }
            catch (Exception ex)
            {
                // A broken map must not cost the whole fish pool (GameDataPools degrades per table).
                monitor?.Log($"Fish depth: could not read the {locationKey} map for '{row.Id}' ({ex.Message}); its fish keep their stars.", LogLevel.Trace);
                return null;
            }
            finally
            {
                Elapsed.Stop();
            }
        }

        /// <summary>Starts a load's count from zero.</summary>
        public static void ResetTiming()
        {
            Elapsed.Reset();
            _rowsAsked = 0;
        }

        /// <summary>One line per load: how long the fish depth reads took across every pool build.</summary>
        public static void LogTiming(IMonitor monitor)
        {
            long ms = Elapsed.ElapsedMilliseconds;
            monitor?.Log($"Fish depth: {_rowsAsked} fish row(s) read in {ms} ms this load.",
                ms > SlowLoadMs ? LogLevel.Info : LogLevel.Debug);
            ResetTiming();
        }

        /// <summary>The tile's fish area (TryGetFishAreaForTile, which falls back to the location's
        /// area with no Position), or null; a row with a FishAreaId bites only where it matches.</summary>
        private static string AreaAt(GameLocation loc, int x, int y)
            => loc.TryGetFishAreaForTile(new Vector2(x, y), out string id, out _) ? id : null;

        private static Analysis AnalysisFor(GameLocation loc)
        {
            if (Cache.TryGetValue(loc, out Analysis cached) && ReferenceEquals(cached.Map, loc.Map))
                return cached;
            // Cached even with no way in, so a map with no entry is not re-read by every build.
            var tiles = new LiveMap(loc);
            List<(int X, int Y)> entries = EntryTiles(loc);
            var analysis = new Analysis
            {
                Map = loc.Map,
                Tiles = tiles,
                Landings = entries.Count == 0 ? null : FishCastReach.Landings(tiles, entries, FishStarDepth.YearOneFishingLevel),
                Depths = new ClearWaterMemo(tiles),
            };
            Cache.AddOrUpdate(loc, analysis);
            return analysis;
        }

        /// <summary>Where the farmer walks in: every warp into this location from another one
        /// lands on its target tile, and the tiles next to this location's own warps are the
        /// way back out. Any one of them on the main walkable area is enough for the flood.</summary>
        private static List<(int X, int Y)> EntryTiles(GameLocation loc)
        {
            var entries = new List<(int X, int Y)>();
            foreach (GameLocation other in Game1.locations)
            {
                if (other?.warps == null) continue;
                foreach (Warp warp in other.warps)
                    if (warp != null && string.Equals(warp.TargetName, loc.Name, StringComparison.OrdinalIgnoreCase))
                        entries.Add((warp.TargetX, warp.TargetY));
            }
            foreach (Warp warp in loc.warps)
            {
                if (warp == null) continue;
                entries.Add((warp.X, warp.Y));
                entries.Add((warp.X + 1, warp.Y));
                entries.Add((warp.X - 1, warp.Y));
                entries.Add((warp.X, warp.Y + 1));
                entries.Add((warp.X, warp.Y - 1));
            }
            return entries;
        }

        /// <summary>The map's tiles, read once into arrays (the walk asks each tile many times).</summary>
        internal sealed class LiveMap : IFishingMap
        {
            private readonly bool[,] _water;
            private readonly bool[,] _fishable;
            private readonly bool[,] _standable;

            public LiveMap(GameLocation loc)
            {
                var back = loc.Map.RequireLayer("Back");
                Width = back.LayerWidth;
                Height = back.LayerHeight;
                _water = new bool[Width, Height];
                _fishable = new bool[Width, Height];
                _standable = new bool[Width, Height];
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        bool water = loc.isWaterTile(x, y);
                        _water[x, y] = water;
                        _fishable[x, y] = loc.isTileFishable(x, y);
                        _standable[x, y] = !water && back.Tiles[x, y] != null && loc.isTilePassable(new Vector2(x, y));
                    }
            }

            public int Width { get; }
            public int Height { get; }
            public bool IsWater(int x, int y) => _water[x, y];
            public bool IsFishable(int x, int y) => _fishable[x, y];
            public bool IsStandable(int x, int y) => _standable[x, y];
        }
    }
}
