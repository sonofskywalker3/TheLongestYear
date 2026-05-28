using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Skips the vanilla CC unlock cutscene chain (Demetrius visit → Wizard reveal) so the
    /// Community Center is accessible from day 1 of every run, and pre-discovers every inner
    /// room's Junimo Note so all six bundle areas are donatable on Spring 1.
    ///
    /// v1 simplification: replace the cutscenes with nothing (silent unlock). Plan 06+ will
    /// add a proper narrative beat ("the Junimos enlist you on day 1") — for now, the door
    /// just opens and the bundles are readable immediately.
    ///
    /// Flags sourced from the 1.6 Android decompile:
    ///   - eventsSeen "112"           — Wizard-reveal event (WizardHouse); endBehaviors adds
    ///                                  canReadJunimoText (Event.cs:10788).
    ///   - mailReceived "ccDoorUnlock"     — GameLocation WarpCommunityCenter action gate (GameLocation.cs:9340).
    ///   - mailReceived "canReadJunimoText" — JunimoNoteMenu shows real bundle names vs "???" scramble
    ///                                        (JunimoNoteMenu.cs:390). GameMenu CC tab gate (GameMenu.cs:121).
    ///   - mailReceived "seenJunimoNote"   — Set by JunimoNoteMenu.setUpMenu when first opened; pre-setting
    ///                                        it suppresses the first-time Junimo-note intro flow.
    ///
    /// CC accessibility itself is handled by <see cref="CcLocationAccessiblePatch"/> — a Harmony
    /// patch on <c>Game1.isLocationAccessible</c> that returns true for "CommunityCenter". Earlier
    /// versions of this class added event 191393 to <c>MasterPlayer.eventsSeen</c> as a shortcut,
    /// but that flag also gates Joja's lightning-destruction <c>WorldChangeEvent(12)</c> + the
    /// destroyed-Joja visual + the Pierre Wednesday closure — all unwanted in v1.
    ///
    /// Per-room note visibility is overridden by <see cref="ShouldNoteAppearPatch"/>; after setting
    /// flags we trigger <c>MakeMapModifications</c> on the loaded CC location so any newly-visible
    /// notes appear immediately (otherwise the player would have to leave and re-enter the CC).
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

            // Gate: pre-mark only the Wizard-reveal event. We deliberately do NOT add 191393
            // (Demetrius visit) — see CcLocationAccessiblePatch for why; the lightning event +
            // destroyed-Joja visual + Pierre Wednesday closure all key off 191393, so we
            // satisfy the CC-accessibility check via a Harmony patch instead of the flag.
            p.eventsSeen.Add("112");    // Wizard reveals Junimo text

            // Gate: door, bundle readability, and first-note intro.
            p.mailReceived.Add("ccDoorUnlock");      // unlocks the CC front door
            p.mailReceived.Add("canReadJunimoText"); // bundles show real names (not "???")
            p.mailReceived.Add("seenJunimoNote");    // suppresses the first-open intro flow

            // Block the stale Wizard letter. JunimoNoteMenu.setUpMenu queues
            // "wizardJunimoNote" for tomorrow's mail the first time the player opens a CC
            // note, unless it's already in mailReceived. In TLY the player can be donating
            // on day 1 — the letter ("come see me, I sense the Junimos") arrives days
            // later, well after they've already met the Junimos. Pre-marking it as received
            // suppresses the addMailForTomorrow call entirely; the Wizard's tower also
            // becomes accessible immediately (Forest.isWizardHouseUnlocked keys off this).
            p.mailReceived.Add("wizardJunimoNote");

            // Place all six Junimo Notes if the CC location is already loaded. (On a fresh
            // SaveLoaded this is always the case — Game1.locations is populated by then.)
            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc != null && cc.Map != null)
            {
                // Defensive: a prior buggy reset (round-3 playtest) may have left
                // netWorldState.Value.Bundles with missing keys, which crashes
                // JunimoNoteMenu.setUpMenu on '[0]'. SetBundleData is idempotent for existing
                // keys and ADDS any missing ones — safe to call on every load.
                Game1.netWorldState.Value.SetBundleData(Game1.netWorldState.Value.BundleData);

                // ShouldNoteAppearPatch makes shouldNoteAppearInArea return true for every
                // incomplete area in [0, 5]; MakeMapModifications iterates areas and calls
                // addJunimoNote for any that don't already have one (CommunityCenter.cs:614).
                cc.MakeMapModifications(force: true);
            }

            // Diagnostic for the 2026-05-26 'CC marked complete on day 2' bug. If any
            // areasComplete[i] is true on a fresh save load, that's the state vanilla is
            // reading when it fires the Junimo-returns cutscene + lightning strike. Logging
            // both the per-area flags AND the related mail / event-completion flags so we can
            // diagnose without another playtest round.
            if (cc != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < cc.areasComplete.Count; i++)
                    sb.Append(cc.areasComplete[i] ? "T" : "F");
                bool hasCcIsComplete = p.mailReceived.Contains("ccIsComplete");
                bool hasJojaMember = p.mailReceived.Contains("JojaMember");
                bool vanillaCcDone = p.hasCompletedCommunityCenter();
                _monitor.Log(
                    $"CC state on load: areasComplete=[{sb}], " +
                    $"ccIsComplete={hasCcIsComplete}, JojaMember={hasJojaMember}, " +
                    $"hasCompletedCommunityCenter={vanillaCcDone}, " +
                    $"numberOfCompleteBundles={cc.numberOfCompleteBundles()}, " +
                    $"areAllAreasComplete={cc.areAllAreasComplete()}.",
                    LogLevel.Info);
            }

            _monitor.Log(
                "CommunityCenterUnlock: door + canReadJunimoText set, all 6 Junimo Notes revealed.",
                LogLevel.Info);
        }
    }
}
