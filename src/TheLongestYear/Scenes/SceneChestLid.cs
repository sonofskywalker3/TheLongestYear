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
        /// <summary>The one lid a scene is driving right now, or null. Only one scene ever runs at
        /// a time, so one is enough, and the patch below needs to find it from a static.</summary>
        private static SceneChestLid _holder;

        /// <summary>Put the driven frame back after <c>Chest.fixLidFrame</c> has snapped it shut.
        ///
        /// THIS IS THE ONLY PLACE THE WRITE SURVIVES, and it cost a live run to find out. The game
        /// runs its ordinary updates AFTER a farm event's tick, not instead of them: Game1.cs:3799
        /// ticks the event and then falls through to Game1.cs:3842, which runs
        /// <c>UpdateCharacters</c>, <c>UpdateLocations</c> and <c>UpdateOther</c> as usual. So
        /// <c>Chest.fixLidFrame</c>, at the top of the chest's own update, ran after everything the
        /// scene wrote and set the lid back to <c>startingLidFrame</c> before a single pixel was
        /// drawn. The screenshots showed a chest that never opened while the log said frame 135.
        /// The same duplicate update is invisible for the scene's night only because the clock is
        /// frozen and the ambient and outdoor colours are set to the same value.</summary>
        public static void ReapplyAfterFix(Chest chest)
        {
            SceneChestLid driving = _holder;
            if (driving == null || !driving._holding || !ReferenceEquals(driving._chest, chest)) return;
            try { LidField.SetValue(chest, driving._wanted); }
            catch (Exception) { /* a lid that will not move is not worth failing the night for. */ }
        }

        /// <summary>The chest's update leaves the lid alone while its frame counter is -1.</summary>
        private const int CounterIdle = -1;

        private static readonly FieldInfo LidField = AccessTools.Field(typeof(Chest), "currentLidFrame");

        private readonly Chest _chest;
        private readonly int _priorLidFrame;
        private readonly int _priorCounter;
        private int _wanted;
        private bool _holding;

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

        /// <summary>The shut lid.</summary>
        public int ShutFrame => Available ? _chest.startingLidFrame.Value : 0;

        /// <summary>The fully open lid. The frames in between are the opening animation, and the
        /// scene walks them rather than snapping, which is what vanilla's own update does
        /// (Chest.cs:1102, one frame every five ticks).</summary>
        public int OpenFrame => Available ? _chest.getLastLidFrame() : 0;

        /// <summary>Hold this lid frame from now on.</summary>
        public void ShowFrame(int frame) => Want(frame);

        /// <summary>What the lid is doing, for the log: a chest whose frames do not move is the one
        /// thing in this scene that cannot be seen from the outside.</summary>
        public string Describe()
        {
            if (!Available) return "unreadable";
            return $"frame {LidField.GetValue(_chest)} of {_chest.startingLidFrame.Value} to {_chest.getLastLidFrame()}";
        }

        private void Want(int frame)
        {
            if (!Available) return;
            _wanted = frame;
            _holding = true;
            _holder = this;
            Hold();
        }

        /// <summary>Write the wanted frame again. Called every tick while the scene runs.</summary>
        public void Hold()
        {
            if (!Available || !_holding) return;
            _chest.frameCounter.Value = CounterIdle;
            LidField.SetValue(_chest, _wanted);
        }

        /// <summary>Put the lid back the way it was found.</summary>
        public void Restore()
        {
            if (!Available || !_holding) return;
            _holding = false;
            if (ReferenceEquals(_holder, this)) _holder = null;
            try
            {
                LidField.SetValue(_chest, _priorLidFrame);
                _chest.frameCounter.Value = _priorCounter;
            }
            catch (Exception) { /* a lid stuck open is not worth failing the night for. */ }
        }
    }
}
