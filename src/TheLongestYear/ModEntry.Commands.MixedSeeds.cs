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

        /// <summary><c>tly_mixedseedpool</c>: the Mixed Seeds pool another mod keeps as this mod reads it,
        /// each seed's share of a bag by season, the bags a farmer can gather by week, and what that
        /// lets a board ask of each crop only the pool gives. Read-only.</summary>
        private void CmdMixedSeedPool(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            TheLongestYear.Core.MixedSeedInputs inputs = _enginePools?.MixedSeeds;
            if (inputs == null || inputs.Pool.Count == 0)
            {
                this.Monitor.Log("tly_mixedseedpool: no mod keeps a Mixed Seeds pool here (Item Extensions missing, switched off, or nothing in its pool); Mixed Seeds are vanilla's.", LogLevel.Info);
                return;
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            this.Monitor.Log($"tly_mixedseedpool: {inputs.Pool.Count} row(s), vanilla slot x{TheLongestYear.Core.MixedSeedPool.VanillaSlots(inputs.Pool)}.", LogLevel.Info);
            foreach (var row in inputs.Pool)
                this.Monitor.Log($"  row {row.SeedId} x{row.Weight} when [{row.Condition}]", LogLevel.Info);
            var seasons = inputs.Crops.ToDictionary(c => c.SeedId, c => c.Seasons, System.StringComparer.Ordinal);
            IReadOnlyList<TheLongestYear.Core.Season> SeasonsOf(string seed) => seasons.TryGetValue(seed, out var s) ? s : null;
            foreach (var season in new[] { TheLongestYear.Core.Season.Spring, TheLongestYear.Core.Season.Summer, TheLongestYear.Core.Season.Fall })
                this.Monitor.Log($"  shares {season}: " + string.Join(", ", TheLongestYear.Core.MixedSeedPool.Shares(season, false, inputs.Pool, SeasonsOf)
                    .OrderByDescending(p => p.Value).Select(p => $"{p.Key} {(100 * p.Value).ToString("0.0", inv)}%")), LogLevel.Info);
            this.Monitor.Log("  shares greenhouse (Winter): " + string.Join(", ", TheLongestYear.Core.MixedSeedPool.Shares(TheLongestYear.Core.Season.Winter, true, inputs.Pool, SeasonsOf)
                .OrderByDescending(p => p.Value).Select(p => $"{p.Key} {(100 * p.Value).ToString("0.0", inv)}%")), LogLevel.Info);
            foreach (var arrival in TheLongestYear.Core.MixedSeedSupply.Arrivals)
                this.Monitor.Log($"  supply day {arrival.DayOfYear} (week {TheLongestYear.Core.Calendar.WeekOfYear((arrival.DayOfYear - 1) / TheLongestYear.Core.Calendar.DaysPerMonth, TheLongestYear.Core.Calendar.DayOfMonthOf(arrival.DayOfYear))}): {arrival.Bags.ToString("0.0", inv)} bag(s), {arrival.Source}", LogLevel.Info);
            var bases = TheLongestYear.Core.MixedSeedSupply.Bases(inputs);
            foreach (var crop in inputs.Crops.OrderBy(c => c.SeedId, System.StringComparer.Ordinal))
            {
                string basis = bases.TryGetValue(crop.HarvestId, out double[] b) ? "basis " + string.Join("/", b.Select(v => v.ToString("0.0", inv))) : "no basis";
                string served = _availability != null && _availability.MixedSeedCropBases.TryGetValue(crop.HarvestId, out double[] m)
                    ? "; model " + string.Join("/", m.Select(v => v.ToString("0.0", inv))) : "";
                this.Monitor.Log($"  crop {crop.HarvestId} from {crop.SeedId}: seasons [{string.Join(",", crop.Seasons)}], grows {crop.GrowthDays}d, regrow {crop.RegrowDays}, least harvest {crop.HarvestMinStack}, only route {crop.OnlyRoute}; {basis}{served}", LogLevel.Info);
            }
        }

        /// <summary>Weeds whose cut never drops anything (the three ice crystals).</summary>
        private static readonly HashSet<string> BarrenWeedIds = new(System.StringComparer.Ordinal) { "319", "320", "321" };
        /// <summary>Weeds whose cut always drops Mixed Seeds (Object.cutWeed).</summary>
        private static readonly HashSet<string> SeedWeedIds = new(System.StringComparer.Ordinal) { "792", "793", "794" };

        /// <summary><c>tly_weedsweep [count | keep n]</c>: counts every weed on every map and takes them away, as a
        /// player who cut every weed everywhere today would (the same ceiling <c>tly_sweepforage</c>
        /// measures for forage). <c>count</c> only counts. A weed drops Mixed Seeds 2.5% of the time
        /// (Object.cutWeed: no Fiber half the time, then 5%), so the day's expected bags are the count
        /// over 40, plus one for each weed that always drops them.</summary>
        private void CmdWeedSweep(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            bool countOnly = args.Length > 0 && args[0].Equals("count", System.StringComparison.OrdinalIgnoreCase);
            // "keep n" leaves n weeds standing on each map, to see how fast a map grows back from a few.
            int keep = args.Length > 1 && args[0].Equals("keep", System.StringComparison.OrdinalIgnoreCase) && int.TryParse(args[1], out int kept) && kept > 0 ? kept : 0;
            var perMap = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            int total = 0, sure = 0;
            foreach (GameLocation location in Game1.locations.ToList())
            {
                if (location?.objects == null) continue;
                var weeds = location.objects.Pairs.Where(p => p.Value != null && p.Value.IsWeeds() && !BarrenWeedIds.Contains(p.Value.ItemId)).ToList();
                if (weeds.Count == 0) continue;
                perMap[location.Name] = weeds.Count;
                total += weeds.Count;
                sure += weeds.Count(p => SeedWeedIds.Contains(p.Value.ItemId));
                if (!countOnly)
                    foreach (var pair in weeds.Skip(keep)) location.objects.Remove(pair.Key);
            }
            this.Monitor.Log($"tly_weedsweep: day {Game1.stats.DaysPlayed} ({Game1.currentSeason} {Game1.dayOfMonth}), {(countOnly ? "counted" : "cut")} {total} weed(s), {sure} that always drop Mixed Seeds: " +
                string.Join(", ", perMap.OrderByDescending(m => m.Value).Select(m => $"{m.Key} {m.Value}")), LogLevel.Info);
        }
    }
}
