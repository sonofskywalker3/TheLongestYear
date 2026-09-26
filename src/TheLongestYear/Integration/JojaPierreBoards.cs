using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace TheLongestYear.Integration
{
    /// <summary>Pierre's shop shut down the way vanilla shows the closed JojaMart (Jeff, 2026-09-25).
    /// Vanilla swaps Joja's tiles to a boarded copy drawn on the Town sheet (Town.showDestroyedJoja,
    /// Town.cs 406, tile index +20). Pierre has no copy, so this builds one while the scene runs, from
    /// the game's own art and nothing shipped: a copy of the current season's Town sheet with Pierre's
    /// building tiles darkened and greyed, the closed JojaMart's boarded door pasted over his glass
    /// door, and planks cut from that same door nailed across his windows and the upstairs door. The
    /// copy is served as a tile sheet added to the Town map; Pierre's tiles point at it for the scene
    /// and are put back when the scene ends (any way it ends).
    ///
    /// Tiles read from Maps/Town (patch export, 2026-09-25): Pierre's building is tiles 38..47 x 46..56
    /// on the Buildings, Front and AlwaysFront layers; his glass door is exactly tiles 43..44 x 55..56;
    /// the closed JojaMart's boarded door is exactly tiles 95..96 x 49..50. Window rectangles are map
    /// pixels from a 6x zoom of the same export.</summary>
    internal static class JojaPierreBoards
    {
        internal const string AssetName = "Maps/TheLongestYear_PierreBoarded";
        private const string TownSheetId = "Town", SheetId = "zzTheLongestYear_PierreBoarded";
        private static readonly Rectangle Building = new(38, 46, 10, 11);
        private static readonly string[] LayerNames = { "Buildings", "Front", "AlwaysFront" };
        private static readonly Point PierreDoor = new(43, 55), JojaDoor = new(95, 49);
        private const int DoorTiles = 2, TilePx = 16;
        private const int ClosedJojaOffset = 20;            // Town.showDestroyedJoja: index + 20
        private const int ClosedJojaMinIndex = 1200;        // only Joja's own tiles have a closed copy
        private static readonly Rectangle[] Windows =
        {
            new(619, 871, 21, 25),   // the shop window left of the notice board
            new(654, 816, 18, 15),   // the small upstairs window
            new(676, 812, 20, 30),   // the upstairs balcony door
        };
        private const int PlankRow = 12, PlankHeight = 5, PlankShift = 3, PlankOverhang = 1;
        private const int ShortWindowPx = 20, ShortPlanks = 2, TallPlanks = 3;
        private const float Dark = 0.72f, Grey = 0.3f;

        private static readonly FieldInfo DestroyedJojaShown = typeof(Town).GetField("isShowingDestroyedJoja", BindingFlags.Instance | BindingFlags.NonPublic);
        // A shut shop keeps no notices: the board's "?" hint (Town.cs 1080, until the player has
        // read it) and the daily quest's "!" (Town.cs 1086) are hidden for the scene and put back.
        private static readonly FieldInfo BoardChecked = typeof(Town).GetField("playerCheckedBoard", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Town _boardTown;
        private static bool _boardWasChecked, _questWasAccepted, _noticesHidden;
        private static IMonitor _monitor;
        private static IModHelper _helper;
        private static Texture2D _built;
        private static Map _map;
        private static TileSheet _sheet;
        private static readonly List<(Layer Layer, int X, int Y, xTile.Tiles.Tile Original)> Swapped = new();

        internal static bool Active => _sheet != null || _noticesHidden;

        internal static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private static void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(AssetName) && _built != null)
                e.LoadFrom(() => _built, AssetLoadPriority.Exclusive);
        }

        /// <summary>Boards Pierre's up on the current (Town) map. Returns a log line.</summary>
        internal static string Apply(GameLocation town)
        {
            Restore();
            Map map = town.Map;
            TileSheet townSheet = map.GetTileSheet(TownSheetId) ?? throw new InvalidOperationException("the Town map has no 'Town' tile sheet");
            Texture2D source = Game1.content.Load<Texture2D>(townSheet.ImageSource);
            var pixels = new Color[source.Width * source.Height];
            source.GetData(pixels);
            var original = (Color[])pixels.Clone();
            int sheetColumns = source.Width / TilePx;

            // Pierre's tiles by map position (every layer; the same sheet index at the same place on
            // two layers is one set of pixels).
            var tilesAt = new Dictionary<(int, int), List<int>>();
            var swaps = new List<(Layer, int, int, StaticTile)>();
            foreach (string name in LayerNames)
            {
                Layer layer = map.GetLayer(name);
                if (layer == null) continue;
                for (int y = Building.Top; y < Building.Bottom; y++)
                    for (int x = Building.Left; x < Building.Right; x++)
                    {
                        if (layer.Tiles[x, y] is not StaticTile tile || tile.TileSheet != townSheet) continue;
                        if (!tilesAt.TryGetValue((x, y), out var list)) tilesAt[(x, y)] = list = new List<int>();
                        list.Add(tile.TileIndex);
                        swaps.Add((layer, x, y, tile));
                    }
            }

            var darkened = new HashSet<int>();
            foreach (List<int> list in tilesAt.Values)
                foreach (int index in list)
                    if (darkened.Add(index)) Darken(pixels, sheetColumns, source.Width, index);

            Color[,] door = ClosedJojaDoor(town, map, townSheet, original, sheetColumns, source.Width);
            string boards = "no boards (the closed JojaMart door was not found)";
            if (door != null)
            {
                for (int py = 0; py < DoorTiles * TilePx; py++)
                    for (int px = 0; px < DoorTiles * TilePx; px++)
                        Paint(pixels, tilesAt, sheetColumns, source.Width, PierreDoor.X * TilePx + px, PierreDoor.Y * TilePx + py, door[px, py]);
                foreach (Rectangle window in Windows)
                {
                    int planks = window.Height < ShortWindowPx ? ShortPlanks : TallPlanks;
                    for (int k = 0; k < planks; k++)
                    {
                        int top = window.Y + (int)((k + 0.5f) * window.Height / planks) - PlankHeight / 2;
                        for (int py = 0; py < PlankHeight; py++)
                            for (int px = -PlankOverhang; px < window.Width + PlankOverhang; px++)
                                Paint(pixels, tilesAt, sheetColumns, source.Width, window.X + px, top + py,
                                    door[(px + PlankShift + DoorTiles * TilePx) % (DoorTiles * TilePx), PlankRow + py]);
                    }
                }
                boards = $"door and {Windows.Length} windows boarded";
            }

            // One texture for the whole session, refilled on each run: SMAPI's cache and the map
            // display device hold this instance, so it is never disposed under them, and a replay
            // allocates nothing new. Only a sheet of another size (a map mod) gets a fresh one.
            if (_built == null || _built.Width != source.Width || _built.Height != source.Height)
            {
                _built = new Texture2D(Game1.graphics.GraphicsDevice, source.Width, source.Height);
                _built.SetData(pixels);
                _helper.GameContent.InvalidateCache(AssetName);
            }
            else
            {
                _built.SetData(pixels);
            }

            TileSheet stale = map.GetTileSheet(SheetId);
            if (stale != null) map.RemoveTileSheet(stale);
            _sheet = new TileSheet(SheetId, map, AssetName, townSheet.SheetSize, townSheet.TileSize);
            map.AddTileSheet(_sheet);
            map.LoadTileSheets(Game1.mapDisplayDevice);
            _map = map;
            HideNotices(town);
            foreach (var (layer, x, y, tile) in swaps)
            {
                var boarded = new StaticTile(layer, _sheet, tile.BlendMode, tile.TileIndex);
                foreach (var property in tile.Properties) boarded.Properties[property.Key] = property.Value;
                layer.Tiles[x, y] = boarded;
                Swapped.Add((layer, x, y, tile));
            }
            return $"{swaps.Count} tiles swapped from {townSheet.ImageSource}; {darkened.Count} sheet tiles darkened; {boards}";
        }

        /// <summary>Puts Pierre's own tiles back and drops the boarded sheet. Never throws.</summary>
        internal static void Restore()
        {
            try
            {
                foreach (var (layer, x, y, tile) in Swapped)
                    layer.Tiles[x, y] = tile;
                Swapped.Clear();
                if (_map != null && _sheet != null && _map.GetTileSheet(SheetId) == _sheet)
                    _map.RemoveTileSheet(_sheet);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Pierre's boards: restore failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
            }
            _sheet = null;
            _map = null;
            try { ShowNotices(); }
            catch (Exception ex) { _monitor?.Log($"Pierre's boards: notices not restored ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn); }
        }

        private static void HideNotices(GameLocation town)
        {
            if (town is not Town t || BoardChecked == null) return;
            _boardTown = t;
            _boardWasChecked = (bool)BoardChecked.GetValue(t);
            _questWasAccepted = Game1.player.acceptedDailyQuest.Value;
            BoardChecked.SetValue(t, true);
            Game1.player.acceptedDailyQuest.Value = true;
            _noticesHidden = true;
        }

        private static void ShowNotices()
        {
            if (!_noticesHidden) return;
            _noticesHidden = false;
            BoardChecked?.SetValue(_boardTown, _boardWasChecked);
            Game1.player.acceptedDailyQuest.Value = _questWasAccepted;
            _boardTown = null;
        }

        /// <summary>The closed JojaMart's boarded door (the +20 copy of its door tiles), read from the
        /// untouched sheet; null when the map does not have Joja's door where vanilla has it.</summary>
        private static Color[,] ClosedJojaDoor(GameLocation town, Map map, TileSheet townSheet, Color[] sheet, int columns, int width)
        {
            Layer buildings = map.GetLayer("Buildings");
            bool alreadyClosed = town is Town && DestroyedJojaShown?.GetValue(town) is true;
            var door = new Color[DoorTiles * TilePx, DoorTiles * TilePx];
            for (int ty = 0; ty < DoorTiles; ty++)
                for (int tx = 0; tx < DoorTiles; tx++)
                {
                    if (buildings?.Tiles[JojaDoor.X + tx, JojaDoor.Y + ty] is not StaticTile tile || tile.TileSheet != townSheet) return null;
                    int index = tile.TileIndex;
                    if (!alreadyClosed)
                    {
                        if (index <= ClosedJojaMinIndex) return null;
                        index += ClosedJojaOffset;
                    }
                    for (int py = 0; py < TilePx; py++)
                        for (int px = 0; px < TilePx; px++)
                            door[tx * TilePx + px, ty * TilePx + py] = sheet[SheetPixel(index, px, py, columns, width)];
                }
            return door;
        }

        private static void Darken(Color[] pixels, int columns, int width, int index)
        {
            for (int py = 0; py < TilePx; py++)
                for (int px = 0; px < TilePx; px++)
                {
                    int i = SheetPixel(index, px, py, columns, width);
                    Color c = pixels[i];
                    if (c.A == 0) continue;
                    float grey = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
                    pixels[i] = new Color(
                        (byte)((c.R * (1 - Grey) + grey * Grey) * Dark),
                        (byte)((c.G * (1 - Grey) + grey * Grey) * Dark),
                        (byte)((c.B * (1 - Grey) + grey * Grey) * Dark), c.A);
                }
        }

        /// <summary>Paints map pixel (mx, my) of Pierre's building into every one of his tiles there.</summary>
        private static void Paint(Color[] pixels, Dictionary<(int, int), List<int>> tilesAt, int columns, int width, int mx, int my, Color colour)
        {
            if (!tilesAt.TryGetValue((mx / TilePx, my / TilePx), out var list)) return;
            foreach (int index in list)
                pixels[SheetPixel(index, mx % TilePx, my % TilePx, columns, width)] = colour;
        }

        private static int SheetPixel(int index, int px, int py, int columns, int width)
            => (index / columns * TilePx + py) * width + index % columns * TilePx + px;
    }
}
