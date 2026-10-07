using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>A strike scene's witness says one line about it, once, the next time the player
    /// talks to him within a week (spec 2026-09-21).</summary>
    internal sealed class WitnessDialogueService
    {
        private const string Marker = "TLY_witness";
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

        /// <summary>Day start: put each live line on top of its NPC's dialogue, drop the dead ones.</summary>
        public void OnDayStarted()
        {
            RunState run = _store.Run;
            if (run.WitnessLines == null || run.WitnessLines.Count == 0) return;
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);
            run.WitnessLines.RemoveAll(r => r.Said || today - r.SceneDayOfYear > WitnessLines.WindowDays);
            foreach (WitnessRecord r in run.WitnessLines)
            {
                if (!WitnessLines.IsLive(r, today)) continue;
                NPC npc = Game1.getCharacterFromName(r.Npc);
                if (npc == null) continue;
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
        }
    }
}
