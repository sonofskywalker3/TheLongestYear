using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_worldshot: draws the current location (actors, event actors and the farmer
    /// included, no HUD or menus) into an off-screen texture at zoom 1 and saves it as a PNG, so a
    /// scene can be checked while the game window stays minimized. Same drawing path as vanilla's
    /// map screenshot, one chunk only. Pair with tly_menushot for an open dialogue box.</summary>
    internal static class WorldShotCommand
    {
        public const string Name = "tly_worldshot";
        public const string Usage = "Debug: save the current location as a PNG without showing the window. Usage: tly_worldshot <path.png> [tileX tileY tilesWide tilesHigh] [+delayMs]";
        private const int MaxPixels = 2048;
        private const int TilePx = 64;
        private const int RegionArgs = 5;

        public static void Run(IMonitor monitor, IModHelper helper, string[] args)
        {
            if (args.Length == 0) { monitor.Log(Usage, LogLevel.Info); return; }
            // A trailing +N waits N ms first, to catch a moment inside an event's pause.
            if (args[^1].StartsWith('+') && int.TryParse(args[^1][1..], out int delayMs))
            {
                string[] rest = args[..^1];
                DelayedAction.functionAfterDelay(() => Shoot(monitor, helper, rest), delayMs);
                return;
            }
            Shoot(monitor, helper, args);
        }

        private static void Shoot(IMonitor monitor, IModHelper helper, string[] args)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc?.map == null) { monitor.Log("tly_worldshot: no location.", LogLevel.Warn); return; }

            string path = args[0];
            int x = 0, y = 0, w = loc.map.DisplayWidth, h = loc.map.DisplayHeight;
            if (args.Length >= RegionArgs
                && int.TryParse(args[1], out int tx) && int.TryParse(args[2], out int ty)
                && int.TryParse(args[3], out int tw) && int.TryParse(args[4], out int th))
            {
                x = tx * TilePx; y = ty * TilePx; w = tw * TilePx; h = th * TilePx;
            }
            w = Math.Clamp(w, 1, MaxPixels);
            h = Math.Clamp(h, 1, MaxPixels);

            Game1 game = Game1.game1;
            GraphicsDevice device = Game1.graphics.GraphicsDevice;
            var lightmapField = helper.Reflection.GetField<RenderTarget2D>(typeof(Game1), "_lightmap");
            var allocateLightmap = helper.Reflection.GetMethod(typeof(Game1), "allocateLightmap");
            var draw = helper.Reflection.GetMethod(game, "_draw");

            xTile.Dimensions.Rectangle oldViewport = Game1.viewport;
            xTile.Dimensions.Rectangle oldUiViewport = Game1.uiViewport;
            bool oldHud = Game1.displayHUD;
            float oldZoom = Game1.options.baseZoomLevel;
            RenderTarget2D cachedLightmap = lightmapField.GetValue();
            RenderTargetBinding[] previous = device.GetRenderTargets();
            using var target = new RenderTarget2D(device, w, h, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            game.takingMapScreenshot = true;
            Game1.options.baseZoomLevel = 1f;
            lightmapField.SetValue(null);
            try
            {
                allocateLightmap.Invoke(w, h);
                Game1.viewport = new xTile.Dimensions.Rectangle(x, y, w, h);
                draw.Invoke(Game1.currentGameTime, target);
            }
            finally
            {
                lightmapField.GetValue()?.Dispose();
                lightmapField.SetValue(cachedLightmap);
                Game1.options.baseZoomLevel = oldZoom;
                game.takingMapScreenshot = false;
                Game1.displayHUD = oldHud;
                Game1.viewport = oldViewport;
                Game1.uiViewport = oldUiViewport;
                device.SetRenderTargets(previous);
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (FileStream file = File.Create(path))
                target.SaveAsPng(file, w, h);
            monitor.Log($"tly_worldshot: saved {loc.Name} ({x / TilePx},{y / TilePx} {w}x{h} px) to {path}.", LogLevel.Info);
        }
    }
}
