using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.WildTrees;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Loop
{
    /// <summary>Data/WildTrees as the three readers need it to know how a tree comes to exist: one
    /// <see cref="WildTreeRow"/> per known entry, and the seeds that plant a wild tree (a Data/FruitTrees
    /// sapling among them grows no fruit tree; <see cref="PlantedTrees.PlantsAWildTree"/>).</summary>
    internal static class WildTreeRows
    {
        /// <summary>Every known Data/WildTrees entry. The seed is resolved as the game resolves it
        /// (Tree._LoadWildTreeData: ItemRegistry.ResolveMetadata, an unknown seed plants nothing).
        /// <paramref name="standing"/> names the tree types a grown tree of stood on a map at load; null
        /// reads none as standing (only the seed lookup needs no live scan).</summary>
        public static List<WildTreeRow> Read(IReadOnlySet<string> standing = null)
        {
            var rows = new List<WildTreeRow>();
            foreach (var kv in KnownData.Load<Dictionary<string, WildTreeData>>("Data/WildTrees"))
            {
                WildTreeData tree = kv.Value;
                if (tree == null) continue;
                string seed = string.IsNullOrWhiteSpace(tree.SeedItemId) ? null : ItemRegistry.ResolveMetadata(tree.SeedItemId)?.QualifiedItemId;
                rows.Add(new WildTreeRow(kv.Key, seed, tree.SeedPlantable, tree.GrowthChance, tree.GrowsInWinter,
                    standing != null && standing.Contains(kv.Key) && PlantedTrees.StandingCounts(kv.Key)));
            }
            return rows;
        }

        /// <summary>The tree types of every full-grown tree standing in the live world.</summary>
        public static IReadOnlySet<string> StandingTypes()
            => new HashSet<string>(GameEffortData.ReadWildTreeSpots().Select(s => s.TreeId), System.StringComparer.Ordinal);

        /// <summary>The local ids of every seed that plants a wild tree.</summary>
        public static IReadOnlySet<string> WildSeedLocalIds() => PlantedTrees.WildSeedLocalIds(Read());
    }
}
