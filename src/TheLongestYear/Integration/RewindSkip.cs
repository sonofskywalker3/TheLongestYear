using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core.Rewind;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>The player's skip for the whole rewind: the bedroom, the Town pan and the morning.
    /// The three beats are drawn by the mod rather than run as a vanilla event, so vanilla's own skip
    /// button never appeared for them (Jeff, 2026-09-16). This draws the same button in the same
    /// corner (Event.skipBounds and its draw, Event.cs 10319 and 10921), takes a click on it or the
    /// controller's Back button (Game1.cs 4447), and then finishes the beats one per tick through their
    /// existing skip entry points, under black, until the morning beat has handed over to the
    /// keep-or-release question. Only the scenes are skipped, never the question or the shrine.
    ///
    /// Offered per <see cref="RewindSkipRule"/>: <see cref="Arm"/> reads it when the bedroom opens,
    /// and <see cref="MarkSeen"/> records the showing when the morning beat ends.</summary>
    internal static class RewindSkip
    {
        /// <summary>The skip graphic on <c>Game1.mouseCursors</c>, as vanilla's event draws it.</summary>
        private static readonly Rectangle SkipSource = new Rectangle(205, 406, 22, 15);
        private const int SkipScale = 4;
        private const int EdgeMargin = 8;
        private const int BottomOffset = 64;
        private const float HoverFade = 0.5f;

        /// <summary>Ticks the skip may wait with no beat on screen (the morning beat can be deferred
        /// behind a vanilla menu) before it gives up rather than cover the screen indefinitely.</summary>
        private const int MaxIdleTicks = 600;

        private static IMonitor _monitor;
        private static IModHelper _helper;
        private static MetaStore _meta;
        private static bool _registered;
        private static bool _armed;
        private static bool _skipping;
        private static int _idleTicks;

        public static void Register(IMonitor monitor, IModHelper helper, MetaStore meta)
        {
            _monitor = monitor;
            _helper = helper;
            _meta = meta;
            if (_registered) return;
            _registered = true;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => Stop();
        }

        /// <summary>Called as the bedroom opens: offers the skip for this rewind if the save has seen one.</summary>
        public static void Arm()
        {
            var state = _meta?.State;
            _armed = state != null && RewindSkipRule.IsSkippable(state.SeasonTurnsSeen, state.CompletedResets);
            _skipping = false;
            _monitor?.Log($"RewindSkip: skip {(_armed ? "offered" : "not offered (first rewind on this save)")}.", LogLevel.Trace);
        }

        /// <summary>Called when the morning beat ends, watched or skipped: the next rewind is skippable.</summary>
        public static void MarkSeen()
        {
            _meta?.State?.SeasonTurnsSeen.Add(RewindSkipRule.SeenName);
            Stop();
        }

        /// <summary>Drops the skip without recording a showing (the pan gave up, or a quit to title).</summary>
        public static void Stop()
        {
            _armed = false;
            _skipping = false;
            _idleTicks = 0;
        }

        private static bool BeatOnScreen()
            => Game1.activeClickableMenu is RewindJunimoScene || RewindPanScene.IsActive;

        private static bool ButtonShown()
            => _armed && !_skipping && !Game1.options.SnappyMenus && BeatOnScreen();

        private static Rectangle Bounds()
        {
            int width = SkipSource.Width * SkipScale;
            var bounds = new Rectangle(
                Game1.uiViewport.Width - width - EdgeMargin, Game1.uiViewport.Height - BottomOffset,
                width, SkipSource.Height * SkipScale);
            Utility.makeSafe(ref bounds);
            return bounds;
        }

        private static void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!_armed || _skipping || !BeatOnScreen()) return;
            bool clicked = e.Button == SButton.MouseLeft && ButtonShown()
                           && Bounds().Contains(Game1.getMouseX(), Game1.getMouseY());
            bool back = e.Button == SButton.ControllerBack;
            if (!clicked && !back) return;

            _helper.Input.Suppress(e.Button);
            Begin(back ? "Back button" : "skip button");
        }

        /// <summary>The same skip the button starts, for <c>tly_skipscene all</c>. False when this
        /// rewind offers no skip or no beat is on screen.</summary>
        public static bool TrySkip(string source)
        {
            if (!_armed || _skipping || !BeatOnScreen()) return false;
            Begin(source);
            return true;
        }

        private static void Begin(string source)
        {
            _skipping = true;
            _idleTicks = 0;
            Game1.playSound("drumkit6");
            _monitor?.Log($"RewindSkip: the player skipped the rewind ({source}).", LogLevel.Info);
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_skipping) return;
            try
            {
                if (Game1.activeClickableMenu is RewindJunimoScene scene)
                {
                    _idleTicks = 0;
                    scene.SkipToEnd();   // the morning beat's end calls MarkSeen, which stops the skip
                    return;
                }
                if (RewindPanScene.SkipToEnd())
                {
                    _idleTicks = 0;
                    return;
                }
                if (++_idleTicks > MaxIdleTicks)
                {
                    _monitor?.Log("RewindSkip: no rewind beat came up to skip; stopping.", LogLevel.Warn);
                    Stop();
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindSkip: {ex.GetType().Name} while skipping: {ex.Message}; the scenes play on.", LogLevel.Error);
                Stop();
            }
        }

        private static void OnRendered(object sender, RenderedEventArgs e)
        {
            if (_skipping)
            {
                // Each beat is on screen for the one frame before it is skipped; cover them. Never
                // cover a vanilla menu the morning beat is waiting behind: the player has to see it.
                if (Game1.activeClickableMenu != null && !BeatOnScreen()) return;
                e.SpriteBatch.Draw(Game1.fadeToBlackRect,
                    new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black);
                return;
            }
            if (!ButtonShown()) return;
            Rectangle bounds = Bounds();
            Color color = bounds.Contains(Game1.getMouseX(), Game1.getMouseY()) ? Color.White * HoverFade : Color.White;
            e.SpriteBatch.Draw(Game1.mouseCursors, new Vector2(bounds.X, bounds.Y), SkipSource, color,
                0f, Vector2.Zero, SkipScale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 1f);
            // The game draws no cursor while controls are frozen (the whole pan), and a menu's
            // cursor is under this button; draw it on top, as IClickableMenu.drawMouse does.
            e.SpriteBatch.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, CursorFrame, CursorTile, CursorTile),
                Color.White, 0f, Vector2.Zero, SkipScale + Game1.dialogueButtonScale / CursorPulseDivisor,
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 1f);
        }

        private const int CursorFrame = 0;
        private const int CursorTile = 16;
        private const float CursorPulseDivisor = 150f;
    }
}
