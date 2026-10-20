using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.Tools;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        internal const string FishSpotsUsage =
            "Debug: how deep the water gets where each fish bites, and which fish never get a quality star. Read-only except plant. " +
            "Usage: tly_fishspots [itemId] | tly_fishspots plant <bundleKey> (live board and stored board: the bundle's first slot becomes a gold Goby, for checking the load-time repair) | " +
            "tly_fishspots grid <location> <x> <y> <w> <h> " +
            "(the game's own grid: a digit is a fishable tile and its clear-water distance, B water the rod cannot fish, L land the farmer can stand on, # land he cannot)";

        private void CmdFishSpots(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("tly_fishspots: load a save first.", LogLevel.Warn); return; }
            if (args.Length >= 6 && args[0] == "grid"
                && int.TryParse(args[2], out int x) && int.TryParse(args[3], out int y)
                && int.TryParse(args[4], out int w) && int.TryParse(args[5], out int h))
            {
                FishSpotsGrid(args[1], x, y, w, h);
                return;
            }
            if (args.Length >= 2 && args[0] == "plant")
            {
                FishSpotsPlant(args[1]);
                return;
            }
            string only = args.Length > 0 ? BundleParsing.NormalizeItemId(args[0]) : null;
            foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                foreach (SpawnFishData row in kv.Value?.Fish ?? new List<SpawnFishData>())
                {
                    if (row?.ItemId == null) continue;
                    string id = BundleParsing.NormalizeItemId(row.ItemId);
                    if (only != null && id != only) continue;
                    int? depth = FishCatchSpots.MaxClearWater(kv.Key, row, this.Monitor);
                    if (only == null && depth == null) continue;
                    this.Monitor.Log(
                        $"{kv.Key} {row.Id}: deepest reachable water {(depth?.ToString() ?? "unknown")} -> " +
                        (FishStarDepth.CanRollStar(depth) ? "can roll a star" : "never a star"),
                        LogLevel.Info);
                }
            IReadOnlySet<string> shallow = _enginePools?.ShallowWaterFishIds;
            this.Monitor.Log(
                $"tly_fishspots: shallow-water fish in the live pools: {(shallow == null || shallow.Count == 0 ? "none" : string.Join(", ", shallow.OrderBy(i => i, StringComparer.Ordinal)))}.",
                LogLevel.Info);
        }

        private void FishSpotsPlant(string key)
        {
            var bundles = Game1.netWorldState.Value.BundleData;
            if (!bundles.TryGetValue(key, out string value)) { this.Monitor.Log($"tly_fishspots: no bundle '{key}'.", LogLevel.Warn); return; }
            string[] fields = value.Split('/');
            string[] tokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            tokens[0] = "Goby";
            tokens[2] = "2";
            fields[2] = string.Join(" ", tokens);
            string planted = string.Join("/", fields);
            Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string> { [key] = planted });
            if (_meta?.State?.WrittenBoard != null && _meta.State.WrittenBoard.ContainsKey(key))
                _meta.State.WrittenBoard[key] = planted;
            this.Monitor.Log($"tly_fishspots: '{key}' is now '{planted}'.", LogLevel.Info);
        }

        private void FishSpotsGrid(string location, int x0, int y0, int w, int h)
        {
            GameLocation loc = Game1.getLocationFromName(location);
            if (loc?.Map == null) { this.Monitor.Log($"tly_fishspots: no map for '{location}'.", LogLevel.Warn); return; }
            var header = new StringBuilder("     ");
            for (int x = x0; x < x0 + w; x++) header.Append((char)('0' + Math.Abs(x) % 10));
            this.Monitor.Log(header.ToString(), LogLevel.Info);
            for (int y = y0; y < y0 + h; y++)
            {
                var row = new StringBuilder($"{y,4} ");
                for (int x = x0; x < x0 + w; x++)
                {
                    if (!loc.isTileOnMap(x, y)) { row.Append(' '); continue; }
                    if (loc.isTileFishable(x, y)) row.Append((char)('0' + FishingRod.distanceToLand(x, y, loc)));
                    else if (loc.isWaterTile(x, y)) row.Append('B');
                    else row.Append(loc.isTilePassable(new Vector2(x, y)) ? 'L' : '#');
                }
                this.Monitor.Log(row.ToString(), LogLevel.Info);
            }
        }
    }
}
