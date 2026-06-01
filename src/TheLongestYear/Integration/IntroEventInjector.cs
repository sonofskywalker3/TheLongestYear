using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// v1.1 narrative intro content + cross-run bookkeeping. The intro is a SINGLE event the
    /// <see cref="IntroSequenceDriver"/> starts once on the first morning of a fresh run. Rather
    /// than warping the player around from the mod (which fights the engine), the event plays the
    /// porch (Lewis) scene, then uses the vanilla in-event <c>changeLocation</c> command to move
    /// itself + the player + the camera into the Community Center for the Junimo scene — exactly
    /// how vanilla stages multi-location cutscenes. When it ends, the engine returns the player to
    /// where it started (the farmhouse) and the driver opens the theme picker.
    ///
    /// Gating: the event adds <see cref="IntroEventKeys.CcSeenMail"/> at the end;
    /// <see cref="MarkIntroSeenIfApplicable"/> (from <c>ModEntry.OnSaving</c>) promotes that to the
    /// cross-run <c>MetaState.HasSeenIntro</c>, which survives resets and suppresses the intro on
    /// every later loop. <c>tly_replayintro</c> clears it to retest.
    /// </summary>
    internal sealed class IntroEventInjector
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public IntroEventInjector(IMonitor monitor, MetaStore meta)
        {
            _monitor = monitor;
            _meta = meta;
        }

        /// <summary>Called from <c>ModEntry.OnSaveLoaded</c>. When the player has already seen the
        /// intro on a prior loop, plant the legacy done flag (kept for replay bookkeeping).</summary>
        public void ApplyMailFlagsForRun()
        {
            if (!_meta.State.HasSeenIntro) return;
            if (Game1.player == null) return;
            if (!Game1.player.mailReceived.Contains(IntroEventKeys.IntroDoneMail))
                Game1.player.mailReceived.Add(IntroEventKeys.IntroDoneMail);
        }

        /// <summary>Called from <c>ModEntry.OnSaving</c>. If the intro event finished this run (it
        /// adds the cc-seen flag), promote it to the cross-run <see cref="MetaState.HasSeenIntro"/>
        /// so the upcoming reset can't lose it.</summary>
        public void MarkIntroSeenIfApplicable()
        {
            if (_meta.State.HasSeenIntro) return;
            if (Game1.player == null) return;
            if (Game1.player.mailReceived.Contains(IntroEventKeys.CcSeenMail))
            {
                _meta.State.HasSeenIntro = true;
                _monitor.Log(
                    "IntroEventInjector: intro completed — promoting to MetaState.HasSeenIntro.",
                    LogLevel.Info);
            }
        }

        /// <summary>Debug helper for <c>tly_replayintro</c>: clear the cross-run flag + the per-run
        /// mail/eventsSeen entries so a <c>tly_reset</c> back to Spring 1 re-fires the intro.</summary>
        public void ClearIntroState()
        {
            _meta.State.HasSeenIntro = false;
            if (Game1.player != null)
            {
                Game1.player.mailReceived.Remove(IntroEventKeys.CcSeenMail);
                Game1.player.mailReceived.Remove(IntroEventKeys.IntroDoneMail);
                Game1.player.eventsSeen.Remove(IntroEventKeys.IntroEventId);
            }
            _monitor.Log(
                "IntroEventInjector: cleared HasSeenIntro + intro mail/eventsSeen entries. " +
                "Run tly_reset to retest the intro.",
                LogLevel.Warn);
        }

        // ---- Event script ------------------------------------------------------------------

        /// <summary>The combined intro event script (the value half of a Data/Events entry — no key
        /// or preconditions, since the driver starts it explicitly). Scene 1 is the farm porch with
        /// Lewis; <c>changeLocation CommunityCenter</c> carries the scene into the CC for the Junimo;
        /// it ends by setting the cc-seen flag. No blocking <c>move</c> commands (a blocked move
        /// hangs the event); actors are placed with <c>warp</c>/<c>addTemporaryActor</c> instead.</summary>
        internal static string BuildIntroEvent() => string.Join("/", new[]
        {
            "none",                                  // music
            "8 8",                                   // initial viewport (farmhouse) — changeLocation follows at once
            "farmer 8 8 2",                          // farmer in the farmhouse; repositioned per-scene below
            "skippable",

            // ---- Scene 1: the farm porch (Lewis) ----
            "changeLocation Farm",
            "warp farmer 66 18 true",
            "addTemporaryActor Lewis 16 32 68 18 3 true Character",
            "viewport 66 18 true",
            "faceDirection farmer 1",
            "pause 1000",
            "speak Lewis \"Hi there, @. Welcome to the valley.#$b#I'm Lewis — mayor of Pelican Town. I came up to greet you and your new farm.$h\"",
            "pause 200",
            "speak Lewis \"I wanted to be the first to say it... but I'm afraid I have to apologize for the timing.$s\"",
            "pause 200",
            "speak Lewis \"Joja Corporation rolled out a 'revitalization initiative' this spring. They've already filed permits to demolish our old Community Center. They're calling it an 'eyesore.'$a\"",
            "pause 200",
            "speak Lewis \"Deadline is Winter 28. If nothing changes by then, it'll be a Joja warehouse by next year.$s\"",
            "pause 400",
            "speak Lewis \"If only there was a way for us to repair the old Community Center.$s#$b#It's a registered historical landmark, you know. The state will protect it — but only if it's restored to full working condition before that deadline.$h\"",
            "pause 200",
            "speak Lewis \"I figured... if anyone's going to take a swing at it, it ought to be you. New blood. Fresh eyes.$h\"",
            "pause 300",
            "speak Lewis \"Here. Take the key to the front door. Have a look inside.$h\"",
            "playSound coin",
            "pause 800",
            "speak Lewis \"Good luck out there, @. Pelican Town's rooting for you.$h\"",
            "pause 600",

            // ---- Scene 2: the Community Center (Junimo) ----
            "changeLocation CommunityCenter",
            "warp farmer 32 16 true",
            "addTemporaryActor Junimo 16 16 32 11 2 false character Junimo",
            "viewport 32 14 true",
            "faceDirection farmer 0",
            "pause 800",
            "playSound junimoMeep1",
            "pause 400",
            "speak Junimo \"Hi! We are the Junimos.#$b#We've lived here since the founders built the Center, ages ago.$h\"",
            "pause 200",
            "speak Junimo \"We helped them keep the valley alive. They brought us Bundles — gifts of crops, fish, fiber, food — and we channeled the seasons back through the land.$h\"",
            "pause 200",
            "speak Junimo \"Then the people stopped coming. The Center fell silent. We slept.$s\"",
            "pause 200",
            "speak Junimo \"Now Joja wants to tear it all down. But you're here. So we woke up.$h\"",
            "pause 300",
            "speak Junimo \"If you fill the Bundles — all of them, every room — the Center will be whole again. Joja cannot touch a restored landmark.$h\"",
            "pause 400",
            "speak Junimo \"But the year is short, @. We can already feel it. You will not finish in time. Not on this run.$s\"",
            "pause 200",
            "speak Junimo \"So if Winter ends with Bundles unfilled, we will turn time back. The seasons, the crops, the buildings — all of it returns to Spring 1.$s\"",
            "pause 300",
            "speak Junimo \"Your progress will be lost. But the energy you spent — every donation, every season worked, every connection — we keep that. We will use it to make the next loop a little kinder.$h\"",
            "pause 300",
            "speak Junimo \"We will do it as many times as we must. There is no shame in starting again.$h\"",
            "pause 300",
            "speak Junimo \"Will you help us?$h\"",
            "pause 800",
            "speak Junimo \"Good luck, @. Spring 1 is yours.$h\"",
            "pause 600",
            "playSound junimoMeep1",
            "pause 1000",
            $"addMailReceived {IntroEventKeys.CcSeenMail}",
            "end"
        });
    }
}
