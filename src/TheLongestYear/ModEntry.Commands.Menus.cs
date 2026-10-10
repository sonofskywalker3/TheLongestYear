using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        private void CmdOpenCookbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCookbook();
        }

        private void CmdOpenCraftbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCraftbook();
        }

        private void CmdOpenHerdBook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenHerdBook();
        }

        private void CmdOpenStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null)
            {
                this.Monitor.Log("No stash chest found. Own stash_1 and run tly_setstash first.", LogLevel.Warn);
                return;
            }
            chest.ShowMenu();
        }

        /// <summary>Debug: open the planning shrine on a tab, the same construction the statue's
        /// checkForAction patch does, so the bridge can exercise every tab's row builder.</summary>
        private void CmdOpenShrine(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var tab = TheLongestYear.UI.ShrinePreviewMenu.ShrineTab.Active;
            if (args.Length > 0 && !System.Enum.TryParse(args[0], ignoreCase: true, out tab))
            {
                this.Monitor.Log("Usage: tly_openshrine [active|boosts|plan|donate]", LogLevel.Warn);
                return;
            }
            var menu = new TheLongestYear.UI.ShrinePreviewMenu(
                _meta.State, _meta.State.EffectiveDifficulty(_config).ShrinePriceFactor, _meta.Run,
                (id, skill) => _boostPurchases.TryBuy(id, skill),
                () => _runController?.IsVoluntaryRestartOffered() == true,
                () => _runController?.AskVoluntaryRestart(),
                ShrineDonationService.Active);
            if (!menu.ShowTab(tab))
            {
                this.Monitor.Log($"tly_openshrine: no {tab} tab this week (no shrine goals); opened on Active.", LogLevel.Warn);
                tab = TheLongestYear.UI.ShrinePreviewMenu.ShrineTab.Active;
            }
            Game1.activeClickableMenu = menu;
            this.Monitor.Log($"tly_openshrine: shrine opened on the {tab} tab.", LogLevel.Info);
            var block = _runController?.VoluntaryRestartBlock() ?? TheLongestYear.Core.Day28.RestartBlock.ResetRunning;
            this.Monitor.Log(
                block == TheLongestYear.Core.Day28.RestartBlock.None
                    ? "tly_openshrine: restart button shown."
                    : $"tly_openshrine: restart button hidden ({block}).",
                LogLevel.Info);
            // Debug: the restart button's bounds against the tabs at this window size.
            var restart = this.Helper.Reflection.GetField<ClickableComponent>(menu, "_restartButton").GetValue();
            if (restart != null)
            {
                var tabs = this.Helper.Reflection.GetField<List<ClickableTextureComponent>>(menu, "_tabs").GetValue();
                var tabRects = tabs.Select(t => t.bounds).ToList();
                if (menu.upperRightCloseButton != null) tabRects.Add(menu.upperRightCloseButton.bounds);
                bool overlap = tabRects.Any(r => r.Intersects(restart.bounds));
                this.Monitor.Log(
                    $"tly_openshrine: viewport {Game1.uiViewport.Width}x{Game1.uiViewport.Height}, restart {restart.bounds}, " +
                    $"tabs+close {string.Join(" ", tabRects)}, overlap={overlap}.", LogLevel.Info);
            }
        }

        private void CmdOpenHub(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenWeeklyHub();
        }

        private void CmdSeasonGoals(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenSeasonGoals();
        }

        private void CmdOpenShop(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenShrineShop();
            // Debug: tly_openshop <tab> [hoverRow] [scroll] shows a tab (for headless screenshots).
            if (args.Length >= 1 && Game1.activeClickableMenu is TheLongestYear.UI.JunimoShrineMenu shop
                && System.Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.UpgradeCategory tab))
                shop.DebugShow(tab,
                    args.Length >= 2 && int.TryParse(args[1], out int hover) ? hover : -1,
                    args.Length >= 3 && int.TryParse(args[2], out int scroll) ? scroll : 0);
        }
    }
}
