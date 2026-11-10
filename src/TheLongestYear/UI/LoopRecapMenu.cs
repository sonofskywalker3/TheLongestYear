using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The loop recap page (spec 2026-11-10 shrine planning, feature 2): what the loop that
    /// is ending earned and left undone, and up to three suggested keeps, with one "Continue to the
    /// shrine" button. Closing it any way runs the rewind chain's continuation (its exitFunction).</summary>
    internal sealed class LoopRecapMenu : IClickableMenu
    {
        private const int BoxWidth = 1000;
        private const int Pad = 40;
        private const int TitleHeight = 72;
        private const int LineGap = 8;
        private const int SectionGap = 20;
        private const int Indent = 32;
        private const int ButtonHeight = 72;
        private const int ButtonPadX = 32;
        private const int ButtonTopGap = 28;
        private const int ContinueId = 0;
        private const string SuggestedMark = "!";

        /// <summary>The shop's suggested badge colour (JunimoShrineMenu.Suggested.cs).</summary>
        internal static readonly Color SuggestedGreen = new Color(30, 140, 30);

        private sealed class Line
        {
            public string Text;
            public SpriteFont Font;
            public int Indent;
            public bool Suggested;
            public int GapBefore;
        }

        private readonly LoopSummary _summary;
        private readonly List<Line> _lines = new();
        private ClickableComponent _continue;
        private bool _hoverContinue;

        public LoopRecapMenu(LoopSummary summary)
        {
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
            Layout();
            if (Game1.options.SnappyMenus)
                snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            Layout();
        }

        private void Layout()
        {
            width = Math.Min(BoxWidth, Game1.uiViewport.Width - 64);
            int textWidth = width - Pad * 2;
            _lines.Clear();
            Add(LoopRecapText.JpLine(_summary), Game1.dialogueFont, 0, textWidth, 0);
            Add(LoopRecapText.SourcesLine(_summary), Game1.smallFont, Indent, textWidth, LineGap);
            Add(LoopRecapText.BundlesLine(_summary), Game1.dialogueFont, 0, textWidth, SectionGap);
            Add(LoopRecapText.DonationsLine(_summary), Game1.dialogueFont, 0, textWidth, LineGap);
            if (LoopRecapText.DarknessLine(_summary) is string darkness)
                Add(darkness, Game1.dialogueFont, 0, textWidth, SectionGap);
            if (_summary.Suggestions.Count > 0)
            {
                Add(LoopRecapText.SuggestedHeader(), Game1.dialogueFont, 0, textWidth, SectionGap);
                foreach (KeepSuggestion s in _summary.Suggestions)
                    Add(LoopRecapText.SuggestionLine(s, _summary.HeldBoard), Game1.smallFont, Indent, textWidth, LineGap, suggested: true);
            }

            int bodyHeight = 0;
            foreach (Line line in _lines)
                bodyHeight += line.GapBefore + (int)line.Font.MeasureString(line.Text).Y;
            height = Math.Min(Pad + TitleHeight + bodyHeight + ButtonTopGap + ButtonHeight + Pad, Game1.uiViewport.Height - 32);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            string label = LoopRecapText.ContinueButton();
            int buttonWidth = (int)Game1.dialogueFont.MeasureString(label).X + ButtonPadX * 2;
            _continue = new ClickableComponent(
                new Rectangle(xPositionOnScreen + (width - buttonWidth) / 2, yPositionOnScreen + height - Pad - ButtonHeight, buttonWidth, ButtonHeight),
                "continue", label)
            {
                myID = ContinueId,
            };
            allClickableComponents = new List<ClickableComponent> { _continue };
        }

        private void Add(string text, SpriteFont font, int indent, int textWidth, int gapBefore, bool suggested = false)
        {
            int markWidth = suggested ? (int)font.MeasureString(SuggestedMark + " ").X : 0;
            _lines.Add(new Line
            {
                Text = Game1.parseText(text, font, textWidth - indent - markWidth),
                Font = font,
                Indent = indent,
                Suggested = suggested,
                GapBefore = gapBefore,
            });
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _continue;
            snapCursorToCurrentSnappedComponent();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_continue.containsPoint(x, y))
                Continue();
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Enter || key == Keys.Space)
            {
                Continue();
                return;
            }
            base.receiveKeyPress(key);   // Escape and the menu button close it too; the shrine still follows
        }

        public override void receiveGamePadButton(Buttons b)
        {
            if (b == Buttons.A || b == Buttons.Start)
            {
                Continue();
                return;
            }
            base.receiveGamePadButton(b);
        }

        /// <summary>Same-tick guard: an A press also arrives as a synthesized left click.</summary>
        private bool _closing;

        private void Continue()
        {
            if (_closing) return;
            _closing = true;
            Game1.playSound("smallSelect");
            exitThisMenu(playSound: false);
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverContinue = _continue.containsPoint(x, y);
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            SpriteText.drawStringHorizontallyCenteredAt(b, LoopRecapText.Title(_summary),
                xPositionOnScreen + width / 2, yPositionOnScreen + Pad - 8);

            int y = yPositionOnScreen + Pad + TitleHeight;
            foreach (Line line in _lines)
            {
                y += line.GapBefore;
                int x = xPositionOnScreen + Pad + line.Indent;
                if (line.Suggested)
                {
                    Utility.drawTextWithShadow(b, SuggestedMark, line.Font, new Vector2(x, y), SuggestedGreen);
                    x += (int)line.Font.MeasureString(SuggestedMark + " ").X;
                }
                Utility.drawTextWithShadow(b, line.Text, line.Font, new Vector2(x, y), Game1.textColor);
                y += (int)line.Font.MeasureString(line.Text).Y;
            }

            bool lit = _hoverContinue || currentlySnappedComponent == _continue && Game1.options.gamepadControls;
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                _continue.bounds.X, _continue.bounds.Y, _continue.bounds.Width, _continue.bounds.Height,
                lit ? Color.Wheat : Color.White, 4f, false);
            Vector2 labelSize = Game1.dialogueFont.MeasureString(_continue.label);
            Utility.drawTextWithShadow(b, _continue.label, Game1.dialogueFont,
                new Vector2(_continue.bounds.Center.X - labelSize.X / 2, _continue.bounds.Center.Y - labelSize.Y / 2 + 4),
                Game1.textColor);

            drawMouse(b);
        }
    }
}
