using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.UI;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug bridge only: headless check of the recipe books' back-out (0.18.141,
    /// gmastern1). In the picker the close button, Escape, the menu key and controller B must
    /// step back to the slot list and leave the book open; from the slot list the same inputs
    /// must close the book and fire its exitFunction once (on the rewind night that exitFunction
    /// is what continues the run). Every input goes through the menu's own handlers:
    /// <c>receiveLeftClick</c> at the centre of the close button, <c>receiveKeyPress</c>, and for
    /// B the same <c>receiveGamePadButton</c> then <c>receiveKeyPress(mapGamePadButtonToKey(B))</c>
    /// pair Game1 sends.
    ///
    /// <c>tly_booktest cook|craft|both</c> opens each book itself and prints PASS/FAIL per input
    /// and mode. <c>tly_booktest live picker</c> runs the picker checks on whichever book is open
    /// (the rewind-night offer) and leaves it open; <c>tly_booktest live close &lt;input&gt;</c>
    /// closes that book from the slot list with one input.</summary>
    internal static class RecipeBookBackOutCommand
    {
        public const string Name = "tly_booktest";
        public const string Usage =
            "Usage: tly_booktest cook|craft|both | tly_booktest live picker | tly_booktest live close click|escape|menukey|padb";

        private const string ModeLive = "live";
        private const string LivePicker = "picker";
        private const string LiveClose = "close";
        private const int MinLiveCloseArgs = 3;

        private static readonly string[] Inputs = { "click", "escape", "menukey", "padb" };

        public static void Run(IMonitor monitor, Action openCookbook, Action openCraftbook, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { monitor.Log(Usage, LogLevel.Warn); return; }

            string mode = args[0].ToLowerInvariant();
            if (mode == ModeLive)
            {
                RunLive(monitor, args);
                return;
            }

            var books = new List<(string name, Action open)>();
            if (mode is "cook" or "both") books.Add(("Cookbook", openCookbook));
            if (mode is "craft" or "both") books.Add(("Craftbook", openCraftbook));
            if (books.Count == 0) { monitor.Log(Usage, LogLevel.Warn); return; }
            if (Game1.activeClickableMenu != null)
            {
                monitor.Log($"tly_booktest: close {Game1.activeClickableMenu.GetType().Name} first.", LogLevel.Warn);
                return;
            }

            int pass = 0, fail = 0;
            foreach ((string name, Action open) in books)
            {
                foreach (string input in Inputs)
                {
                    bool ok = PickerCase(monitor, name, open, input);
                    if (ok) pass++; else fail++;
                }
                foreach (string input in Inputs)
                {
                    bool ok = SlotCase(monitor, name, open, input);
                    if (ok) pass++; else fail++;
                }
            }
            monitor.Log($"tly_booktest: done, {pass} PASS, {fail} FAIL.", fail == 0 ? LogLevel.Info : LogLevel.Error);
        }

        /// <summary>Open the book, open the picker on an empty slot, send one input: the book must
        /// stay open on the slot list and its exitFunction must not fire.</summary>
        private static bool PickerCase(IMonitor monitor, string name, Action open, string input)
        {
            if (!OpenFresh(monitor, name, open, out IClickableMenu menu, out Func<int> exits)) return false;
            bool ok = PickerCheck(monitor, name, menu, exits, input);
            if (Game1.activeClickableMenu == menu) menu.exitThisMenu(playSound: false);
            return ok;
        }

        /// <summary>Open the book on its slot list and send one input: the book must close and
        /// its exitFunction must fire exactly once.</summary>
        private static bool SlotCase(IMonitor monitor, string name, Action open, string input)
        {
            if (!OpenFresh(monitor, name, open, out IClickableMenu menu, out Func<int> exits)) return false;
            return SlotCloseCheck(monitor, name, menu, exits(), exits, input);
        }

        private static bool PickerCheck(IMonitor monitor, string name, IClickableMenu menu, Func<int> exits, string input)
        {
            var book = (IRecipeBookMenu)menu;
            Rectangle? empty = book.FirstEmptySlotBounds();
            if (empty is not { } row)
            {
                monitor.Log($"tly_booktest: {name} picker/{input} SETUP FAIL: no empty slot row (book full?).", LogLevel.Error);
                return false;
            }
            menu.receiveLeftClick(row.Center.X, row.Center.Y, playSound: false);
            if (!book.PickerOpen)
            {
                monitor.Log($"tly_booktest: {name} picker/{input} SETUP FAIL: clicking the empty slot did not open the picker (nothing bankable?).", LogLevel.Error);
                return false;
            }
            int exitsBefore = exits();
            Send(menu, input);
            bool stillOpen = Game1.activeClickableMenu == menu;
            bool pickerClosed = !book.PickerOpen;
            bool noExit = exits() == exitsBefore;
            bool ok = stillOpen && pickerClosed && noExit;
            monitor.Log(
                $"tly_booktest: {name} picker/{input} {(ok ? "PASS" : "FAIL")}: bookStillOpen={stillOpen}, pickerClosed={pickerClosed}, " +
                $"exitFunctionFired={!noExit}, activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}.",
                ok ? LogLevel.Info : LogLevel.Error);
            return ok;
        }

        private static bool SlotCloseCheck(IMonitor monitor, string name, IClickableMenu menu, int exitsBefore, Func<int> exits, string input)
        {
            var book = (IRecipeBookMenu)menu;
            if (book.PickerOpen)
            {
                monitor.Log($"tly_booktest: {name} slot/{input} SETUP FAIL: the picker is open.", LogLevel.Error);
                return false;
            }
            Send(menu, input);
            bool closed = Game1.activeClickableMenu != menu;
            int fired = exits == null ? -1 : exits() - exitsBefore;
            bool ok = closed && (exits == null || fired == 1);
            monitor.Log(
                $"tly_booktest: {name} slot/{input} {(ok ? "PASS" : "FAIL")}: bookClosed={closed}, " +
                $"exitFunctionFired={(exits == null ? "n/a (live)" : fired.ToString())}, " +
                $"activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}.",
                ok ? LogLevel.Info : LogLevel.Error);
            return ok;
        }

        private static bool OpenFresh(IMonitor monitor, string name, Action open, out IClickableMenu menu, out Func<int> exits)
        {
            menu = null;
            exits = null;
            open();
            if (Game1.activeClickableMenu is not IRecipeBookMenu)
            {
                monitor.Log($"tly_booktest: {name} did not open (activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}).", LogLevel.Error);
                return false;
            }
            menu = Game1.activeClickableMenu;
            int count = 0;
            // Stands in for the rewind night's continuation (RunController.WatchRewindMenu).
            menu.exitFunction = () => count++;
            exits = () => count;
            return true;
        }

        private static void RunLive(IMonitor monitor, string[] args)
        {
            if (Game1.activeClickableMenu is not IRecipeBookMenu)
            {
                monitor.Log($"tly_booktest live: no recipe book is open (activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}).", LogLevel.Warn);
                return;
            }
            IClickableMenu menu = Game1.activeClickableMenu;
            string name = menu.GetType().Name;
            string step = args.Length > 1 ? args[1].ToLowerInvariant() : "";
            if (step == LivePicker)
            {
                // The rewind night's exitFunction is left alone: it must not run while the picker
                // backs out. The log shows the reset continuing if it did.
                int fails = 0;
                foreach (string input in Inputs)
                    if (!PickerCheck(monitor, name, menu, () => 0, input)) fails++;
                monitor.Log($"tly_booktest live picker: {Inputs.Length - fails} PASS, {fails} FAIL; book left open.", fails == 0 ? LogLevel.Info : LogLevel.Error);
                return;
            }
            if (step == LiveClose && args.Length >= MinLiveCloseArgs && Array.IndexOf(Inputs, args[2].ToLowerInvariant()) >= 0)
            {
                SlotCloseCheck(monitor, name, menu, 0, null, args[2].ToLowerInvariant());
                return;
            }
            monitor.Log(Usage, LogLevel.Warn);
        }

        /// <summary>Deliver one input through the menu's own handlers, the way Game1 would.</summary>
        private static void Send(IClickableMenu menu, string input)
        {
            switch (input)
            {
                case "click":
                    Rectangle close = menu.upperRightCloseButton.bounds;
                    menu.receiveLeftClick(close.Center.X, close.Center.Y, playSound: false);
                    break;
                case "escape":
                    menu.receiveKeyPress(Keys.Escape);
                    break;
                case "menukey":
                    menu.receiveKeyPress(Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.menuButton));
                    break;
                case "padb":
                    // Game1 sends receiveGamePadButton first, then the mapped key if the menu is
                    // still the active one (Game1.cs, gamepad menu input).
                    menu.receiveGamePadButton(Buttons.B);
                    if (Game1.activeClickableMenu == menu)
                        menu.receiveKeyPress(Utility.mapGamePadButtonToKey(Buttons.B));
                    break;
            }
        }
    }
}
