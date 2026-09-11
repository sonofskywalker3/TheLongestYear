using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace TheLongestYear.Integration
{
    /// <summary>A speech box about half the height of the game's portrait dialogue box: the speaker's
    /// portrait at half size on the left, the line beside it, pages on click. No name label: "Junimo"
    /// said nothing and Lewis's fell off the bottom of the box (Jeff, 2026-09-08).
    /// Vanilla's portrait box is a fixed 384 px because its portrait frame is 388 px tall, and at
    /// 1080p that covered half of every ending scene (Jeff, 2026-09-07). Opened by the tlySay event
    /// command, which watches for it to close and then advances the script.</summary>
    internal sealed class EndingSpeechBox : IClickableMenu
    {
        private const int BoxWidth = 1200, MinHeight = 212, Pad = 20;
        private const int PortraitSize = 64, PortraitScale = 2, PortraitDrawn = PortraitSize * PortraitScale;
        // The frame sat one Pad from the box edge, which read as leaning against the wall (Jeff,
        // 2026-09-10). PortraitX is the art's left edge; the frame itself starts FrameEdge px
        // further left, so this is the inset of the frame plus its own border.
        private const int FrameEdge = 12;
        private const int PortraitX = Pad + FrameEdge + 16;
        private const int TextX = PortraitX + PortraitDrawn + 28;
        private const int BottomMargin = 64;
        private const int CharMs = 22;
        private const int OpenGuardMs = 200;   // ignore the click that opened us

        private readonly Texture2D _portrait;
        private readonly List<string> _pages;
        private readonly int _textWidth;
        private int _page;
        private int _shown;
        private float _charTimer;
        private float _age;

        public EndingSpeechBox(Texture2D portrait, List<string> pages)
        {
            _portrait = portrait;
            _pages = pages;
            _textWidth = BoxWidth - TextX - Pad;
            width = BoxWidth;
            Resize();
            Game1.playSound("bigSelect");
        }

        private void Resize()
        {
            int textHeight = SpriteText.getHeightOfString(_pages[_page], _textWidth);
            height = System.Math.Max(MinHeight, textHeight + Pad * 2);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = Game1.uiViewport.Height - height - BottomMargin;
        }

        private bool PageDone => _shown >= _pages[_page].Length;

        private void Advance()
        {
            if (_age < OpenGuardMs) return;
            if (!PageDone) { _shown = _pages[_page].Length; return; }
            if (_page + 1 < _pages.Count)
            {
                _page++;
                _shown = 0;
                Resize();
                return;
            }
            Game1.exitActiveMenu();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true) => Advance();
        public override void receiveRightClick(int x, int y, bool playSound = true) => Advance();

        public override void receiveKeyPress(Keys key)
        {
            if (Game1.options.doesInputListContain(Game1.options.actionButton, key)
                || Game1.options.doesInputListContain(Game1.options.menuButton, key)
                || key == Keys.Enter || key == Keys.Space)
                Advance();
        }

        public override void receiveGamePadButton(Buttons b)
        {
            if (b == Buttons.A || b == Buttons.B || b == Buttons.X || b == Buttons.Start) Advance();
        }

        public override void update(GameTime time)
        {
            base.update(time);
            _age += time.ElapsedGameTime.Milliseconds;
            if (PageDone) return;
            _charTimer += time.ElapsedGameTime.Milliseconds;
            while (_charTimer >= CharMs && !PageDone)
            {
                _charTimer -= CharMs;
                _shown++;
            }
        }

        public override void draw(SpriteBatch b)
        {
            int x = xPositionOnScreen, y = yPositionOnScreen;
            drawTextureBox(b, x, y, width, height, Color.White);
            if (_portrait != null)
            {
                // Centred in the box's own height rather than pinned to the top pad.
                int px = x + PortraitX, py = y + (height - PortraitDrawn) / 2;
                drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), px - FrameEdge, py - FrameEdge, PortraitDrawn + FrameEdge * 2, PortraitDrawn + FrameEdge * 2, Color.White, 4f, drawShadow: false);
                b.Draw(_portrait, new Rectangle(px, py, PortraitDrawn, PortraitDrawn), new Rectangle(0, 0, PortraitSize, PortraitSize), Color.White);
            }
            SpriteText.drawString(b, _pages[_page], x + TextX, y + Pad, _shown, _textWidth);
            if (PageDone)
            {
                // The little "next" chevron the game uses, bottom right.
                b.Draw(Game1.mouseCursors, new Vector2(x + width - 44, y + height - 44 + (float)System.Math.Sin(_age / 150f) * 3f),
                    new Rectangle(232, 346, 9, 9), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0.9f);
            }
            drawMouse(b);
        }
    }
}
