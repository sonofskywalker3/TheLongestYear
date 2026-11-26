using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// A hotkey (P or L3 by default, set in the config menu) that opens the game menu straight on the
    /// Collections page's Bundle Log tab, and closes it again while the log is showing. Saves the trip
    /// through the menu tabs and down the side-tab column, which is slow on a controller.
    /// </summary>
    internal sealed class BundleLogHotkey
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly Func<string> _setting;
        private string _parsedFrom;
        private KeybindList _keys = new();

        public BundleLogHotkey(IModHelper helper, IMonitor monitor, Func<string> setting)
        {
            _helper = helper;
            _monitor = monitor;
            _setting = setting;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        }

        /// <summary>The configured keys, parsed once per change of the setting. A setting SMAPI cannot
        /// read is logged once and leaves the hotkey off.</summary>
        public KeybindList Keys
        {
            get
            {
                string raw = _setting() ?? "";
                if (raw == _parsedFrom) return _keys;
                _parsedFrom = raw;
                if (KeybindList.TryParse(raw, out KeybindList parsed, out string[] errors)) _keys = parsed;
                else
                {
                    _keys = new KeybindList();
                    _monitor.Log($"Bundle Log hotkey \"{raw}\" could not be read ({string.Join("; ", errors)}); the hotkey is off.", LogLevel.Warn);
                }
                return _keys;
            }
        }

        private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            KeybindList keys = this.Keys;
            if (!keys.JustPressed()) return;
            this.Act(() => _helper.Input.SuppressActiveKeybinds(keys));
        }

        /// <summary>Does what one press of the hotkey does: open, close or nothing. <paramref name="suppress"/>
        /// keeps the press from reaching the game when it did something. Public for the tly_press debug
        /// command, which runs it without the input layer (SMAPI reads no input while the window is in
        /// the background).</summary>
        public void Act(Action suppress)
        {
            CollectionsPage shown = (Game1.activeClickableMenu as GameMenu)?.GetCurrentPage() as CollectionsPage;
            bool logShowing = shown != null && shown.currentTab == BundleLogSideTabRules.TabKey && BundleLogSideTab.LogOn(shown) != null;
            // Opens only where vanilla's own menu key would: no minigame, festival contest or overnight event.
            bool canOpen = Game1.activeClickableMenu == null && Game1.CanShowPauseMenu();
            BundleLogSideTabRules.HotkeyAction action = BundleLogSideTabRules.OnHotkey(RunActivation.IsActive, logShowing, canOpen);
            _monitor.Log($"Bundle Log hotkey: {action} (run {RunActivation.IsActive}, log showing {logShowing}, menu {Game1.activeClickableMenu?.GetType().Name ?? "none"}, can open menus {Game1.CanShowPauseMenu()}).", LogLevel.Trace);
            switch (action)
            {
                case BundleLogSideTabRules.HotkeyAction.Open:
                    suppress();
                    BundleLogSideTab.OpenInGameMenu();
                    break;
                case BundleLogSideTabRules.HotkeyAction.Close:
                    suppress();
                    Game1.activeClickableMenu.exitThisMenu();
                    break;
            }
        }
    }
}
