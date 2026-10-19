using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Focus;

namespace TheLongestYear.UI
{
    /// <summary>The chosen focus cards as a row of small icons under the JP box. A card the player
    /// can buy now glows; a bought (or fully kept) card is drawn faded. Drawn by the JP HUD, so it
    /// hides with it. Host only, like the purchase.</summary>
    internal static class FocusHudIcons
    {
        private const int IconPx = 32;
        private const int Gap = 4;
        private const int TopGap = 4;
        private const float IconScale = 0.5f;      // drawInMenu 1f is 64px
        private const int SpriteInset = 16;        // a 0.5 sprite sits 16px inside drawInMenu's 64px box
        private const float FadedAlpha = 0.5f;
        private const float GlowMin = 0.15f;
        private const float GlowSpan = 0.35f;
        private const double GlowPeriodMs = 1200;

        /// <summary>Quoting a tool card scans every chest, so the cards are quoted at most this often.</summary>
        private const int RefreshTicks = 30;

        private static IReadOnlyList<(FocusHand Hand, FocusQuote Quote)> _cards = Array.Empty<(FocusHand, FocusQuote)>();
        private static int _quotedTick = int.MinValue;

        /// <summary>Draws the icons right-aligned to <paramref name="rightX"/>, starting just below
        /// <paramref name="topY"/>. Nothing is drawn when no card is chosen.</summary>
        public static void Draw(SpriteBatch b, int rightX, int topY, FocusService focus, long jp)
        {
            if (focus == null || !Context.IsMainPlayer) return;
            if (Game1.ticks - _quotedTick >= RefreshTicks || Game1.ticks < _quotedTick)
            {
                _cards = focus.ChosenCards();
                _quotedTick = Game1.ticks;
            }
            if (_cards.Count == 0) return;

            float pulse = (float)(0.5 + 0.5 * Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds * 2 * Math.PI / GlowPeriodMs));
            int x = rightX - (_cards.Count * IconPx + (_cards.Count - 1) * Gap);
            int y = topY + TopGap;
            foreach ((FocusHand hand, FocusQuote quote) in _cards)
            {
                FocusCardState state = FocusCardStrip.StateOf(hand, quote);
                bool faded = state is FocusCardState.Owned or FocusCardState.Done;
                // The same rule as FocusService.CanAfford, on the cached quote and the live JP.
                bool glow = state == FocusCardState.Buy && jp >= quote.Price;
                if (glow)
                    b.Draw(Game1.staminaRect, new Rectangle(x - 2, y - 2, IconPx + 4, IconPx + 4),
                        Color.White * (GlowMin + GlowSpan * pulse));
                FocusCardStrip.FaceOf(hand, quote).Icon?.drawInMenu(b,
                    new Vector2(x - SpriteInset, y - SpriteInset), IconScale, faded ? FadedAlpha : 1f, 1f);
                x += IconPx + Gap;
            }
        }
    }
}
