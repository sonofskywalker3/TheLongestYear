using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    internal sealed partial class SeasonGoalsMenu
    {
        // ---------- drawing ----------

        private const int BadgeGap = 24;        // least space between the headline and the badge
        private const int TwoLineHeadY = 6;     // row offsets when the badge takes its own line
        private const int TwoLineBadgeY = 34;

        public override void draw(SpriteBatch b)
        {
            // As the game menu's tab page, the game menu has already drawn the dim and the box.
            if (!_asPage)
            {
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                    Color.Black * 0.5f);
                IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            }

            // Title bar.
            // NOTE: each Strings.Get() call below keeps its own inline token-dictionary literal
            // (rather than sharing one `tokens` variable across the switch) because
            // I18nGuardTests.EveryTokenedKey_HasCallSiteSupplyingAllTokens statically scans source
            // text for "Strings.Get(\"key\", new Dictionary<string, string> { ... })" at each call
            // site: a shared variable passed by reference is invisible to that regex and would
            // make every key here report as having no call site.
            bool held = _meta != null && _meta.ConsecutiveHolds > 0 && _meta.BundlesGeneratedForReset >= 0;
            string season = SeasonName(_season);
            string day = _run.DayOfMonth.ToString();
            string holds = held ? _meta.ConsecutiveHolds.ToString() : "0";
            string title = held
                ? Strings.Get("menu.goals.title-held", new Dictionary<string, string>
                {
                    ["season"] = season,
                    ["day"] = day,
                    ["holds"] = holds,
                })
                : Strings.Get("menu.goals.title", new Dictionary<string, string>
                {
                    ["season"] = season,
                    ["day"] = day,
                });
            if (_asPage)
                DrawPageTitle(b, title);
            else
                SpriteText.drawStringHorizontallyCenteredAt(b, title,
                    xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            // Visible rows (the Vault/bus-repair goal is now one of these rows, added in BuildEntries).
            int visibleCount = Math.Min(_rowsPerPage, Math.Max(0, _entries.Count - _scrollIndex));
            for (int i = 0; i < visibleCount; i++)
                DrawRow(b, _rowSlots[i], _entries[_scrollIndex + i]);

            // Scroll arrows.
            _scrollUp.draw(b, _scrollIndex > 0 ? Color.White : Color.Gray, 1f);
            _scrollDown.draw(b, _scrollIndex + _rowsPerPage < _entries.Count ? Color.White : Color.Gray, 1f);

            if (!_asPage)
                base.draw(b);
            if (!string.IsNullOrEmpty(_hoverText))
                HoverText.Draw(b, _hoverText);
            if (_asPage) return;    // the game menu draws the cursor
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        /// <summary>The page is narrower than the book's own menu, so the title uses the dialogue
        /// font, dropping to the small font when a long title would not fit.</summary>
        private void DrawPageTitle(SpriteBatch b, string title)
        {
            int room = width - PageSideInset * 2;
            SpriteFont font = Game1.dialogueFont.MeasureString(title).X <= room ? Game1.dialogueFont : Game1.smallFont;
            Vector2 size = font.MeasureString(title);
            Utility.drawTextWithShadow(b, title, font,
                new Vector2(xPositionOnScreen + (width - size.X) / 2f, yPositionOnScreen + PageTitleY), Game1.textColor);
        }


        private void DrawRow(SpriteBatch b, ClickableComponent slot, BundleEntry e)
        {
            bool satisfiedThisSeason = e.MissingThisSeasonCount == 0;
            Color tint = satisfiedThisSeason ? Color.LightGreen * 0.7f : Color.White;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

            // Top line: "BundleName  (Theme)   N/X" (vault row reuses the same shape via Title/ThemeTag).
            string headline = Strings.Get("menu.goals.row-headline", new Dictionary<string, string>
            {
                ["title"] = e.Title,
                ["theme"] = e.ThemeTag,
                ["have"] = e.Have.ToString(),
                ["need"] = e.Need.ToString(),
            });
            Color headColor = satisfiedThisSeason ? Color.DarkGreen : Game1.textColor;
            string badge = BadgeFor(e);
            Vector2 badgeSize = Game1.smallFont.MeasureString(badge);
            // The badge sits on the right of the top line; when the headline would run into it (the
            // narrower tab page, long bundle names) it drops to a second line above the icons.
            bool badgeFits = Game1.smallFont.MeasureString(headline).X + BadgeGap + badgeSize.X <= slot.bounds.Width - 32;
            int headY = slot.bounds.Y + (badgeFits ? 12 : TwoLineHeadY);
            int badgeY = badgeFits ? headY : slot.bounds.Y + TwoLineBadgeY;
            Utility.drawTextWithShadow(b, headline, Game1.smallFont,
                new Vector2(slot.bounds.X + 16, headY), headColor);
            Color badgeColor = satisfiedThisSeason ? Color.DarkGreen : new Color(160, 34, 34);
            Utility.drawTextWithShadow(b, badge, Game1.smallFont,
                new Vector2(slot.bounds.X + slot.bounds.Width - 16 - badgeSize.X, badgeY), badgeColor);

            // Bottom line: missing-item icons (or a "done" marker).
            if (satisfiedThisSeason) return;

            int iconRowX = slot.bounds.X + 16;
            int iconRowY = slot.bounds.Y + IngredientIconY;
            int maxIcons = Math.Max(1,
                (slot.bounds.Width - 32) / (IngredientIconSize + IngredientIconGap));

            // Vault row has no item ingredients — draw one gold-coin icon per still-owed payment,
            // mirroring the bundle rows' one-sprite-per-missing-item layout.
            if (e.IsVault)
            {
                int coins = Math.Min(e.MissingThisSeasonCount, maxIcons);
                for (int k = 0; k < coins; k++)
                {
                    b.Draw(Game1.mouseCursors,
                        new Rectangle(iconRowX + k * (IngredientIconSize + IngredientIconGap), iconRowY,
                            IngredientIconSize, IngredientIconSize),
                        CoinIconSource, Color.White);
                }
                return;
            }

            // Leave room for the "+N more" note inside the row's box when not every icon fits.
            int drawCount = IconRowFit.Shown(e.MissingItems.Count, slot.bounds.Width - 32,
                IngredientIconSize, IngredientIconSize + IngredientIconGap,
                hidden => (int)Math.Ceiling(Game1.smallFont.MeasureString(MoreNote(hidden)).X));
            for (int k = 0; k < drawCount; k++)
            {
                string itemId = e.MissingItems[k];
                // Pull stack + quality from THIS bundle's ingredient data so the icon
                // renders the donation count badge + the right-tier quality star
                // (Quality Crops needs gold, Pantry crops need basic, same id different shape).
                int stack = StackFor(e.Bundle, itemId);
                int quality = e.Bundle.IngredientQualities.TryGetValue(itemId, out int q) ? q : 0;
                Item probe = ResolveItem(itemId, stack, quality);
                var pos = new Vector2(
                    iconRowX + k * (IngredientIconSize + IngredientIconGap),
                    iconRowY);
                if (probe != null)
                {
                    // Draw_OneInclusive so a one-item goal shows "1" like the rest.
                    probe.drawInMenu(b, pos, IngredientIconSize / 64f, 1f, 0.86f,
                        StackDrawType.Draw_OneInclusive, Color.White, drawShadow: true);
                }
                else
                {
                    var qSrc = new Rectangle(403, 496, 5, 7);
                    const int qScale = 3;
                    int qW = qSrc.Width * qScale;
                    int qH = qSrc.Height * qScale;
                    b.Draw(Game1.mouseCursors,
                        new Rectangle((int)pos.X + (IngredientIconSize - qW) / 2,
                                      (int)pos.Y + (IngredientIconSize - qH) / 2, qW, qH),
                        qSrc, Color.White);
                }
            }
            // "+N more" overflow indicator if the row can't hold all missing icons.
            if (e.MissingItems.Count > drawCount)
            {
                string more = MoreNote(e.MissingItems.Count - drawCount);
                Utility.drawTextWithShadow(b, more, Game1.smallFont,
                    new Vector2(iconRowX + drawCount * (IngredientIconSize + IngredientIconGap),
                                iconRowY + 20),
                    Game1.textColor);
            }
        }

        private static string MoreNote(int hidden)
            => Strings.Get("menu.goals.more", new Dictionary<string, string> { ["count"] = hidden.ToString() });

        /// <summary>Right-aligned status badge: "checkpoint met" once the season's quota is
        /// satisfied; "needs N before {NextSeason} 1" otherwise; Winter swaps to "by run end"
        /// since Winter day-28 IS the run-end checkpoint (no next-season day 1 to point at).</summary>
        private string BadgeFor(BundleEntry e)
        {
            if (e.MissingThisSeasonCount == 0) return Strings.Get("menu.goals.badge-met");
            if (_season == CoreSeason.Winter)
                return Strings.Get("menu.goals.badge-needs-end",
                    new Dictionary<string, string> { ["count"] = e.MissingThisSeasonCount.ToString() });
            return Strings.Get("menu.goals.badge-needs-before", new Dictionary<string, string>
            {
                ["count"] = e.MissingThisSeasonCount.ToString(),
                ["season"] = SeasonName((CoreSeason)((int)_season + 1)),
            });
        }
    }
}
