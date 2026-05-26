using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Skips the vanilla CC unlock cutscene chain (Demetrius visit → Wizard reveal) so the
    /// Community Center is accessible from day 1 of every run. Called on save load and after
    /// every in-place world reset.
    ///
    /// v1 simplification: replace the cutscenes with nothing (silent unlock). Plan 06+ will
    /// add a proper narrative beat ("the Junimos enlist you on day 1") — for now, the door
    /// just opens and the bundles are readable immediately.
    ///
    /// Flags sourced from the 1.6 Android decompile:
    ///   - eventsSeen "191393"        — Demetrius-visit event; Game1.isLocationAccessible("CommunityCenter")
    ///                                  returns true only when this is seen (Game1.cs:13121).
    ///   - eventsSeen "112"           — Wizard-reveal event (WizardHouse); endBehaviors adds
    ///                                  canReadJunimoText (Event.cs:10788).
    ///   - mailReceived "ccDoorUnlock"     — GameLocation WarpCommunityCenter action gate (GameLocation.cs:9340).
    ///   - mailReceived "canReadJunimoText" — JunimoNoteMenu shows real bundle names vs "???" scramble
    ///                                        (JunimoNoteMenu.cs:390). GameMenu CC tab gate (GameMenu.cs:121).
    ///   - mailReceived "seenJunimoNote"   — Set by JunimoNoteMenu.setUpMenu when first opened; pre-setting
    ///                                        it suppresses the first-time Junimo-note intro flow.
    /// </summary>
    internal sealed class CommunityCenterUnlock
    {
        private readonly IMonitor _monitor;

        public CommunityCenterUnlock(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Mark the CC unlocked for the master player. Idempotent — safe to call repeatedly.</summary>
        public void Apply()
        {
            Farmer p = Game1.MasterPlayer;

            // Gate: events that trigger the cutscene chain must be pre-seen.
            p.eventsSeen.Add("191393"); // Demetrius visit → CC becomes accessible
            p.eventsSeen.Add("112");    // Wizard reveals Junimo text

            // Gate: door, bundle readability, and first-note intro.
            p.mailReceived.Add("ccDoorUnlock");      // unlocks the CC front door
            p.mailReceived.Add("canReadJunimoText"); // bundles show real names (not "???")
            p.mailReceived.Add("seenJunimoNote");    // suppresses the first-open intro flow

            _monitor.Log(
                "CommunityCenterUnlock: set eventsSeen {191393, 112} + mailReceived {ccDoorUnlock, canReadJunimoText, seenJunimoNote}. CC accessible from day 1.",
                LogLevel.Info);
        }
    }
}
