using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Re-draw the clock/date/money HUD during festivals + draw the always-on JP
        /// HUD. Vanilla's drawHUD short-circuits on eventUp (Game1.cs:15410) so the festival
        /// re-draw is needed for the clock when FestivalTimeFlow is active; the JP HUD piggy-
        /// backs on the same event hook with its own visibility gating.</summary>
        private void OnRenderedHud(object sender, StardewModdingAPI.Events.RenderingHudEventArgs e)
        {
            if (!RunActivation.IsActive)
                return;
            if (Game1.isFestival() && Game1.dayTimeMoneyBox != null)
                Game1.dayTimeMoneyBox.draw(e.SpriteBatch);

            DrawJpHud(e.SpriteBatch);
        }

        /// <summary>
        /// Always-on top-right HUD showing banked JP + the current week's theme. Two lines max:
        /// <c>JP: 123</c> on top, <c>Mining (1.5x)</c> (or <c>Mining (1.5x, lifted)</c> when the
        /// weekly theme quest is complete and the drawback is suppressed) on the bottom.
        /// Positioned directly below the vanilla day/time/money box so it doesn't fight other
        /// HUD elements for screen space. Hidden when the player has toggled the HUD off
        /// (<c>Game1.displayHUD</c>), during cutscenes (<c>Game1.eventUp</c>), or when the
        /// mod-side toggle <see cref="GameplayConfig.ShowJpHud"/> is off.
        /// </summary>
        private void DrawJpHud(Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            if (_meta == null) return;
            if (!Context.IsWorldReady) return;
            if (!_config.ShowJpHud) return;
            if (!Game1.displayHUD) return;
            if (Game1.eventUp) return;

            long jp = _meta.State.JunimoPoints;
            // 2026-05-29 playtest: theme line removed. The current theme + lifted/active state
            // already shows on the WeeklyThemeQuest entry in the player's quest log, so the
            // HUD echoing it was redundant and made the box too tall after the dialogueFont
            // bump. Keep this minimal — just the banked JP count.
            var lines = new System.Collections.Generic.List<string> { $"JP: {jp}" };

            const int Padding = 14;
            const int LineGap = 6;
            // dialogueFont scaled to 0.95 — the unscaled version was "about 5% too big" per
            // the 2026-05-29 playtest. Padding also pulled back from 16 → 14 to match the
            // tighter text bounds.
            var font = Game1.dialogueFont;
            const float TextScale = 0.95f;

            float maxWidth = 0f;
            float totalHeight = 0f;
            foreach (string line in lines)
            {
                Microsoft.Xna.Framework.Vector2 size = font.MeasureString(line) * TextScale;
                if (size.X > maxWidth) maxWidth = size.X;
                totalHeight += size.Y;
            }
            if (lines.Count > 1) totalHeight += LineGap * (lines.Count - 1);

            int boxWidth = (int)maxWidth + Padding * 2;
            int boxHeight = (int)totalHeight + Padding * 2;

            // Position: top-right, BELOW the vanilla day/time/money box. 2026-05-28 round 4:
            // user reported the HUD sat "a little too low" — dropped the spacer from 80px to
            // 24px so it nests just under the box without leaving a visible gap. Read the
            // box's height via reflection (DayTimeMoneyBox.height is a static on PC, instance
            // on Android — same field name, different shape).
            int x = Game1.uiViewport.Width - boxWidth - 8;
            int boxTopY = Game1.dayTimeMoneyBox?.yPositionOnScreen ?? 0;
            int hudBoxHeight = 228;
            var hf = typeof(StardewValley.Menus.DayTimeMoneyBox).GetField("height",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.FlattenHierarchy);
            if (hf != null)
            {
                object hv = hf.IsStatic ? hf.GetValue(null) : hf.GetValue(Game1.dayTimeMoneyBox);
                if (hv is int hi && hi > 0) hudBoxHeight = hi;
            }
            int y = boxTopY + hudBoxHeight + 24;

            StardewValley.Menus.IClickableMenu.drawTextureBox(b, x, y, boxWidth, boxHeight,
                Microsoft.Xna.Framework.Color.White);

            int textY = y + Padding;
            foreach (string line in lines)
            {
                StardewValley.Utility.drawTextWithShadow(b, line, font,
                    new Microsoft.Xna.Framework.Vector2(x + Padding, textY), Game1.textColor,
                    scale: TextScale);
                textY += (int)(font.MeasureString(line).Y * TextScale) + LineGap;
            }
        }

        /// <summary>Developer bridge only: the game pauses its update loop whenever its window is not
        /// the foreground window (Game1.cs:4693, unless options.pauseWhenOutOfFocus is off), which is
        /// why every earlier unattended run had to steal focus to make a queued command execute. With
        /// the bridge on, the vanilla "pause when inactive" option is switched off so tly_* commands
        /// keep flowing while Jeff works in another window. A shipped build never touches it.</summary>
        private void KeepRunningUnfocused(string when)
        {
            if (!_config.EnableDebugCommandBridge || Game1.options == null) return;
            if (!Game1.options.pauseWhenOutOfFocus) return;
            Game1.options.pauseWhenOutOfFocus = false;
            this.Monitor.Log(
                $"Debug bridge: 'pause when window is inactive' switched off at {when} so queued commands run without focus.",
                LogLevel.Info);
        }

        /// <summary>SDV doesn't persist a windowed width/height (it always boots at 1280×720 in
        /// windowed mode), and the dev redeploy loop force-kills the game so it never saves one on
        /// exit. When <see cref="GameplayConfig.WindowWidth"/>/<c>Height</c> are positive and the
        /// game is NOT in fullscreen, nudge the window to that size once the game is up — the game's
        /// own ClientSizeChanged handler then re-derives the viewport. 0 (either dim) = leave alone.</summary>
        private void ApplyWindowSize()
        {
            int w = _config.WindowWidth, h = _config.WindowHeight;
            if (w <= 0 || h <= 0)
                return;
            if (Game1.graphics == null || Game1.graphics.IsFullScreen)
                return;
            if (Game1.graphics.PreferredBackBufferWidth == w && Game1.graphics.PreferredBackBufferHeight == h)
                return;

            Game1.graphics.PreferredBackBufferWidth = w;
            Game1.graphics.PreferredBackBufferHeight = h;
            Game1.graphics.ApplyChanges();
            this.Monitor.Log($"Window: set to {w}x{h} (config dial).", LogLevel.Info);
        }
    }
}
