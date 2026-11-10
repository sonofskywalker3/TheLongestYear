using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace TheLongestYear.Debug
{
    /// <summary>Debug only (tly_pad): for a few ticks MonoGame's own <see cref="GamePad.GetState(PlayerIndex)"/>
    /// reports a connected pad holding one button. SMAPI and the game read that like a real pad, so the
    /// whole controller path runs (SMAPI's input state, pressed-button detection, the menu's
    /// receiveGamePadButton, the synthesized A click) with no pad attached. Patched on first use only;
    /// outside a press the prefix lets the real read through.</summary>
    internal static class ScriptedPadInput
    {
        /// <summary>Full trigger travel, so the game reads the trigger as pressed.</summary>
        private const float TriggerDown = 1f;

        private static bool _patched;
        private static Buttons _button;
        private static int _releaseTick = -1;
        private static int _endTick = -1;

        /// <summary>True while a press (or the release tick after it) is still being reported.</summary>
        public static bool Running => Game1.ticks <= _endTick;

        /// <summary>Hold <paramref name="button"/> for <paramref name="heldTicks"/> ticks, then report it
        /// released for two more so the game sees the release.</summary>
        public static void Press(Harmony harmony, Buttons button, int heldTicks)
        {
            EnsurePatched(harmony);
            _button = button;
            _releaseTick = Game1.ticks + heldTicks;
            _endTick = _releaseTick + 2;
        }

        private static void EnsurePatched(Harmony harmony)
        {
            if (_patched) return;
            _patched = true;
            var prefix = new HarmonyMethod(typeof(ScriptedPadInput), nameof(Prefix));
            foreach (MethodInfo getState in typeof(GamePad).GetMethods(BindingFlags.Public | BindingFlags.Static)
                         .Where(m => m.Name == nameof(GamePad.GetState) && m.ReturnType == typeof(GamePadState)))
                harmony.Patch(getState, prefix: prefix);
        }

        private static bool Prefix(ref GamePadState __result)
        {
            if (!Running) return true;
            bool held = Game1.ticks <= _releaseTick;
            float left = held && _button == Buttons.LeftTrigger ? TriggerDown : 0f;
            float right = held && _button == Buttons.RightTrigger ? TriggerDown : 0f;
            // Triggers also carry their button flag, as MonoGame's own pad read sets it past the threshold.
            Buttons face = held && !IsDPad(_button) ? _button : 0;
            ButtonState Dir(Buttons b) => held && _button == b ? ButtonState.Pressed : ButtonState.Released;
            __result = new GamePadState(
                new GamePadThumbSticks(Vector2.Zero, Vector2.Zero),
                new GamePadTriggers(left, right),
                new GamePadButtons(face),
                new GamePadDPad(Dir(Buttons.DPadUp), Dir(Buttons.DPadDown), Dir(Buttons.DPadLeft), Dir(Buttons.DPadRight)));
            return false;
        }

        private static bool IsDPad(Buttons b)
            => b is Buttons.DPadUp or Buttons.DPadDown or Buttons.DPadLeft or Buttons.DPadRight;
    }
}
