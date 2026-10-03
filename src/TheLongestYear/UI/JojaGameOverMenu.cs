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
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The end of Morris's offer taken (JojaBadEnding's <c>tlyGameOver</c>): full-screen
    /// black, "Game Over" a third of the way down, the message in the middle, and in the bottom
    /// third the farmer in a suit, bare-headed and in black boots, beside Morris, both with glowing
    /// red eyes. The button, any key or any controller button exits to the title without saving.
    ///
    /// Input is swallowed for the first 600 ms (<see cref="InputDelayMs"/>) so a player mashing
    /// through the scene's last dialogue does not skip the screen unseen. The only way out is that
    /// input path: <c>exitThisMenu</c> would close the screen without reaching the title and leave
    /// the finished event idling, which is why <c>tly_dismiss</c> sends this menu a key press
    /// instead of closing it.
    ///
    /// The farmer is re-dressed live (hat off, shirt and pants overrides, black boots, red eyes):
    /// nothing is saved after this screen, so the change never reaches the save on disk.</summary>
    internal sealed class JojaGameOverMenu : IClickableMenu
    {
        // Characters/Farmer/shoeColors: one 4-pixel row per shoe colour, read by FarmerRenderer.ApplyShoeColor.
        private const string ShoeColoursAsset = "Characters/Farmer/shoeColors";
        private const int ShoeRowWidth = 4;
        private const int ColourSpreadWeight = 2;
        private static readonly string[] SuitWords = { "Suit", "Tuxedo" };
        private const float Scale = 4f;
        private const int SpriteW = 16, SpriteH = 32;
        private const int FigureW = (int)(SpriteW * Scale), FigureH = (int)(SpriteH * Scale);
        private const int FigureGap = 48;
        private const int FiguresBelowTwoThirds = 16;
        private const int TitleLift = 40;
        private const int ButtonGap = 32, ButtonPadX = 32, ButtonPadY = 20;
        private const double InputDelayMs = 600;
        private const float PulseMs = 300f;
        // A core over the whole eye (iris and white), with two faint rims around it for the glow,
        // padded in screen pixels (red drawn on a red iris alone would not show). Toned down from
        // a 16-pixel halo at 0.18 / 0.35 after Jeff's playthrough (note 5, 2026-10-02).
        private static readonly (int Pad, float Alpha)[] GlowLayers = { (4, 0.07f), (2, 0.16f), (0, 0.85f) };
        private const float GlowMin = 0.55f, GlowRange = 0.45f;
        // Vanilla has no suit pants (Data/Pants, 1.6): the farmer's own pants go charcoal instead.
        private static readonly Color SuitPantsColour = new(58, 58, 68);
        private const float FigureLayer = 0.8f;
        private static readonly Color MessageColour = new(220, 220, 220);
        private static readonly Color TitleColour = new(200, 30, 30);

        // FarmerRenderer's eye swatches on row 0 of the base sheet: every pixel of these two colours
        // is recoloured to the eye colour, so they mark the irises on any base sheet.
        private static readonly int[] IrisSwatches = { 276, 277 };
        // Frame 0 eyes (iris and white) on the vanilla male base, sprite pixels: used only when the
        // farmer's sheet cannot be read. The female base's sit one row lower.
        private static readonly PixelBox[] FallbackFarmerEyes = { new(5, 11, 2, 2), new(9, 11, 2, 2) };
        // Morris's recoloured irises (MorrisDarkSprite, sprite pixels (7,9)/(9,9) on frame 0). His
        // whites are darkened with the rest of him, so the glow sits on the irises alone.
        private static readonly PixelBox[] MorrisEyes = { new(7, 9, 1, 1), new(9, 9, 1, 1) };

        private readonly IMonitor _monitor;
        private readonly string _title;
        private readonly string[] _messageLines;
        private readonly string _buttonText;
        private readonly Texture2D _morris;
        private readonly Rectangle[] _farmerEyes;
        private readonly Rectangle[] _morrisEyes = ToFigure(MorrisEyes);
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
            _farmerEyes = ToFigure(FindFarmerEyes());

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

        /// <summary>No hat, a suit shirt and pants, black boots, red eyes. Each slot is independent:
        /// one that cannot be found or throws is logged and left as it was.</summary>
        private void DressFarmer()
        {
            Farmer who = Game1.player;
            try { who.hat.Value = null; }
            catch (Exception ex) { _monitor.Log($"Joja game over: hat: {ex.GetType().Name}: {ex.Message}.", LogLevel.Warn); }

            try
            {
                int row = DarkestShoeRow();
                who.changeShoeColor(row.ToString());
                _monitor.Log($"Joja game over: shoe colour row {row}.", LogLevel.Trace);
            }
            catch (Exception ex) { _monitor.Log($"Joja game over: boots: {ex.GetType().Name}: {ex.Message}.", LogLevel.Warn); }

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

        /// <summary>The farmer's eyes on the frame this screen draws, read once from the base sheet
        /// FarmerRenderer draws (male or female, bald or not): the iris pixels plus the whites
        /// touching them. Hair, hats and the skin and eye recolours are separate layers or swaps,
        /// so the source sheet alone places the eyes for every farmer.</summary>
        private IReadOnlyList<PixelBox> FindFarmerEyes()
        {
            string sheet = Game1.player.FarmerRenderer?.textureName.Value;
            try
            {
                Texture2D tex = Game1.content.Load<Texture2D>(sheet);
                var colours = new Color[tex.Width * tex.Height];
                tex.GetData(colours);
                var px = new uint[colours.Length];
                for (int i = 0; i < colours.Length; i++) px[i] = colours[i].PackedValue;

                var iris = new List<uint>();
                foreach (int swatch in IrisSwatches)
                    if (swatch < px.Length) iris.Add(px[swatch]);
                IReadOnlyList<PixelBox> eyes = SpriteEyes.Find(px, tex.Width, 0, 0, SpriteW, SpriteH, iris);
                if (eyes.Count > 0)
                {
                    _monitor.Log($"Joja game over: farmer eyes on {sheet}: {string.Join(", ", eyes)}.", LogLevel.Trace);
                    return eyes;
                }
                _monitor.Log($"Joja game over: no eyes found on {sheet}; using the vanilla male positions.", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Joja game over: farmer eyes from {sheet}: {ex.GetType().Name}: {ex.Message}; using the vanilla male positions.", LogLevel.Warn);
            }
            return FallbackFarmerEyes;
        }

        /// <summary>Sprite-pixel boxes to screen pixels from a figure's top-left (both figures draw at <see cref="Scale"/>).</summary>
        private static Rectangle[] ToFigure(IReadOnlyList<PixelBox> boxes)
        {
            var r = new Rectangle[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
                r[i] = new Rectangle((int)(boxes[i].X * Scale), (int)(boxes[i].Y * Scale), (int)(boxes[i].Width * Scale), (int)(boxes[i].Height * Scale));
            return r;
        }

        /// <summary>The blackest shoeColors row: dark AND grey, scored as brightness plus twice each
        /// shade's colour spread, lowest wins. Brightness alone picks vanilla row 8, a dark red
        /// (live 2026-09-25); this picks row 7, black to charcoal grey ((0,0,0) to (66,66,66)).</summary>
        private static int DarkestShoeRow()
        {
            Texture2D tex = Game1.content.Load<Texture2D>(ShoeColoursAsset);
            var px = new Color[tex.Width * tex.Height];
            tex.GetData(px);
            int best = 0, bestSum = int.MaxValue;
            for (int row = 0; row < tex.Height; row++)
            {
                int sum = 0;
                for (int i = 0; i < ShoeRowWidth; i++)
                {
                    Color c = px[row * ShoeRowWidth + i];   // the renderer indexes rows as which * 4
                    sum += c.R + c.G + c.B + ColourSpreadWeight * (Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)));
                }
                if (sum < bestSum) { bestSum = sum; best = row; }
            }
            return best;
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
            DrawGlow(b, farmerPos, _farmerEyes, pulse);
            DrawGlow(b, morrisPos, _morrisEyes, pulse);

            Rectangle r = _button.bounds;
            int grow = _button.scale > 1f ? 4 : 0;
            drawTextureBox(b, r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2, Color.White);
            Vector2 ts = Game1.dialogueFont.MeasureString(_buttonText);
            Utility.drawTextWithShadow(b, _buttonText, Game1.dialogueFont,
                new Vector2(r.X + (r.Width - ts.X) / 2f, r.Y + (r.Height - ts.Y) / 2f), Game1.textColor);

            drawMouse(b);
        }

        private static void DrawGlow(SpriteBatch b, Vector2 origin, Rectangle[] eyes, float pulse)
        {
            foreach (var (pad, alpha) in GlowLayers)
                foreach (Rectangle eye in eyes)
                    b.Draw(Game1.staminaRect,
                        new Rectangle((int)origin.X + eye.X - pad, (int)origin.Y + eye.Y - pad, eye.Width + pad * 2, eye.Height + pad * 2),
                        null, Color.Red * (alpha * pulse), 0f, Vector2.Zero, SpriteEffects.None, 1f);
        }
    }
}
