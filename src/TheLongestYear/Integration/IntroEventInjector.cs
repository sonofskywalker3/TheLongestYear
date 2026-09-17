using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Cross-run bookkeeping for the opening: promotes the cc-seen flag to MetaState.HasSeenIntro
    /// at first save, plants the legacy done flag on later loops, and clears everything for
    /// tly_replayintro. The opening itself is vanilla's chain with OpeningStringsEditor and
    /// OpeningEventInjector.
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
                Game1.player.eventsSeen.Remove("60367");
            }
            _monitor.Log(
                "IntroEventInjector: cleared HasSeenIntro + intro mail/eventsSeen entries. " +
                "Run tly_reset to retest the intro.",
                LogLevel.Warn);
        }

    }
}
