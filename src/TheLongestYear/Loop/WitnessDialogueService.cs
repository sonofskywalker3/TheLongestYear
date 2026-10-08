using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>A strike scene's witness says one line about it, once, the next time the player
    /// talks to him within a week (spec 2026-09-21). On a first meeting the line follows vanilla's
    /// introduction in the same conversation (<see cref="WitnessIntroPatch"/>, designer 2026-10-08).</summary>
    internal sealed class WitnessDialogueService
    {
        /// <summary>The translation key every witness Dialogue carries, so the patch can find it.</summary>
        internal const string Marker = "TLY_witness";
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;

        public WitnessDialogueService(IMonitor monitor, MetaStore store) { _monitor = monitor; _store = store; WitnessIntroPatch.Monitor = monitor; }

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

    /// <summary>Vanilla's first-meeting introduction (and any unseen conversation topic) clears the
    /// NPC's dialogue stack before pushing itself (<c>NPC.checkForNewCurrentDialogue</c>), which threw
    /// the witness line away: Linus introduced himself and said nothing about the crows (designer,
    /// 2026-10-08). The prefix notes the waiting line; when vanilla's pick cleared it or buried it
    /// under a location line, the postfix chains it after that dialogue, so the line plays straight
    /// after the introduction in the same conversation. The rule is
    /// <see cref="WitnessLines.FollowsTopic"/>.</summary>
    [HarmonyPatch(typeof(NPC), nameof(NPC.checkForNewCurrentDialogue))]
    internal static class WitnessIntroPatch
    {
        internal static IMonitor Monitor;

        /// <summary>How long after the topic dialogue closes the line opens: vanilla's own gap for a
        /// follow-up box (NPC.tryToReceiveActiveObject's quest items).</summary>
        private const int FollowUpDelayMs = 200;

        private static void Prefix(NPC __instance, out Dialogue __state)
        {
            __state = null;
            foreach (Dialogue d in __instance.CurrentDialogue)
                if (d?.TranslationKey == WitnessDialogueService.Marker) { __state = d; return; }
        }

        private static void Postfix(NPC __instance, bool __result, Dialogue __state)
        {
            Stack<Dialogue> stack = __instance.CurrentDialogue;
            bool onTop = stack.Count > 0 && ReferenceEquals(stack.Peek(), __state);
            if (!WitnessLines.FollowsTopic(__state != null, onTop, __result)) return;
            if (stack.Count == 0)
            {
                stack.Push(__state);
                return;
            }
            NPC npc = __instance;
            Dialogue line = __state;
            // Buried under a location line rather than cleared: lift it out, it follows instead.
            if (stack.Contains(line))
            {
                var rest = new List<Dialogue>(stack);
                rest.Remove(line);
                stack.Clear();
                for (int i = rest.Count - 1; i >= 0; i--) stack.Push(rest[i]);
            }
            Dialogue topic = stack.Peek();
            topic.onFinish += () => DelayedAction.functionAfterDelay(() => Speak(npc, line), FollowUpDelayMs);
            Monitor?.Log($"Witness: {npc.Name}'s line follows his {topic.TranslationKey} dialogue.", LogLevel.Debug);
        }

        /// <summary>Open the line now; if something else holds the screen, leave it on his stack for
        /// the next talk.</summary>
        private static void Speak(NPC npc, Dialogue line)
        {
            if (!npc.CurrentDialogue.Contains(line)) npc.CurrentDialogue.Push(line);
            if (Game1.activeClickableMenu == null && !Game1.eventUp && Game1.player.currentLocation == npc.currentLocation)
                Game1.drawDialogue(npc);
        }
    }
}
