using System;
using System.Runtime.InteropServices;
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
        /// <summary>Each call is logged up to this many times per session, so a run left alone can
        /// show afterwards whether the game moved the real mouse, without flooding the log.</summary>
        private const int MaxLoggedCalls = 40;

        private static IMonitor _monitor;
        private static int _logged;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;
            harmony.Patch(
                AccessTools.Method(typeof(Mouse), nameof(Mouse.SetPosition), new[] { typeof(int), typeof(int) }),
                prefix: new HarmonyMethod(typeof(UnfocusedCursorGuard), nameof(SkipWhenUnfocused)));
            monitor.Log("Debug bridge: menu snaps will not move the mouse while the game window is not in front.", LogLevel.Info);
        }

        // SDL_WindowFlags (SDL_video.h). Game.IsActive alone is not enough: it reads true for the
        // first seconds after launch even when the window starts minimized, and the title screen's
        // snaps then moved the real cursor twice per launch (seen 2026-11-24).
        private const uint SdlWindowMinimized = 0x00000040;
        private const uint SdlWindowInputFocus = 0x00000200;

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SDL_GetWindowFlags(IntPtr window);

        private static bool _sdlUnavailable;

        /// <summary>Whether the game window really has the keyboard and is not minimized.</summary>
        internal static bool WindowInFront()
        {
            if (Game1.game1 == null) return true;
            if (!Game1.game1.IsActive) return false;
            if (_sdlUnavailable) return true;
            try
            {
                uint flags = SDL_GetWindowFlags(Game1.game1.Window.Handle);
                return (flags & SdlWindowInputFocus) != 0 && (flags & SdlWindowMinimized) == 0;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                _sdlUnavailable = true;
                _monitor?.Log($"Debug bridge: SDL window flags unavailable ({ex.GetType().Name}); the mouse guard falls back to Game.IsActive.", LogLevel.Warn);
                return true;
            }
        }

        private static bool SkipWhenUnfocused(int x, int y)
        {
            bool allow = WindowInFront();
            if (_logged < MaxLoggedCalls)
            {
                _logged++;
                _monitor?.Log(
                    $"Debug bridge: Mouse.SetPosition({x}, {y}) {(allow ? "MOVED the mouse (window active)" : "blocked (window not active)")} " +
                    $"from {Game1.activeClickableMenu?.GetType().Name ?? "no menu"}.",
                    allow ? LogLevel.Warn : LogLevel.Trace);
            }
            return allow;
        }
    }
}
