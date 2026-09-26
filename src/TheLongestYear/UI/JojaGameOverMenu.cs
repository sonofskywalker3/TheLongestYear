using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.Pants;
using StardewValley.GameData.Shirts;
using StardewValley.Menus;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The end of Morris's offer taken (JojaBadEnding's <c>tlyGameOver</c>): full-screen
    /// black, "Game Over" a third of the way down, the message in the middle, and in the bottom
    /// third the farmer in a suit and fedora beside Morris, both with glowing red eyes. The button,
    /// any key or any controller button exits to the title without saving.
    ///
    /// The farmer is re-dressed live (hat, shirt and pants overrides, red eyes) for the picture:
    /// nothing is saved after this screen, so the change never reaches the save on disk.</summary>
    internal sealed class JojaGameOverMenu : IClickableMenu
    {
        private const string FedoraName = "Fedora";
        private static readonly string[] SuitWords = { "Suit", "Tuxedo" };
        private const float Scale = 4f;
        private const int SpriteW = 16, SpriteH = 32;
        private const int FigureW = (int)(SpriteW * Scale), FigureH = (int)(SpriteH * Scale);
        private const int FigureGap = 48;
        private const int FiguresBelowTwoThirds = 16;
        private const int TitleLift = 40;
        private const int ButtonGap = 32, ButtonPadX = 32, ButtonPadY = 20;
        // Swallow input this long after opening so a player mashing through the scene's last
        // dialogue does not skip the screen unseen.
        private const double InputDelayMs = 600;
        private const float PulseMs = 300f;
        // A 4x4 core over the 4x4 iris, with two fainter, larger squares around it for the glow
        // (red drawn on a red iris alone would not show).
        private static readonly (int Size, float Alpha)[] GlowLayers = { (16, 0.18f), (10, 0.35f), (4, 1f) };
        private const float GlowMin = 0.35f, GlowRange = 0.65f;
        // Vanilla has no suit pants (Data/Pants, 1.6): the farmer's own pants go charcoal instead.
        private static readonly Color SuitPantsColour = new(58, 58, 68);
        private const float FigureLayer = 0.8f;
        private static readonly Color MessageColour = new(220, 220, 220);
        private static readonly Color TitleColour = new(200, 30, 30);

        // Eye centres within a figure, in screen pixels from its top-left at 4x. Measured on the
        // live Game Over frame (2026-09-25, 1920x1080, joja-task7/go-1.png): the farmer's red irises
        // cover x 24-27 and 36-39 at y 48-51 (frame 0 facing down, fedora on), Morris's recoloured
        // irises (MorrisDarkSprite, sprite pixels (7,9)/(9,9)) cover x 28-31 and 36-39 at y 36-39.
        private static readonly Vector2[] FarmerEyes = { new(26, 50), new(38, 50) };
        private static readonly Vector2[] MorrisEyes = { new(30, 38), new(38, 38) };

        private readonly IMonitor _monitor;
        private readonly string _title;
        private readonly string[] _messageLines;
        private readonly string _buttonText;
        private readonly Texture2D _morris;
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

            try { _morris = Game1.content.Load<Texture2D>(MorrisDarkSprite.AssetName); }
            catch (Exception ex) { _monitor.Log($"Joja game over: no Morris sprite ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn); }

            DressFarmer();

            Vector2 size = Game1.dialogueFont.MeasureString(_buttonText);
            int bw = (int)size.X + ButtonPadX * 2, bh = (int)size.Y + ButtonPadY * 2;
            int by = FiguresTop + FigureH + ButtonGap;
            _button = new ClickableComponent(new Rectangle((width - bw) / 2, by, bw, bh), "ReturnToTitle") { myID = 0 };
            allClickableComponents = new List<ClickableComponent> { _button };
            if (Game1.options.SnappyMenus)
                snapToDefaultClickableComponent();
            _monitor.Log("Joja: game over screen", LogLevel.Info);
        }

        private int FiguresTop => height * 2 / 3 + FiguresBelowTwoThirds;
        private int FarmerX => width / 2 - FigureGap / 2 - FigureW;
        private int MorrisX => width / 2 + FigureGap / 2;

        /// <summary>Fedora, a suit shirt and pants, red eyes. Each slot is independent: one that
        /// cannot be found or throws is logged and left as it was.</summary>
        private void DressFarmer()
        {
            Farmer who = Game1.player;
            try
            {
                string hatId = null;
                foreach (var (id, raw) in DataLoader.Hats(Game1.content))
                    if (raw.Split('/')[0] == FedoraName) { hatId = id; break; }
                if (hatId == null) _monitor.Log("Joja game over: no Fedora in Data/Hats; hat unchanged.", LogLevel.Warn);
                else who.hat.Value = ItemRegistry.Create<Hat>("(H)" + hatId);
            }
            catch (Exception ex) { _monitor.Log($"Joja game over: hat: {ex.GetType().Name}: {ex.Message}.", LogLevel.Warn); }

            try
            {
                string shirtId = FindSuit(DataLoader.Shirts(Game1.content), d => d.Name, out string shirtName);
                if (shirtId == null) _monitor.Log("Joja game over: no suit shirt in Data/Shirts; shirt unchanged.", LogLevel.Warn);
                else
                {
                    who.changeShirt(shirtId);
                    _monitor.Log($"Joja game over: shirt {shirtId} ({shirtName}).", LogLevel.Trace);
                }
            }
            catch (Exception ex) { _monitor.Log($"Joja game over: shirt: {ex.GetType().Name}: {ex.Message}.", LogLevel.Warn); }

            try
            {
                var pants = DataLoader.Pants(Game1.content);
                string pantsId = FindSuit(pants, d => d.Name, out string pantsName);
                if (pantsId == null)
                {
                    who.changePantsColor(SuitPantsColour);
                    _monitor.Log("Joja game over: no suit pants in Data/Pants; own pants dyed charcoal.", LogLevel.Trace);
                }
                else
                {
                    who.changePantStyle(pantsId);
                    // An override is drawn in the farmer's own pants colour; use the suit's.
                    who.changePantsColor(Utility.StringToColor(pants[pantsId].DefaultColor) ?? Color.White);
                    _monitor.Log($"Joja game over: pants {pantsId} ({pantsName}).", LogLevel.Trace);
                }
            }
            catch (Exception ex) { _monitor.Log($"Joja game over: pants: {ex.GetType().Name}: {ex.Message}.", LogLevel.Warn); }

            try { who.changeEyeColor(Color.Red); }
            catch (Exception ex) { _monitor.Log($"Joja game over: eyes: {ex.GetType().Name}: {ex.Message}.", LogLevel.Warn); }
        }

        private static string FindSuit<T>(IDictionary<string, T> data, Func<T, string> name, out string found)
        {
            foreach (var (id, entry) in data)
            {
                string n = name(entry) ?? "";
                foreach (string word in SuitWords)
                    if (n.Contains(word, StringComparison.OrdinalIgnoreCase)) { found = n; return id; }
            }
            found = null;
            return null;
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

            var farmerPos = new Vector2(FarmerX, FiguresTop);
            FarmerRenderer.isDrawingForUI = true;
            try
            {
                Game1.player.FarmerRenderer.draw(b, new FarmerSprite.AnimationFrame(0, 0, false, false), 0,
                    new Rectangle(0, 0, SpriteW, SpriteH), farmerPos, Vector2.Zero, FigureLayer, 2, Color.White, 0f, 1f, Game1.player);
            }
            finally { FarmerRenderer.isDrawingForUI = false; }

            var morrisPos = new Vector2(MorrisX, FiguresTop);
            if (_morris != null)
                b.Draw(_morris, morrisPos, new Rectangle(0, 0, SpriteW, SpriteH), Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, FigureLayer);

            double t = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
            float pulse = GlowMin + GlowRange * (float)(0.5 + 0.5 * Math.Sin(t / PulseMs));
            DrawGlow(b, farmerPos, FarmerEyes, pulse);
            DrawGlow(b, morrisPos, MorrisEyes, pulse);

            Rectangle r = _button.bounds;
            int grow = _button.scale > 1f ? 4 : 0;
            drawTextureBox(b, r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2, Color.White);
            Vector2 ts = Game1.dialogueFont.MeasureString(_buttonText);
            Utility.drawTextWithShadow(b, _buttonText, Game1.dialogueFont,
                new Vector2(r.X + (r.Width - ts.X) / 2f, r.Y + (r.Height - ts.Y) / 2f), Game1.textColor);

            drawMouse(b);
        }

        private static void DrawGlow(SpriteBatch b, Vector2 origin, Vector2[] eyes, float pulse)
        {
            foreach (var (size, alpha) in GlowLayers)
                foreach (Vector2 eye in eyes)
                    b.Draw(Game1.staminaRect, new Rectangle((int)(origin.X + eye.X) - size / 2, (int)(origin.Y + eye.Y) - size / 2, size, size),
                        null, Color.Red * (alpha * pulse), 0f, Vector2.Zero, SpriteEffects.None, 1f);
        }
    }
}
