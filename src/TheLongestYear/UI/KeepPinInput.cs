using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The pin toggle shared by the rewind shrine and the planning shrine's Plan tab:
    /// controller Y or a right-click on a keep row pins its chain to the top of the tab
    /// (<see cref="PinnedKeeps"/>). Host only, like banish: the pins are one list per save.</summary>
    internal sealed class KeepPinInput
    {
        /// <summary>Game tick of the last toggle. One press can reach a menu twice in the same
        /// tick (a gamepad press plus a synthesized click), which would pin and unpin at once.</summary>
        private int _lastToggleTick = -1;

        public static bool CanPin => Context.IsMainPlayer;

        /// <summary>True for a right-click the player just pressed. The game repeats
        /// receiveRightClick while the mouse button (or the controller X it maps from) is held, and
        /// a held button must not flip the pin back and forth.</summary>
        public static bool IsFreshRightClick()
            => Game1.oldMouseState.RightButton == ButtonState.Released
               && !Game1.oldPadState.IsButtonDown(Buttons.X);

        /// <summary>Pins or unpins <paramref name="keepId"/>'s chain. Returns false when nothing
        /// changed (not the host, or a second dispatch of the same press).</summary>
        public bool TryToggle(MetaState state, string keepId)
        {
            if (!CanPin || keepId == null || Game1.ticks == _lastToggleTick)
                return false;
            _lastToggleTick = Game1.ticks;
            bool pinned = PinnedKeeps.Toggle(state, keepId);
            Game1.playSound(pinned ? "dwop" : "smallSelect");
            return true;
        }
    }

    /// <summary>The gold star drawn on a pinned keep row, in every row state.</summary>
    internal static class KeepPinBadge
    {
        /// <summary>The gold quality star in Cursors.png (8x8).</summary>
        private static readonly Rectangle StarSource = new(346, 400, 8, 8);
        private const int StarSourceSize = 8;

        /// <summary>Star scale on the rewind shrine's tall rows (24px).</summary>
        public const float ShopScale = 3f;

        /// <summary>Star scale on the planning shrine's one-line rows (20px).</summary>
        public const float PlanScale = 2.5f;

        public static int SizeFor(float scale) => (int)(StarSourceSize * scale);

        public static void Draw(SpriteBatch b, Vector2 topLeft, float scale)
            => b.Draw(Game1.mouseCursors, topLeft, StarSource, Color.White, 0f, Vector2.Zero, scale,
                SpriteEffects.None, 1f);
    }
}
