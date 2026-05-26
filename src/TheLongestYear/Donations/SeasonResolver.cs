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
    ///   2. Forage spawn seasons — derived from Data/Locations (each SpawnForageData has
    ///      an optional Season field; null means year-round, a value restricts to that season).
    ///   3. Year-round fallback — for anything not covered by crops or forage (fish, minerals,
    ///      bars, artisan goods, animal products, cooked dishes).
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
        private readonly Dictionary<string, IReadOnlySet<CoreSeason>> _forageSeasonsByItemId;

        public SeasonResolver()
        {
            _cropSeasonsByHarvestId = BuildCropSeasonMap();
            _forageSeasonsByItemId = BuildForageSeasonMap();
        }

        public IReadOnlySet<CoreSeason> SeasonsFor(string qualifiedItemId)
        {
            // Crops first — unambiguous, highest priority.
            if (_cropSeasonsByHarvestId.TryGetValue(qualifiedItemId, out var cropSeasons))
                return cropSeasons;

            // Forage second — pulled from Data/Locations spawn tables.
            if (_forageSeasonsByItemId.TryGetValue(qualifiedItemId, out var forageSeasons))
                return forageSeasons;

            // Everything else (fish, minerals, bars, artisan, animal products, etc.): year-round.
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

        // -----------------------------------------------------------------------------------------
        // Forage map — built from Data/Locations
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Iterates every location's Forage list. Each <see cref="SpawnForageData"/> entry has:
        ///   - ItemId (string, the bare or qualified item id being spawned)
        ///   - Season? (null = spawns in all seasons; a value restricts to that season)
        ///
        /// We accumulate per-item which seasons it can spawn in. An entry with null Season
        /// contributes all four seasons; otherwise only the specific season is added.
        ///
        /// If the same item appears in multiple locations with different season constraints, the
        /// union of all valid seasons is used (i.e. the item is obtainable whenever any location
        /// has it). This is the correct real-game semantic.
        /// </summary>
        private static Dictionary<string, IReadOnlySet<CoreSeason>> BuildForageSeasonMap()
        {
            // Mutable accumulation — values start as HashSet<CoreSeason>.
            var accumulator = new Dictionary<string, HashSet<CoreSeason>>();

            IDictionary<string, LocationData> locations = Game1.locationData;
            if (locations == null)
                return new Dictionary<string, IReadOnlySet<CoreSeason>>();

            foreach (KeyValuePair<string, LocationData> locKvp in locations)
            {
                LocationData locData = locKvp.Value;
                if (locData?.Forage == null || locData.Forage.Count == 0)
                    continue;

                foreach (SpawnForageData entry in locData.Forage)
                {
                    string rawId = entry.ItemId;
                    if (string.IsNullOrWhiteSpace(rawId))
                        continue;

                    string qualifiedId = BundleParsing.NormalizeItemId(rawId);

                    if (!accumulator.TryGetValue(qualifiedId, out HashSet<CoreSeason> seasons))
                    {
                        seasons = new HashSet<CoreSeason>();
                        accumulator[qualifiedId] = seasons;
                    }

                    if (entry.Season == null)
                    {
                        // No season restriction — available in all seasons.
                        seasons.Add(CoreSeason.Spring);
                        seasons.Add(CoreSeason.Summer);
                        seasons.Add(CoreSeason.Fall);
                        seasons.Add(CoreSeason.Winter);
                    }
                    else
                    {
                        // Cast is safe: both enums share Spring=0, Summer=1, Fall=2, Winter=3.
                        seasons.Add((CoreSeason)(int)entry.Season.Value);
                    }
                }
            }

            // Seal into IReadOnlySet<CoreSeason> — no further mutation needed.
            var result = new Dictionary<string, IReadOnlySet<CoreSeason>>(accumulator.Count);
            foreach (KeyValuePair<string, HashSet<CoreSeason>> kv in accumulator)
                result[kv.Key] = kv.Value;

            return result;
        }
    }
}
