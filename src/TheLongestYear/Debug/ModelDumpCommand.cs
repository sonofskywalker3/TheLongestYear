using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.DebugCommands
{
    /// <summary><c>tly_dumpmodel [fileName]</c>: writes the whole item availability model, not just the
    /// board's items, as a TSV in the mod folder: every Data/Objects id (plus the guild reward ids)
    /// under all four difficulty steps, with placed, pacing week, hard week, gate, effort, source and
    /// basis, then every engine pool's membership, the reachability verdicts and the generated dish
    /// bases. Read-only: the models are built on the side and the live one is left alone. Made for
    /// before/after comparisons of a rule change (the vanilla-unchanged check of the mod-support
    /// work, 2026-10-08) and for listing every modded item still at the unknown week.</summary>
    internal static class ModelDumpCommand
    {
        public const string Name = "tly_dumpmodel";
        public const string Description =
            "Write every item's availability (all four difficulty steps), the engine pools and the reachability verdicts as a TSV in the mod folder, for before/after comparisons. Read-only. Usage: tly_dumpmodel [fileName]";

        private const string DefaultFileName = "model-dump.tsv";

        public static void Run(
            IMonitor monitor,
            ItemPools pools,
            EffortData effortData,
            SourceReachability reachability,
            Func<DifficultyStep, ItemAvailabilityModel> buildDetached,
            string modDirectory,
            string[] args)
        {
            if (!Context.IsWorldReady || pools == null || effortData == null)
            {
                monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string bare in effortData.Objects.Keys) ids.Add(BundleParsing.NormalizeItemId(bare));
            foreach (string id in AvailabilityWeeks.GuildRewardWeeks.Keys) ids.Add(id);

            var sb = new StringBuilder();
            sb.AppendLine("section\tid\tvanilla\tstep\tplaced\tpacing\thard\tgate\teffort\tsource\tbasis");
            foreach (DifficultyStep step in Enum.GetValues(typeof(DifficultyStep)))
            {
                ItemAvailabilityModel model = buildDetached(step);
                foreach (string id in ids)
                {
                    ItemAvailability a = model.For(id);
                    sb.Append("model\t").Append(id).Append('\t').Append(VanillaItemIds.All.Contains(id) ? "v" : "m").Append('\t')
                        .Append(step).Append('\t').Append(model.IsPlaced(id) ? "placed" : "UNKNOWN").Append('\t')
                        .Append(a.PacingWeek).Append('\t').Append(a.HardWeekOrPacing).Append('\t').Append(a.Gate).Append('\t')
                        .Append(a.Effort).Append('\t').Append(a.Source).Append('\t').Append(Clean(a.Basis)).AppendLine();
                }
                foreach (KeyValuePair<string, double[]> dish in model.DishBases.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    sb.Append("dishbasis\t").Append(dish.Key).Append('\t').Append(VanillaItemIds.All.Contains(dish.Key) ? "v" : "m").Append('\t')
                        .Append(step).Append('\t')
                        .Append(string.Join(",", dish.Value.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)))).AppendLine();
            }

            foreach ((string pool, IReadOnlyList<PoolItem> items) in new (string, IReadOnlyList<PoolItem>)[]
                {
                    ("Crops", pools.Crops), ("Fish", pools.Fish), ("CrabPot", pools.CrabPot), ("Forage", pools.Forage),
                    ("MonsterDrops", pools.MonsterDrops), ("Metals", pools.Metals), ("ArtisanGoods", pools.ArtisanGoods),
                    ("Artifacts", pools.Artifacts), ("Books", pools.Books), ("Saplings", pools.Saplings),
                    ("GeodeMinerals", pools.GeodeMinerals), ("Cooking", pools.Cooking), ("TapperGoods", pools.TapperGoods),
                })
                foreach (PoolItem item in (items ?? Array.Empty<PoolItem>()).OrderBy(p => p.ItemId, StringComparer.Ordinal))
                    sb.Append("pool\t").Append(item.ItemId).Append('\t').Append(VanillaItemIds.All.Contains(item.ItemId) ? "v" : "m").Append('\t')
                        .Append(pool).Append('\t').Append(item.Weight).Append('\t')
                        .Append(string.Join("/", item.Seasons)).Append('\t').Append(string.Join("/", item.Locations)).AppendLine();

            foreach (KeyValuePair<string, Season> pin in pools.DerivedSeasonPins.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                sb.Append("pin\t").Append(pin.Key).Append('\t').Append(VanillaItemIds.All.Contains(pin.Key) ? "v" : "m").Append('\t')
                    .Append(pin.Value).AppendLine();

            if (reachability != null)
                foreach (KeyValuePair<string, string> reason in reachability.Reasons.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    sb.Append("unreachable\t").Append(reason.Key).Append('\t').Append(VanillaItemIds.All.Contains(reason.Key) ? "v" : "m").Append('\t')
                        .Append(Clean(reason.Value)).AppendLine();

            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : DefaultFileName;
            string path = Path.Combine(modDirectory, fileName);
            File.WriteAllText(path, sb.ToString());
            monitor.Log($"{Name}: wrote {path} ({ids.Count} ids x 4 steps).", LogLevel.Info);
        }

        private static string Clean(string text) => (text ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }
}
