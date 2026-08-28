using System.Collections.Generic;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Locations;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Maps a qualified item id to its obtainable seasons.
    ///
    /// Priority order:
    ///   1. Crop harvest seasons — derived from Data/Crops (unambiguous).
    ///   2. Fish, crab-pot and forage spawn seasons — the engine pools' own seasons
    ///      (Data/Locations spawn rows plus condition seasons, location exclusions applied).
    ///   3. Year-round fallback — for anything not covered above (minerals, bars, artisan
    ///      goods, animal products, cooked dishes).
    ///
    /// Items that are progression-locked (animal products needing Deluxe Coop/Barn, cooked dishes,
    /// Calico Desert items, etc.) should be excluded upstream via the deny list in
    /// <see cref="BundleCatalogBuilder"/> before SeasonResolver is ever consulted.
    /// </summary>
    internal sealed class SeasonResolver
    {
        private static readonly IReadOnlySet<CoreSeason> AllSeasons =
            new HashSet<CoreSeason> { CoreSeason.Spring, CoreSeason.Summer, CoreSeason.Fall, CoreSeason.Winter };

        private readonly Dictionary<string, IReadOnlySet<CoreSeason>> _cropSeasonsByHarvestId;
        private readonly IReadOnlyDictionary<string, IReadOnlySet<CoreSeason>> _spawnSeasonsByItemId;

        /// <param name="spawnSeasons">Fish, crab-pot and forage spawn seasons from the engine
        /// pools (<see cref="SpawnSeasonMap.FromPools"/>). Without it, fish and forage keep the
        /// year-round fallback — which let a Spring weekly theme ask for Pike, a
        /// Summer/Winter fish (Nexus 1122423). Forage joined the map on 2026-08-29: the old
        /// raw Data/Locations scan here had no location exclusions and ignored condition
        /// seasons, so Ginger Island's season-less cave rows made Chanterelle and Purple
        /// Mushroom read as year-round weekly goals.</param>
        public SeasonResolver(
            IReadOnlyDictionary<string, IReadOnlySet<CoreSeason>> spawnSeasons = null)
        {
            _cropSeasonsByHarvestId = BuildCropSeasonMap();
            _spawnSeasonsByItemId = spawnSeasons ?? new Dictionary<string, IReadOnlySet<CoreSeason>>();
        }

        public IReadOnlySet<CoreSeason> SeasonsFor(string qualifiedItemId)
        {
            // Crop harvest seasons UNION forage spawn seasons UNION fish spawn seasons — an
            // item obtainable through any path in a season counts for that season (Grape:
            // Fall crop, Summer forage). Before 2026-08-21 crops won outright, which hid
            // Grape from Summer weekly goals; before 2026-08-24 fish had no map at all.
            HashSet<CoreSeason> union = null;
            foreach (var map in new[] { (IReadOnlyDictionary<string, IReadOnlySet<CoreSeason>>)_cropSeasonsByHarvestId, _spawnSeasonsByItemId })
            {
                if (!map.TryGetValue(qualifiedItemId, out IReadOnlySet<CoreSeason> seasons))
                    continue;
                if (union == null) union = new HashSet<CoreSeason>(seasons);
                else union.UnionWith(seasons);
            }
            if (union != null)
                return union;

            // Everything else (minerals, bars, artisan, animal products, etc.): year-round.
            // Progression-locked items are denied before this point by BundleCatalogBuilder.
            return AllSeasons;
        }

        // -----------------------------------------------------------------------------------------
        // Crop map
        // -----------------------------------------------------------------------------------------

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
