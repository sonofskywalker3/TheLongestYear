using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.UI
{
    /// <summary>The day-28 bedtime Junimo cutscene, drawn entirely by us: a full-screen black
    /// background with the Junimo's dialogue centered over it, advanced page-by-page by the player.
    /// We render it ourselves (rather than via a vanilla Event) because every event approach failed
    /// in playtest — <c>fade</c> revealed the room, <c>globalFade</c> blinked back, and a
    /// <c>RenderedWorld</c> overlay painted over the event's own dialogue box. A menu draws the
    /// black AND the text in one ordered pass, so the text is always readable, and Junimo has no
    /// portrait asset to spam-load. Non-skippable: <see cref="readyToClose"/> is always false and
    /// the cancel/ESC inputs are ignored; the scene only ends by advancing past the last page.</summary>
    internal sealed class Day28CutsceneMenu : IClickableMenu
    {
        private const string ContinueHint = "(click or press A to continue)";

        private readonly IReadOnlyList<string> _pages;
        private readonly Action _onComplete;
        private int _pageIndex;
        private bool _done;

        public Day28CutsceneMenu(Day28Branch branch, Action onComplete)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
        {
            string raw = branch == Day28Branch.Fail
                ? Day28CutsceneContent.FailDialogue
                : Day28CutsceneContent.ContinueDialogue;
            _pages = Day28DialogueScript.ToPages(raw, Game1.player?.Name ?? string.Empty);
            _onComplete = onComplete;
            Game1.playSound("junimoMeep1");
        }

        private void Advance()
        {
            if (_done) return;
            _pageIndex++;
            if (_pageIndex >= _pages.Count)
            {
                Finish();
                return;
            }
            Game1.playSound("junimoMeep1");
        }

        private void Finish()
        {
            if (_done) return;
            _done = true;
            if (Game1.activeClickableMenu == this)
                Game1.activeClickableMenu = null;
            _onComplete?.Invoke();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true) => Advance();

        public override void receiveGamePadButton(Buttons b)
        {
            if (b == Buttons.A || b == Buttons.Start)
                Advance();
            // ignore B / Back: forced scene, not skippable.
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Enter || key == Keys.Space
                || Game1.options.doesInputListContain(Game1.options.actionButton, key))
                Advance();
            // deliberately ignore the menu/cancel button — the scene can't be skipped.
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        // Forced scene: never satisfy the engine's close paths (ESC / controller-B). We close
        // ourselves in Finish() once the last page is advanced.
        public override bool readyToClose() => false;

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            width = Game1.uiViewport.Width;
            height = Game1.uiViewport.Height;
        }

        public override void draw(SpriteBatch b)
        {
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;

            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, w, h), Color.Black);

            string text = (_pageIndex >= 0 && _pageIndex < _pages.Count) ? _pages[_pageIndex] : string.Empty;
            if (text.Length > 0)
            {
                int maxWidth = (int)(w * 0.6f);
                string wrapped = Game1.parseText(text, Game1.dialogueFont, maxWidth);
                Vector2 size = Game1.dialogueFont.MeasureString(wrapped);
                Vector2 pos = new Vector2((w - size.X) / 2f, (h - size.Y) / 2f);
                Utility.drawTextWithShadow(b, wrapped, Game1.dialogueFont, pos, Color.White);
            }

            Vector2 hintSize = Game1.smallFont.MeasureString(ContinueHint);
            Utility.drawTextWithShadow(b, ContinueHint, Game1.smallFont,
                new Vector2((w - hintSize.X) / 2f, h - 96), Color.White * 0.7f);
        }
    }
}
