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
    /// Year-1 progression-locked items are filtered before catalog construction. These are items whose
    /// real-world gate (Deluxe Coop/Barn, Calico Desert bus repair, cooking recipes, rare seeds) cannot
    /// reliably be completed in a single roguelite year. Skipping them makes contracts feasible. A warning
    /// is logged noting that some bundles may be short an ingredient in pure-vanilla mode; Plan 06 will
    /// model per-month progression gates properly.
    /// </summary>
    internal sealed class BundleCatalogBuilder
    {
        // -----------------------------------------------------------------------------------------
        // Year-1 progression deny list
        // Items here are skipped entirely — they should not appear in any contract because they
        // require progression that won't reliably complete in a single roguelite year restart.
        // -----------------------------------------------------------------------------------------

        private static readonly IReadOnlySet<string> Year1ProgressionLocked = new HashSet<string>
        {
            // --- Animal products requiring Deluxe Coop (rabbit) or Deluxe Barn (pig / sheep) ---
            // These animals cost 10,000–20,000g for the building upgrade alone, on top of the
            // animal purchase. In a fresh Y1 restart this is usually mid-to-late year at best.
            "(O)446",   // Rabbit's Foot  — Rabbit, Deluxe Coop
            "(O)430",   // Truffle         — Pig,    Deluxe Barn
            "(O)440",   // Wool            — Sheep,  Deluxe Barn

            // Big Barn / Big Coop animals (still 2-upgrade chain from Coop/Barn)
            "(O)436",   // Goat Milk       — Goat, Big Barn
            "(O)438",   // Large Goat Milk — Goat, Big Barn
            "(O)444",   // Duck Feather    — Duck, Big Coop

            // --- Calico Desert forage (bus repair required — normally unlocked after spending
            //     35,000g to repair the bus, which for most players is mid-year or later) ---
            "(O)90",    // Cactus Fruit    — Desert forage
            "(O)88",    // Coconut         — Desert forage

            // --- Cooked dishes (need cooking recipe + ingredients; bundles ask for cooked items) ---
            // Players start without most cooking recipes; they're gated behind friendship + TV.
            "(O)196",   // Salad
            "(O)197",   // Cheese Cauliflower
            "(O)203",   // Strange Bun
            "(O)204",   // Lucky Lunch
            "(O)206",   // Pizza
            "(O)211",   // Pancakes
            "(O)214",   // Trout Soup
            "(O)228",   // Maki Roll
            "(O)265",   // Seafoam Pudding

            // --- Progression-gated rare crops ---
            "(O)417",   // Sweet Gem Berry — grown from Rare Seed (Traveling Cart only; rare drop)
            "(O)454",   // Ancient Fruit   — grown from Ancient Seed (multi-step artifact/donate chain)

            // --- Highly unlikely in Year 1 ---
            "(O)74",    // Prismatic Shard — extremely rare mine drop; effectively never in Y1 for most players
        };

        // -----------------------------------------------------------------------------------------

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
            int progressionSkipped = 0;

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

                    // Skip progression-locked items before dedup so we don't silently consume the
                    // seen-slot for an item that will never appear in a contract.
                    if (Year1ProgressionLocked.Contains(id))
                    {
                        progressionSkipped++;
                        continue;
                    }

                    if (!seen.Add(id))
                        continue;

                    Rarity rarity = ItemRarityResolver.Resolve(id, _thresholds);
                    IReadOnlySet<CoreSeason> seasons = _seasons.SeasonsFor(id);
                    items.Add(new CcItem(id, theme, rarity, seasons));
                }
            }

            _monitor.Log(
                $"Bundle catalog built: {items.Count} concrete CC items " +
                $"({categorySkipped} category ingredients skipped, " +
                $"{progressionSkipped} Y1-progression-locked items skipped).",
                LogLevel.Info);

            if (progressionSkipped > 0)
            {
                _monitor.Log(
                    $"[TheLongestYear] Y1 progression deny list removed {progressionSkipped} bundle ingredient(s). " +
                    "Some vanilla bundles may be short an ingredient in pure-vanilla mode. " +
                    "Per-month progression modelling is planned for Plan 06.",
                    LogLevel.Warn);
            }

            return items;
        }
    }
}
