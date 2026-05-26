using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Drives the run loop: syncs RunState from the game date, opens the Sunday-night planning
    /// hub with the weekly selection offer, evaluates the day-end gate via RunManager + bundle
    /// requirements, and executes the action (fail → reset next morning, advance month → consume
    /// any day-28 pre-pick, win → log). Interim JP is banked on run end.
    /// </summary>
    internal sealed class RunController
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly WorldResetService _reset;
        private readonly RunManager _runManager = new RunManager(new GateEvaluator());
        private readonly JpCalculator _jp;
        private readonly System.Collections.Generic.IReadOnlyList<CcItem> _catalog;
        private readonly System.Collections.Generic.IReadOnlyList<BundleRequirement> _requirements;

        private bool _pendingReset;
        private TheLongestYear.UI.MenuLauncher _launcher;

        /// <summary>Classified bundle requirements for this run; exposed for the UI + donation layer.</summary>
        public System.Collections.Generic.IReadOnlyList<BundleRequirement> Requirements => _requirements;

        /// <summary>CcItem catalog (rarity/season/theme metadata); exposed so the UI can look up
        /// per-season obtainability when computing the bonus-item preview per card.</summary>
        public System.Collections.Generic.IReadOnlyList<CcItem> Catalog => _catalog;

        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset,
            System.Collections.Generic.IReadOnlyList<CcItem> catalog,
            System.Collections.Generic.IReadOnlyList<BundleRequirement> requirements = null)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _jp = new JpCalculator(config.Jp);
            _catalog = (catalog != null && catalog.Count > 0) ? catalog : CcItemCatalog.Items;
            _requirements = requirements ?? new System.Collections.Generic.List<BundleRequirement>();
        }

        private RunState Run => _store.Run;

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            Run.Season = (CoreSeason)(int)Game1.season;
            Run.DayOfMonth = Game1.dayOfMonth;

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);
        }

        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (_pendingReset)
            {
                _pendingReset = false;
                _reset.PerformReset(_config.StartingMoney);
                Run.BeginNewRun(NewSeed());
                _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
                return;
            }

            // Sync state from the game date; a new month clears the month's selections (the
            // previous-day's Sunday-night day-28 pre-pick is consumed inside BeginNewMonth →
            // CurrentSelection).
            var season = (CoreSeason)(int)Game1.season;
            if (season != Run.Season)
            {
                Run.BeginNewMonth(season);
                // BeginNewMonth may have just installed NextMonthSelection as CurrentSelection;
                // the bonus list still reflects last month's season, so re-sample now.
                if (Run.CurrentSelection.HasValue)
                {
                    PopulateBonusItemsForCurrentSelection();
                    _monitor.Log(
                        $"Day-28 pre-pick applied: {Run.CurrentSelection} is the week-1 selection of {season}.",
                        LogLevel.Info);
                }
            }
            Run.Season = season;
            Run.DayOfMonth = Game1.dayOfMonth;

            // Safety net for the FIRST day of a fresh run (Spring 1 of a new run has no previous
            // day-28 to trigger Sunday-night planning, and the player has no selection yet).
            if (Run.DayOfMonth == 1 && Run.Season == CoreSeason.Spring
                && !Run.CurrentSelection.HasValue && Run.OfferPresentedWeek != Run.WeekOfYear)
            {
                PresentOffer(targetWeekOfYear: Run.WeekOfYear);
            }
        }

        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
            RunAction action = _runManager.EvaluateDayEnd(Run, _requirements, vaultGateSatisfied);
            switch (action)
            {
                case RunAction.Continue:
                    break;

                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    break; // game advances the date; OnDayStarted clears the month's selections

                case RunAction.FailReset:
                    AwardInterimJp("run failed");
                    _pendingReset = true;
                    break;

                case RunAction.Win:
                    AwardInterimJp("run WON — loop broken");
                    break;
            }

            // Sunday-night planning: open the hub at week-end so the player picks for the
            // upcoming week before sleeping. Day-28 case crosses to next season — the hub
            // shows next season's bundles + bonus preview, and the pick is stored on
            // RunState.NextMonthSelection which BeginNewMonth consumes on tomorrow's
            // OnDayStarted. Suppressed on a pending reset (the run is about to end anyway).
            if (!_pendingReset && action != RunAction.Win && Calendar.IsWeekEnd(Run.DayOfMonth))
            {
                bool isMonthEnd = Calendar.IsMonthEnd(Run.DayOfMonth);
                CoreSeason? seasonOverride = isMonthEnd ? (CoreSeason?)NextSeason(Run.Season) : null;
                // Next week-of-year — for day 28 of Winter we'd cross past the year so skip.
                int? targetWeek = isMonthEnd
                    ? (Run.Season == CoreSeason.Winter ? (int?)null : Run.WeekOfYear + 1)
                    : Run.WeekOfYear + 1;
                if (targetWeek.HasValue)
                    PresentOffer(targetWeekOfYear: targetWeek.Value, seasonOverride: seasonOverride);
            }
        }

        private static CoreSeason NextSeason(CoreSeason s) => s switch
        {
            CoreSeason.Spring => CoreSeason.Summer,
            CoreSeason.Summer => CoreSeason.Fall,
            CoreSeason.Fall => CoreSeason.Winter,
            _ => CoreSeason.Spring
        };

        /// <summary>Select one of this week's offered themes (driven by the UI; debug command + UI).</summary>
        public void SelectByName(string themeName)
        {
            if (!Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            {
                _monitor.Log($"Unknown theme '{themeName}'. Options: {string.Join(", ", Enum.GetNames(typeof(Theme)))}.", LogLevel.Warn);
                return;
            }

            var offer = SelectionService.OfferForWeek(Run);
            if (!offer.Contains(theme))
            {
                _monitor.Log($"{theme} is not offered this week. Offer: {string.Join(", ", offer)}.", LogLevel.Warn);
                return;
            }

            Run.Select(theme);
            PopulateBonusItemsForCurrentSelection();
            var (bonus, liability) = ThemeModifiers.For(theme);
            _monitor.Log(
                $"Selected {theme} (bonus {bonus}, liability {liability}). " +
                $"Bonus items this week: [{string.Join(", ", Run.CurrentWeekBonusItems)}].",
                LogLevel.Info);
        }

        /// <summary>Sample the per-week bonus list for the current selection and store it on RunState.</summary>
        private void PopulateBonusItemsForCurrentSelection()
        {
            Run.CurrentWeekBonusItems.Clear();
            if (!Run.CurrentSelection.HasValue) return;

            int maxCount = BonusListSizeForCurrentSeason();
            var sample = BonusItemSampler.SampleForTheme(
                Run.Seed, Run.WeekOfYear,
                Run.CurrentSelection.Value, Run.Season,
                _requirements,
                IsObtainableInCurrentSeason,
                maxCount);

            Run.CurrentWeekBonusItems.AddRange(sample);
        }

        /// <summary>How big the per-card bonus-item preview list should be for the season.
        /// Lives in <see cref="BonusItemSampler.DefaultMaxCountBySeason"/>.</summary>
        public int BonusListSizeForCurrentSeason()
            => BonusItemSampler.DefaultMaxCountBySeason[(int)Run.Season];

        /// <summary>Obtainability predicate for the sampler: looks up the item in the CcItem
        /// catalog and tests against this season's ObtainableSeasons. Items not in the catalog
        /// default to obtainable so SVE/mod additions aren't silently excluded.</summary>
        private bool IsObtainableInCurrentSeason(string itemId)
            => IsObtainableInSeason(itemId, Run.Season);

        /// <summary>Mirror for the UI: same predicate, callable from the menu's bonus-preview path.</summary>
        public bool IsObtainableForCurrentSeason(string itemId)
            => IsObtainableInCurrentSeason(itemId);

        /// <summary>Same predicate but for an arbitrary season — used by the Sunday-night day-28
        /// hub when previewing NEXT season's bonus pool.</summary>
        public bool IsObtainableInSeason(string itemId, CoreSeason season)
        {
            foreach (var item in _catalog)
                if (item.Id == itemId)
                    return item.ObtainableSeasons.Contains(season);
            return true;
        }

        /// <summary>Day-28 Sunday-night flow: store the player's pick for week 1 of next month.</summary>
        public void PreSelectForNextMonth(Theme theme)
        {
            Run.NextMonthSelection = theme;
            _monitor.Log(
                $"Pre-pick set: {theme} will be the week-1 selection of {NextSeason(Run.Season)}.",
                LogLevel.Info);
        }

        /// <summary>Simulate a CC donation (the real donation surface is via DonationService).</summary>
        public void Donate(string itemId)
        {
            Run.RecordDonation(itemId);
            _monitor.Log($"Donated '{itemId}'. Ledger size {Run.DonatedItemIds.Count}.", LogLevel.Info);
        }

        public void PrintRunState()
        {
            _monitor.Log(
                $"Run {Run.RunNumber}: {Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}). " +
                $"Selection={Run.CurrentSelection?.ToString() ?? "none"}, " +
                $"selectedThisMonth=[{string.Join(",", Run.SelectedThemesThisMonth)}], " +
                $"donated={Run.DonatedItemIds.Count}, JP banked={_store.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>Wired by ModEntry after the launcher is constructed.</summary>
        public void AttachLauncher(TheLongestYear.UI.MenuLauncher launcher) => _launcher = launcher;

        /// <summary>Open the planning hub for a specific upcoming week. Sunday-night flow passes
        /// <c>Run.WeekOfYear + 1</c> (and a <paramref name="seasonOverride"/> on day 28) so the
        /// offer pool reflects what the player is actually choosing for.</summary>
        public void PresentOffer(int? targetWeekOfYear = null, CoreSeason? seasonOverride = null)
        {
            int week = targetWeekOfYear ?? Run.WeekOfYear;

            if (Run.OfferPresentedWeek == week)
            {
                _monitor.Log($"PresentOffer: already shown for week {week}, skipping.", LogLevel.Trace);
                return;
            }

            var selectionsForOffer = seasonOverride.HasValue
                ? (System.Collections.Generic.IReadOnlyCollection<Theme>)System.Array.Empty<Theme>()
                : Run.SelectedThemesThisMonth;
            var offer = SelectionService.OfferForWeek(Run.Seed, week, selectionsForOffer);

            string seasonTag = seasonOverride.HasValue ? $" (for {seasonOverride.Value})" : "";
            _monitor.Log(
                $"Week {week}{seasonTag} selection offer: {string.Join(" OR ", offer)} (opening planning hub).",
                LogLevel.Info);

            Run.OfferPresentedWeek = week;
            _launcher?.OpenWeeklyHub(seasonOverride);
        }

        private void AwardInterimJp(string reason)
        {
            var lines = Run.DonatedItemIds
                .GroupBy(CcItemCatalog.RarityOf)
                .Select(g => new DonationLine(g.Key, g.Count()));
            long awarded = _jp.ForDonationBatch(lines, Run.WeekOfYear, bundlesCompleted: 0, roomsCompleted: 0);
            _store.State.JunimoPoints += awarded;
            _monitor.Log(
                $"Interim JP for {reason}: +{awarded} (now {_store.State.JunimoPoints}). Persists on this day's save.",
                LogLevel.Info);
        }

        private string DescribeWeek() => $"{Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}).";

        private static int NewSeed() => Guid.NewGuid().GetHashCode();
    }
}
