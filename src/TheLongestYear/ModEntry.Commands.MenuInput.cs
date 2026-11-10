using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear
{
    public partial class ModEntry
    {
        /// <summary>Debug: a left or right click at a UI point, handed to the open menu's own click
        /// handler, so a headless run can press a row (pin a keep, open a section) without the mouse.
        /// Usage: tly_menuclick left|right x y</summary>
        private void CmdMenuClick(string command, string[] args)
        {
            IClickableMenu menu = Game1.activeClickableMenu;
            if (menu == null) { this.Monitor.Log("tly_menuclick: no menu open.", LogLevel.Warn); return; }
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
    }
}
