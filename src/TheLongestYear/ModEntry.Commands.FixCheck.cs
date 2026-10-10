using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear
{
    /// <summary>tly_fixcheck: headless live checks for the core-systems bug fixes (review 2,
    /// 0.19.42 to 0.19.59, and 0.19.91). Developer-only; every subcommand logs what it saw.</summary>
    public sealed partial class ModEntry
    {
        internal const string FixCheckUsage =
            "Debug: live checks for the review-2 fixes. Usage: tly_fixcheck " +
            "collect [list] (milk pail on every cow/goat, shears on every sheep with produce) | " +
            "stash list | stash unknown (adds a record whose mod is not loaded) | " +
            "failstep <step name>|off (the next reset's restore step with that name throws) | " +
            "trees <n> (plants n grown oaks on the Farm and chops each until it falls) | " +
            "forage [location] | seed <location> <n> (places n spawned Daffodils) | spawn <location> <n> (runs spawnObjects n times) | " +
            "liab (selection, drawbacks, the Random Pairings drawback for the current week) | nextpick <theme> | " +
            "pairings on|off (in memory) | exhaust (marks every theme picked this month) | " +
            "greenrain | rod | stashfill | meta";

        private void CmdFixCheck(string command, string[] args)
        {
            if (!Context.IsWorldReady || _meta == null) { this.Monitor.Log("tly_fixcheck: load a save first.", LogLevel.Warn); return; }
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            switch (mode)
            {
                case "collect": FixCheckCollect(args.Length > 1 && args[1] == "list"); break;
                case "stash": FixCheckStash(args.Length > 1 ? args[1] : "list"); break;
                case "failstep":
                    WorldResetService.DebugFailStep = args.Length > 1 && args[1] != "off" ? string.Join(" ", args.Skip(1)) : null;
                    this.Monitor.Log($"tly_fixcheck: forced restore-step failure = '{WorldResetService.DebugFailStep ?? "off"}'.", LogLevel.Info);
                    break;
                case "trees": FixCheckTrees(args.Length > 1 && int.TryParse(args[1], out int n) ? n : 5); break;
                case "forage": FixCheckForage(args.Length > 1 ? args[1] : null); break;
                case "seed": FixCheckSeedForage(args.Length > 1 ? args[1] : "Forest", args.Length > 2 && int.TryParse(args[2], out int m) ? m : 20); break;
                case "spawn": FixCheckSpawn(args.Length > 1 ? args[1] : "Forest", args.Length > 2 && int.TryParse(args[2], out int k) ? k : 3); break;
                case "liab": FixCheckLiability(); break;
                case "nextpick":
                    if (args.Length > 1 && Enum.TryParse(args[1], true, out Theme t))
                    {
                        _meta.Run.NextMonthSelection = t;
                        this.Monitor.Log($"tly_fixcheck: NextMonthSelection = {t}.", LogLevel.Info);
                    }
                    else this.Monitor.Log("tly_fixcheck: nextpick <theme>", LogLevel.Warn);
                    break;
                case "pairings":
                    _config.Randomizer.RandomPairings = args.Length > 1 && args[1] == "on";
                    this.Monitor.Log($"tly_fixcheck: config Randomizer.RandomPairings = {_config.Randomizer.RandomPairings} (in memory only).", LogLevel.Info);
                    break;
                case "exhaust":
                    _meta.Run.SelectedThemesThisMonth = Enum.GetValues<Theme>().ToList();
                    this.Monitor.Log($"tly_fixcheck: SelectedThemesThisMonth = [{string.Join(",", _meta.Run.SelectedThemesThisMonth)}].", LogLevel.Info);
                    break;
                case "greenrain": FixCheckGreenRain(); break;
                case "rod": FixCheckRod(); break;
                case "stashfill": FixCheckStashFill(); break;
                case "meta":
                    this.Monitor.Log($"tly_fixcheck: BackupDone={_meta.State.BackupDone}, CompletedResets={_meta.State.CompletedResets}, " +
                                     $"PendingResetDrops={_meta.State.PendingResetDrops.Count}, StashItems={_meta.State.StashItems.Count}, " +
                                     $"displayHUD={Game1.displayHUD}, date={Game1.season} {Game1.dayOfMonth} Y{Game1.year}, save={Constants.SaveFolderName}.", LogLevel.Info);
                    break;
                default: this.Monitor.Log(FixCheckUsage, LogLevel.Info); break;
            }
        }

        private static IEnumerable<FarmAnimal> AllFarmAnimals()
        {
            Farm farm = Game1.getFarm();
            foreach (FarmAnimal a in farm.animals.Values) yield return a;
            foreach (Building b in farm.buildings)
                if (b.GetIndoors() is AnimalHouse house)
                    foreach (FarmAnimal a in house.animals.Values) yield return a;
        }

        /// <summary>The milk pail or shears used on each animal with produce, through the tool's own
        /// DoFunction (the same call a swing makes once beginUsing has picked the animal).</summary>
        private void FixCheckCollect(bool listOnly)
        {
            RunState run = _meta.Run;
            this.Monitor.Log($"tly_fixcheck collect: owed second products [{string.Join(", ", (run.DoubleProduceToday ?? new()).Select(r => $"{r.AnimalId}:{r.ProduceId}"))}].", LogLevel.Info);
            foreach (FarmAnimal a in AllFarmAnimals().ToList())
            {
                string tool = a.GetAnimalData()?.HarvestTool;
                this.Monitor.Log($"tly_fixcheck collect: {a.displayName} ({a.type.Value}, id {a.myID.Value}) adult={a.isAdult()} produce={a.currentProduce.Value ?? "none"} tool={tool ?? "none"}.", LogLevel.Info);
                if (listOnly || a.currentProduce.Value == null || !a.isAdult()) continue;
                for (int pass = 1; pass <= 3 && a.currentProduce.Value != null; pass++)
                {
                    Tool t = tool?.Replace(" ", "") switch { "MilkPail" => new MilkPail(), "Shears" => new Shears(), _ => null };
                    if (t == null) break;
                    string produce = a.currentProduce.Value;
                    int before = Game1.player.Items.Where(i => i?.ItemId == produce).Sum(i => i.Stack);
                    if (t is MilkPail pail) pail.animal = a; else ((Shears)t).animal = a;
                    GameLocation where = a.currentLocation ?? Game1.player.currentLocation;
                    t.DoFunction(where, 0, 0, 1, Game1.player);
                    int after = Game1.player.Items.Where(i => i?.ItemId == produce).Sum(i => i.Stack);
                    this.Monitor.Log($"tly_fixcheck collect: {a.displayName} {tool} pass {pass}: +{after - before} {produce} in the inventory, produce now {a.currentProduce.Value ?? "none"}.", LogLevel.Info);
                }
            }
        }

        private void FixCheckStash(string sub)
        {
            if (sub == "unknown")
            {
                var record = new StashItemRecord("(O)FixCheck.MissingMod_Widget", 3, 0);
                _meta.State.StashItems.Add(record);
                // Through the restore path, as a load does (the chest gets what it can recreate).
                Chest chest = _stashService?.FindStashChest();
                int chestBefore = chest?.Items.Count ?? -1;
                chest?.Items.Clear();
                _stashService?.PopulateFromMeta();
                this.Monitor.Log($"tly_fixcheck stash: added {record.ItemId} x{record.Quantity}; chest {chestBefore} -> {chest?.Items.Count ?? -1} item(s).", LogLevel.Info);
            }
            foreach (StashItemRecord r in _meta.State.StashItems)
                this.Monitor.Log($"tly_fixcheck stash: record {r.ItemId} x{r.Quantity}", LogLevel.Info);
            this.Monitor.Log($"tly_fixcheck stash: {_meta.State.StashItems.Count} record(s) in the stash data.", LogLevel.Info);
        }

        private void FixCheckStashFill()
        {
            string[] ids = { "(O)388", "(O)390", "(O)382", "(O)378", "(O)380", "(O)384", "(O)330", "(O)771", "(O)767", "(O)766", "(O)92", "(O)709" };
            int stored = 0;
            foreach (string id in ids)
            {
                Item left = _stashService?.TryDeposit(ItemRegistry.Create(id, 5));
                if (left == null) stored++;
                else break;
            }
            _stashService?.BankToMeta();
            this.Monitor.Log($"tly_fixcheck stashfill: {stored} stack(s) deposited; stash data now {_meta.State.StashItems.Count} record(s).", LogLevel.Info);
        }

        /// <summary>Plants grown oaks on open Farm tiles near the player and chops each with an
        /// iridium axe until it starts to fall; the fall ends in Tree.tickUpdate over the next
        /// ticks (the player must be on the Farm).</summary>
        private void FixCheckTrees(int count)
        {
            Farm farm = Game1.getFarm();
            var axe = new Axe { UpgradeLevel = 4 };
            AccessTools.Field(typeof(Tool), "lastUser").SetValue(axe, Game1.player);
            Vector2 origin = Game1.player.currentLocation == farm ? Game1.player.Tile : new Vector2(64, 20);
            int planted = 0;
            for (int r = 3; r < 30 && planted < count; r++)
                for (int dx = -r; dx <= r && planted < count; dx += 3)
                    for (int dy = -r; dy <= r && planted < count; dy += 3)
                    {
                        var tile = new Vector2(origin.X + dx, origin.Y + dy);
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        if (!farm.isTileOnMap(tile) || farm.terrainFeatures.ContainsKey(tile) || farm.objects.ContainsKey(tile)) continue;
                        if (!farm.CanItemBePlacedHere(tile) || farm.resourceClumps.Any(c => c.occupiesTile((int)tile.X, (int)tile.Y))) continue;
                        if (farm.terrainFeatures.Keys.Any(k => Vector2.Distance(k, tile) < 2.5f)) continue;
                        var tree = new Tree("1", 5);
                        farm.terrainFeatures.Add(tile, tree);
                        int hits = 0;
                        while (!tree.falling.Value && hits < 40)
                        {
                            tree.performToolAction(axe, 0, tile);
                            hits++;
                        }
                        this.Monitor.Log($"tly_fixcheck trees: oak at ({tile.X}, {tile.Y}) falling={tree.falling.Value} after {hits} hit(s); debris on the Farm {farm.debris.Count}.", LogLevel.Info);
                        planted++;
                    }
            this.Monitor.Log($"tly_fixcheck trees: {planted} tree(s) felled; current location {Game1.currentLocation?.NameOrUniqueName}; bonus {ActiveEffectsProvider.BonusId ?? "none"}.", LogLevel.Info);
        }

        private static int CountForage(GameLocation loc)
            => loc.objects.Values.Count(o => o != null && o.IsSpawnedObject && o.isForage());

        private void FixCheckForage(string only)
        {
            int total = 0;
            foreach (GameLocation loc in Game1.locations.Where(l => l.IsOutdoors && (only == null || l.NameOrUniqueName == only)))
            {
                int n = CountForage(loc);
                total += n;
                if (n > 0) this.Monitor.Log($"tly_fixcheck forage: {loc.NameOrUniqueName} {n} (numberOfSpawnedObjectsOnMap {loc.numberOfSpawnedObjectsOnMap}).", LogLevel.Info);
            }
            this.Monitor.Log($"tly_fixcheck forage: total {total} on {Game1.season} {Game1.dayOfMonth}; foragers_eye tier {UpgradeChecker.GetTier("foragers_eye", 5)}; drawback {ActiveEffectsProvider.LiabilityId ?? "none"}.", LogLevel.Info);
        }

        private void FixCheckSpawn(string name, int times)
        {
            GameLocation loc = Game1.getLocationFromName(name);
            if (loc == null) { this.Monitor.Log($"tly_fixcheck spawn: no location '{name}'.", LogLevel.Warn); return; }
            var spawn = AccessTools.Method(typeof(GameLocation), "spawnObjects");
            for (int i = 1; i <= times; i++)
            {
                int before = CountForage(loc);
                spawn.Invoke(loc, null);
                this.Monitor.Log($"tly_fixcheck spawn: {name} pass {i}: forage {before} -> {CountForage(loc)}.", LogLevel.Info);
            }
        }

        /// <summary>Places n spawned Daffodils (as the overnight spawn would) on open tiles, so a
        /// later spawn pass has old forage it must not re-roll.</summary>
        private void FixCheckSeedForage(string name, int count)
        {
            GameLocation loc = Game1.getLocationFromName(name);
            if (loc == null) { this.Monitor.Log($"tly_fixcheck seed: no location '{name}'.", LogLevel.Warn); return; }
            int placed = 0;
            var rng = new Random(7);
            for (int tries = 0; tries < 5000 && placed < count; tries++)
            {
                var tile = new Vector2(rng.Next(0, loc.map.Layers[0].LayerWidth), rng.Next(0, loc.map.Layers[0].LayerHeight));
                if (loc.objects.ContainsKey(tile) || loc.terrainFeatures.ContainsKey(tile) || !loc.CanItemBePlacedHere(tile)) continue;
                if (loc.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Spawnable", "Back") == null) continue;
                var o = ItemRegistry.Create<StardewValley.Object>("(O)18");
                o.IsSpawnedObject = true;
                o.CanBeGrabbed = true;
                if (loc.dropObject(o, tile * 64f, Game1.viewport, initialPlacement: true)) placed++;
            }
            this.Monitor.Log($"tly_fixcheck seed: {placed} spawned Daffodil(s) on {name}; forage now {CountForage(loc)}.", LogLevel.Info);
        }

        private void FixCheckLiability()
        {
            RunState run = _meta.Run;
            string expected = run.CurrentSelection is Theme sel && _runController != null
                ? RandomPairing.LiabilityFor(run.Seed, run.WeekOfYear, sel, _runController.RandomizerForWeekPeek(run.WeekOfYear).RandomPairings)
                : "n/a";
            string themeDefault = run.CurrentSelection is Theme s2 ? ThemeModifiers.For(s2).LiabilityId : "n/a";
            this.Monitor.Log(
                $"tly_fixcheck liab: run {run.Season} {run.DayOfMonth} (week {run.WeekOfYear}), calendar {Game1.season} {Game1.dayOfMonth}; " +
                $"selection {run.CurrentSelection?.ToString() ?? "none"}, CurrentLiabilityId {run.CurrentLiabilityId ?? "null"}, " +
                $"Random Pairings drawback for this week {expected}, theme default {themeDefault}; next pick {run.NextMonthSelection?.ToString() ?? "none"}; " +
                $"goal slots {run.CurrentWeekBonusSlots.Count}, shrine goals {run.CurrentWeekShrineGoals.Count}; " +
                $"effects bonus {ActiveEffectsProvider.BonusId ?? "none"}, drawback {ActiveEffectsProvider.LiabilityId ?? "none"}; " +
                $"picked this month [{string.Join(",", run.SelectedThemesThisMonth)}]; quests [{string.Join(", ", Game1.player.questLog.Select(q => q.GetName()))}].",
                LogLevel.Info);
        }

        private void FixCheckGreenRain()
        {
            var days = new List<int>();
            for (int d = 1; d <= 28; d++)
                if (Utility.isGreenRainDay(d, StardewValley.Season.Summer)) days.Add(d);
            this.Monitor.Log($"tly_fixcheck greenrain: year {Game1.year} green rain on Summer [{string.Join(",", days)}]; today {Game1.season} {Game1.dayOfMonth}.", LogLevel.Info);
        }

        private void FixCheckRod()
        {
            var rods = new List<string>();
            foreach (Item i in Game1.player.Items)
                if (i is FishingRod) rods.Add(i.QualifiedItemId);
            Utility.ForEachItem(i => { if (i is FishingRod && !Game1.player.Items.Contains(i)) rods.Add(i.QualifiedItemId + "(placed)"); return true; });
            this.Monitor.Log($"tly_fixcheck rod: rods [{string.Join(", ", rods)}]; eventsSeen 739330={Game1.player.eventsSeen.Contains("739330")}; " +
                             $"mail willyBackRoom={Game1.player.mailReceived.Contains("willyBackRoom")}; CurrentEvent {Game1.CurrentEvent?.id ?? "none"}; location {Game1.currentLocation?.NameOrUniqueName}.", LogLevel.Info);
        }
    }
}
