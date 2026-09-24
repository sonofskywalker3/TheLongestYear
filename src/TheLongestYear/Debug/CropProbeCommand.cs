using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_cropprobe: headless check for the growth boosts on the Farm (tanky24u, Nexus bug
    /// 2026-09-23). <c>plant &lt;seedId&gt; &lt;count&gt;</c> hoes, plants and waters free tiles
    /// (the player must be standing on the Farm); <c>water</c> waters every planted tile; no args
    /// reports the crops. A wild seed crop that is ready to pick while still a crop is the bug:
    /// vanilla turns it into forage the night it finishes, so a correct morning shows zero.</summary>
    internal static class CropProbeCommand
    {
        public const string Usage =
            "Debug: probe Farm crops. Usage: tly_cropprobe [plant <seedId> <count> | water]. No args reports.";

        private const int Watered = 1;
        private const int SpawnedForageMarker = 724519;

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farm farm = Game1.getFarm();
            string verb = args.Length > 0 ? args[0] : "report";
            switch (verb)
            {
                case "plant": Plant(monitor, farm, args); break;
                case "water": monitor.Log($"tly_cropprobe: watered {Water(farm)} tiles.", LogLevel.Info); break;
                default: Report(monitor, farm); break;
            }
        }

        private static void Plant(IMonitor monitor, Farm farm, string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[2], out int count) || count <= 0)
            {
                monitor.Log("Usage: tly_cropprobe plant <seedId> <count>", LogLevel.Warn);
                return;
            }
            if (Game1.player.currentLocation != farm)
            {
                monitor.Log("tly_cropprobe: stand on the Farm first (debug warp Farm 64 15).", LogLevel.Warn);
                return;
            }
            string seedId = args[1];
            int planted = 0;
            for (int y = 0; y < farm.Map.Layers[0].LayerHeight && planted < count; y++)
                for (int x = 0; x < farm.Map.Layers[0].LayerWidth && planted < count; x++)
                {
                    var tile = new Vector2(x, y);
                    if (farm.doesTileHaveProperty(x, y, "Diggable", "Back") == null) continue;
                    if (!farm.isTilePassable(tile) || farm.objects.ContainsKey(tile)
                        || farm.terrainFeatures.ContainsKey(tile) || farm.IsTileBlockedBy(tile))
                        continue;
                    var dirt = new HoeDirt(Watered, farm);
                    farm.terrainFeatures.Add(tile, dirt);
                    if (dirt.plant(seedId, Game1.player, isFertilizer: false))
                        planted++;
                    else
                        farm.terrainFeatures.Remove(tile);
                }
            monitor.Log($"tly_cropprobe: planted {planted} of {count} ({seedId}).", LogLevel.Info);
        }

        private static int Water(Farm farm)
        {
            int watered = 0;
            foreach (HoeDirt dirt in farm.terrainFeatures.Values.OfType<HoeDirt>())
            {
                if (dirt.crop == null) continue;
                dirt.state.Value = Watered;
                watered++;
            }
            return watered;
        }

        private static void Report(IMonitor monitor, Farm farm)
        {
            int crops = 0, wild = 0, wildStuck = 0, readyShownUnripe = 0, ready = 0;
            foreach (HoeDirt dirt in farm.terrainFeatures.Values.OfType<HoeDirt>())
            {
                Crop crop = dirt.crop;
                if (crop == null) continue;
                crops++;
                bool finalPhase = crop.currentPhase.Value >= crop.phaseDays.Count - 1;
                bool harvestable = dirt.readyForHarvest();
                if (harvestable) ready++;
                if (crop.isWildSeedCrop())
                {
                    wild++;
                    if (finalPhase && !crop.fullyGrown.Value) wildStuck++;
                    if (harvestable && crop.phaseToShow.Value != -1) readyShownUnripe++;
                }
            }
            int forage = farm.objects.Values.Count(o => o.IsSpawnedObject && o.SpecialVariable == SpawnedForageMarker);
            monitor.Log(
                $"tly_cropprobe: day {Game1.dayOfMonth} {Game1.currentSeason}: crops={crops} ready={ready} wild={wild} "
                + $"wildStuck={wildStuck} readyShownUnripe={readyShownUnripe} wildSeedForage={forage}.",
                LogLevel.Info);
        }
    }
}
