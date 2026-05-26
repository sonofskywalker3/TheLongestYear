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

        private YearPlan _plan;
        private bool _pendingReset;

        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset,
            System.Collections.Generic.IReadOnlyList<CcItem> catalog)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _jp = new JpCalculator(config.Jp);
            _catalog = (catalog != null && catalog.Count > 0) ? catalog : CcItemCatalog.Items;
        }

        private RunState Run => _store.Run;

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed and build its plan.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            Run.Season = (CoreSeason)(int)Game1.season;
            Run.DayOfMonth = Game1.dayOfMonth;
            _plan = new ContractGenerator().Generate(_catalog, Run.Seed);

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);
        }

        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (_pendingReset)
            {
                _pendingReset = false;
                _reset.PerformReset(_config.StartingMoney);
                Run.BeginNewRun(NewSeed());
                _plan = new ContractGenerator().Generate(_catalog, Run.Seed);
                _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
                return;
            }

            // Sync state from the game date; a new month clears championing (incl. the current champion).
            var season = (CoreSeason)(int)Game1.season;
            if (season != Run.Season)
                Run.BeginNewMonth(season);
            Run.Season = season;
            Run.DayOfMonth = Game1.dayOfMonth;

            // Only at week start: the previous week's champion expires and a fresh offer is presented.
            // (Mid-week reloads keep the persisted CurrentChampion, so the day-7 gate still sees it.)
            if (IsWeekStart(Run.DayOfMonth))
            {
                Run.CurrentChampion = null;
                PresentOffer();
            }
        }

        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            RunAction action = _runManager.EvaluateDayEnd(Run, _plan);
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
            var (bonus, liability) = ThemeModifiers.For(theme);
            _monitor.Log($"Championed {theme} (bonus {bonus}, liability {liability}). Required: {RequiredFor(theme)}.", LogLevel.Info);
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

        /// <summary>Log this week's champion offer (driven by the UI in Plan 05; debug command + week-start now).</summary>
        public void PresentOffer()
        {
            var offer = ChampionService.OfferForWeek(Run);
            _monitor.Log(
                $"Week {Run.WeekOfYear} champion offer: {string.Join(" OR ", offer)} " +
                $"(use 'tly_champion <theme>').",
                LogLevel.Info);
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
