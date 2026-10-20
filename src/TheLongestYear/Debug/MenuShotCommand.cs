using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_menushot: draws the open menu into an off-screen texture and saves it as a PNG, so
    /// a menu can be checked while the game window stays minimized (PrintWindow of a minimized
    /// window is blank). Black backdrop instead of the world; the menu itself draws as in play, at
    /// the current UI scale.</summary>
    internal static class MenuShotCommand
    {
        public const string Name = "tly_menushot";
        public const string Usage = "Debug: save the open menu as a PNG without showing the window. Usage: tly_menushot <path.png>";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (args.Length == 0) { monitor.Log(Usage, LogLevel.Info); return; }
            if (Game1.activeClickableMenu == null) { monitor.Log("tly_menushot: no menu open.", LogLevel.Warn); return; }
            string path = string.Join(" ", args);

            int width = Math.Max(1, Game1.uiViewport.Width);
            int height = Math.Max(1, Game1.uiViewport.Height);
            GraphicsDevice device = Game1.graphics.GraphicsDevice;
            RenderTargetBinding[] previous = device.GetRenderTargets();
            using var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            try
            {
                device.SetRenderTarget(target);
                device.Clear(Color.Black);
                SpriteBatch b = Game1.spriteBatch;
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                Game1.activeClickableMenu.draw(b);
                b.End();
            }
            finally
            {
                device.SetRenderTargets(previous);
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (FileStream file = File.Create(path))
                target.SaveAsPng(file, width, height);
            monitor.Log($"tly_menushot: saved {Game1.activeClickableMenu.GetType().Name} ({width}x{height}) to {path}.", LogLevel.Info);
        }
    }
}
