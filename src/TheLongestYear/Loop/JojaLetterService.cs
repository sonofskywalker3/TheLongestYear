using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;

namespace TheLongestYear.Loop
{
    /// <summary>Morris's letters (spec 2026-09-25-joja-offer-design): up to eight "come see me"
    /// letters a loop until the player walks into JojaMart, then one "make a decision" letter a week
    /// for four weeks. The fourth takes the silence as a rejection. None ever again once rejected.</summary>
    internal sealed class JojaLetterService
    {
        public const string ComePrefix = "TLY_JojaCome", DecidePrefix = "TLY_JojaDecide";
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public JojaLetterService(IMonitor monitor, MetaStore meta) { _monitor = monitor; _meta = meta; }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo("Data/Mail")) return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                for (int i = 1; i <= JojaOffer.ComeLetters; i++)
                    data[ComePrefix + i] = Strings.Get($"mail.joja.come-{i}") + "[#]" + Strings.Get("mail.joja.come.title");
                for (int i = 1; i <= JojaOffer.DecisionLetters; i++)
                    data[DecidePrefix + i] = Strings.Get($"mail.joja.decide-{i}") + "[#]" + Strings.Get("mail.joja.decide.title");
            }, AssetEditPriority.Default);
        }

        /// <summary>Called from ModEntry's DayStarted, BEFORE RunController syncs Run.Season and
        /// Run.DayOfMonth to the new day, so today is read from the game's own date. Reading the run
        /// calendar here delivered every letter a day late (live check 2026-09-25: day 8 planned,
        /// arrived on day 9).
        ///
        /// <paramref name="rewindPending"/> is RunController.IsRewindChainRunning: on the morning a
        /// loop ends, DayStarted fires with the OLD loop's state before the rewind chain begins the
        /// new one, so no letter goes out (JojaOffer.MorningLetter owns that rule).</summary>
        public void OnDayStarted(bool rewindPending)
        {
            if (!RunActivation.IsActive || Game1.player == null) return;
            RunState run = _meta.Run;
            MetaState meta = _meta.State;
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);

            JojaLetter letter = JojaOffer.MorningLetter(run, meta, today, rewindPending);
            if (letter.Kind == JojaLetterKind.None) return;
            Deliver((letter.Kind == JojaLetterKind.Come ? ComePrefix : DecidePrefix) + letter.Number);
            JojaOffer.Record(run, meta, letter);
            if (letter.Rejects)
                _monitor.Log($"Joja: the fourth decision letter went unanswered; rejected in loop {run.RunNumber}.", LogLevel.Info);
        }

        /// <summary>True for any of Morris's letter keys.</summary>
        public static bool IsLetterKey(string key)
            => key != null && (key.StartsWith(ComePrefix, System.StringComparison.Ordinal)
                               || key.StartsWith(DecidePrefix, System.StringComparison.Ordinal));

        /// <summary>Called by the loop reset: an unread Morris letter from the old loop must not sit
        /// in the new loop's mailbox. Returns how many were removed.</summary>
        public static int PurgeFromMail(Farmer who)
        {
            if (who == null) return 0;
            int removed = 0;
            for (int i = who.mailbox.Count - 1; i >= 0; i--)
            {
                if (!IsLetterKey(who.mailbox[i])) continue;
                who.mailbox.RemoveAt(i);
                removed++;
            }
            foreach (string key in System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(who.mailForTomorrow, IsLetterKey)))
            {
                who.mailForTomorrow.Remove(key);
                removed++;
            }
            return removed;
        }

        private void Deliver(string key)
        {
            // mailReceived clears on a rewind, but be safe on a same-loop reload.
            Game1.player.mailReceived.Remove(key);
            if (!Game1.mailbox.Contains(key)) Game1.mailbox.Add(key);
            _monitor.Log($"Joja: letter '{key}' delivered.", LogLevel.Info);
        }
    }
}
