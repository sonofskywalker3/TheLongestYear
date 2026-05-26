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
    /// Drives the run loop: builds the YearPlan from the run seed, syncs RunState from the game date,
    /// offers the weekly champion, evaluates the day-end gate via RunManager, and executes the action
    /// (fail -> reset next morning, advance month -> clear championing, win -> log). Interim JP is banked
    /// on run end (full JP + donation surface are Plan 04).
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

        private YearPlan _plan;
        private bool _pendingReset;
        private TheLongestYear.UI.MenuLauncher _launcher;

        /// <summary>The current run's contract plan; exposed for the UI layer (Plan 05).</summary>
        public YearPlan CurrentPlan => _plan;

        /// <summary>Classified bundle requirements for this run; exposed for the UI + donation layer.</summary>
        public System.Collections.Generic.IReadOnlyList<BundleRequirement> Requirements => _requirements;

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

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed and build its plan.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            Run.Season = (CoreSeason)(int)Game1.season;
            Run.DayOfMonth = Game1.dayOfMonth;
            _plan = BuildPlan();

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);
            DumpAssignmentTable("OnRunLoaded");
        }

        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (_pendingReset)
            {
                _pendingReset = false;
                _reset.PerformReset(_config.StartingMoney);
                Run.BeginNewRun(NewSeed());
                _plan = BuildPlan();
                _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
                return;
            }

            // Sync state from the game date; a new month clears championing (incl. the current champion).
            var season = (CoreSeason)(int)Game1.season;
            if (season != Run.Season)
                Run.BeginNewMonth(season);
            Run.Season = season;
            Run.DayOfMonth = Game1.dayOfMonth;

            // Hub on Monday morning at week-start. TODO(2026-05-26): move to Sunday night per
            // user request; needs careful handling of day-28 cross-season transitions so the
            // hub picks for next season's offer pool rather than the (already-championed) current.
            if (IsWeekStart(Run.DayOfMonth))
            {
                Run.CurrentChampion = null;
                PresentOffer();
            }
        }

        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            AwardChampionContractBonusIfDue();
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
            RunAction action = _runManager.EvaluateDayEnd(Run, _requirements, vaultGateSatisfied);
            switch (action)
            {
                case RunAction.Continue:
                    break;

                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    break; // game advances the date; OnDayStarted clears championing

                case RunAction.FailReset:
                    AwardInterimJp("run failed");
                    _pendingReset = true;
                    break;

                case RunAction.Win:
                    AwardInterimJp("run WON — loop broken");
                    break;
            }
        }

        /// <summary>Champion one of this week's offered themes (driven by the UI in Plan 05; debug command now).</summary>
        public void ChampionByName(string themeName)
        {
            if (!Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            {
                _monitor.Log($"Unknown theme '{themeName}'. Options: {string.Join(", ", Enum.GetNames(typeof(Theme)))}.", LogLevel.Warn);
                return;
            }

            var offer = ChampionService.OfferForWeek(Run);
            if (!offer.Contains(theme))
            {
                _monitor.Log($"{theme} is not offered this week. Offer: {string.Join(", ", offer)}.", LogLevel.Warn);
                return;
            }

            Run.Champion(theme);
            PopulateBonusItemsForCurrentChampion();
            var (bonus, liability) = ThemeModifiers.For(theme);
            _monitor.Log(
                $"Championed {theme} (bonus {bonus}, liability {liability}). " +
                $"Bonus items this week: [{string.Join(", ", Run.CurrentWeekBonusItems)}].",
                LogLevel.Info);
        }

        /// <summary>Sample the per-week bonus list for the current champion and store it on RunState.
        /// Uses the live CcItem catalog for in-season obtainability lookups.</summary>
        private void PopulateBonusItemsForCurrentChampion()
        {
            Run.CurrentWeekBonusItems.Clear();
            if (!Run.CurrentChampion.HasValue) return;

            int maxCount = _config.BonusListSizeBySeason != null
                && _config.BonusListSizeBySeason.Length > (int)Run.Season
                    ? _config.BonusListSizeBySeason[(int)Run.Season]
                    : 5;

            var sample = BonusItemSampler.SampleForTheme(
                Run.Seed, Run.WeekOfYear,
                Run.CurrentChampion.Value, Run.Season,
                _requirements,
                IsObtainableInCurrentSeason,
                maxCount);

            Run.CurrentWeekBonusItems.AddRange(sample);
        }

        /// <summary>Obtainability predicate for the sampler: looks up the item in the CcItem
        /// catalog and tests against this season's ObtainableSeasons. Items not in the catalog
        /// (rare — e.g. SVE additions that didn't classify) default to obtainable so the player
        /// isn't silently denied a bonus opportunity.</summary>
        private bool IsObtainableInCurrentSeason(string itemId)
        {
            foreach (var item in _catalog)
                if (item.Id == itemId)
                    return item.ObtainableSeasons.Contains(Run.Season);
            return true;
        }

        /// <summary>Simulate a CC donation (the real donation surface is Plan 04).</summary>
        public void Donate(string itemId)
        {
            Run.RecordDonation(itemId);
            _monitor.Log($"Donated '{itemId}'. Ledger size {Run.DonatedItemIds.Count}.", LogLevel.Info);
        }

        public void PrintRunState()
        {
            _monitor.Log(
                $"Run {Run.RunNumber}: {Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}). " +
                $"Champion={Run.CurrentChampion?.ToString() ?? "none"}, " +
                $"championedThisMonth=[{string.Join(",", Run.ChampionedThemesThisMonth)}], " +
                $"donated={Run.DonatedItemIds.Count}, JP banked={_store.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>Wired by ModEntry after the launcher is constructed (it needs CurrentPlan, which we own).</summary>
        public void AttachLauncher(TheLongestYear.UI.MenuLauncher launcher) => _launcher = launcher;

        /// <summary>
        /// Pick a new seed and regenerate the year plan. Does NOT touch the active menu — callers
        /// that want to re-open the hub should call <see cref="Reroll"/> instead. The planning hub's
        /// refresh button calls this directly so the menu can re-read CurrentPlan without flicker.
        /// </summary>
        public void RerollPlan()
        {
            int newSeed = NewSeed();
            Run.Seed = newSeed;
            _plan = BuildPlan();
            Run.CurrentChampion = null;      // clear last week's pick so the offer is fresh
            _monitor.Log(
                $"Reroll: new seed {newSeed}; plan regenerated " +
                "(placement is deterministic — partition will match unless catalog/overrides changed).",
                LogLevel.Info);
        }

        /// <summary>Build a plan from the live catalog and the user's season overrides.</summary>
        private TheLongestYear.Core.YearPlan BuildPlan()
        {
            var overrides = ParseSeasonOverrides(_config.SeasonOverrides);
            return new ContractGenerator(_config.ContractItemCapBySeason, overrides)
                .Generate(_catalog, Run.Seed);
        }

        /// <summary>
        /// Merge GameplayConfig.DefaultSeasonOverrides with the user's loaded config overrides
        /// (user wins on conflict). Both maps are validated against the Season enum; unparseable
        /// values are skipped with a Warn so a typo in config.json doesn't kill the load.
        /// </summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, CoreSeason> ParseSeasonOverrides(
            System.Collections.Generic.Dictionary<string, string> raw)
        {
            var merged = new System.Collections.Generic.Dictionary<string, CoreSeason>();

            foreach (var kv in TheLongestYear.Core.GameplayConfig.DefaultSeasonOverrides)
                if (Enum.TryParse(kv.Value, ignoreCase: true, out CoreSeason s))
                    merged[kv.Key] = s;

            if (raw != null)
            {
                foreach (var kv in raw)
                {
                    if (Enum.TryParse(kv.Value, ignoreCase: true, out CoreSeason s))
                        merged[kv.Key] = s;        // user wins
                    else
                        _monitor.Log(
                            $"SeasonOverrides: '{kv.Value}' is not a valid season for id '{kv.Key}' — ignoring.",
                            LogLevel.Warn);
                }
            }

            return merged;
        }

        /// <summary>Log the (season, theme) → required-items table for review.</summary>
        public void DumpAssignmentTable(string reason)
        {
            if (_plan == null) return;
            _monitor.Log($"--- Year assignment table ({reason}) ---", LogLevel.Info);
            foreach (CoreSeason s in Enum.GetValues(typeof(CoreSeason)))
            {
                int seasonTotal = _plan.ForSeason(s).Sum(c => c.RequiredItemIds.Count);
                _monitor.Log($"{s} (total {seasonTotal}):", LogLevel.Info);
                foreach (Theme t in Enum.GetValues(typeof(Theme)))
                {
                    var c = _plan.Get(s, t);
                    string items = c.RequiredItemIds.Count == 0
                        ? "(empty)"
                        : string.Join(", ", c.RequiredItemIds);
                    _monitor.Log($"  {t} ({c.RequiredItemIds.Count}): {items}", LogLevel.Info);
                }
            }
            _monitor.Log("--- end assignment table ---", LogLevel.Info);
        }

        /// <summary>
        /// Debug-only: reroll + clear the per-week presentation guard + (re)open the planning hub.
        /// Used by the tly_reroll bridge command path. The in-menu refresh button uses
        /// <see cref="RerollPlan"/> directly to avoid menu re-open flicker.
        /// </summary>
        public void Reroll()
        {
            RerollPlan();
            Run.OfferPresentedWeek = -1;
            _launcher?.OpenWeeklyHub();
        }

        /// <summary>Log this week's champion offer (driven by the UI in Plan 05; debug command + week-start now).</summary>
        public void PresentOffer()
        {
            // The hub opens at most once per week. OfferPresentedWeek tracks the last week we presented.
            if (Run.OfferPresentedWeek == Run.WeekOfYear)
            {
                _monitor.Log($"PresentOffer: already shown this week ({Run.WeekOfYear}), skipping.", LogLevel.Trace);
                return;
            }

            var offer = ChampionService.OfferForWeek(Run);
            _monitor.Log(
                $"Week {Run.WeekOfYear} champion offer: {string.Join(" OR ", offer)} (opening planning hub).",
                LogLevel.Info);

            Run.OfferPresentedWeek = Run.WeekOfYear;
            _launcher?.OpenWeeklyHub();
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

        /// <summary>
        /// If the player has a championed theme this week and its contract is satisfied by current
        /// donations, award the (season-scaled) CompletedContractBonus once per week. Plan 06 will
        /// wire this to the real "championed contract just cleared" event; here we check on day-end.
        /// </summary>
        private void AwardChampionContractBonusIfDue()
        {
            if (!Run.CurrentChampion.HasValue || _plan == null) return;
            if (Run.AwardedChampionWeeks.Contains(Run.WeekOfYear)) return;

            Contract contract = _plan.Get(Run.Season, Run.CurrentChampion.Value);
            if (!contract.IsSatisfiedBy(Run.DonatedSet())) return;

            long bonus = _jp.CompletedContractBonus(Run.WeekOfYear);
            _store.State.JunimoPoints += bonus;
            Run.AwardedChampionWeeks.Add(Run.WeekOfYear);
            _monitor.Log(
                $"Championed {Run.CurrentChampion} contract complete -> +{bonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        private string RequiredFor(Theme theme)
        {
            var items = _plan.Get(Run.Season, theme).RequiredItemIds;
            return items.Count == 0 ? "(nothing)" : string.Join(", ", items);
        }

        private string DescribeWeek() => $"{Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}).";

        private static bool IsWeekStart(int dayOfMonth) => (dayOfMonth - 1) % Calendar.DaysPerWeek == 0;

        private static int NewSeed() => Guid.NewGuid().GetHashCode();
    }
}
