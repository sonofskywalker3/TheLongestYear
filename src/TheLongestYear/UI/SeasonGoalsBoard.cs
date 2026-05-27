using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// In-world interactable that opens <see cref="SeasonGoalsMenu"/> when the player presses the
    /// action button on a chosen tile inside the Community Center.
    ///
    /// First version tried <c>GameLocation.RegisterTileAction</c> + painting an "Action" tile
    /// property — but vanilla's <c>checkAction</c> only reads that property off an EXISTING tile
    /// at the coord (GameLocation.cs:7776-7779). The default tile (45,11) is open floor with no
    /// Buildings-layer tile to paint, so the property never landed and the action never fired
    /// (2026-05-26 playtest: "I see the icon, but it's just a bouncing yellow bit, and I can't
    /// click on anything").
    ///
    /// This version skips tile properties entirely and uses a Harmony prefix on
    /// <c>GameLocation.checkAction</c> instead. The prefix intercepts the action *before* vanilla
    /// looks for any tile property, so it fires regardless of map contents — anywhere the player
    /// presses Action on (X,Y) in the Community Center will open the goals menu.
    /// </summary>
    internal static class SeasonGoalsBoard
    {
        /// <summary>Active board instance — populated by <see cref="ConnectTo"/> at mod entry, read
        /// by the static Harmony prefix and the RenderedWorld handler. We can't pass instance
        /// state through Harmony's static patch methods, so we store it here.</summary>
        private static SeasonGoalsBoardInstance _instance;

        /// <summary>Build the board, wire its lifecycle events, and register the static Harmony
        /// patch handle so <see cref="CheckActionPrefix"/> can reach it.</summary>
        public static void ConnectTo(IModHelper helper, IMonitor monitor, GameplayConfig config,
            Func<MenuLauncher> launcherAccessor)
        {
            _instance = new SeasonGoalsBoardInstance(monitor, config, launcherAccessor);
            helper.Events.Display.RenderedWorld += _instance.OnRenderedWorld;
        }

        /// <summary>Harmony prefix on <c>GameLocation.checkAction</c>. Fires for every action-button
        /// press anywhere in the world; we filter to the CC + the configured board tile and short-
        /// circuit vanilla when it's our tile.</summary>
        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkAction))]
        internal static class CheckActionPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony parameter convention.
            private static bool Prefix(GameLocation __instance, xTile.Dimensions.Location tileLocation,
                Farmer who, ref bool __result)
            {
                if (_instance == null) return true;
                if (__instance is not CommunityCenter) return true;
                if (tileLocation.X != _instance.Config.SeasonGoalsBoardTileX) return true;
                if (tileLocation.Y != _instance.Config.SeasonGoalsBoardTileY) return true;

                _instance.OpenGoals();
                __result = true;
                return false;    // skip the rest of vanilla checkAction for this tile press
            }
        }
    }

    /// <summary>Instance-side state for the board. Holds config/monitor/launcher refs and owns the
    /// RenderedWorld overlay. Separated from <see cref="SeasonGoalsBoard"/> so the Harmony patch
    /// class stays purely static (Harmony's PatchAll only discovers attributed types).</summary>
    internal sealed class SeasonGoalsBoardInstance
    {
        public GameplayConfig Config { get; }

        private readonly IMonitor _monitor;
        private readonly Func<MenuLauncher> _launcherAccessor;

        // Pre-loaded textures (loaded lazily so we don't touch Game1.content before the game's ready).
        private Texture2D _signTexture;

        public SeasonGoalsBoardInstance(IMonitor monitor, GameplayConfig config,
            Func<MenuLauncher> launcherAccessor)
        {
            _monitor = monitor;
            Config = config;
            _launcherAccessor = launcherAccessor;
        }

        // ---------- action ----------

        /// <summary>Open the goals menu. Called from the Harmony prefix when the player presses
        /// Action on the configured tile.</summary>
        public void OpenGoals()
        {
            MenuLauncher launcher = _launcherAccessor();
            if (launcher == null)
            {
                _monitor.Log("Season Goals tile pressed before launcher was ready.", LogLevel.Warn);
                return;
            }
            launcher.OpenSeasonGoals();
        }

        // ---------- visual hint ----------

        /// <summary>Draw a parchment-style notice board at the configured tile so the player can
        /// find it. Without an overlay the tile would be invisible — vanilla doesn't draw anything
        /// for an Action property alone, and our prefix-based detection doesn't put anything on
        /// the map either.</summary>
        public void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.currentLocation is not CommunityCenter) return;

            int tileX = Config.SeasonGoalsBoardTileX;
            int tileY = Config.SeasonGoalsBoardTileY;

            // World pixel coord of the tile's top-left → viewport-local pixel coord for drawing.
            Vector2 worldPos = new Vector2(tileX * Game1.tileSize, tileY * Game1.tileSize);
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, worldPos);

            // Subtle vertical bob so the eye catches it.
            float bob = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalSeconds * 2.5) * 3f;

            // Sign body: 64px wide, ~52px tall, sitting just above the tile so it doesn't bury
            // any decoration on the tile itself.
            const int signW = 64;
            const int signH = 52;
            int signX = (int)screenPos.X + (Game1.tileSize - signW) / 2;
            int signY = (int)screenPos.Y - signH + (int)bob;

            SpriteBatch b = e.SpriteBatch;

            // Drop shadow for legibility against the wall behind it.
            b.Draw(Game1.staminaRect, new Rectangle(signX + 4, signY + 4, signW, signH),
                Color.Black * 0.4f);

            // Parchment fill + dark border (mimics the in-game notice-paper look).
            Color parchment = new Color(245, 222, 179);   // wheat / parchment
            Color border = new Color(99, 64, 32);         // dark brown
            b.Draw(Game1.staminaRect, new Rectangle(signX, signY, signW, signH), border);
            b.Draw(Game1.staminaRect, new Rectangle(signX + 3, signY + 3, signW - 6, signH - 6),
                parchment);

            // Label. smallFont is missing the ASCII subset on some assets — use plain "Goals"
            // (4 chars × ~10px = ~40px, fits in 64px sign with margin).
            const string label = "Goals";
            Vector2 size = Game1.smallFont.MeasureString(label);
            Vector2 labelPos = new Vector2(
                signX + (signW - size.X) / 2f,
                signY + (signH - size.Y) / 2f);
            Utility.drawTextWithShadow(b, label, Game1.smallFont, labelPos, border);

            // Small downward-pointing arrow connecting sign to tile, so it's obvious "this floor
            // tile is the interactable, not the sign itself."
            int arrowCx = (int)screenPos.X + Game1.tileSize / 2;
            int arrowTop = signY + signH;
            int arrowBottom = (int)screenPos.Y + 4;
            if (arrowBottom > arrowTop)
            {
                b.Draw(Game1.staminaRect, new Rectangle(arrowCx - 2, arrowTop, 4, arrowBottom - arrowTop),
                    border * 0.6f);
            }
        }
    }
}
