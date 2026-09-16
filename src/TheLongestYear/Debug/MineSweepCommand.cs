using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_minesweep: measure what mine floors really hold and really drop, headless
    /// (Jeff, 2026-09-16: "write a command that will instantly break all rocks on a floor and model
    /// the actual output", "have it kill all monsters too and see how that compares to the built
    /// table"). Generates each floor in the range through the game's own generator, walks every
    /// breakable stone through <see cref="GameLocation.OnStoneDestroyed"/> and every monster through
    /// <see cref="GameLocation.monsterDrop"/>, then reads the debris off the map. Nothing is walked,
    /// swung or fought, and nothing is persisted: the generated floors are dropped afterwards.
    ///
    /// Played BARE, like the forage sweep: the farmer's mining level, luck level, daily luck and
    /// professions are zeroed for the duration and restored after, so no skill is in the numbers.
    /// A stone's drop roll is seeded by the day, the save, the tile and the floor
    /// (Utility.CreateDaySaveRandom), so the day counter is bumped between samples to get a spread
    /// and put back afterwards.
    ///
    /// Usage: tly_minesweep &lt;fromFloor&gt; &lt;toFloor&gt; [samples=5]. Floors 121 and up are the
    /// Skull Cavern. Writes one CSV row per (sample, floor, kind, id) to mine-sweep-results.csv in
    /// the mod folder and logs a per-10-floor-band summary.</summary>
    internal static class MineSweepCommand
    {
        public const string Name = "tly_minesweep";
        public const string Description =
            "Measure real mine floor contents and drops, headless: generate every floor in the range, " +
            "break every stone and kill every monster through the game's own drop code with a bare farmer " +
            "(no skill, no luck, no professions), tally the debris. Nothing persisted. " +
            "Usage: tly_minesweep <fromFloor> <toFloor> [samples=5]  (121+ = Skull Cavern)";

        private const int DefaultSamples = 5;
        private const int MaxFloorsPerRun = 200;
        private const int DaysBetweenSamples = 7;
        private const int BandSize = 10;
        private const int TopItemsInSummary = 30;
        private const string ResultsFile = "mine-sweep-results.csv";
        private const string BurglarRingId = "526";
        private const string StoneKind = "stone";
        private const string NodeKind = "node";
        private const string MonsterKind = "monster";
        private const string DropKind = "drop";

        /// <summary>Stone ids that are ore, gem, geode or special nodes rather than plain rock,
        /// so the summary can count nodes per floor by type (decompile: createLitterObject,
        /// OnStoneDestroyed, breakStone).</summary>
        private static readonly IReadOnlyDictionary<string, string> NodeNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["751"] = "Copper node", ["290"] = "Iron node", ["764"] = "Gold node", ["765"] = "Iridium node",
            ["2"] = "Diamond node", ["4"] = "Ruby node", ["6"] = "Jade node", ["8"] = "Amethyst node",
            ["10"] = "Topaz node", ["12"] = "Emerald node", ["14"] = "Aquamarine node",
            ["44"] = "Gem node (mixed)", ["46"] = "Mystic stone",
            ["75"] = "Geode node", ["76"] = "Frozen geode node", ["77"] = "Magma geode node",
            ["95"] = "Radioactive node", ["843"] = "Cinder shard node", ["844"] = "Cinder shard node",
            ["819"] = "Omni geode node", ["25"] = "Mussel node",
            ["BasicCoalNode0"] = "Coal node", ["BasicCoalNode1"] = "Coal node",
            ["VolcanoCoalNode0"] = "Coal node", ["VolcanoCoalNode1"] = "Coal node", ["VolcanoGoldNode"] = "Gold node",
        };

        private sealed class Tally
        {
            public readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);
            public void Add(string key, int n = 1) => Counts[key] = Counts.TryGetValue(key, out int c) ? c + n : n;
        }

        public static void Run(IMonitor monitor, IModHelper helper, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }
            if (args.Length < 2 || !int.TryParse(args[0], out int from) || !int.TryParse(args[1], out int to) || from < 1 || to < from)
            {
                monitor.Log("Usage: tly_minesweep <fromFloor> <toFloor> [samples=5]", LogLevel.Warn);
                return;
            }
            if (to - from + 1 > MaxFloorsPerRun)
            {
                monitor.Log($"tly_minesweep: at most {MaxFloorsPerRun} floors a run.", LogLevel.Warn);
                return;
            }
            int samples = args.Length > 2 && int.TryParse(args[2], out int s) && s > 0 ? s : DefaultSamples;

            Farmer who = Game1.player;
            if (who.isWearingRing(BurglarRingId))
                monitor.Log("tly_minesweep: you are wearing the Burglar's Ring, which doubles monster drops. Take it off for a bare measurement.", LogLevel.Warn);

            // Bare farmer for the duration.
            int savedMining = who.miningLevel.Value;
            int savedLuck = who.luckLevel.Value;
            double savedDailyLuck = who.team.sharedDailyLuck.Value;
            List<int> savedProfessions = who.professions.ToList();
            uint savedDays = Game1.stats.DaysPlayed;

            var perFloor = new List<(int Sample, int Floor, Tally Tally)>();
            try
            {
                who.miningLevel.Value = 0;
                who.luckLevel.Value = 0;
                who.team.sharedDailyLuck.Value = 0.0;
                who.professions.Clear();

                for (int sample = 0; sample < samples; sample++)
                {
                    Game1.stats.DaysPlayed = savedDays + (uint)(sample * DaysBetweenSamples);
                    for (int floor = from; floor <= to; floor++)
                    {
                        Tally tally = SweepFloor(floor, who);
                        perFloor.Add((sample, floor, tally));
                    }
                }
            }
            finally
            {
                Game1.stats.DaysPlayed = savedDays;
                who.miningLevel.Value = savedMining;
                who.luckLevel.Value = savedLuck;
                who.team.sharedDailyLuck.Value = savedDailyLuck;
                who.professions.Clear();
                foreach (int p in savedProfessions) who.professions.Add(p);
                MineShaft.activeMines.RemoveAll(m => m.mineLevel >= from && m.mineLevel <= to && m != Game1.currentLocation);
            }

            WriteCsv(monitor, helper, perFloor);
            LogSummary(monitor, perFloor, from, to, samples);
        }

        /// <summary>Generate one floor fresh, smash every stone, kill every monster, read the debris.</summary>
        private static Tally SweepFloor(int floor, Farmer who)
        {
            string name = "UndergroundMine" + floor;
            MineShaft.activeMines.RemoveAll(m => m.Name == name && m != Game1.currentLocation);
            MineShaft mine = MineShaft.GetMine(name);
            var tally = new Tally();
            mine.debris.Clear();

            // Stones and nodes, before anything is broken.
            var stones = new List<(Vector2 Tile, string Id)>();
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in mine.Objects.Pairs.ToList())
            {
                StardewValley.Object obj = pair.Value;
                if (obj == null || !obj.IsBreakableStone()) continue;
                stones.Add((pair.Key, obj.ItemId));
                tally.Add(StoneKind);
                if (NodeNames.TryGetValue(obj.ItemId, out string node))
                    tally.Add(NodeKind + ":" + node);
            }
            foreach ((Vector2 tile, string id) in stones)
                mine.OnStoneDestroyed(id, (int)tile.X, (int)tile.Y, who);

            // Monsters: their drop list was rolled when the floor spawned them (Monster.parseMonsterInfo).
            foreach (Monster monster in mine.characters.OfType<Monster>().ToList())
            {
                tally.Add(MonsterKind + ":" + monster.Name);
                mine.monsterDrop(monster, (int)monster.Position.X, (int)monster.Position.Y, who);
            }

            // Everything that landed on the floor.
            foreach (Debris d in mine.debris.ToList())
            {
                string id; int count;
                if (d.item != null) { id = d.item.QualifiedItemId; count = Math.Max(1, d.item.Stack); }
                else if (!string.IsNullOrEmpty(d.itemId.Value)) { id = QualifyObject(d.itemId.Value); count = Math.Max(1, d.Chunks.Count); }
                else if (d.debrisType.Value == Debris.DebrisType.RESOURCE) { id = QualifyObject(d.chunkType.Value.ToString(CultureInfo.InvariantCulture)); count = Math.Max(1, d.Chunks.Count); }
                else continue;
                tally.Add(DropKind + ":" + id, count);
            }
            mine.debris.Clear();
            return tally;
        }

        private static string QualifyObject(string id)
            => id.StartsWith("(", StringComparison.Ordinal) ? id : "(O)" + id;

        private static void WriteCsv(IMonitor monitor, IModHelper helper, List<(int Sample, int Floor, Tally Tally)> perFloor)
        {
            string path = Path.Combine(helper.DirectoryPath, ResultsFile);
            bool fresh = !File.Exists(path);
            using StreamWriter w = new(path, append: true);
            if (fresh) w.WriteLine("loop,sample,floor,kind,id,count");
            long loop = (long)(Game1.uniqueIDForThisGame % 100000UL);
            int rows = 0;
            foreach ((int sample, int floor, Tally tally) in perFloor)
                foreach (KeyValuePair<string, int> kv in tally.Counts.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    int colon = kv.Key.IndexOf(':');
                    string kind = colon < 0 ? kv.Key : kv.Key[..colon];
                    string id = colon < 0 ? "" : kv.Key[(colon + 1)..];
                    w.WriteLine($"{loop},{sample},{floor},{kind},\"{id}\",{kv.Value}");
                    rows++;
                }
            monitor.Log($"[MineSweep] appended {rows} row(s) to {path}", LogLevel.Info);
        }

        private static void LogSummary(IMonitor monitor, List<(int Sample, int Floor, Tally Tally)> perFloor, int from, int to, int samples)
        {
            monitor.Log($"=== tly_minesweep: floors {from} to {to}, {samples} sample(s) each, bare farmer ===", LogLevel.Info);
            foreach (IGrouping<int, (int Sample, int Floor, Tally Tally)> band in perFloor.GroupBy(p => (p.Floor - 1) / BandSize).OrderBy(g => g.Key))
            {
                int lo = band.Key * BandSize + 1, hi = lo + BandSize - 1;
                int floors = band.Count();
                var sum = new Tally();
                foreach ((_, _, Tally t) in band)
                    foreach (KeyValuePair<string, int> kv in t.Counts) sum.Add(kv.Key, kv.Value);

                double Mean(string key) => sum.Counts.TryGetValue(key, out int c) ? (double)c / floors : 0.0;
                List<int> stonesPerFloor = band.Select(p => p.Tally.Counts.TryGetValue(StoneKind, out int c) ? c : 0).OrderBy(x => x).ToList();
                int median = stonesPerFloor.Count == 0 ? 0 : stonesPerFloor[stonesPerFloor.Count / 2];
                int monsters = sum.Counts.Where(k => k.Key.StartsWith(MonsterKind + ":", StringComparison.Ordinal)).Sum(k => k.Value);

                monitor.Log($"  Floors {lo}-{hi} ({floors} floor-samples): stones mean {Mean(StoneKind):F1} median {median}, monsters mean {(double)monsters / floors:F1}", LogLevel.Info);
                foreach (KeyValuePair<string, int> kv in sum.Counts.Where(k => k.Key.StartsWith(NodeKind + ":", StringComparison.Ordinal)).OrderByDescending(k => k.Value))
                    monitor.Log($"    {kv.Key[(NodeKind.Length + 1)..],-22} {(double)kv.Value / floors,6:F2} per floor", LogLevel.Info);
                foreach (KeyValuePair<string, int> kv in sum.Counts.Where(k => k.Key.StartsWith(MonsterKind + ":", StringComparison.Ordinal)).OrderByDescending(k => k.Value))
                    monitor.Log($"    {kv.Key[(MonsterKind.Length + 1)..],-22} {(double)kv.Value / floors,6:F2} per floor", LogLevel.Info);
                monitor.Log("    Drops per floor:", LogLevel.Info);
                foreach (KeyValuePair<string, int> kv in sum.Counts.Where(k => k.Key.StartsWith(DropKind + ":", StringComparison.Ordinal)).OrderByDescending(k => k.Value).Take(TopItemsInSummary))
                {
                    string id = kv.Key[(DropKind.Length + 1)..];
                    string display = ItemRegistry.GetDataOrErrorItem(id).DisplayName;
                    monitor.Log($"    {display,-24} {id,-16} {(double)kv.Value / floors,7:F2}", LogLevel.Info);
                }
            }
        }
    }
}
