using System;
using System.Reflection;
using HarmonyLib;
using StardewValley.Objects;

namespace TheLongestYear.Scenes
{
    /// <summary>Opening and shutting a real chest's lid for an overnight strike scene (spec
    /// 2026-09-21, the thief). The chest in the frame is the real one, so its lid has to be the
    /// real lid and not a sprite painted over it.
    ///
    /// HOW THE LID IS DRAWN. <c>Chest.draw</c> picks its lid rectangle straight out of the private
    /// field <c>currentLidFrame</c> (Chest.cs:1258 and 1331), which runs from
    /// <c>startingLidFrame</c> to <c>getLastLidFrame()</c>. There is no public setter: vanilla only
    /// ever moves it through the mutex dance a player's click starts (Chest.cs:1102 onwards), which
    /// would open a menu and ask the network for a lock. So the frame is written directly, and
    /// <see cref="Hold"/> writes it again every tick, because <c>Chest.fixLidFrame</c> runs at the
    /// top of the chest's own update and snaps an unlocked player chest back shut. That is the same
    /// per-tick rewrite, for the same reason, that the scene's night needs.
    ///
    /// <c>frameCounter</c> is parked at -1 for the duration so the chest's update does not try to
    /// animate the lid underneath us, and both fields go back in <see cref="Restore"/>.</summary>
    internal sealed class SceneChestLid
    {
        /// <summary>The chest's update leaves the lid alone while its frame counter is -1.</summary>
        private const int CounterIdle = -1;

        private static readonly FieldInfo LidField = AccessTools.Field(typeof(Chest), "currentLidFrame");

        private readonly Chest _chest;
        private readonly int _priorLidFrame;
        private readonly int _priorCounter;
        private int _wanted;
        private bool _driving;

        /// <summary>False when this build of the game does not have the field the lid is drawn
        /// from. The scene then plays without the lid moving rather than failing.</summary>
        public bool Available { get; }

        public SceneChestLid(Chest chest)
        {
            _chest = chest ?? throw new ArgumentNullException(nameof(chest));
            Available = LidField != null;
            if (!Available) return;
            _priorLidFrame = (int)LidField.GetValue(_chest);
            _priorCounter = _chest.frameCounter.Value;
            _wanted = _priorLidFrame;
        }

        /// <summary>The lid up, on the chest's last lid frame.</summary>
        public void Open() => Want(_chest.getLastLidFrame());

        /// <summary>The lid down again.</summary>
        public void Close() => Want(_chest.startingLidFrame.Value);

        private void Want(int frame)
        {
            if (!Available) return;
            _wanted = frame;
            _driving = true;
            Hold();
        }

        /// <summary>Write the wanted frame again. Called every tick while the scene runs.</summary>
        public void Hold()
        {
            if (!Available || !_driving) return;
            _chest.frameCounter.Value = CounterIdle;
            LidField.SetValue(_chest, _wanted);
        }

        /// <summary>Put the lid back the way it was found.</summary>
        public void Restore()
        {
            if (!Available || !_driving) return;
            _driving = false;
            try
            {
                LidField.SetValue(_chest, _priorLidFrame);
                _chest.frameCounter.Value = _priorCounter;
            }
            catch (Exception) { /* a lid stuck open is not worth failing the night for. */ }
        }
    }
}
