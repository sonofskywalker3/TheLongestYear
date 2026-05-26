using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Centralises opening the planning hub and shrine shop. Listens for the configurable hotkey,
    /// and exposes Open* methods called by RunController at week-start and by debug commands.
    /// Guards against opening over an existing menu or before a save is loaded.
    /// </summary>
    internal sealed class MenuLauncher
    {
        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly MetaStore _store;
        private readonly RunController _runController;
        private readonly UpgradePurchaseService _purchases;
        private readonly KeybindList _hotkey;

        public MenuLauncher(IMonitor monitor, GameplayConfig config, MetaStore store,
            RunController runController, UpgradePurchaseService purchases, IModEvents events)
        {
            _monitor = monitor;
            _config = config;
            _store = store;
            _runController = runController;
            _purchases = purchases;

            _hotkey = KeybindList.Parse(_config.WeeklyHubHotkey ?? "P");
            events.Input.ButtonsChanged += this.OnButtonsChanged;
        }

        public void OpenWeeklyHub()
        {
            if (!CanOpen())
                return;

            var offer = ChampionService.OfferForWeek(_store.Run);
            Game1.activeClickableMenu = new ContractPickMenu(
                _monitor, _runController, _config, _store.Run, _runController.CurrentPlan, offer);
            _monitor.Log($"Opened planning hub (week {_store.Run.WeekOfYear}, offer: {string.Join(",", offer)}).",
                LogLevel.Info);
        }

        public void OpenShrineShop()
        {
            if (!CanOpen())
                return;

            Game1.activeClickableMenu = new JunimoShrineMenu(_monitor, _store, _purchases);
            _monitor.Log($"Opened Junimo Shrine (JP: {_store.State.JunimoPoints}).", LogLevel.Info);
        }

        private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (_hotkey.JustPressed())
                OpenWeeklyHub();
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
