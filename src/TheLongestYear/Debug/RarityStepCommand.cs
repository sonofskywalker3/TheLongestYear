using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug bridge only: switch the save to one Item rarity step without a reset, the
    /// way a reset would (stamp the profile resolved from config with that one step changed, then
    /// rebuild the availability model in the step's week mode), and write the model's weeks for
    /// every item the engine can place to <c>rarity-&lt;step&gt;.tsv</c> in the mod folder. After
    /// it, <c>tly_genbundles</c>, <c>tly_gatecheck</c> and <c>tly_itemmodel</c> answer for that step,
    /// so Easy/Normal/Hard/Extreme can be compared on the same save and the same seeds.
    /// In memory only; the stamp persists on the next save like any MetaState edit, and the next
    /// reset re-stamps from config.</summary>
    internal static class RarityStepCommand
    {
        public const string Name = "tly_raritystep";
        public const string Usage = "Usage: tly_raritystep easy|normal|hard|extreme";

        private const string UnknownPlaced = "UNKNOWN";

        public static void Run(
            IMonitor monitor,
            GameplayConfig config,
            MetaState meta,
            Func<DifficultyStep, ItemAvailabilityModel> rebuild,
            ItemPools pools,
            IEnumerable<string> catalogIds,
            Func<string, string> displayName,
            string modDirectory,
            string[] args)
        {
            if (!StardewModdingAPI.Context.IsWorldReady || meta == null || pools == null)
            {
                monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }
            if (args.Length < 1 || !Enum.TryParse(args[0], ignoreCase: true, out DifficultyStep step))
            {
                monitor.Log(Usage, LogLevel.Warn);
                return;
            }

            DifficultySettings settings = config.Difficulty;
            DifficultyStep configured = settings.ItemRarity;
            settings.ItemRarity = step;
            try { meta.Difficulty = DifficultyResolver.Resolve(settings, config); }
            finally { settings.ItemRarity = configured; }

            ItemAvailabilityModel model = rebuild(step);

            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (IReadOnlyList<PoolItem> list in new[]
                {
                    pools.Crops, pools.Fish, pools.CrabPot, pools.Forage, pools.MonsterDrops, pools.Metals,
                    pools.ArtisanGoods, pools.Artifacts, pools.Books, pools.Saplings, pools.GeodeMinerals,
                    pools.Cooking, pools.TapperGoods,
                })
                foreach (PoolItem item in list ?? new List<PoolItem>())
                    ids.Add(item.ItemId);
            foreach (IReadOnlyList<PoolItem> list in pools.ByKind.Values)
                foreach (PoolItem item in list)
                    ids.Add(item.ItemId);
            foreach (string id in catalogIds)
                if (!string.IsNullOrEmpty(id)) ids.Add(id);

            var sb = new StringBuilder();
            sb.AppendLine("id\tname\tplaced\tpacingWeek\thardWeek\tgateWeek\tgoalWeek\tgateSeason\tbasis");
            int unknown = 0;
            foreach (string id in ids)
            {
                ItemAvailability a = model.For(id);
                string placed = model.IsDerived(id) ? "derived"
                    : !model.IsPlaced(id) ? UnknownPlaced
                    : a.Basis.Contains("override", StringComparison.Ordinal) ? "override"
                    : AvailabilityWeeks.IsJudgementBasis(a.Basis) ? "judgement" : "rule";
                if (placed == UnknownPlaced) unknown++;
                sb.Append(id).Append('\t').Append(displayName(id)).Append('\t').Append(placed).Append('\t')
                  .Append(a.PacingWeek).Append('\t').Append(a.HardWeekOrPacing).Append('\t').Append(a.Week).Append('\t')
                  .Append(a.GoalWeek).Append('\t').Append(a.Gate).Append('\t')
                  .Append(a.Basis.Replace('\t', ' ')).AppendLine();
            }
            string path = Path.Combine(modDirectory, $"rarity-{step.ToString().ToLowerInvariant()}.tsv");
            File.WriteAllText(path, sb.ToString());
            monitor.Log(
                $"tly_raritystep: item rarity {step} stamped (config says {configured}), model rebuilt in {model.Mode} mode; " +
                $"wrote {ids.Count} item(s) to {path}, {unknown} unknown.",
                LogLevel.Info);
        }
    }
}
