using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    internal sealed partial class RunController
    {
        /// <summary>Rarity lookup for the bonus sampler: pull from the pre-built CcItem catalog so
        /// the hot path doesn't re-resolve via ItemRegistry per call. Unknown ids default to Common
        /// (full weight) so SVE/mod additions still surface in the bonus pool.</summary>
        private Rarity RarityForItem(string itemId)
        {
            foreach (var item in _catalog)
                if (item.Id == itemId)
                    return item.Rarity;
            return Rarity.Common;
        }

        /// <summary>Live per-slot completion state for a bundle (vanilla source of truth), or
        /// null when absent. Same NetBundles access pattern as ItemDonationSync/VaultPaymentSync:
        /// FieldDict.ContainsKey is the safe presence check.</summary>
        internal static bool[] SlotStateForBundle(int bundleIndex)
        {
            var bundles = Game1.netWorldState?.Value?.Bundles;
            if (bundles?.FieldDict == null) return null;
            return bundles.FieldDict.ContainsKey(bundleIndex) ? bundles[bundleIndex] : null;
        }

        /// <summary>Sample this week's goal slots for a theme+season — shared by the hub preview
        /// and the selection-time commit so both show the same goals. Pool = open, in-play slots
        /// (already-donated slots are never sampled; a complete bundle's leftover lines are dead).
        /// The pool is re-derived from live CC state at call time; the selection-time result is
        /// persisted in RunState.CurrentWeekBonusSlots, so committed goals don't reshuffle as
        /// slots complete mid-week.</summary>
        public System.Collections.Generic.IReadOnlyList<BonusSlot> SampleSlotsForTheme(
            Theme theme, CoreSeason season, int weekOfYear)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null) return System.Array.Empty<BonusSlot>();
            var pool = SlotPoolBuilder.OpenSlotsForTheme(
                bundleData, SlotStateForBundle, _requirements,
                theme, season, id => IsObtainableInWeek(id, weekOfYear), weekOfYear, ItemKindOf, RouteBasisOf);
            // Spec 2026-08-28-theme-week-budget: the season cap is a ceiling; the week asks for
            // its share of what the pool still holds so week 4 looks like week 1.
            int dueLines = 0;
            foreach (BonusSlot s in pool) if (s.Due) dueLines++;
            int budget = GoalBudget.For(
                BonusListSizeFor(season), dueLines, pool.Count - dueLines,
                _config.FillerAllowanceFor(season), GoalBudget.WeeksLeftInSeason(weekOfYear));
            return BonusSlotSampler.SampleSlots(
                Run.Seed, weekOfYear, theme, pool, RarityForItem, budget,
                remainingNeedForBundle: idx => SeasonNeedForBundle(idx, season),
                caps: GoalCaps,
                rules: RulesFor(season, weekOfYear));
        }

        /// <summary>Rules A, B and E for a season: filler allowance from config, effort from the
        /// derived model (null for an id no rule placed, which then takes the price bucket).</summary>
        private GoalSamplingRules RulesFor(CoreSeason season, int weekOfYear)
            => new GoalSamplingRules(season, _config.FillerAllowanceFor(season), EffortOf,
                Even: RandomizerForWeekPeek(weekOfYear).RandomThemeItems);

        private int? EffortOf(string itemId)
            => Availability != null && Availability.HasDerivedEffort(itemId)
                ? Availability.For(itemId).Effort
                : (int?)null;

        /// <summary>An item's availability basis text, for SlotPoolBuilder's routeTagOf (spec
        /// 2026-08-28-obtainable-board-4-boosts): it checks the basis for "Sneak Peek" to tag a
        /// dish goal that only exists because of that Boost.</summary>
        private string RouteBasisOf(string itemId)
            => Availability?.For(itemId).Basis;

        /// <summary>Rule C's number: how many goals the sampler would actually produce for the
        /// theme this week (tier 1 plus allowed filler, after every cap).</summary>
        public int AskableCount(Theme theme, CoreSeason season, int weekOfYear)
            => SampleSlotsForTheme(theme, season, weekOfYear).Count;

        /// <summary>The week's two cards under rule C. Every offer the mod shows or validates
        /// comes through here so the hub, the console pick and the Sunday-night preview agree.</summary>
        public System.Collections.Generic.IReadOnlyList<Theme> OfferFor(
            int weekOfYear, CoreSeason season, System.Collections.Generic.IReadOnlyCollection<Theme> selections)
            => SelectionService.OfferForWeek(Run.Seed, weekOfYear, selections, t => AskableCount(t, season, weekOfYear));

        /// <summary>The themes the hub's playtest re-roll may shuffle among (rule C's candidate list).</summary>
        public System.Collections.Generic.IReadOnlyList<Theme> OfferCandidates(
            int weekOfYear, CoreSeason season, System.Collections.Generic.IReadOnlyCollection<Theme> selections)
            => SelectionService.Candidates(selections, t => AskableCount(t, season, weekOfYear));

        /// <summary>tly_themepool: every open line for a theme with its tier and weight.</summary>
        public System.Collections.Generic.IReadOnlyList<GoalWeight> DescribeGoalPool(
            Theme theme, CoreSeason season, int weekOfYear, out System.Collections.Generic.IReadOnlyList<BonusSlot> pool)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null)
            {
                pool = System.Array.Empty<BonusSlot>();
                return System.Array.Empty<GoalWeight>();
            }
            pool = SlotPoolBuilder.OpenSlotsForTheme(
                bundleData, SlotStateForBundle, _requirements,
                theme, season, id => IsObtainableInWeek(id, weekOfYear), weekOfYear, ItemKindOf, RouteBasisOf);
            return GoalWeighting.For(pool.Select(s => s.ItemId), RulesFor(season, weekOfYear), RarityForItem);
        }

        /// <summary>How many more ingredient lines a bundle can still take: its required count
        /// minus the lines already filled. A bundle that only needs some of its listed items puts
        /// every open line in the goal pool, so without this the week can ask for three items from
        /// a bundle that needs two (Jeff, 2026-08-26, from emmalution's stream) - and since a goal
        /// needs a real deposit, the extra ask can never be met. Returns int.MaxValue when the
        /// bundle cannot be resolved, so an unknown bundle is never over-restricted.</summary>
        /// <summary>Spec 2026-08-28-even-year (sim H): the weekly goals follow the gate exactly. A
        /// pick-X-of-Y bundle may be asked for at most what its ramp demands by the end of this
        /// season minus what is already in, so a goal-completing player cannot donate Winter's share
        /// in Summer. Other kinds keep the plain required-minus-completed cap.</summary>
        private int SeasonNeedForBundle(int bundleIndex, CoreSeason season)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null) return int.MaxValue;
            foreach (var kvp in bundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (parsed.Index != bundleIndex) continue;
                BundleRequirement req = null;
                foreach (BundleRequirement r in _requirements)
                    if (string.Equals(r.Name, parsed.Name, StringComparison.Ordinal)) { req = r; break; }
                if (req == null) return RemainingNeedForBundle(bundleIndex);
                bool[] state = SlotStateForBundle(bundleIndex);
                int completed = 0;
                if (state != null)
                {
                    int lines = System.Math.Min(parsed.Ingredients.Count, state.Length);
                    for (int i = 0; i < lines; i++)
                        if (state[i]) completed++;
                }
                return SeasonNeed.For(req, season, completed);
            }
            return int.MaxValue;
        }

        private int RemainingNeedForBundle(int bundleIndex)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null) return int.MaxValue;

            foreach (var kvp in bundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (parsed.Index != bundleIndex) continue;

                int required = parsed.NumberOfSlots > 0 ? parsed.NumberOfSlots : parsed.Ingredients.Count;
                bool[] state = SlotStateForBundle(bundleIndex);
                int completed = 0;
                if (state != null)
                {
                    int lines = System.Math.Min(parsed.Ingredients.Count, state.Length);
                    for (int i = 0; i < lines; i++)
                        if (state[i]) completed++;
                }
                int remaining = required - completed;
                return remaining > 0 ? remaining : 0;
            }
            return int.MaxValue;
        }

        /// <summary>How big the per-card bonus-item preview list should be for the given season.
        /// Lives in <see cref="BonusItemSampler.DefaultMaxCountBySeason"/>.</summary>
        public int BonusListSizeFor(CoreSeason season)
            => BonusItemSampler.DefaultMaxCountBySeason[(int)season];

        /// <summary>
        /// Number of weather preview days to reveal (the next N days, starting tomorrow).
        /// Equals the highest Weather Sage tier owned (weather_sage_1 through weather_sage_6).
        /// Returns 0 if none owned.
        /// </summary>
        public int WeatherSageTier()
            => _store.State.HighestKeptTier("weather_sage_", 6);

        /// <summary>Obtainability predicate for an arbitrary season: looks up the item in the CcItem
        /// catalog and tests it against that season. Items not in the catalog default to obtainable
        /// so SVE/mod additions aren't silently excluded. Used by the Sunday-night day-28 hub when
        /// previewing NEXT season's bonus pool.</summary>
        public bool IsObtainableInSeason(string itemId, CoreSeason season)
            => IsObtainableInWeek(itemId, AvailabilityWeeks.LastWeekOf(season));

        /// <summary>Spec 2026-08-28-even-year: a weekly goal may name an item only from the week
        /// the availability model says it first exists (mines 30 floors a week, and so on).</summary>
        public bool IsObtainableInWeek(string itemId, int weekOfYear)
        {
            System.Collections.Generic.IReadOnlySet<CoreSeason> catalogSeasons = null;
            foreach (var item in _catalog)
                if (item.Id == itemId)
                {
                    catalogSeasons = item.ObtainableSeasons;
                    break;
                }
            return GoalObtainability.IsObtainable(catalogSeasons, Availability, itemId, weekOfYear);
        }

        /// <summary>Derived item model (fish, crab-pot, metals floors incl. location gating), so a
        /// weekly goal never names a desert or deep-mine fish before the season the gates allow
        /// it (Scorpion Carp as a Summer goal, bundle-loop audit 2026-08-29). Null = seasons only.</summary>
        public ItemAvailabilityModel Availability { get; set; }
    }
}
