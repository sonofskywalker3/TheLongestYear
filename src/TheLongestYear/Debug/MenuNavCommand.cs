using System;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug bridge only: press the controller's d-pad in the open menu without a
    /// controller, to check a menu's snap navigation headless (Celjhyn, 2026-11-07: down did nothing
    /// in the shrine). Each press goes through the pair Game1 sends for a pad button,
    /// <c>receiveGamePadButton</c> then <c>receiveKeyPress(mapGamePadButtonToKey(b))</c>, with
    /// <c>gamepadControls</c> and <c>snappyMenus</c> on for the call, as they are for a controller
    /// player. A snap moves the mouse to the new component; the press runs with a stand-in input
    /// whose mouse warp does nothing, so Jeff's real cursor never moves. Logs the snapped component
    /// (and the menu's scroll index, when it has one) after every press.</summary>
    internal static class MenuNavCommand
    {
        public const string Name = "tly_menunav";
        public const string Usage = "Usage: tly_menunav <up|down|left|right|a|lb|rb> [presses]";

        public static void Run(IMonitor monitor, string[] args)
        {
            IClickableMenu menu = Game1.activeClickableMenu;
            if (menu == null) { monitor.Log("tly_menunav: no menu is open.", LogLevel.Warn); return; }
            if (args.Length < 1 || !TryButton(args[0], out Buttons button)) { monitor.Log(Usage, LogLevel.Warn); return; }
            int presses = args.Length > 1 && int.TryParse(args[1], out int n) && n > 0 ? n : 1;

            while (menu.GetChildMenu() != null)
                menu = menu.GetChildMenu();
            monitor.Log($"tly_menunav: {menu.GetType().Name}, start at {Describe(menu)}.", LogLevel.Info);

            bool pad = Game1.options.gamepadControls, snappy = Game1.options.snappyMenus;
            InputState realInput = Game1.input;
            try
            {
                Game1.options.gamepadControls = true;
                Game1.options.snappyMenus = true;
                Game1.input = new NoWarpInput(realInput);
                if (Snapped(menu).currentlySnappedComponent == null)
                    Snapped(menu).snapToDefaultClickableComponent();
                for (int i = 0; i < presses; i++)
                {
                    menu.receiveGamePadButton(button);
                    if (Game1.activeClickableMenu != null)
                        menu.receiveKeyPress(Utility.mapGamePadButtonToKey(button));
                    monitor.Log($"tly_menunav: {args[0]} {i + 1}/{presses} -> {Describe(menu)}.", LogLevel.Info);
                }
            }
            finally
            {
                Game1.input = realInput;
                Game1.options.gamepadControls = pad;
                Game1.options.snappyMenus = snappy;
            }
        }

        private static bool TryButton(string arg, out Buttons button)
        {
            button = arg.ToLowerInvariant() switch
            {
                "up" => Buttons.DPadUp,
                "down" => Buttons.DPadDown,
                "left" => Buttons.DPadLeft,
                "right" => Buttons.DPadRight,
                "a" => Buttons.A,
                "lb" => Buttons.LeftShoulder,
                "rb" => Buttons.RightShoulder,
                _ => 0,
            };
            return button != 0;
        }

        /// <summary>The menu that holds the snap: a hosting menu's page (the one book keeps it on the
        /// open page), else the menu itself.</summary>
        private static IClickableMenu Snapped(IClickableMenu menu)
            => AccessTools.Field(menu.GetType(), "_page")?.GetValue(menu) as IClickableMenu ?? menu;

        private static string Describe(IClickableMenu outer)
        {
            IClickableMenu menu = Snapped(outer);
            ClickableComponent c = menu.currentlySnappedComponent;
            string snapped = c == null ? "nothing snapped" : $"'{c.name}' id {c.myID}";
            object scroll = AccessTools.Field(menu.GetType(), "_scrollIndex")?.GetValue(menu)
                ?? AccessTools.Field(menu.GetType(), "_pickerScroll")?.GetValue(menu);
            if (AccessTools.Field(menu.GetType(), "_sourceTabs")?.GetValue(menu) is System.Collections.IList tabs && tabs.Count > 1
                && AccessTools.Field(menu.GetType(), "_sourceTab")?.GetValue(menu) is int tab)
                snapped += $", source tab {tab + 1}/{tabs.Count} '{tabs[tab]}'";
            return scroll == null ? snapped : $"{snapped}, scroll {scroll}";
        }

        /// <summary>The game's input with the mouse warp switched off; everything else is the real one.</summary>
        private sealed class NoWarpInput : InputState
        {
            private readonly InputState _real;
            public NoWarpInput(InputState real) => _real = real;
            public override void SetMousePosition(int x, int y) { }
            public override MouseState GetMouseState() => _real.GetMouseState();
            public override KeyboardState GetKeyboardState() => _real.GetKeyboardState();
            public override GamePadState GetGamePadState() => _real.GetGamePadState();
        }
    }
}
