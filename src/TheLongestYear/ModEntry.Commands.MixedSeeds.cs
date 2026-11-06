using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        private const int DefaultMixedSeedPlantings = 400;
        private const int MixedSeedStack = 5;

        /// <summary>Plants Mixed Seeds again and again on the tile to the farmer's right, through the
        /// game's own placement call (so every mod's patch on the way runs as it does for a player),
        /// and tallies which seed went into the ground. The tile and the held item are put back.</summary>
        private void CmdMixedSeeds(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            int n = args.Length > 0 && int.TryParse(args[0], out int asked) && asked > 0 ? asked : DefaultMixedSeedPlantings;
            GameLocation here = Game1.currentLocation;
            Farmer p = Game1.player;
            Vector2 tile = p.Tile + new Vector2(1, 0);
            int slot = p.CurrentToolIndex;
            Item held = p.Items[slot];
            here.terrainFeatures.TryGetValue(tile, out TerrainFeature before);

            var tally = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    here.terrainFeatures.Remove(tile);
                    var dirt = new HoeDirt(0, here);
                    here.terrainFeatures.Add(tile, dirt);
                    p.Items[slot] = ItemRegistry.Create("(O)" + TheLongestYear.Core.MixedSeedsOutcome.MixedSeedsId, MixedSeedStack);
                    bool placed = Utility.tryToPlaceItem(here, (StardewValley.Object)p.Items[slot], (int)tile.X * Game1.tileSize + Game1.tileSize / 2, (int)tile.Y * Game1.tileSize + Game1.tileSize / 2);
                    string seed = placed && dirt.crop != null ? dirt.crop.netSeedIndex.Value ?? "(no seed id)" : "(not planted)";
                    tally[seed] = tally.TryGetValue(seed, out int had) ? had + 1 : 1;
                }
            }
            finally
            {
                here.terrainFeatures.Remove(tile);
                if (before != null) here.terrainFeatures.Add(tile, before);
                p.Items[slot] = held;
            }
            this.Monitor.Log($"tly_mixedseeds: {n} planting(s) at ({tile.X},{tile.Y}) in '{here.Name}', {here.GetSeason()}: " +
                string.Join(", ", tally.OrderByDescending(t => t.Value).Select(t => $"{t.Key} {t.Value} ({100.0 * t.Value / n:0.0}%)")), LogLevel.Info);
        }
    }
}
