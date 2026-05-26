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
        /// to <see cref="RunState.NextMonthChampion"/> rather than CurrentChampion.</summary>
        public void OpenWeeklyHub(CoreSeason? seasonOverride = null)
        {
            if (!CanOpen()) return;

            CoreSeason offerSeason = seasonOverride ?? _store.Run.Season;
            // Offer pool: for cross-month, ignore current month's championing (empty list).
            var championingForOffer = seasonOverride.HasValue
                ? (System.Collections.Generic.IReadOnlyCollection<Theme>)System.Array.Empty<Theme>()
                : _store.Run.ChampionedThemesThisMonth;
            var offer = ChampionService.OfferForWeek(
                _store.Run.Seed,
                seasonOverride.HasValue ? _store.Run.WeekOfYear + 1 : _store.Run.WeekOfYear,
                championingForOffer);

            Game1.activeClickableMenu = new ContractPickMenu(
                _monitor, _runController, _config, _store.Run, _runController.Requirements,
                offer, offerSeason, isPreChampionForNextMonth: seasonOverride.HasValue);
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
            if (Game1.eventUp || !Game1.player.CanMove)
            {
                _monitor.Log("Cannot open menu: cutscene or input lock.", LogLevel.Trace);
                return false;
            }
            return true;
        }
    }
}
