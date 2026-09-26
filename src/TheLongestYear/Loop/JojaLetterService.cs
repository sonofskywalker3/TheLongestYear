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
        /// arrived on day 9).</summary>
        public void OnDayStarted()
        {
            if (!RunActivation.IsActive || Game1.player == null) return;
            RunState run = _meta.Run;
            MetaState meta = _meta.State;
            if (run.JojaLetterDays == null || run.JojaLetterDays.Count == 0)
                run.JojaLetterDays = JojaOffer.PlanLetterDays(unchecked(run.Seed * 31 + 0x4A6F6A61));
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);

            int come = JojaOffer.ComeLetterDue(run, meta, today);
            if (come > 0)
            {
                Deliver(ComePrefix + come);
                run.JojaLettersSent = come;
                return;   // at most one Morris letter a morning
            }
            int decide = JojaOffer.DecisionLetterDue(run, meta, today);
            if (decide > 0)
            {
                Deliver(DecidePrefix + decide);
                run.JojaDecisionLettersSent = decide;
                if (decide == JojaOffer.DecisionLetters)
                {
                    JojaOffer.Reject(meta, run.RunNumber);
                    _monitor.Log($"Joja: the fourth decision letter went unanswered; rejected in loop {run.RunNumber}.", LogLevel.Info);
                }
            }
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
