using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Adds the one-time Spring-1 onboarding letter that explains the loop, JP scaling, the
    /// "wait for the weekly theme before donating" mechanic, and that festivals intentionally
    /// give your day back. Sent only on the FIRST loop (MetaStore.State.CompletedResets == 0)
    /// and only on a real TLY run (gated by the caller on RunActivation.IsActive).
    /// </summary>
    internal sealed class OnboardingMailService
    {
        public const string MailKey = "TLY_Intro";
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public OnboardingMailService(IMonitor monitor, MetaStore meta)
        {
            _monitor = monitor;
            _meta = meta;
        }

        /// <summary>Edit Data/Mail to register the letter body. Hooked from ModEntry.Entry.</summary>
        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
                return;

            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                // Mail format: body text, ^ = line break, [#] separates the letter title.
                data[MailKey] =
                    "Welcome to The Longest Year!^^"
                    + "You're stuck looping Year 1 until you finish the Community Center. "
                    + "Each loop you spend Junimo Points (JP) on permanent upgrades.^^"
                    + "Two things that trip everyone up:^"
                    + "1) You earn a LOT more JP the deeper into the year you get, so don't feel behind early.^"
                    + "2) DON'T rush to complete bundles! Hold an item until its weekly theme comes up, then donate it "
                    + "- you bank a 1.5x JP bonus AND clear that week's downside. Rushing donations wastes both.^^"
                    + "Also: festivals give your day back on purpose. You can leave and come back, and leaving early "
                    + "keeps your clock - returning 'early' is intended, not a bug.^^"
                    + "Good luck. The Junimos are counting on you."
                    + "[#]The Longest Year";
            }, AssetEditPriority.Default);
        }

        /// <summary>Deliver the letter on Spring 1 of the first loop. Idempotent:
        /// CompletedResets == 0 is the durable first-loop gate (it lives in MetaState and
        /// survives the reset, which clears mailReceived); the mailReceived/mailbox checks
        /// guard same-loop double-delivery (e.g. re-entering Spring 1 after a save-reload).</summary>
        public void OnDayStarted()
        {
            Farmer p = Game1.player;
            if (p == null) return;
            if (_meta.State.CompletedResets != 0) return;          // first loop only
            if (Game1.season != Season.Spring || Game1.dayOfMonth != 1) return;
            if (p.mailReceived.Contains(MailKey)) return;           // already got it
            if (Game1.mailbox.Contains(MailKey)) return;            // already queued today

            Game1.mailbox.Add(MailKey);
            _monitor.Log($"Onboarding letter '{MailKey}' delivered (Spring 1, first loop).", LogLevel.Info);
        }
    }
}
