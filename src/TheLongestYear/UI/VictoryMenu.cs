using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The 0.9 "basic win" screen, drawn entirely by us (NOT a vanilla Event — same hard-won
    /// reason as <see cref="Day28CutsceneMenu"/>: events fade/blink/cover the dialogue and a portrait-
    /// less Junimo spams TryLoadPortraits). A single page over full black: a row of three differently-
    /// tinted Junimos, a title line, and the loop-count payoff. Dismissed by A / click / Enter; B and
    /// ESC are ignored. Closes itself and invokes <paramref name="onComplete"/> (the win path then
    /// opens the JP shrine and the keep-playing choice). The elaborate payoff cutscene is deferred to 1.0.</summary>
    internal sealed class VictoryMenu : IClickableMenu
    {
        private const string TitleLine = "The Junimos sing! You restored the Community Center.";
        private const string ContinueHint = "(press A or click to continue)";

        // Junimo sprite: frame 0 of the 16×16 Characters\Junimo sheet (a white silhouette meant to be
        // tinted), scaled up. Three are drawn in a row, each a different colour, for a little payoff.
        private const int JunimoFrame = 16;
        private const float JunimoScale = 5f;
        private const float JunimoGap = 48f; // gap between the scaled sprites
        private static readonly Color[] JunimoTints =
        {
            new Color(110, 200, 74),   // classic green
            new Color(90, 160, 235),   // blue
            new Color(235, 110, 110),  // red
        };

        private readonly string _loopLine;
        private readonly Action _onComplete;
        private readonly Texture2D _junimoTexture;
        private bool _done;

        public VictoryMenu(int runNumber, Action onComplete)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
        {
            _loopLine = WinSummary.LoopLine(runNumber);
            _onComplete = onComplete;

            try { _junimoTexture = Game1.content.Load<Texture2D>("Characters\\Junimo"); }
            catch (Exception) { _junimoTexture = null; }

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

        public override void receiveLeftClick(int x, int y, bool playSound = true) => Finish();

        public override void receiveGamePadButton(Buttons b)
        {
            if (b == Buttons.A || b == Buttons.Start)
                Finish();
            // ignore B / Back.
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Enter || key == Keys.Space
                || Game1.options.doesInputListContain(Game1.options.actionButton, key))
                Finish();
            // ignore the menu/cancel button.
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        // Forced screen: never satisfy the engine's close paths (ESC / controller-B). We close
        // ourselves in Finish() on the dismiss input.
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

            float junimoSize = JunimoFrame * JunimoScale;
            float junimoY = h * 0.32f;

            // Row of three differently-tinted Junimos, centered horizontally.
            if (_junimoTexture != null)
            {
                int count = JunimoTints.Length;
                float rowWidth = count * junimoSize + (count - 1) * JunimoGap;
                float startX = (w - rowWidth) / 2f;
                for (int i = 0; i < count; i++)
                {
                    float x = startX + i * (junimoSize + JunimoGap);
                    b.Draw(_junimoTexture,
                        new Vector2(x, junimoY),
                        new Rectangle(0, 0, JunimoFrame, JunimoFrame),
                        JunimoTints[i], 0f, Vector2.Zero, JunimoScale, SpriteEffects.None, 0.9f);
                }
            }

            float textY = junimoY + junimoSize + 48f;

            // Title line.
            int maxWidth = System.Math.Min(900, (int)(w * 0.6f));
            string title = Game1.parseText(TitleLine, Game1.dialogueFont, maxWidth);
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2((w - titleSize.X) / 2f, textY), Color.White);

            // Loop-count line, below the title.
            float loopY = textY + titleSize.Y + 24f;
            string loop = Game1.parseText(_loopLine, Game1.dialogueFont, maxWidth);
            Vector2 loopSize = Game1.dialogueFont.MeasureString(loop);
            Utility.drawTextWithShadow(b, loop, Game1.dialogueFont,
                new Vector2((w - loopSize.X) / 2f, loopY), Color.White);

            // Continue hint, near the bottom.
            Vector2 hintSize = Game1.smallFont.MeasureString(ContinueHint);
            Utility.drawTextWithShadow(b, ContinueHint, Game1.smallFont,
                new Vector2((w - hintSize.X) / 2f, h - 96), Color.White * 0.7f);
        }
    }
}
