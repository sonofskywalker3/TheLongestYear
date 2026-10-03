using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The end of Morris's offer taken (JojaBadEnding's <c>tlyGameOver</c>): full-screen
    /// black, "Game Over" a third of the way down, the message in the middle and the button below
    /// it. The button, any key or any controller button exits to the title without saving. (The
    /// farmer in a suit beside Morris, red-eyed, was cut: Jeff, 2026-10-02, "just get rid of the
    /// sprites, it's fine.")
    ///
    /// Input is swallowed for the first 600 ms (<see cref="InputDelayMs"/>) so a player mashing
    /// through the scene's last dialogue does not skip the screen unseen. The only way out is that
    /// input path: <c>exitThisMenu</c> would close the screen without reaching the title and leave
    /// the finished event idling, which is why <c>tly_dismiss</c> sends this menu a key press
    /// instead of closing it.</summary>
    internal sealed class JojaGameOverMenu : IClickableMenu
    {
        private const int TitleLift = 40;
        private const int ButtonPadX = 32, ButtonPadY = 20;
        private const int ButtonBelowTwoThirds = 16;
        private const double InputDelayMs = 600;
        private static readonly Color MessageColour = new(220, 220, 220);
        private static readonly Color TitleColour = new(200, 30, 30);

        private readonly IMonitor _monitor;
        private readonly string _title;
        private readonly string[] _messageLines;
        private readonly string _buttonText;
        private readonly ClickableComponent _button;
        private readonly double _openedAt;
        private bool _exiting;

        public JojaGameOverMenu(IMonitor monitor)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            _monitor = monitor;
            _title = Strings.Get("joja.gameover.title");
            _messageLines = Game1.parseText(Strings.Get("joja.gameover.message"), Game1.dialogueFont, width * 2 / 3).Split('\n');
            _buttonText = Strings.Get("joja.gameover.button");
            _openedAt = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;

            Vector2 size = Game1.dialogueFont.MeasureString(_buttonText);
            int bw = (int)size.X + ButtonPadX * 2, bh = (int)size.Y + ButtonPadY * 2;
            int by = height * 2 / 3 + ButtonBelowTwoThirds;
            _button = new ClickableComponent(new Rectangle((width - bw) / 2, by, bw, bh), "ReturnToTitle") { myID = 0 };
            allClickableComponents = new List<ClickableComponent> { _button };
            if (Game1.options.SnappyMenus)
                snapToDefaultClickableComponent();
            _monitor.Log("Joja: game over screen", LogLevel.Info);
        }

        private bool InputReady =>
            !_exiting && (Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? double.MaxValue) - _openedAt >= InputDelayMs;

        private void Exit()
        {
            if (_exiting) return;
            _exiting = true;
            // Never saves: the save on disk is still this morning, undecided (spec: Yes is not saved).
            Game1.ExitToTitle();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _button;
            snapCursorToCurrentSnappedComponent();
        }

        public override bool readyToClose() => false;

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (InputReady && _button.containsPoint(x, y)) Exit();
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveKeyPress(Keys key)
        {
            if (key != Keys.None && InputReady) Exit();
        }

        public override void receiveGamePadButton(Buttons b)
        {
            if (InputReady) Exit();
        }

        public override void performHoverAction(int x, int y)
            => _button.scale = _button.containsPoint(x, y) ? 1.1f : 1f;

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, width, height), Color.Black);

            SpriteText.drawStringHorizontallyCenteredAt(b, _title, width / 2, height / 3 - TitleLift, color: TitleColour);

            float lineH = Game1.dialogueFont.LineSpacing;
            float y = height / 2f - _messageLines.Length * lineH / 2f;
            foreach (string line in _messageLines)
            {
                float w = Game1.dialogueFont.MeasureString(line).X;
                b.DrawString(Game1.dialogueFont, line, new Vector2((width - w) / 2f, y), MessageColour);
                y += lineH;
            }

            Rectangle r = _button.bounds;
            int grow = _button.scale > 1f ? 4 : 0;
            drawTextureBox(b, r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2, Color.White);
            Vector2 ts = Game1.dialogueFont.MeasureString(_buttonText);
            Utility.drawTextWithShadow(b, _buttonText, Game1.dialogueFont,
                new Vector2(r.X + (r.Width - ts.X) / 2f, r.Y + (r.Height - ts.Y) / 2f), Game1.textColor);

            drawMouse(b);
        }
    }
}
