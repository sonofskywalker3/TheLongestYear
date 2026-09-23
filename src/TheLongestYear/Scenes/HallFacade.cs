using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace TheLongestYear.Scenes
{
    /// <summary>Where the Community Center is in Town and where its front windows are, measured
    /// (spec 2026-09-21, the hall). It is its own file because it is DATA about a building rather
    /// than anything the scene does, and because the numbers in it were expensive to get and should
    /// be easy to find again.</summary>
    internal static class HallFacade
    {
        /// <summary>The building's own tiles. Vanilla's bounds, from
        /// <c>Town.refurbishCommunityCenter</c> (Town.cs:377), which walks
        /// <c>new Rectangle(47, 11, 11, 9)</c> with <c>x &lt;= Right</c> and <c>y &lt;= Bottom</c>
        /// and so covers twelve tiles by ten.</summary>
        public static readonly Rectangle Tiles = new Rectangle(47, 11, 12, 10);

        /// <summary>The front door, which the camera and the walk are both hung on.</summary>
        public static readonly Vector2 DoorTile = new Vector2(52, 20);

        /// <summary>How far above the bottom of the building the camera sits, so the whole front and
        /// the path below it share the frame.</summary>
        private const int CameraAboveFootTiles = 3;

        /// <summary>The top left tile the window rectangles are measured from.</summary>
        public static Vector2 OriginTile => new Vector2(Tiles.X, Tiles.Y);

        /// <summary>The tile the camera centres on.</summary>
        public static Vector2 CameraTile => new Vector2(DoorTile.X, Tiles.Bottom - CameraAboveFootTiles);

        /// <summary>The glass of the front windows, in PIXELS relative to <see cref="OriginTile"/>.
        ///
        /// MEASURED, NOT GUESSED, AND THERE ARE TWO OF THEM. The task asked for four to six, but the
        /// abandoned Community Center has exactly two windows on its front, one either side of the
        /// door, each with its shutters open on a boarded pane. They were read off a frame of the
        /// hall scene itself at zoom 1 with the viewport at (2400,644) (see the task 9 report), and
        /// Town's own map has no <c>WindowLight</c> property on the building at all, so there was
        /// nothing in the map data to take them from. They sit a little inside the glass rather than
        /// flush with the frame, because a pane that overshoots by a pixel reads as a glowing wall.
        ///
        /// ON THE TEXEL GRID. The art is drawn at 4x, so every edge sits on a multiple of four
        /// pixels from the building's corner. The first measurement (118,414 60x92) was two pixels
        /// off that grid on every side, which left half a texel of the dark frame lit round each
        /// pane (review, 2026-09-21). These are pulled in to the whole texels of glass.
        ///
        /// THEY ARE THE ABANDONED FACADE'S. A restored or Joja-bought Community Center is different
        /// art, so <see cref="IsRestored"/> exists to say so before anything is lit.</summary>
        public static readonly IReadOnlyList<Rectangle> FrontWindows = new[]
        {
            new Rectangle(120, 416, 56, 88),
            new Rectangle(592, 416, 56, 88),
        };

        /// <summary>Has the building's front changed out from under the measurements? True once the
        /// Community Center is finished or the Joja warehouse has replaced it, which is exactly when
        /// <c>Town.refurbishCommunityCenter</c> swaps the facade tiles (Town.cs:544).</summary>
        public static bool IsRestored
        {
            get
            {
                Farmer master = Game1.MasterPlayer;
                if (master == null) return false;
                return master.mailReceived.Contains("ccIsComplete")
                    || master.mailReceived.Contains("JojaMember")
                    || master.hasCompletedCommunityCenter();
            }
        }

        /// <summary>What the map itself says about lit windows on the hall, for the log. The window
        /// rectangles above were measured off the facade, and this line is how a later reader can
        /// tell whether the map ever came to agree with them.</summary>
        public static string DescribeMapWindowLights(GameLocation town)
        {
            if (town == null) return "Town was not there to ask about WindowLight.";
            try
            {
                string[] lights = town.GetMapPropertySplitBySpaces("WindowLight");
                var mine = new List<string>();
                for (int i = 0; i + 1 < lights.Length; i += 3)
                {
                    if (!int.TryParse(lights[i], out int x) || !int.TryParse(lights[i + 1], out int y)) continue;
                    if (Tiles.Contains(x, y)) mine.Add($"({x},{y})");
                }
                return mine.Count == 0
                    ? "Town's map declares no WindowLight on the hall."
                    : $"Town's map declares WindowLight on the hall at {string.Join(" ", mine)}.";
            }
            catch (Exception ex)
            {
                return $"Town's WindowLight property could not be read ({ex.GetType().Name}).";
            }
        }
    }
}
