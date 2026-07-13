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
            // NOT skippable: the opening intro carries the only explanation of the loop, and a skip
            // bypasses the end command that sets the cc-seen flag — leaving CcSeen false so the
            // driver re-fires the event, closing+reopening the dialog forever (2026-06-01 playtest).
            // Omitting "skippable" makes the event play through to its addMailReceived/"end".

            // ---- Scene 1: the farm porch (Lewis) ----
            "changeLocation Farm",
            "warp farmer 66 18 true",
            "addTemporaryActor Lewis 16 32 68 18 3 true Character",
            "viewport 66 18 true",
            "faceDirection farmer 1",
            "pause 1000",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-1")}\"",
            "pause 200",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-2")}\"",
            "pause 200",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-3")}\"",
            "pause 200",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-4")}\"",
            "pause 400",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-5")}\"",
            "pause 200",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-6")}\"",
            "pause 300",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-7")}\"",
            "playSound coin",
            "pause 800",
            $"speak Lewis \"{Strings.Get("event.intro.lewis-8")}\"",
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
            $"speak Junimo \"{Strings.Get("event.intro.junimo-1")}\"",
            "pause 200",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-2")}\"",
            "pause 200",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-3")}\"",
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-4")}\"",
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-5")}\"",
            "pause 400",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-6")}\"",
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-7")}\"",
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-8")}\"",
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-9")}\"",
            "pause 300",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-10")}\"",
            "pause 800",
            $"speak Junimo \"{Strings.Get("event.intro.junimo-11")}\"",
            "pause 600",
            "playSound junimoMeep1",
            "pause 1000",
            $"addMailReceived {IntroEventKeys.CcSeenMail}",
            "end"
        });
    }
}
