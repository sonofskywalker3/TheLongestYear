using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The card drawn while the lexicon works out answers for a set of mods it has none
    /// stored for (the one build per mod set, and reading the support files): without it the screen
    /// sits black for seconds and looks frozen. The work runs on the game thread inside the save load,
    /// where the game draws nothing, so each stage draws one frame itself and presents it. Drawing is
    /// best effort: any failure switches the card off for the session and the load carries on.</summary>
    internal static class LoadProgress
    {
        private const float TitleScale = 1f;
        private const int LineGap = 16;
        private const int DotCount = 3;
        private const int BarWidth = 480;
        private const int BarHeight = 12;
        private const int BarTravel = 6;

        private static bool _broken;
        private static int _frame;

        /// <summary>Logs a draw failure once; set by the mod entry.</summary>
        public static Action<string> Warn { get; set; }

        /// <summary>Logs each stage shown, so a log tells where a slow load spent its time; set by the mod entry.</summary>
        public static Action<string> Trace { get; set; }

        public static void Reading() => Show(Strings.Get("load.learning.reading"), "reading");

        public static void Routes(int pass)
            => Show(Strings.Get("load.learning.routes", new Dictionary<string, string> { ["pass"] = pass.ToString() }), "routes " + pass);

        public static void YearTwo(int pass)
            => Show(Strings.Get("load.learning.year-two", new Dictionary<string, string> { ["pass"] = pass.ToString() }), "year two " + pass);

        public static void Weeks() => Show(Strings.Get("load.learning.weeks"), "weeks");

        public static void Saving() => Show(Strings.Get("load.learning.saving"), "saving");

        /// <summary>Draws the card with this stage under it. Each call moves the bar one step.</summary>
        private static void Show(string stage, string traceName)
        {
            if (_broken) return;
            try
            {
                SpriteBatch batch = Game1.spriteBatch;
                GraphicsDevice device = Game1.graphics?.GraphicsDevice;
                SpriteFont font = Game1.dialogueFont, small = Game1.smallFont;
                if (batch == null || device == null || font == null || small == null) return;

                string title = Strings.Get("load.learning.title");
                string once = Strings.Get("load.learning.once");
                stage += new string('.', 1 + _frame % DotCount);

                Rectangle view = device.Viewport.Bounds;
                Vector2 titleSize = font.MeasureString(title) * TitleScale;
                Vector2 onceSize = small.MeasureString(once);
                Vector2 stageSize = small.MeasureString(stage);
                float height = titleSize.Y + onceSize.Y + stageSize.Y + BarHeight + LineGap * 3;
                float y = view.Center.Y - height / 2;

                device.SetRenderTarget(null);
                device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                batch.DrawString(font, title, new Vector2(view.Center.X - titleSize.X / 2, y), Color.White, 0, Vector2.Zero, TitleScale, SpriteEffects.None, 1);
                y += titleSize.Y + LineGap;
                batch.DrawString(small, once, new Vector2(view.Center.X - onceSize.X / 2, y), Color.LightGray);
                y += onceSize.Y + LineGap;
                batch.DrawString(small, stage, new Vector2(view.Center.X - stageSize.X / 2, y), Color.LightGray);
                y += stageSize.Y + LineGap;
                // A block that walks along the bar: the build has no known length, only stages.
                var bar = new Rectangle(view.Center.X - BarWidth / 2, (int)y, BarWidth, BarHeight);
                batch.Draw(Game1.staminaRect, bar, Color.DimGray);
                int step = BarWidth / BarTravel;
                batch.Draw(Game1.staminaRect, new Rectangle(bar.X + step * (_frame % BarTravel), bar.Y, step, BarHeight), Color.White);
                batch.End();
                device.Present();
                _frame++;
                Trace?.Invoke($"Load card: {traceName}.");
            }
            catch (Exception e) when (e is InvalidOperationException or NullReferenceException or ArgumentException or NotSupportedException)
            {
                _broken = true;
                Warn?.Invoke($"Load progress card could not be drawn ({e.GetType().Name}: {e.Message}); loads go on without it.");
            }
        }
    }
}
