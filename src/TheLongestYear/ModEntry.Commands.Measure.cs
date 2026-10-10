using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Where <c>tly_sweepforage</c> keeps its haul. A fixed Farm tile so the chest is
        /// the same one every day of a run and can just be opened and read at the end.</summary>
        private static readonly Microsoft.Xna.Framework.Vector2 SweepChestTile = new(64f, 16f);

        /// <summary>One chest per season, side by side on the Farm, so a single 112-day run leaves
        /// four readable hauls instead of one mixed pile. Indexed Spring, Summer, Fall, Winter -
        /// the same order as <see cref="TheLongestYear.Core.Season"/>.</summary>
        private static readonly string[] SweepSeasons = { "spring", "summer", "fall", "winter" };

        private static Microsoft.Xna.Framework.Vector2 SweepTileFor(string season)
        {
            int index = Array.IndexOf(SweepSeasons, (season ?? "").ToLowerInvariant());
            if (index < 0) index = 0;
            return new Microsoft.Xna.Framework.Vector2(SweepChestTile.X + index, SweepChestTile.Y);
        }

        /// <summary><c>tly_sweepforage [report]</c>: take every piece of spawned forage on every
        /// map and put it in the sweep chest on the Farm.
        ///
        /// A real harvest, not an estimate. It takes the objects the game's own
        /// <c>spawnObjects</c> laid down this morning (<c>Object.IsSpawnedObject</c>), which is
        /// exactly what a player who walked every map and picked up everything would end the day
        /// with. Clearing the maps also drops <c>numberOfSpawnedObjectsOnMap</c> back to 0, so
        /// tomorrow's spawn is not throttled by yesterday's leftovers - the same reason the
        /// "checked everywhere every day" assumption is the ceiling.
        ///
        /// The chest IS the tally: run it once a day across a season, then open the chest (or
        /// <c>tly_sweepforage report</c>) and read the stacks. Nothing is kept in memory, so a
        /// reload mid-run loses nothing.</summary>
        private void CmdSweepForage(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            StardewValley.Objects.Chest chest = GetOrCreateSweepChest();
            if (chest == null)
            {
                this.Monitor.Log("tly_sweepforage: could not place the sweep chest on the Farm.", LogLevel.Error);
                return;
            }

            // Every season's chest, so one 112-day run reports as four seasons.
            if (args.Length > 0 && args[0].Equals("report", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string season in SweepSeasons)
                {
                    StardewValley.Objects.Chest seasonChest = GetOrCreateSweepChest(season);
                    if (seasonChest != null)
                        ReportSweepChest(seasonChest, season);
                }
                return;
            }

            // Start a fresh measurement run. Also drops whatever forage is lying about from
            // before the run began, so day 1 is not credited with someone else's leftovers.
            if (args.Length > 0 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                int had = 0;
                foreach (string season in SweepSeasons)
                {
                    StardewValley.Objects.Chest seasonChest = GetOrCreateSweepChest(season);
                    if (seasonChest == null) continue;
                    had += seasonChest.Items.Count(i => i != null);
                    seasonChest.Items.Clear();
                }
                foreach (GameLocation loc in Game1.locations.ToList())
                {
                    if (loc?.objects == null) continue;
                    foreach (var pair in loc.objects.Pairs.Where(p => p.Value != null && p.Value.IsSpawnedObject).ToList())
                    {
                        loc.objects.Remove(pair.Key);
                        loc.numberOfSpawnedObjectsOnMap = Math.Max(0, loc.numberOfSpawnedObjectsOnMap - 1);
                    }
                }
                this.Monitor.Log(
                    $"[Sweep] cleared: all four season chests emptied ({had} stack(s)) and stale map forage discarded. Ready for a fresh run.",
                    LogLevel.Info);
                return;
            }

            var today = new Dictionary<string, int>(StringComparer.Ordinal);
            int maps = 0, overflow = 0;

            foreach (GameLocation location in Game1.locations.ToList())
            {
                if (location == null) continue;

                // Only spawned forage: crops, machines, placed items and litter stay put.
                var forage = location.objects.Pairs
                    .Where(p => p.Value != null && p.Value.IsSpawnedObject)
                    .ToList();
                if (forage.Count == 0) continue;

                maps++;
                foreach (var pair in forage)
                {
                    StardewValley.Object obj = pair.Value;
                    string id = obj.QualifiedItemId ?? obj.ItemId ?? "(unknown)";
                    int stack = Math.Max(1, obj.Stack);

                    location.objects.Remove(pair.Key);
                    location.numberOfSpawnedObjectsOnMap = Math.Max(0, location.numberOfSpawnedObjectsOnMap - 1);

                    // A spawned forage object carries flags that stop it stacking in a chest.
                    obj.IsSpawnedObject = false;
                    obj.CanBeSetDown = true;

                    if (chest.addItem(obj) != null)
                        overflow += stack;   // chest full: 36 stacks
                    else
                        today[id] = today.GetValueOrDefault(id) + stack;
                }
            }

            int picked = today.Values.Sum();
            string breakdown = today.Count == 0
                ? "nothing"
                : string.Join(", ", today.OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{DisplayName(kv.Key)} x{kv.Value}"));

            this.Monitor.Log(
                $"[Sweep] {Game1.currentSeason} {Game1.dayOfMonth}: picked {picked} from {maps} map(s): {breakdown}"
                + (overflow > 0 ? $"  WARNING: chest full, {overflow} item(s) lost" : ""),
                LogLevel.Info);
        }

        /// <summary>That season's sweep chest, placed if it is not there yet. Anything else on the
        /// tile is cleared off rather than kept.</summary>
        private StardewValley.Objects.Chest GetOrCreateSweepChest(string season = null)
        {
            GameLocation farm = Game1.getLocationFromName("Farm");
            if (farm == null) return null;

            Microsoft.Xna.Framework.Vector2 tile = SweepTileFor(season ?? Game1.currentSeason);

            if (farm.objects.TryGetValue(tile, out StardewValley.Object existing))
            {
                if (existing is StardewValley.Objects.Chest found) return found;
                farm.objects.Remove(tile);
            }

            var chest = new StardewValley.Objects.Chest(playerChest: true, tile);
            farm.objects[tile] = chest;
            this.Monitor.Log(
                $"tly_sweepforage: placed the {season ?? Game1.currentSeason} chest on the Farm at {tile.X},{tile.Y}.",
                LogLevel.Info);
            return chest;
        }

        /// <summary>Where every measured sweep run is kept. One CSV, appended to, so runs across
        /// seeds and seasons accumulate into something that can be averaged later instead of
        /// living only in a log that gets archived on the next deploy.</summary>
        private const string SweepResultsFile = "forage-sweep-results.csv";

        /// <summary>Water bodies worth potting, one pot cluster each. Crab pot catch tables are
        /// keyed off whether the water is ocean or fresh (decompile CrabPot.DayUpdate), so the
        /// Beach is the one that matters most and the rest cover the freshwater table.</summary>
        private static readonly string[] CrabPotZones = { "Beach", "Forest", "Town", "Mountain" };

        /// <summary>Crab pot hauls go in their own chests, one row below the forage ones. They MUST
        /// be separate: Cockle, Mussel and Oyster come from both routes, and the whole point of
        /// measuring pots is to find out how much of those the pots supply that forage does not.</summary>
        private static Microsoft.Xna.Framework.Vector2 CrabTileFor(string season)
        {
            Microsoft.Xna.Framework.Vector2 t = SweepTileFor(season);
            return new Microsoft.Xna.Framework.Vector2(t.X, t.Y + 2);
        }

        /// <summary><c>tly_crabpots [place|report] [count]</c>: measure what crab pots really yield.
        ///
        /// <c>place</c> drops <paramref name="count"/> (default 10) baited pots in each water zone.
        /// Bare <c>tly_crabpots</c> is the daily step: take every ready catch into that season's
        /// crab chest and re-bait every pot, which is exactly what a player working their pots does
        /// (decompile CrabPot.DayUpdate: no bait or an unclaimed catch means no new catch).
        ///
        /// Run it alongside the forage sweep across a season and the two chests together give the
        /// real supply of the shellfish that <see cref="TheLongestYear.Core.ForageAskLimits"/>
        /// currently refuses to clamp for want of this number.</summary>
        private void CmdCrabPots(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (mode == "report")
            {
                foreach (string season in SweepSeasons)
                {
                    GameLocation f = Game1.getLocationFromName("Farm");
                    if (f != null && f.objects.TryGetValue(CrabTileFor(season), out StardewValley.Object o)
                        && o is StardewValley.Objects.Chest c)
                        ReportSweepChest(c, season + "-crabpot");
                }
                return;
            }

            if (mode == "place")
            {
                int want = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 10;
                int placed = 0;
                foreach (string zoneName in CrabPotZones)
                {
                    GameLocation zone = Game1.getLocationFromName(zoneName);
                    if (zone == null) continue;
                    int here = 0;
                    for (int x = 0; x < zone.Map.Layers[0].LayerWidth && here < want; x++)
                    for (int y = 0; y < zone.Map.Layers[0].LayerHeight && here < want; y++)
                    {
                        var tile = new Microsoft.Xna.Framework.Vector2(x, y);
                        if (!zone.isWaterTile(x, y) || zone.objects.ContainsKey(tile)) continue;
                        var pot = new StardewValley.Objects.CrabPot { TileLocation = tile };
                        pot.bait.Value = new StardewValley.Object("685", 1);
                        zone.objects[tile] = pot;
                        here++; placed++;
                    }
                    this.Monitor.Log($"[CrabPot] {zoneName}: {here} pot(s) placed.", LogLevel.Info);
                }
                this.Monitor.Log($"[CrabPot] {placed} pot(s) total, all baited. Run tly_crabpots daily.", LogLevel.Info);
                return;
            }

            StardewValley.Objects.Chest chest = GetOrCreateCrabChest();
            if (chest == null) { this.Monitor.Log("tly_crabpots: no crab chest.", LogLevel.Error); return; }

            var today = new Dictionary<string, int>(StringComparer.Ordinal);
            int pots = 0, rebaited = 0;
            foreach (string zoneName in CrabPotZones)
            {
                GameLocation zone = Game1.getLocationFromName(zoneName);
                if (zone == null) continue;
                foreach (var pair in zone.objects.Pairs.ToList())
                {
                    if (pair.Value is not StardewValley.Objects.CrabPot pot) continue;
                    pots++;
                    StardewValley.Object caught = pot.heldObject.Value;
                    if (caught != null)
                    {
                        string id = caught.QualifiedItemId ?? caught.ItemId;
                        today[id] = today.GetValueOrDefault(id) + Math.Max(1, caught.Stack);
                        chest.addItem(caught);
                        pot.heldObject.Value = null;
                        pot.readyForHarvest.Value = false;
                    }
                    // Re-bait: an empty, unbaited pot catches nothing tomorrow.
                    pot.bait.Value = new StardewValley.Object("685", 1);
                    rebaited++;
                }
            }

            string breakdown = today.Count == 0 ? "nothing"
                : string.Join(", ", today.OrderByDescending(k => k.Value).Select(k => $"{DisplayName(k.Key)} x{k.Value}"));
            this.Monitor.Log(
                $"[CrabPot] {Game1.currentSeason} {Game1.dayOfMonth}: {today.Values.Sum()} from {pots} pot(s) "
                + $"({rebaited} re-baited): {breakdown}", LogLevel.Info);
        }

        private StardewValley.Objects.Chest GetOrCreateCrabChest()
        {
            GameLocation farm = Game1.getLocationFromName("Farm");
            if (farm == null) return null;
            Microsoft.Xna.Framework.Vector2 tile = CrabTileFor(Game1.currentSeason);
            if (farm.objects.TryGetValue(tile, out StardewValley.Object existing))
            {
                if (existing is StardewValley.Objects.Chest found) return found;
                farm.objects.Remove(tile);
            }
            var chest = new StardewValley.Objects.Chest(playerChest: true, tile);
            farm.objects[tile] = chest;
            return chest;
        }

        private void ReportSweepChest(StardewValley.Objects.Chest chest, string season)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Item item in chest.Items.Where(i => i != null))
                totals[item.QualifiedItemId] = totals.GetValueOrDefault(item.QualifiedItemId) + item.Stack;

            if (totals.Count == 0)
            {
                this.Monitor.Log($"=== tly_sweepforage: {season} chest is empty ===", LogLevel.Info);
                return;
            }
            this.Monitor.Log($"=== tly_sweepforage: {season} chest ===", LogLevel.Info);
            foreach (var kv in totals.OrderByDescending(k => k.Value))
                this.Monitor.Log($"  {DisplayName(kv.Key),-28} {kv.Key,-18} {kv.Value,5}", LogLevel.Info);
            this.Monitor.Log(
                $"[SweepTotal] {season}: {totals.Values.Sum()} items, {totals.Count} distinct, {chest.Items.Count(i => i != null)}/36 slots used.",
                LogLevel.Info);

            WriteSweepResults(totals, season);
        }

        /// <summary>Append this run to the results CSV. Every row carries the run's identity (seed,
        /// loop, season, the day it was read) so two runs can never be confused for one another.</summary>
        private void WriteSweepResults(IReadOnlyDictionary<string, int> totals, string season)
        {
            try
            {
                string path = System.IO.Path.Combine(this.Helper.DirectoryPath, SweepResultsFile);
                bool isNew = !System.IO.File.Exists(path);

                var sb = new System.Text.StringBuilder();
                if (isNew)
                    sb.AppendLine("stamp,seed,loop,season,readOnDay,itemId,itemName,count");

                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                long seed = Game1.player?.UniqueMultiplayerID ?? 0;
                int loop = _meta?.Run?.RunNumber ?? 0;

                foreach (var kv in totals.OrderByDescending(k => k.Value))
                    sb.AppendLine(string.Join(",",
                        stamp, seed, loop, season, Game1.dayOfMonth,
                        kv.Key, Csv(DisplayName(kv.Key)), kv.Value));

                System.IO.File.AppendAllText(path, sb.ToString());
                this.Monitor.Log(
                    $"[SweepFile] appended {totals.Count} row(s) for loop {loop} to {path}", LogLevel.Info);
            }
            catch (System.IO.IOException ex)
            {
                this.Monitor.Log($"tly_sweepforage: could not write the results file: {ex.Message}", LogLevel.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                this.Monitor.Log($"tly_sweepforage: could not write the results file: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>Quote a CSV field only when it needs it (item names can carry commas).</summary>
        private static string Csv(string value)
        {
            value ??= "";
            return value.Contains(',') || value.Contains('"')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        /// <summary><c>tly_forageyield [season|day] [item]</c>: what "checked everywhere every day"
        /// is actually worth per forage item, by a cutoff day. Read-only; nothing is clamped by
        /// this yet. Sorted by yield so the scarcest asks are easy to spot, and the 20-80% band is
        /// printed next to each so a proposed maximum ask can be read straight off.</summary>
        private void CmdForageYield(string command, string[] args)
        {
            if (!Context.IsWorldReady || _effortData == null)
            {
                this.Monitor.Log("Load a save first (the simulator reads live game data).", LogLevel.Warn);
                return;
            }

            string arg = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            int cutoff;
            string label;
            switch (arg)
            {
                case "": case "year": cutoff = Calendar.DaysPerYear; label = "the whole loop"; break;
                case "spring": cutoff = Calendar.DaysPerMonth; label = "Spring 28"; break;
                case "summer": cutoff = Calendar.DaysPerMonth * 2; label = "Summer 28"; break;
                case "fall": cutoff = Calendar.DaysPerMonth * 3; label = "Fall 28"; break;
                case "winter": cutoff = Calendar.DaysPerYear; label = "Winter 28"; break;
                default:
                    if (!int.TryParse(arg, out cutoff) || cutoff < 1 || cutoff > Calendar.DaysPerYear)
                    {
                        this.Monitor.Log($"Usage: tly_forageyield [spring|summer|fall|winter|<day 1-{Calendar.DaysPerYear}>] [itemId]", LogLevel.Warn);
                        return;
                    }
                    label = $"day {cutoff}";
                    break;
            }

            string filter = args.Length > 1 ? args[1] : null;

            IReadOnlyDictionary<string, TheLongestYear.Core.Availability.ForageYieldResult> yields =
                TheLongestYear.Core.Availability.ForageYieldSimulator.SimulateTo(cutoff, _effortData.ForageSpawns, _effortData.ForageRates);

            if (_effortData.ForageRates.Count == 0)
            {
                this.Monitor.Log("No per-location forage rates were captured - nothing to simulate.", LogLevel.Warn);
                return;
            }

            this.Monitor.Log(
                $"=== tly_forageyield: expected forage by {label}, if every reachable map is cleared every day ===",
                LogLevel.Info);
            this.Monitor.Log(
                "  This is an OPTIMISTIC ceiling (every spawn finds a tile, unknown conditions pass). "
                + "A requirement should sit in the 20-80% band; 80% is the highest it should ever roll.",
                LogLevel.Info);

            foreach (TheLongestYear.Core.Availability.ForageYieldResult r in yields.Values
                .Where(r => filter == null || r.ItemId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.ExpectedTotal))
            {
                this.Monitor.Log(
                    $"  {DisplayName(r.ItemId),-28} {r.ItemId,-18} max {r.ExpectedTotal,7:F1}"
                    + $"  band {r.ExpectedTotal * 0.20,5:F0}-{r.ExpectedTotal * 0.80,-5:F0}"
                    + $"  {r.SpawningDays,3}d  {string.Join(", ", r.Locations)}",
                    LogLevel.Info);
            }

            this.Monitor.Log(
                $"tly_forageyield: {yields.Count} forage item(s) over {_effortData.ForageRates.Count} map(s). "
                + "Forage only - fish, bushes and dig spots are not modelled.",
                LogLevel.Info);
        }

        /// <summary><c>tly_warpgraph [filter]</c>: print every loaded location and its warp targets,
        /// for verifying reachability derivation. Kept permanently as a diagnostic.</summary>
        private void CmdWarpGraph(string command, string[] args)
        {
            string filter = args.Length > 0 ? args[0] : null;
            int locations = 0, edges = 0;
            var lines = new List<string>();
            foreach (GameLocation location in Game1.locations)
            {
                if (location?.Name == null)
                    continue;
                locations++;
                var targets = new List<string>();
                foreach (Warp warp in location.warps)
                {
                    if (string.IsNullOrEmpty(warp?.TargetName))
                        continue;
                    edges++;
                    if (!targets.Contains(warp.TargetName))
                        targets.Add(warp.TargetName);
                }
                if (filter != null && location.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                lines.Add($"  {location.Name} -> {(targets.Count == 0 ? "(none)" : string.Join(", ", targets))}");
            }
            this.Monitor.Log($"Warp graph: {locations} locations, {edges} warp edges.", LogLevel.Info);
            foreach (string line in lines)
                this.Monitor.Log(line, LogLevel.Info);
        }
    }
}
