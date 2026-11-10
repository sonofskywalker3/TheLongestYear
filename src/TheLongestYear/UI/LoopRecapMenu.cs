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
            public float Scale;
            public int Height;
            public int MarkWidth;
        }

        /// <summary>Space kept between the panel and the screen's top and bottom edges.</summary>
        private const int ScreenMargin = 32;

        private readonly LoopSummary _summary;
        private List<Line> _lines = new();
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

        /// <summary>Fits the page to the screen: the text shrinks to <see cref="LoopRecapText.MinScale"/>,
        /// then the per-source JP line goes, and as a last resort lines are dropped from the bottom,
        /// so the Continue button always sits below the last drawn line inside the panel.</summary>
        private void Layout()
        {
            width = Math.Min(BoxWidth, Game1.uiViewport.Width - 64);
            int textWidth = width - Pad * 2;
            int chrome = Pad + TitleHeight + ButtonTopGap + ButtonHeight + Pad;
            int available = Game1.uiViewport.Height - ScreenMargin - chrome;

            (float scale, bool sources) = LoopRecapText.Fit((sc, src) => BodyHeight(BuildLines(sc, src, textWidth)), available);
            _lines = BuildLines(scale, sources, textWidth);
            while (_lines.Count > 1 && BodyHeight(_lines) > available)
                _lines.RemoveAt(_lines.Count - 1);

            height = chrome + BodyHeight(_lines);
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

        private List<Line> BuildLines(float scale, bool sources, int textWidth)
        {
            var lines = new List<Line>();
            void Add(string text, SpriteFont font, int indent, int gapBefore, bool suggested = false)
            {
                // Wrapped at the unscaled width that fills the line once drawn at this scale.
                int markWidth = suggested ? (int)(font.MeasureString(SuggestedMark + " ").X * scale) : 0;
                string wrapped = Game1.parseText(text, font, (int)((textWidth - indent - markWidth) / scale));
                lines.Add(new Line
                {
                    Text = wrapped,
                    Font = font,
                    Indent = indent,
                    Suggested = suggested,
                    GapBefore = (int)(gapBefore * scale),
                    Scale = scale,
                    Height = (int)Math.Ceiling(font.MeasureString(wrapped).Y * scale),
                    MarkWidth = markWidth,
                });
            }

            Add(LoopRecapText.JpLine(_summary), Game1.dialogueFont, 0, 0);
            if (sources)
                Add(LoopRecapText.SourcesLine(_summary), Game1.smallFont, Indent, LineGap);
            Add(LoopRecapText.BundlesLine(_summary), Game1.dialogueFont, 0, SectionGap);
            Add(LoopRecapText.DonationsLine(_summary), Game1.dialogueFont, 0, LineGap);
            if (LoopRecapText.DarknessLine(_summary) is string darkness)
                Add(darkness, Game1.dialogueFont, 0, SectionGap);
            if (_summary.Suggestions.Count > 0)
            {
                Add(LoopRecapText.SuggestedHeader(), Game1.dialogueFont, 0, SectionGap);
                foreach (KeepSuggestion s in _summary.Suggestions)
                    Add(LoopRecapText.SuggestionLine(s, _summary.HeldBoard), Game1.smallFont, Indent, LineGap, suggested: true);
            }
            return lines;
        }

        private static int BodyHeight(List<Line> lines)
        {
            int total = 0;
            foreach (Line line in lines)
                total += line.GapBefore + line.Height;
            return total;
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
                    Utility.drawTextWithShadow(b, SuggestedMark, line.Font, new Vector2(x, y), SuggestedGreen, line.Scale);
                    x += line.MarkWidth;
                }
                Utility.drawTextWithShadow(b, line.Text, line.Font, new Vector2(x, y), Game1.textColor, line.Scale);
                y += line.Height;
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
