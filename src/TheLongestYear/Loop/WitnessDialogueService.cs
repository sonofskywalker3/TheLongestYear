using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>A strike scene's witness says one line about it, once, the next time the player
    /// talks to him within a week (spec 2026-09-21). On a morning when the player has not met him
    /// yet the line is held for a day, so vanilla's introduction plays alone
    /// (<see cref="WitnessLines.Decide"/>, designer 2026-10-09).</summary>
    internal sealed class WitnessDialogueService
    {
        /// <summary>The translation key every witness Dialogue carries.</summary>
        internal const string Marker = "TLY_witness";

        /// <summary>Vanilla's first-meeting dialogue event and dialogue key.</summary>
        private const string IntroductionKey = "Introduction";

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;

        public WitnessDialogueService(IMonitor monitor, MetaStore store) { _monitor = monitor; _store = store; }

        /// <summary>A scene just played (or was skipped): its witness, if it has one, owes a line.
        /// The game's own date has already rolled to N+1 when the overnight scene plays (live check,
        /// 2026-10-07), so the night is read from the run's calendar, which still says N.</summary>
        public void OnScenePlayed(DarknessEvent e)
        {
            string npc = WitnessLines.NpcFor(e);
            if (npc == null) return;
            RunState run = _store.Run;
            int day = Calendar.DayOfYear((int)run.Season, run.DayOfMonth);
            (run.WitnessLines ??= new()).Add(new WitnessRecord { Npc = npc, SceneDayOfYear = day });
            _monitor.Log($"Witness: {npc} saw the {e} scene on day {day}.", LogLevel.Debug);
        }

        /// <summary>Day start: put each live line on top of its NPC's dialogue, hold the ones whose
        /// NPC the player has not met yet, drop the dead ones.</summary>
        public void OnDayStarted()
        {
            RunState run = _store.Run;
            if (run.WitnessLines == null || run.WitnessLines.Count == 0) return;
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);
            run.WitnessLines.RemoveAll(r => Morning(r, today) == WitnessMorning.Drop);
        }

        /// <summary>One record's morning: queue it, hold it or let it go.</summary>
        private WitnessMorning Morning(WitnessRecord r, int today)
        {
            NPC npc = Game1.getCharacterFromName(r.Npc);
            WitnessMorning call = WitnessLines.Decide(r, today, notMetYet: npc != null && NotMetYet(npc));
            if (call == WitnessMorning.Say && npc != null) Queue(npc, r, today);
            else if (call == WitnessMorning.Hold)
                _monitor.Log($"Witness: {r.Npc} has not been met yet; his introduction plays alone today and his line waits for tomorrow (scene day {r.SceneDayOfYear}, today {today}).", LogLevel.Debug);
            return call;
        }

        /// <summary>Has the player not met him yet, so his next talk is vanilla's introduction? Never
        /// spoken to, or his Introduction still owed (vanilla plays it while the Introduction dialogue
        /// event is active and his "_Introduction" mail flag is missing).</summary>
        private static bool NotMetYet(NPC npc)
        {
            Farmer player = Game1.player;
            if (!player.friendshipData.ContainsKey(npc.Name)) return true;
            return player.activeDialogueEvents.ContainsKey(IntroductionKey)
                   && !player.mailReceived.Contains(npc.Name + "_" + IntroductionKey)
                   && npc.Dialogue != null && npc.Dialogue.ContainsKey(IntroductionKey);
        }

        /// <summary>Put one record's line on top of its NPC's dialogue stack.</summary>
        private void Queue(NPC npc, WitnessRecord r, int today)
        {
            string when = Strings.Get(WitnessLines.WhenKey(r.SceneDayOfYear, today));
            // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
            string text = r.Npc == "Linus"
                ? Strings.Get("dialogue.witness.linus", new Dictionary<string, string> { ["when"] = when })
                : Strings.Get("dialogue.witness.shane", new Dictionary<string, string> { ["when"] = when });
            WitnessRecord record = r;
            var dialogue = new Dialogue(npc, Marker, text) { onFinish = () => record.Said = true };
            npc.CurrentDialogue.Push(dialogue);
            _monitor.Log($"Witness: {r.Npc} will say his line (scene day {r.SceneDayOfYear}, today {today}).", LogLevel.Debug);
        }

        /// <summary>Debug (<c>tly_witness arm</c>): as if he saw last night's scene, with this
        /// morning's call made now (queued, or held when he has not been met).</summary>
        public string ArmForTest(string npcName)
        {
            if (npcName != "Linus" && npcName != "Shane") return null;
            RunState run = _store.Run;
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);
            var record = new WitnessRecord { Npc = npcName, SceneDayOfYear = today - 1 };
            (run.WitnessLines ??= new()).Add(record);
            return Morning(record, today).ToString();
        }
    }
}
