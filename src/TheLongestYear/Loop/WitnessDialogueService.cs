using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
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
            WitnessIntroPatch.Forget();
            RunState run = _store.Run;
            if (run.WitnessLines == null || run.WitnessLines.Count == 0) return;
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);
            run.WitnessLines.RemoveAll(r => r.Said || today - r.SceneDayOfYear > WitnessLines.WindowDays);
            foreach (WitnessRecord r in run.WitnessLines)
                if (WitnessLines.IsLive(r, today)) Queue(r, today);
        }

        /// <summary>Put one record's line on top of its NPC's dialogue stack.</summary>
        private void Queue(WitnessRecord r, int today)
        {
            NPC npc = Game1.getCharacterFromName(r.Npc);
            if (npc == null) return;
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

        /// <summary>Debug (<c>tly_witness arm</c>): as if he saw last night's scene, his line queued now.</summary>
        public bool ArmForTest(string npcName)
        {
            if (npcName != "Linus" && npcName != "Shane") return false;
            RunState run = _store.Run;
            int today = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);
            var record = new WitnessRecord { Npc = npcName, SceneDayOfYear = today - 1 };
            (run.WitnessLines ??= new()).Add(record);
            Queue(record, today);
            return true;
        }
    }

    /// <summary>Vanilla's first-meeting introduction (and any unseen conversation topic) clears the
    /// NPC's dialogue stack before pushing itself (<c>NPC.checkForNewCurrentDialogue</c>), which threw
    /// the witness line away: Linus introduced himself and said nothing about the crows (designer,
    /// 2026-10-08). The prefix notes the waiting line; when vanilla's pick cleared it or buried it
    /// under a location line, the postfix puts it straight UNDER that dialogue on his stack
    /// (<see cref="WitnessLines.PlaceUnderTop{T}"/>), so vanilla's own close pops the topic and
    /// leaves the line next. When the topic's box has closed, <see cref="OnMenuChanged"/> opens the
    /// line at once, so it plays in the same conversation; if it cannot, the line simply waits for
    /// the next talk.
    ///
    /// The first fix (d5f9e4f) opened the line from the topic's <c>onFinish</c> after 200 ms. That
    /// fires on the last page while the box is still animating out; the line was pushed on top of
    /// the not-yet-popped topic and <c>DialogueBox.closeDialogue</c> popped it (it pops the top of
    /// the speaker's stack), so the line vanished: logged, never shown.</summary>
    [HarmonyPatch(typeof(NPC), nameof(NPC.checkForNewCurrentDialogue))]
    internal static class WitnessIntroPatch
    {
        internal static IMonitor Monitor;

        /// <summary>The conversation waiting for its topic box to close: who, the topic, the line.</summary>
        private static (NPC Npc, Dialogue Topic, Dialogue Line)? _pending;

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
            // Top first, as the stack enumerates; rebuilt bottom up.
            List<Dialogue> order = WitnessLines.PlaceUnderTop(new List<Dialogue>(stack), __state);
            stack.Clear();
            for (int i = order.Count - 1; i >= 0; i--) stack.Push(order[i]);
            Dialogue topic = stack.Peek();
            if (ReferenceEquals(topic, __state)) return;
            _pending = (__instance, topic, __state);
            Monitor?.Log($"Witness: {__instance.Name}'s line waits under his {topic.TranslationKey} dialogue, to open when it closes.", LogLevel.Debug);
        }

        /// <summary>The topic's box closed: open the line now, in the same conversation. SMAPI raises
        /// this after <c>DialogueBox.closeDialogue</c> has run, so the topic is already popped and the
        /// farmer is free again.</summary>
        internal static void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (_pending is not { } p) return;
            if (e.OldMenu is not DialogueBox closed || !ReferenceEquals(closed.characterDialogue, p.Topic)) return;
            _pending = null;
            Stack<Dialogue> stack = p.Npc.CurrentDialogue;
            bool lineIsNext = stack.Count > 0 && ReferenceEquals(stack.Peek(), p.Line);
            bool open = WitnessLines.OpensAfterTopic(
                closedBoxWasTopic: true,
                screenIsFree: e.NewMenu == null && Game1.activeClickableMenu == null,
                lineIsNext: lineIsNext,
                eventUp: Game1.eventUp,
                sameLocation: Game1.player.currentLocation == p.Npc.currentLocation);
            if (open) Game1.drawDialogue(p.Npc);
            Monitor?.Log(open
                ? $"Witness: {p.Npc.Name}'s line opens after his {p.Topic.TranslationKey} dialogue."
                : $"Witness: {p.Npc.Name}'s line waits for the next talk (next={lineIsNext}, menu={e.NewMenu?.GetType().Name ?? "none"}, event={Game1.eventUp}).",
                LogLevel.Debug);
        }

        /// <summary>A new day or the title screen: nothing is mid-conversation any more.</summary>
        internal static void Forget() => _pending = null;
    }
}
