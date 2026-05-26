using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Builds the run's CC ground truth from the live vanilla bundle definitions. Each concrete-item
    /// requirement becomes one <see cref="CcItem"/> (vanilla qualified id, so it matches donated item ids),
    /// tagged with its room's theme, price-derived rarity, and crop/forage-derived (else year-round) seasons.
    /// Category ingredients and non-item rooms (Vault, Joja) are skipped — see plan v1 simplifications.
    ///
    /// Design note (post-playtest, 2026-05-26): we do NOT filter by current-availability. An item like
    /// Cactus Fruit (Desert) or Rabbit's Foot (Deluxe Coop animal) is a valid contract target — the
    /// player just has to invest in the unlock during the run. The catalog reflects what the CC needs;
    /// the player decides how hard a contract is worth chasing. Real seasonality (Nautilus Shell = winter
    /// beach, Red Mushroom = summer/fall forage) still applies via SeasonResolver so contracts don't ask
    /// for an item that literally does not exist in the assigned season.
    /// </summary>
    internal sealed class BundleCatalogBuilder
    {
        private readonly RarityThresholds _thresholds;
        private readonly SeasonResolver _seasons;
        private readonly IMonitor _monitor;

        public BundleCatalogBuilder(RarityThresholds thresholds, SeasonResolver seasons, IMonitor monitor)
        {
            _thresholds = thresholds;
            _seasons = seasons;
            _monitor = monitor;
        }

        public IReadOnlyList<CcItem> Build()
        {
            var items = new List<CcItem>();
            var seen = new HashSet<string>();
            int categorySkipped = 0;

            Dictionary<string, string> bundleData = Game1.netWorldState.Value.BundleData;
            foreach (KeyValuePair<string, string> kvp in bundleData)
            {
                ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(bundle.Room, out Theme theme))
                    continue;

                int take = System.Math.Min(bundle.NumberOfSlots, bundle.Ingredients.Count);
                for (int i = 0; i < take; i++)
                {
                    string itemRef = bundle.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(itemRef))
                    {
                        categorySkipped++;
                        continue;
                    }

                    string id = BundleParsing.NormalizeItemId(itemRef);
                    if (!seen.Add(id))
                        continue;

                    Rarity rarity = ItemRarityResolver.Resolve(id, _thresholds);
                    IReadOnlySet<CoreSeason> seasons = _seasons.SeasonsFor(id);
                    items.Add(new CcItem(id, theme, rarity, seasons));
                }
            }

            _monitor.Log(
                $"Bundle catalog built: {items.Count} concrete CC items " +
                $"({categorySkipped} category ingredients skipped).",
                LogLevel.Info);
            return items;
        }
    }
}
