using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear
{
    public partial class ModEntry
    {
        /// <summary>Debug: a left or right click at a UI point, handed to the open menu's own click
        /// handler, so a headless run can press a row (pin a keep, open a section) without the mouse.
        /// Usage: tly_menuclick left|right x y. With no arguments it lists the open menu's buttons and
        /// where they are, which is how to find the point to click on a menu another mod draws.</summary>
        private void CmdMenuClick(string command, string[] args)
        {
            IClickableMenu menu = Game1.activeClickableMenu;
            if (menu == null) { this.Monitor.Log("tly_menuclick: no menu open.", LogLevel.Warn); return; }
            if (args.Length == 0) { this.LogMenuButtons(menu); return; }
            if (args.Length < 3 || !int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y))
            {
                this.Monitor.Log("Usage: tly_menuclick left|right x y", LogLevel.Warn);
                return;
            }
            if (args[0] == "right") menu.receiveRightClick(x, y);
            else menu.receiveLeftClick(x, y);
            this.Monitor.Log(
                $"tly_menuclick: {args[0]} click at {x},{y} on {menu.GetType().Name} (UI viewport {Game1.uiViewport.Width}x{Game1.uiViewport.Height}).",
                LogLevel.Info);
        }

        /// <summary>Every button the menu holds in a field, alone or in a list, with its bounds.</summary>
        private void LogMenuButtons(IClickableMenu menu)
        {
            const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            this.Monitor.Log(
                $"tly_menuclick: {menu.GetType().FullName} at ({menu.xPositionOnScreen},{menu.yPositionOnScreen}) " +
                $"{menu.width}x{menu.height} (UI viewport {Game1.uiViewport.Width}x{Game1.uiViewport.Height}).", LogLevel.Info);
            for (Type type = menu.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(AnyField | BindingFlags.DeclaredOnly))
                {
                    object value = field.GetValue(menu);
                    IEnumerable<ClickableComponent> buttons = value switch
                    {
                        ClickableComponent one => new[] { one },
                        IEnumerable<ClickableComponent> many => many,
                        _ => null
                    };
                    if (buttons == null) continue;
                    int index = 0;
                    foreach (ClickableComponent button in buttons)
                    {
                        if (button == null) continue;
                        Rectangle b = button.bounds;
                        this.Monitor.Log(
                            $"tly_menuclick:   {field.Name}[{index++}] '{button.name}' label '{button.label}' " +
                            $"bounds ({b.X},{b.Y}) {b.Width}x{b.Height}, centre {b.Center.X} {b.Center.Y}.", LogLevel.Info);
                    }
                }
            }
        }

        /// <summary>Ticks a scripted button stays held: one press, seen as pressed exactly once.</summary>
        private const int PadHeldTicks = 2;

        private bool _padWatching;
        private Microsoft.Xna.Framework.Input.Buttons _padButton;
        private bool _padGamepadControlsBefore;

        /// <summary>Debug: press one controller button through the game's own controller path (see
        /// <see cref="TheLongestYear.Debug.ScriptedPadInput"/>). Usage: tly_pad &lt;Button&gt;, e.g. Y,
        /// LeftTrigger, A, B, DPadDown. Controller mode is on for the press and set back after it.</summary>
        private void CmdPad(string command, string[] args)
        {
            if (_padWatching) { this.Monitor.Log("tly_pad: a press is still running.", LogLevel.Warn); return; }
            if (args.Length < 1 || !System.Enum.TryParse(args[0], ignoreCase: true, out Microsoft.Xna.Framework.Input.Buttons button))
            {
                this.Monitor.Log("Usage: tly_pad <Button> (Y, LeftTrigger, A, B, DPadUp, DPadDown, ...)", LogLevel.Warn);
                return;
            }
            _padGamepadControlsBefore = Game1.options.gamepadControls;
            Game1.options.gamepadControls = true;
            Game1.lastCursorMotionWasMouse = false;
            TheLongestYear.Debug.ScriptedPadInput.Press(new HarmonyLib.Harmony(this.ModManifest.UniqueID + ".debugpad"), button, PadHeldTicks);
            _padWatching = true;
            _padButton = button;
            this.Helper.Events.GameLoop.UpdateTicked += this.OnScriptedPadTick;
            this.Monitor.Log($"tly_pad: {button} pressed on {Game1.activeClickableMenu?.GetType().Name ?? "no menu"}.", LogLevel.Info);
        }

        private void OnScriptedPadTick(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (TheLongestYear.Debug.ScriptedPadInput.Running)
            {
                var pad = Game1.input.GetGamePadState();
                this.Monitor.Log($"tly_pad: tick {Game1.ticks}: connected {pad.IsConnected}, buttons [{pad.Buttons}], LT {pad.Triggers.Left:0.0}, " +
                                 $"down {_padButton}={pad.IsButtonDown(_padButton)}, old {Game1.oldPadState.IsButtonDown(_padButton)}.", LogLevel.Trace);
                return;
            }
            _padWatching = false;
            Game1.options.gamepadControls = _padGamepadControlsBefore;
            this.Helper.Events.GameLoop.UpdateTicked -= this.OnScriptedPadTick;
            IClickableMenu menu = Game1.activeClickableMenu;
            ClickableComponent snapped = menu?.currentlySnappedComponent;
            this.Monitor.Log(
                $"tly_pad: released; menu {menu?.GetType().Name ?? "none"}, snapped {(snapped == null ? "none" : $"#{snapped.myID} '{snapped.name}' at {snapped.bounds}")}.",
                LogLevel.Info);
        }
    }
}
