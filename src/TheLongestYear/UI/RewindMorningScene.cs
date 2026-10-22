using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        /// <summary>The pan's last two seconds fade the screen to black and it hands over on the
        /// black frame, so this beat opens on black and fades up. Both halves happen inside one
        /// synchronous step of the driver's hand-off, so there is never a lit frame between them.</summary>
        private const float FadeInMs = 700f;

        /// <summary>And out again once the line is done. The beat used to end by tearing its actors
        /// down and handing straight to the hold question, so the Junimo circle blinked out from
        /// under the player mid-frame (playtest 2026-09-11). It fades out instead, and
        /// <see cref="RewindBlackout"/> picks the black up from here and holds it through the
        /// question, the shrine and the theme picker.</summary>
        private const float FadeOutMs = 900f;

        protected override string JunimoNamePrefix => "TlyRewindMorningJunimo";

        private float _fadeInElapsed;
        private float _fadeOutElapsed = -1f;   // negative until the line closes

        public RewindMorningScene(Action onComplete)
            : base(onComplete)
        {
            SpawnJunimos();

            string playerName = Game1.player?.Name ?? string.Empty;
            string line = Strings.Get("cutscene.rewind.morning").Replace("@", playerName);
            ActiveBox = new EndingSpeechBox(PortraitFor(ClosingSpeaker), new List<string> { line }) { AutoAdvance = true };
        }

        /// <summary>The only line of the beat has finished, so the beat starts fading out. It does
        /// NOT finish here: <see cref="update"/> finishes it once the screen is black.</summary>
        protected override void OnBoxClosed() => _fadeOutElapsed = 0f;

        /// <summary>Skipping lands on the black a watched run ends on, so the hand-off into the hold
        /// question looks the same either way.</summary>
        public override void SkipToEnd()
        {
            _fadeOutElapsed = FadeOutMs;
            RewindBlackout.Begin();
            base.SkipToEnd();
        }

        public override void update(GameTime time)
        {
            base.update(time);   // keeps the Junimos bobbing even after the line is done
            float ms = (float)time.ElapsedGameTime.TotalMilliseconds;
            _fadeInElapsed += ms;
            if (Completed) return;

            if (_fadeOutElapsed >= 0f)
            {
                _fadeOutElapsed += ms;
                if (_fadeOutElapsed >= FadeOutMs)
                {
                    // Hand the black over BEFORE finishing, in the same step: Finish() tears the
                    // Junimos down and runs OnCutsceneEnded, which opens the hold question, and the
                    // blackout has to already be painting by then or that frame flashes lit.
                    RewindBlackout.Begin();
                    Finish();
                }
                return;
            }

            ForwardToBox(box => box.update(time));   // an auto-advancing box can close itself here
        }

        public override void draw(SpriteBatch b)
        {
            float alpha = _fadeOutElapsed >= 0f
                ? MathHelper.Clamp(_fadeOutElapsed / FadeOutMs, 0f, 1f)
                : 1f - MathHelper.Clamp(_fadeInElapsed / FadeInMs, 0f, 1f);
            if (alpha > 0f)
            {
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                    Color.Black * alpha);
            }
            base.draw(b);   // the speech box on top
        }
    }
}
