using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using xTile.Layers;
using xTile.Tiles;

namespace TheLongestYear.Integration
{
    /// <summary>The Community Center turned into the Joja warehouse for the bad ending (Jeff,
    /// 2026-10-02). Vanilla's Joja route draws it from two pieces (Town.cs, PC 1.6 decompile):
    ///
    /// - The restored building's tiles: <c>refurbishCommunityCenter</c> (Town.cs 358) adds 12 to every
    ///   Town-sheet tile above index 1200 in the CC bounds (47,11 to 58,20 inclusive) on the Back,
    ///   Buildings, Front and AlwaysFront layers.
    /// - The Joja facade over them: while the private <c>ccJoja</c> flag is set, <c>Town.draw</c>
    ///   (Town.cs 1076) and <c>drawAboveAlwaysFrontLayer</c> (Town.cs 1124) draw the warehouse front
    ///   from Cursors, unless the movie theater has replaced the CC (map override "Town-TheaterCC").
    ///
    /// Vanilla only gets there from the <c>JojaMember</c> mail (Town.cs 365, 544). This borrows the
    /// look without the mail: it sets <c>ccJoja</c> and shifts the tiles itself, both in memory, and
    /// <see cref="Restore"/> puts the flag and every shifted tile back when the scene ends (any way it
    /// ends). <c>ccRefurbished</c> and the player's mail are never touched, so nothing that keys on
    /// them (Joja mail, CC events, Morris, the festival decorations, the night lights) can react.
    /// Neither field is saved (plain private fields on Town), and a load reads the map fresh.</summary>
    internal static class JojaWarehouse
    {
        private static readonly Rectangle Bounds = new(47, 11, 12, 10);   // vanilla's ccBounds, its inclusive Right/Bottom loop
        private static readonly string[] LayerNames = { "Back", "Buildings", "Front", "AlwaysFront" };
        private const string TownSheetId = "Town";
        private const int RefurbishedOffset = 12, RefurbishMinIndex = 1200;
        private const string TheaterOverride = "Town-TheaterCC";

        private static readonly FieldInfo CcJoja = typeof(Town).GetField("ccJoja", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CcRefurbished = typeof(Town).GetField("ccRefurbished", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AppliedOverrides = typeof(GameLocation).GetField("_appliedMapOverrides", BindingFlags.Instance | BindingFlags.NonPublic);

        private static IMonitor _monitor;
        private static Town _town;
        private static bool _wasJoja;
        private static readonly List<(Layer Layer, int X, int Y, Tile Tile, int Index)> Shifted = new();

        internal static bool Active => _town != null;

        internal static void Register(IMonitor monitor) => _monitor = monitor;

        /// <summary>Puts the warehouse up on the current (Town) map. Returns a log line.</summary>
        internal static string Apply(GameLocation location)
        {
            Restore();
            if (location is not Town town) throw new InvalidOperationException($"not in Town ({location?.Name})");
            if (CcJoja == null || CcRefurbished == null) throw new InvalidOperationException("Town.ccJoja or Town.ccRefurbished not found");
            if (AppliedOverrides?.GetValue(town) is HashSet<string> applied && applied.Contains(TheaterOverride))
                return "the Community Center is the movie theater on this save; left as it is";

            _town = town;
            _wasJoja = (bool)CcJoja.GetValue(town);
            bool refurbished = (bool)CcRefurbished.GetValue(town);
            if (!refurbished)
            {
                // The same shift refurbishCommunityCenter makes, remembered tile by tile.
                foreach (string name in LayerNames)
                {
                    Layer layer = town.Map.GetLayer(name);
                    if (layer == null) continue;
                    for (int x = Bounds.Left; x < Bounds.Right; x++)
                        for (int y = Bounds.Top; y < Bounds.Bottom; y++)
                        {
                            Tile tile = layer.Tiles[x, y];
                            if (tile?.TileSheet.Id != TownSheetId || tile.TileIndex <= RefurbishMinIndex) continue;
                            Shifted.Add((layer, x, y, tile, tile.TileIndex));
                            tile.TileIndex += RefurbishedOffset;
                        }
                }
            }
            CcJoja.SetValue(town, true);
            return $"Joja facade on (ccJoja was {_wasJoja}); {(refurbished ? "the hall was already restored, no tiles shifted" : $"{Shifted.Count} tiles shifted to the restored hall")}";
        }

        /// <summary>Puts the flag and every shifted tile back. Never throws.</summary>
        internal static void Restore()
        {
            if (_town == null) return;
            try
            {
                foreach (var (layer, x, y, tile, index) in Shifted)
                    if (layer.Tiles[x, y] == tile) tile.TileIndex = index;
                CcJoja?.SetValue(_town, _wasJoja);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Joja warehouse: restore failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
            }
            Shifted.Clear();
            _town = null;
        }
    }
}
