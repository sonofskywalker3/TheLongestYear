using System.Collections.Generic;
using StardewValley;
using StardewValley.GameData.Crops;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Maps an item id to its obtainable seasons. Crops are derived from Data/Crops (harvest item -> seasons).
    /// All other items (fish, forage, minerals, bars, artisan) are treated as year-round for v1; fish/forage
    /// season accuracy is a Plan 06 fairness refinement.
    /// </summary>
    internal sealed class SeasonResolver
    {
        private static readonly IReadOnlySet<CoreSeason> AllSeasons =
            new HashSet<CoreSeason> { CoreSeason.Spring, CoreSeason.Summer, CoreSeason.Fall, CoreSeason.Winter };

        private readonly Dictionary<string, IReadOnlySet<CoreSeason>> _cropSeasonsByHarvestId;

        public SeasonResolver()
        {
            _cropSeasonsByHarvestId = BuildCropSeasonMap();
        }

        public IReadOnlySet<CoreSeason> SeasonsFor(string qualifiedItemId)
            => _cropSeasonsByHarvestId.TryGetValue(qualifiedItemId, out var seasons) ? seasons : AllSeasons;

        private static Dictionary<string, IReadOnlySet<CoreSeason>> BuildCropSeasonMap()
        {
            var map = new Dictionary<string, IReadOnlySet<CoreSeason>>();
            foreach (KeyValuePair<string, CropData> kvp in Game1.cropData)
            {
                CropData crop = kvp.Value;
                if (crop?.HarvestItemId == null || crop.Seasons == null || crop.Seasons.Count == 0)
                    continue;

                string harvestId = BundleParsing.NormalizeItemId(crop.HarvestItemId);
                var seasons = new HashSet<CoreSeason>();
                foreach (StardewValley.Season s in crop.Seasons)
                    seasons.Add((CoreSeason)(int)s);

                if (seasons.Count > 0)
                    map[harvestId] = seasons;
            }
            return map;
        }
    }
}
