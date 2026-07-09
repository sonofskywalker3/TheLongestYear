using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Builds the run's CC ground truth from the live vanilla bundle definitions. Two outputs:
    /// <list type="bullet">
    ///   <item><see cref="Build"/> — flat <see cref="CcItem"/> list (per-item rarity / season /
    ///         theme metadata), still consumed by the rarity resolver, season pin merger, and
    ///         the bonus-list sampler's obtainability lookup.</item>
    ///   <item><see cref="BuildRequirements"/> — bundle-level <see cref="BundleRequirement"/>
    ///         list (Seasonal / PerItem / Percentage gates), consumed by <see cref="BundleGate"/>
    ///         to evaluate the day-28 win/fail condition.</item>
    /// </list>
    /// Category ingredients and the Vault / Joja rooms are skipped — see plan v1 simplifications.
    ///
    /// Design note (post-playtest, 2026-05-26): we do NOT filter by current-availability. An item
    /// like Cactus Fruit (Desert) or Rabbit's Foot (Deluxe Coop animal) is a valid bundle target —
    /// the player just has to invest in the unlock during the run. The catalog reflects what the
    /// CC needs; the player decides how hard a bundle is worth chasing. Real seasonality (Nautilus
    /// Shell = winter beach, Red Mushroom = summer/fall forage) still applies via SeasonResolver
    /// so bonus-list samples don't suggest an item that literally does not exist in the season.
    /// </summary>
    internal sealed class BundleCatalogBuilder
    {
        private readonly RarityThresholds _thresholds;
        private readonly SeasonResolver _seasons;
        private readonly IMonitor _monitor;
        private readonly IReadOnlyDictionary<string, Theme> _themeOverrides;
        private readonly IReadOnlyDictionary<string, CoreSeason> _itemSeasonPins;
        private readonly IReadOnlyDictionary<string, int[]> _bundleQuotas;

        public BundleCatalogBuilder(
            RarityThresholds thresholds,
            SeasonResolver seasons,
            IMonitor monitor,
            IReadOnlyDictionary<string, Theme> themeOverrides = null,
            IReadOnlyDictionary<string, CoreSeason> itemSeasonPins = null,
            IReadOnlyDictionary<string, int[]> bundleQuotas = null)
        {
            _thresholds = thresholds;
            _seasons = seasons;
            _monitor = monitor;
            _themeOverrides = themeOverrides ?? new Dictionary<string, Theme>();
            _itemSeasonPins = itemSeasonPins ?? new Dictionary<string, CoreSeason>();
            _bundleQuotas = bundleQuotas ?? new Dictionary<string, int[]>();
        }

        public IReadOnlyList<CcItem> Build()
        {
            var items = new List<CcItem>();
            var seen = new HashSet<string>();
            int categorySkipped = 0;
            int unresolvedSkipped = 0;

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

                    // Probe ItemRegistry: an id that can't be resolved into an Item shouldn't make it
                    // into the catalog -- the donation surface and UI both expect Create() to succeed.
                    // (Diagnoses the v3.X "hay + grey squares" Mixed-contract bug.)
                    Item probe = ItemRegistry.Create(id, 1, 0, allowNull: true);
                    if (probe == null)
                    {
                        _monitor.Log(
                            $"BundleCatalogBuilder: ItemRegistry returned null for '{id}' " +
                            $"(bundle '{bundle.Name}', room '{bundle.Room}') -- excluding from catalog.",
                            LogLevel.Warn);
                        unresolvedSkipped++;
                        continue;
                    }

                    Rarity rarity = ItemRarityResolver.Resolve(id, _thresholds);
                    IReadOnlySet<CoreSeason> seasons = _seasons.SeasonsFor(id);
                    Theme finalTheme = _themeOverrides.TryGetValue(id, out Theme pinnedTheme)
                        ? pinnedTheme
                        : theme;
                    items.Add(new CcItem(id, finalTheme, rarity, seasons));
                }
            }

            _monitor.Log(
                $"Bundle catalog built: {items.Count} concrete CC items " +
                $"({categorySkipped} category ingredients skipped, {unresolvedSkipped} unresolved ids skipped).",
                LogLevel.Info);
            return items;
        }

        /// <summary>
        /// Produce one <see cref="BundleRequirement"/> per classifiable vanilla bundle, skipping
        /// the Vault/Joja rooms (no item theme) and any bundle whose name doesn't match a known
        /// rule. Each requirement carries its kind-specific gate (Seasonal/PerItem/Percentage)
        /// and is consumed by <see cref="BundleGate"/> at season-end.
        /// </summary>
        public IReadOnlyList<BundleRequirement> BuildRequirements()
        {
            var reqs = new List<BundleRequirement>();
            int categorySkipped = 0;
            int unclassifiedSkipped = 0;

            Dictionary<string, string> bundleData = Game1.netWorldState.Value.BundleData;
            foreach (KeyValuePair<string, string> kvp in bundleData)
            {
                ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(bundle.Room, out Theme theme))
                    continue;

                BundleRequirement req;
                try
                {
                    req = BundleClassifier.Classify(bundle, theme, _itemSeasonPins, _bundleQuotas);
                }
                catch (System.Exception ex)
                {
                    _monitor.Log(
                        $"BundleCatalogBuilder: classification threw for bundle '{bundle.Name}' " +
                        $"(room '{bundle.Room}'): {ex.Message} -- skipping.",
                        LogLevel.Warn);
                    unclassifiedSkipped++;
                    continue;
                }

                if (req == null)
                {
                    // Only category-only bundles classify to null now; unknown pick-X-of-Y
                    // bundles get a derived quota (see BundleClassifier decision order #4).
                    categorySkipped++;
                    continue;
                }

                if (req.Kind == BundleKind.Percentage && !_bundleQuotas.ContainsKey(bundle.Name))
                {
                    _monitor.Log(
                        $"BundleCatalogBuilder: bundle '{bundle.Name}' (room '{bundle.Room}', " +
                        $"X={req.NumberOfSlots}, Y={req.Ingredients.Count}) has no curated quota — " +
                        $"using derived ramp [{string.Join(", ", req.CumulativeRequiredBySeason)}].",
                        LogLevel.Info);
                }

                reqs.Add(req);
            }

            _monitor.Log(
                $"Bundle requirements built: {reqs.Count} classified " +
                $"({categorySkipped} category-only skipped, {unclassifiedSkipped} unclassified skipped).",
                LogLevel.Info);
            return reqs;
        }

        /// <summary>
        /// Build a (qualified item id → stack required) lookup across every classifiable bundle
        /// ingredient. Used by the planning-hub bonus-item icons so the player sees the actual
        /// quantity needed for a slot (e.g. Wood = 99, not just "1"). When the same item appears
        /// in multiple bundles with different stack requirements (rare but possible), we keep the
        /// MAX — that's the largest amount the player would need to fill any one slot, which is
        /// the safer "make sure you have enough" framing.
        /// </summary>
        public IReadOnlyDictionary<string, int> BuildIngredientStacks()
        {
            var stacks = new Dictionary<string, int>(System.StringComparer.Ordinal);
            Dictionary<string, string> bundleData = Game1.netWorldState.Value.BundleData;
            foreach (KeyValuePair<string, string> kvp in bundleData)
            {
                ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(bundle.Room, out _))
                    continue;

                foreach (BundleIngredient ing in bundle.Ingredients)
                {
                    if (BundleParsing.IsCategoryRef(ing.ItemRef))
                        continue;
                    string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                    int stack = ing.Stack > 0 ? ing.Stack : 1;
                    if (!stacks.TryGetValue(id, out int existing) || stack > existing)
                        stacks[id] = stack;
                }
            }
            return stacks;
        }

        /// <summary>
        /// Build a (qualified item id → minimum quality required) lookup, same MAX-aggregation
        /// pattern as <see cref="BuildIngredientStacks"/>. Added 2026-05-29 to fix a bonus-item
        /// preview bug — Quality Crops needs gold-star Parsnips but the hub was rendering them
        /// as regular quality (no gold star). Pulling the max across every bundle this id
        /// appears in surfaces the strictest quality the player would need to plan for; same
        /// id with lower quality elsewhere still satisfies, so MAX is the safer framing.
        /// Quality scale matches Stardew: 0=basic, 1=silver, 2=gold, 4=iridium.
        /// </summary>
        public IReadOnlyDictionary<string, int> BuildIngredientQualities()
        {
            var qualities = new Dictionary<string, int>(System.StringComparer.Ordinal);
            Dictionary<string, string> bundleData = Game1.netWorldState.Value.BundleData;
            foreach (KeyValuePair<string, string> kvp in bundleData)
            {
                ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(bundle.Room, out _))
                    continue;

                foreach (BundleIngredient ing in bundle.Ingredients)
                {
                    if (BundleParsing.IsCategoryRef(ing.ItemRef))
                        continue;
                    string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                    int quality = ing.Quality;
                    if (!qualities.TryGetValue(id, out int existing) || quality > existing)
                        qualities[id] = quality;
                }
            }
            return qualities;
        }
    }
}
