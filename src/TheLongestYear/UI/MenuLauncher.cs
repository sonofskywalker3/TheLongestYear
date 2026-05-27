using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Centralises opening the planning hub and shrine shop. Exposes Open* methods called by RunController
    /// at week-start and by debug commands. Guards against opening over an existing menu or before a save
    /// is loaded. Hotkey wiring removed in Plan 05 (unused in v1; Plan 06 will re-enable the hotkey).
    /// </summary>
    internal sealed class MenuLauncher
    {
        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly MetaStore _store;
        private readonly RunController _runController;
        private readonly UpgradePurchaseService _purchases;

        public MenuLauncher(IMonitor monitor, GameplayConfig config, MetaStore store,
            RunController runController, UpgradePurchaseService purchases)
        {
            _monitor = monitor;
            _config = config;
            _store = store;
            _runController = runController;
            _purchases = purchases;
        }

        /// <summary>Open the planning hub. If <paramref name="seasonOverride"/> is set (Sunday-night
        /// day-28 case), the menu shows next season's bundles + bonus preview and routes the pick
        /// to <see cref="RunState.NextMonthSelection"/> rather than CurrentSelection.</summary>
        public void OpenWeeklyHub(CoreSeason? seasonOverride = null)
        {
            if (!CanOpen()) return;

            CoreSeason offerSeason = seasonOverride ?? _store.Run.Season;
            // Offer pool: for cross-month, ignore current month's selections (empty list).
            var selectionsForOffer = seasonOverride.HasValue
                ? (System.Collections.Generic.IReadOnlyCollection<Theme>)System.Array.Empty<Theme>()
                : _store.Run.SelectedThemesThisMonth;
            var offer = SelectionService.OfferForWeek(
                _store.Run.Seed,
                seasonOverride.HasValue ? _store.Run.WeekOfYear + 1 : _store.Run.WeekOfYear,
                selectionsForOffer);

            Game1.activeClickableMenu = new WeeklyHubMenu(
                _monitor, _runController, _config, _store.Run, _runController.Requirements,
                offer, offerSeason, isPreSelectForNextMonth: seasonOverride.HasValue);
            _monitor.Log(
                $"Opened planning hub (week {_store.Run.WeekOfYear}{(seasonOverride.HasValue ? $" → {offerSeason}" : "")}, " +
                $"offer: {string.Join(",", offer)}).",
                LogLevel.Info);
        }

        public void OpenShrineShop()
        {
            if (!CanOpen())
                return;

            Game1.activeClickableMenu = new JunimoShrineMenu(_monitor, _store, _purchases);
            _monitor.Log($"Opened Junimo Shrine (JP: {_store.State.JunimoPoints}).", LogLevel.Info);
        }

        public void OpenCookbook()
        {
            if (!CanOpen()) return;
            Game1.activeClickableMenu = new CookbookMenu(_monitor, _store.State);
            _monitor.Log("Opened Cookbook menu.", LogLevel.Info);
        }

        public void OpenCraftbook()
        {
            if (!CanOpen()) return;
            Game1.activeClickableMenu = new CraftbookMenu(_monitor, _store.State);
            _monitor.Log("Opened Craftbook menu.", LogLevel.Info);
        }

        /// <summary>UX2: per-season goal tracker, separate from the weekly hub selection surface.
        /// Opened on demand via the SeasonGoalsHotkey config.</summary>
        public void OpenSeasonGoals()
        {
            if (!CanOpen()) return;

            Game1.activeClickableMenu = new SeasonGoalsMenu(_monitor, _store.Run, _runController.Requirements);
            _monitor.Log(
                $"Opened Season Goals ({_store.Run.Season} day {_store.Run.DayOfMonth}, " +
                $"{_runController.Requirements.Count} bundles tracked).",
                LogLevel.Info);
        }

        private bool CanOpen()
        {
            if (!Context.IsWorldReady)
            {
                _monitor.Log("Cannot open menu: no save loaded.", LogLevel.Warn);
                return false;
            }
            if (Game1.activeClickableMenu != null)
            {
                _monitor.Log("Cannot open menu: another menu is already open.", LogLevel.Trace);
                return false;
            }
            // Only block on an active event — NOT on !Game1.player.CanMove. SMAPI fires
            // DayStarted before the wake-up animation finishes clearing input locks, so on
            // day 8+ wake-ups CanMove is still false at the moment OnDayStarted calls us,
            // and the hub silently swallows itself ("Cannot open menu: cutscene or input
            // lock" trace in the 2026-05-26 night-2 playtest log, lines 278-279). eventUp
            // is the cutscene-real check; CanMove was an over-defensive guard added earlier.
            // Day-1 fresh-run path is unaffected: CanMove is already true on fresh load.
            if (Game1.eventUp)
            {
                _monitor.Log("Cannot open menu: cutscene active (eventUp).", LogLevel.Trace);
                return false;
            }
            return true;
        }
    }
}
