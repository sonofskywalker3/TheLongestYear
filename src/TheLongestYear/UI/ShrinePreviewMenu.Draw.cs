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
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    internal sealed partial class ShrinePreviewMenu
    {
        // ------------------------------------------------------------------ draw

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            SpriteText.drawStringHorizontallyCenteredAt(b, Strings.Get("menu.shrine-preview.title"),
                xPositionOnScreen + width / 2, yPositionOnScreen + 24);
            Utility.drawTextWithShadow(b,
                Strings.Get("menu.shrine-preview.banked", new Dictionary<string, string> { ["jp"] = _state.JunimoPoints.ToString() }),
                Game1.smallFont,
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 80), Game1.textColor);

            // Keeps are bought at the loop boundary; this board sells boosts only. Say so on the
            // JP line so nobody tries to buy a keep from the Plan tab (several testers did).
            string planningNote = Strings.Get("menu.shrine-preview.planning-note");
            Vector2 noteSize = Game1.smallFont.MeasureString(planningNote);
            string bankedLine = Strings.Get("menu.shrine-preview.banked", new Dictionary<string, string> { ["jp"] = _state.JunimoPoints.ToString() });
            float bankedRight = xPositionOnScreen + 40 + Game1.smallFont.MeasureString(bankedLine).X + 32;
            float noteX = xPositionOnScreen + width - 40 - noteSize.X;
            if (noteX > bankedRight)   // only when it fits beside the JP line; never overlap it
                Utility.drawTextWithShadow(b, planningNote, Game1.smallFont,
                    new Vector2(noteX, yPositionOnScreen + 80), NoteBrown);

            for (int i = 0; i < _tabs.Count; i++)
            {
                ClickableTextureComponent tab = _tabs[i];
                Color tint = (int)_tab == i ? Color.White : Color.White * 0.7f;
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, tint, 1f, false);
                string label = TabLabel((ShrineTab)i);
                Vector2 labelSize = Game1.smallFont.MeasureString(label);
                Utility.drawTextWithShadow(b, label, Game1.smallFont,
                    new Vector2(tab.bounds.X + (tab.bounds.Width - labelSize.X) / 2f,
                        tab.bounds.Y + (tab.bounds.Height - labelSize.Y) / 2f),
                    Game1.textColor);
            }

            if (_restartButton != null)
            {
                Rectangle r = _restartButton.bounds;
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                    r.X, r.Y, r.Width, r.Height, Color.White, 4f, drawShadow: false);
                string restartLabel = Strings.Get("shrine.restart.button");
                Vector2 restartSize = Game1.smallFont.MeasureString(restartLabel);
                Utility.drawTextWithShadow(b, restartLabel, Game1.smallFont,
                    new Vector2(r.X + (r.Width - restartSize.X) / 2f, r.Y + (r.Height - restartSize.Y) / 2f),
                    Game1.textColor);
            }

            DrawForesight(b);
            if (_tab == ShrineTab.Donate)
                DrawDonate(b);

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                DrawRow(b, _rows[idx], _listY + i * RowHeight);
            }

            if (MaxScroll() > 0)
            {
                _scrollUp.draw(b, _scrollIndex > 0 ? Color.White : Color.Gray, 1f);
                _scrollDown.draw(b, _scrollIndex < MaxScroll() ? Color.White : Color.Gray, 1f);
            }

            base.draw(b);
            if (!string.IsNullOrEmpty(_hoverText))
                HoverText.Draw(b, _hoverText);
            else
                DrawDonateTooltip(b);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawRow(SpriteBatch b, Row row, int rowY)
        {
            switch (row.Kind)
            {
                case RowKind.Header:
                    Utility.drawTextWithShadow(b, row.Text, Game1.dialogueFont,
                        new Vector2(_listX, rowY + 6), Game1.textColor);
                    break;
                case RowKind.Note:
                    Utility.drawTextWithShadow(b, row.Text, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), Game1.textColor * 0.85f);
                    break;
                case RowKind.Running:
                {
                    Utility.drawTextWithShadow(b, row.Text, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), OwnedGreen);
                    Vector2 noteSize = Game1.smallFont.MeasureString(row.Note);
                    Utility.drawTextWithShadow(b, row.Note, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - noteSize.X, rowY + 6), OwnedGreen);
                    break;
                }
                case RowKind.Boost:
                    DrawBoostRow(b, row, rowY);
                    break;
                case RowKind.Upgrade when row.IsOwned:
                {
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), OwnedGreen);
                    string ownedLabel = Strings.Get("menu.shrine.owned");
                    Vector2 ownedSize = Game1.smallFont.MeasureString(ownedLabel);
                    Utility.drawTextWithShadow(b, ownedLabel, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - ownedSize.X, rowY + 6), OwnedGreen);
                    break;
                }
                case RowKind.Upgrade:
                {
                    long costJp = UpgradePricing.EffectiveCost(row.Def, _priceFactor, _state);
                    bool affordable = _state.JunimoPoints >= costJp;
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), Game1.textColor);
                    string cost = Strings.Get("menu.shrine-preview.cost",
                        new Dictionary<string, string> { ["cost"] = costJp.ToString() });
                    Vector2 costSize = Game1.smallFont.MeasureString(cost);
                    Utility.drawTextWithShadow(b, cost, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - costSize.X, rowY + 6),
                        affordable ? Game1.textColor : Color.Brown);
                    break;
                }
                case RowKind.LockedToggle:
                {
                    string arrow = _expandedLocked.Contains(row.Category) ? "v " : "> ";
                    Utility.drawTextWithShadow(b, arrow + row.Text, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), LockedGray);
                    break;
                }
                case RowKind.Locked:
                {
                    // "Keep Big Coop - unlocked once it's built", cost right-aligned like a buyable row.
                    string title = row.Def.DisplayName
                        + (string.IsNullOrEmpty(row.Requirement) ? "" : " - " + row.Requirement);
                    Utility.drawTextWithShadow(b, title, Game1.smallFont,
                        new Vector2(_listX + SubRowIndent, rowY + 6), LockedGray);
                    long lockedCost = UpgradePricing.EffectiveCost(row.Def, _priceFactor, _state);
                    string lockedCostText = Strings.Get("menu.shrine-preview.cost",
                        new Dictionary<string, string> { ["cost"] = lockedCost.ToString() });
                    Vector2 lockedCostSize = Game1.smallFont.MeasureString(lockedCostText);
                    Utility.drawTextWithShadow(b, lockedCostText, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - lockedCostSize.X, rowY + 6), LockedGray);
                    break;
                }
            }
        }

        /// <summary>One Boosts row: name and cost on the left, and on the right either a Buy button
        /// (greyed when the JP is not there, but still clickable so the HUD can say why), an Active
        /// label, or a "Not now" label. Crash Course draws a label row plus five skill sub-rows;
        /// Elevator Pass shows the floors.</summary>
        private void DrawBoostRow(SpriteBatch b, Row row, int rowY)
        {
            BoostDefinition boost = row.Boost;
            if (boost.Id == BoostId.CrashCourse && row.Skill < 0)
            {
                Utility.drawTextWithShadow(b, Strings.Get(boost.NameKey), Game1.smallFont,
                    new Vector2(_listX + 24, rowY + 6), Game1.textColor);
                // The price multiplier so far this loop (3^n), right-aligned above the Buy buttons.
                string multiplier = Strings.Get("shrine.boosts.multiplier", new Dictionary<string, string>
                {
                    ["factor"] = ((long)Math.Pow(3, _run.SkillLevelsBoughtTotal)).ToString(),
                });
                Vector2 multSize = Game1.smallFont.MeasureString(multiplier);
                Utility.drawTextWithShadow(b, multiplier, Game1.smallFont,
                    new Vector2(_listX + _listWidth - 64 - multSize.X, rowY + 6), NoteBrown);
                return;
            }

            BoostContext ctx = ContextFor(row);
            string name;
            int x = _listX + 24;
            if (boost.Id == BoostId.CrashCourse)
            {
                int from = ctx.SkillLevels[row.Skill];
                name = Strings.Get("shrine.boosts.crash-course-row", new Dictionary<string, string>
                {
                    ["skill"] = SkillName(row.Skill), ["from"] = from.ToString(), ["to"] = (from + 1).ToString(),
                });
                x = _listX + SubRowIndent;
            }
            else if (boost.Id == BoostId.ElevatorPass)
            {
                name = Strings.Get(boost.NameKey) + ": " + Strings.Get("shrine.boosts.elevator-row", new Dictionary<string, string>
                {
                    ["from"] = ctx.MineFloor.ToString(),
                    ["to"] = BoostPricing.ElevatorLanding(ctx.MineFloor).ToString(),
                });
            }
            else
            {
                name = Strings.Get(boost.NameKey);
            }

            BoostRowState rowState = StateOf(row);
            long costJp = BoostPricing.CostOf(boost, _run, ctx);
            bool affordable = _state.JunimoPoints >= costJp;

            Utility.drawTextWithShadow(b, name, Game1.smallFont, new Vector2(x, rowY + 6), Game1.textColor);

            Rectangle button = BoostButtonBounds(rowY);
            if (rowState != BoostRowState.NotAvailable)
            {
                string cost = Strings.Get("menu.shrine-preview.cost",
                    new Dictionary<string, string> { ["cost"] = costJp.ToString() });
                Vector2 costSize = Game1.smallFont.MeasureString(cost);
                Utility.drawTextWithShadow(b, cost, Game1.smallFont,
                    new Vector2(button.X - 16 - costSize.X, rowY + 6),
                    affordable ? Game1.textColor : Color.Brown);
            }

            if (rowState == BoostRowState.Buy)
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                    button.X, button.Y, button.Width, button.Height,
                    affordable ? Color.White : Color.Gray, 4f, drawShadow: false);
                string buy = Strings.Get("shrine.boosts.buy");
                Vector2 buySize = Game1.smallFont.MeasureString(buy);
                Utility.drawTextWithShadow(b, buy, Game1.smallFont,
                    new Vector2(button.X + (button.Width - buySize.X) / 2f,
                        button.Y + (button.Height - buySize.Y) / 2f),
                    Game1.textColor);
                return;
            }

            string label = rowState == BoostRowState.Active
                ? Strings.Get("shrine.boosts.active")
                : boost.Id == BoostId.ElevatorPass && ctx.MineFloor <= 0
                    ? Strings.Get("shrine.boosts.enter-mine")
                    : boost.Id == BoostId.ElevatorPass
                        ? Strings.Get("shrine.boosts.bottom-reached")
                        : Strings.Get("shrine.boosts.not-available");
            Color labelColor = rowState == BoostRowState.Active ? OwnedGreen : Color.Brown;
            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(button.X + (button.Width - labelSize.X) / 2f, rowY + 6), labelColor);
        }
    }
}
