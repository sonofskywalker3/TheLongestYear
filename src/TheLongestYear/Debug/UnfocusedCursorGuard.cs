using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug bridge only: while the game window is not in front, a menu snap must not
    /// move the real mouse. Controller-style menus warp the cursor to the snapped component
    /// (<c>Mouse.SetPosition</c>), and an unattended run with those on (Stardew Access forces
    /// them) would drag the cursor of whoever is working at the desk. Applied only when
    /// <c>EnableDebugCommandBridge</c> is on; a shipped build never patches this.</summary>
    internal static class UnfocusedCursorGuard
    {
        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            harmony.Patch(
                AccessTools.Method(typeof(Mouse), nameof(Mouse.SetPosition), new[] { typeof(int), typeof(int) }),
                prefix: new HarmonyMethod(typeof(UnfocusedCursorGuard), nameof(SkipWhenUnfocused)));
            monitor.Log("Debug bridge: menu snaps will not move the mouse while the game window is not in front.", LogLevel.Info);
        }

        private static bool SkipWhenUnfocused() => Game1.game1 == null || Game1.game1.IsActive;
    }
}
