using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>Beats 11-12 of the rewind cutscene (design 2026-09-11): the morning after the pan and
    /// the paint. Respawns the same four Junimos <see cref="RewindBedroomScene"/> tore down before the
    /// pan, now around the sleeping farmer in the farmhouse with NO light source: "no light aura" is
    /// the whole point of this beat, and the room's actual look belongs to
    /// <c>RewindSpringPaint</c> by now, not to this scene. One line plays, then the scene finishes
    /// into <c>RunController.OnCutsceneEnded</c>.
    ///
    /// Everything structural (the actors, the idle animation, the teardown, the menu-steal watch, the
    /// speech-box forwarding, the single-shot finish) is <see cref="RewindJunimoScene"/>'s. This scene
    /// is the one line and the deliberate absence of a light, which is all that ever distinguished it:
    /// before the 2026-09-11 extraction it was a near line-for-line copy of the bedroom scene that had
    /// been forked before the steal watch was added, and so leaked four Junimos into the farmhouse
    /// whenever a menu took the frame during beat 12.
    ///
    /// Moved out of Day28CutsceneDriver.cs at the same time, where it had been a nested class: the
    /// driver is the thing that chains the beats, not a place to keep one.</summary>
    internal sealed class RewindMorningScene : RewindJunimoScene
    {
        /// <summary>Which of the six speaks the closing line. The ending's hall scene has Junimo 0
        /// open the circle and come back to close it; this is the same shape across the rewind, whose
        /// four bedroom lines run 0, 1, 2, 3.</summary>
        private const int ClosingSpeaker = 0;

        protected override string JunimoNamePrefix => "TlyRewindMorningJunimo";

        public RewindMorningScene(Action onComplete)
            : base(onComplete)
        {
            SpawnJunimos();

            string playerName = Game1.player?.Name ?? string.Empty;
            string line = Strings.Get("cutscene.rewind.morning").Replace("@", playerName);
            ActiveBox = new EndingSpeechBox(PortraitFor(ClosingSpeaker), new List<string> { line });
        }

        /// <summary>The only line of the beat has finished, so the beat has.</summary>
        protected override void OnBoxClosed() => Finish();

        public override void update(GameTime time)
        {
            base.update(time);   // keeps the Junimos bobbing even after the line is done
            if (Completed) return;
            ActiveBox?.update(time);
        }
    }
}
