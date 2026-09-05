using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>One-button notice shown when the player ticks "Skip intro" on character creation:
    /// the opening cutscene is the only in-game explanation of the loop, so first-timers are
    /// advised to watch it. Attached as a CHILD of the active menu (the title menu) rather than
    /// replacing it, because <c>Game1.updateActiveMenu</c> and the menu draw loop both walk the
    /// child chain, while a vanilla <c>DialogueBox</c> only closes when it IS the active menu.
    /// The checkbox keeps whatever the player set; OK just dismisses.</summary>
    internal sealed class IntroSkipNoticeMenu : IClickableMenu
    {
        private const int BoxWidth = 900;
        private const int Pad = 32;
        private const int OkSize = 64;
        private const int TextToButtonGap = 24;

        private readonly string _text;
        private readonly ClickableTextureComponent _ok;

        public IntroSkipNoticeMenu()
        {
            _text = Game1.parseText(Strings.Get("intro.skip-notice"), Game1.dialogueFont, BoxWidth - Pad * 2);
            int textHeight = (int)Game1.dialogueFont.MeasureString(_text).Y;
            width = BoxWidth;
            height = Pad * 2 + textHeight + TextToButtonGap + OkSize;
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            _ok = new ClickableTextureComponent(
                "OK",
                new Rectangle(xPositionOnScreen + width - Pad - OkSize, yPositionOnScreen + height - Pad - OkSize, OkSize, OkSize),
                null, null, Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46), 1f)
            {
                myID = 0,
            };
            allClickableComponents = new System.Collections.Generic.List<ClickableComponent> { _ok };
            if (Game1.options.SnappyMenus)
                snapToDefaultClickableComponent();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _ok;
            snapCursorToCurrentSnappedComponent();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_ok.containsPoint(x, y))
                Dismiss();
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) => Dismiss();

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape || key == Keys.Enter || key == Keys.Space
                || Game1.options.doesInputListContain(Game1.options.menuButton, key))
                Dismiss();
        }

        public override void receiveGamePadButton(Buttons b)
        {
            if (b == Buttons.A || b == Buttons.B || b == Buttons.Start)
                Dismiss();
        }

        private void Dismiss()
        {
            Game1.playSound("bigDeSelect");
            GetParentMenu()?.SetChildMenu(null);
        }

        public override void performHoverAction(int x, int y)
            => _ok.scale = _ok.containsPoint(x, y) ? 1.1f : 1f;

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
            Utility.drawTextWithShadow(b, _text, Game1.dialogueFont,
                new Vector2(xPositionOnScreen + Pad, yPositionOnScreen + Pad), Game1.textColor);
            _ok.draw(b);
            drawMouse(b);
        }
    }
}
