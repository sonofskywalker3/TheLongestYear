using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Places festival memory lines (deja-vu phase 2, spec 2026-10-09 and the build plan's
    /// "Decisions"). Festival day: pushed on top of each winner's stack at set-up, so the memory plays on
    /// the first talk and the festival line on the second. Post-result: scripted as a speak command where
    /// the festival ends in a cutscene (egg hunt, Luau, ice fishing), pushed on Pierre's stack at the Fair,
    /// appended as a second page for dance.again and the Winter Star. One memory per festival per loop.</summary>
    internal static class FestivalMemoryInjector
    {
        private const string YesKey = "yes";
        private const string DanceAskKey = "danceAsk";
        private const string NullCommand = "null";
        private const string GlobalFadeCommand = "globalFade";

        private static void Log(string text, LogLevel level = LogLevel.Trace) => FestivalMemoryContext.Monitor?.Log(text, level);

        // ---- festival day ----------------------------------------------------------------

        /// <summary>Roll every actor once (stable for the loop) and push the winners' lines. A repeat
        /// visit after the contest pushes nothing: those lines are about the contest.</summary>
        public static void OnFestivalOpen(Event ev, string festival)
        {
            if (!FestivalMemoryContext.CanSpeak) return;
            RunState run = FestivalMemoryContext.Run;
            if (FestivalIds.WithContest.Contains(festival)
                && FestivalMainEvent.AlreadyPlayed(run, ev.id, Game1.Date?.TotalDays ?? -1)) return;
            bool force = FestivalMemoryContext.IsForced(festival);
            var planned = FestivalMemoryRules.PlanFestivalDay(FestivalMemoryContext.Meta, run, festival,
                FestivalMemoryContext.Actors(ev), FestivalMemoryContext.Config,
                npc => FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltFestivalDay, npc), force).ToList();
            var pushed = new List<string>();
            foreach (PlannedMemory p in planned)
            {
                string text = FestivalMemoryContext.Line(run, festival, p.Memory, p.Npc, false, p.ItemId);
                if (FestivalMemoryContext.Push(FestivalMemoryContext.Actor(ev, p.Npc), festival, text))
                    pushed.Add($"{p.Npc}:{p.Memory}{(p.Guaranteed ? "(guaranteed)" : "")}");
            }
            if (pushed.Count > 0 || force)
                Log($"Festival memories at {festival} (loop {run.RunNumber}{(force ? ", forced" : "")}): {pushed.Count} line(s) pushed [{string.Join(", ", pushed)}].", LogLevel.Info);
        }

        /// <summary>Heard: the top of the speaker's stack is a memory as the box opens (Game1.cs 9070).</summary>
        [HarmonyPatch(typeof(Game1), nameof(Game1.drawDialogue), new[] { typeof(NPC) })]
        internal static class HeardPatch
        {
            private static void Postfix(NPC speaker)
            {
                try
                {
                    if (speaker?.CurrentDialogue == null || speaker.CurrentDialogue.Count == 0) return;
                    Dialogue top = speaker.CurrentDialogue.Peek();
                    if (!FestivalMemoryContext.IsMemory(top) || !FestivalMemoryContext.CanRecord) return;
                    string festival = top.TranslationKey.Substring(FestivalMemoryContext.KeyPrefix.Length);
                    FestivalMemoryContext.Spend(Game1.CurrentEvent, festival, speaker, $"{speaker.Name} \"{top.getCurrentDialogue()}\"");
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory heard hook failed: {ex}", LogLevel.Error); }
            }
        }

        // ---- contest start, dance ----------------------------------------------------------

        /// <summary>The host's "yes" starts a contest: unheard "before" lines are pulled (not spent, so
        /// the post-result line can still play). "danceAsk": the dance memories.</summary>
        [HarmonyPatch(typeof(Event), nameof(Event.answerDialogueQuestion))]
        internal static class AnswerPatch
        {
            private static void Prefix(out bool __state) => __state = Game1.player?.dancePartner.Value == null;

            private static void Postfix(Event __instance, NPC who, string answerKey, bool __state)
            {
                try
                {
                    string festival = FestivalMemoryContext.FestivalOf(__instance);
                    if (festival == null || !FestivalMemoryContext.CanRecord) return;
                    if (answerKey == YesKey && FestivalIds.WithContest.Contains(festival))
                    {
                        int pulled = FestivalMemoryContext.Pull(__instance, festival);
                        if (pulled > 0) Log($"Festival memories at {festival}: contest started, {pulled} unheard line(s) pulled.");
                    }
                    else if (answerKey == DanceAskKey && festival == FestivalIds.FlowerDance && __state && who != null
                        && Game1.player.dancePartner.Value is NPC partner && partner.Name == who.Name
                        && Game1.player.spouse != who.Name)
                        OnNewDancePartner(__instance, who);
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory answer hook failed: {ex}", LogLevel.Error); }
            }
        }

        private static void OnNewDancePartner(Event ev, NPC partner)
        {
            if (!FestivalMemoryContext.CanSpeak) return;
            const string festival = FestivalIds.FlowerDance;
            RunState run = FestivalMemoryContext.Run;
            MetaState meta = FestivalMemoryContext.Meta;
            bool force = FestivalMemoryContext.IsForced(festival);

            // dance.again: the villager who just said yes danced with the player in an earlier loop.
            // Appended as a second page of the acceptance the open box is showing.
            if (FestivalMemoryRules.BondHit(meta, run, festival, partner.Name, FestivalMemoryContext.Config,
                    FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltBond, partner.Name), force)
                && partner.CurrentDialogue.Count > 0)
            {
                string text = FestivalMemoryContext.Line(run, festival, FestivalMemoryKeys.DanceAgain, partner.Name, false);
                if (text != null)
                {
                    partner.CurrentDialogue.Peek().dialogues.Add(new DialogueLine(text));
                    FestivalMemoryContext.Spend(ev, festival, partner, $"{partner.Name} dance.again \"{text}\"");
                }
            }

            // dance.other: every other past partner, now watching the player dance with someone else.
            var pushed = new List<string>();
            foreach (NPC npc in ev.actors)
            {
                if (npc == null || npc.Name == partner.Name || npc.Name == Game1.player.spouse) continue;
                if (!FestivalMemoryRules.BondHit(meta, run, festival, npc.Name, FestivalMemoryContext.Config,
                        FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltBond, npc.Name), force)) continue;
                string text = FestivalMemoryContext.Line(run, festival, FestivalMemoryKeys.DanceOther, npc.Name, false,
                    partnerName: partner.displayName);
                if (FestivalMemoryContext.Push(npc, festival, text)) pushed.Add(npc.Name);
            }
            if (pushed.Count > 0) Log($"Festival memories at {festival}: dance.other pushed for [{string.Join(", ", pushed)}].", LogLevel.Info);
        }

        /// <summary>An item in the Luau pot: "before the soup" lines are done (Event.cs 12726).</summary>
        [HarmonyPatch(typeof(Event), nameof(Event.addItemToLuauSoup))]
        internal static class LuauPotPatch
        {
            private static void Postfix(Event __instance)
            {
                try
                {
                    if (FestivalMemoryContext.FestivalOf(__instance) != FestivalIds.Luau || !FestivalMemoryContext.CanRecord) return;
                    int pulled = FestivalMemoryContext.Pull(__instance, FestivalIds.Luau);
                    if (pulled > 0) Log($"Festival memories at {FestivalIds.Luau}: item in the pot, {pulled} unheard line(s) pulled.");
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory Luau pot hook failed: {ex}", LogLevel.Error); }
            }
        }

        // ---- post-result -------------------------------------------------------------------

        /// <summary>The post-result line and who says it, or null.</summary>
        private static (NPC speaker, string text) PlanAfter(Event ev, string festival, string outcomeNow)
        {
            if (!FestivalMemoryContext.CanSpeak) return (null, null);
            RunState run = FestivalMemoryContext.Run;
            bool force = FestivalMemoryContext.IsForced(festival);
            string memory = FestivalMemoryRules.PlanAfter(FestivalMemoryContext.Meta, run, festival, outcomeNow,
                FestivalMemoryContext.Config, FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltAfter, festival), force);
            if (memory == null) return (null, null);
            foreach (string slug in FestivalMemoryLines.AfterSpeakers(memory, FestivalMemoryContext.Keys))
            {
                NPC speaker = FestivalMemoryContext.Actor(ev, slug);
                if (speaker == null) continue;
                string text = FestivalMemoryContext.Line(run, festival, memory, speaker.Name, true);
                if (text != null) return (speaker, text);
            }
            Log($"Festival memories at {festival}: {memory} after-line rolled but its speaker is not in the scene.");
            return (null, null);
        }

        /// <summary>Egg hunt and ice fishing: the winner is named by a cutscene command followed by
        /// "null", which waits for Lewis's box to close. The line goes right after it, before the
        /// AbbyWin / DickWin fork replaces the command list.</summary>
        public static void AfterScriptedResult(Event ev, string festival, string outcomeNow)
        {
            (NPC speaker, string text) = PlanAfter(ev, festival, outcomeNow);
            if (speaker == null) return;
            int at = ev.CurrentCommand + 1;
            for (int i = ev.CurrentCommand + 1; i < ev.eventCommands.Length; i++)
                if (ev.eventCommands[i] == NullCommand) { at = i + 1; break; }
            FestivalMemoryContext.InsertSpeak(ev, at, speaker, text);
            FestivalMemoryContext.Spend(ev, festival, speaker, $"{speaker.Name} after the result \"{text}\"");
        }

        private static Event _luauEvent;
        private static NPC _luauSpeaker;
        private static string _luauText;

        /// <summary>The Luau's result picks a reaction script through switchEvent; the line waits for it.</summary>
        public static void AfterLuau(Event ev, string outcomeNow)
        {
            (NPC speaker, string text) = PlanAfter(ev, FestivalIds.Luau, outcomeNow);
            _luauEvent = speaker == null ? null : ev;
            _luauSpeaker = speaker;
            _luauText = text;
        }

        /// <summary>Insert the queued Luau line before the reaction script's last globalFade, after
        /// Lewis's closing words.</summary>
        [HarmonyPatch(typeof(Event.DefaultCommands), nameof(Event.DefaultCommands.SwitchEvent))]
        internal static class SwitchEventPatch
        {
            private static void Postfix(Event @event)
            {
                try
                {
                    if (_luauEvent == null || !ReferenceEquals(@event, _luauEvent)) return;
                    Event ev = _luauEvent;
                    NPC speaker = _luauSpeaker;
                    string text = _luauText;
                    _luauEvent = null; _luauSpeaker = null; _luauText = null;
                    int at = Array.LastIndexOf(ev.eventCommands, GlobalFadeCommand);
                    if (at < 0) at = ev.eventCommands.Length;
                    FestivalMemoryContext.InsertSpeak(ev, at, speaker, text);
                    FestivalMemoryContext.Spend(ev, FestivalIds.Luau, speaker, $"{speaker.Name} after the tasting \"{text}\"");
                }
                catch (Exception ex) { FestivalMemoryContext.Monitor?.Log($"Festival memory Luau reaction hook failed: {ex}", LogLevel.Error); }
            }
        }

        /// <summary>Fair: free roam goes on after judging, so Pierre's line goes on his rebuilt stack.</summary>
        public static void AfterGrange(Event ev, string outcomeNow)
        {
            (NPC speaker, string text) = PlanAfter(ev, FestivalIds.Fair, outcomeNow);
            if (speaker == null) return;
            FestivalMemoryContext.Pull(ev, FestivalIds.Fair);
            if (FestivalMemoryContext.Push(speaker, FestivalIds.Fair, text))
                Log($"Festival memories at {FestivalIds.Fair}: {speaker.Name} carries the after-judging line.", LogLevel.Info);
        }

        /// <summary>Winter Star: this year's secret friend, given to in an earlier loop too, adds a second
        /// page to the thank-you line the open box is showing.</summary>
        public static void AfterSecretGift(Event ev, NPC recipient)
        {
            if (!FestivalMemoryContext.CanSpeak || recipient.CurrentDialogue.Count == 0) return;
            const string festival = FestivalIds.WinterStar;
            RunState run = FestivalMemoryContext.Run;
            if (!FestivalMemoryRules.BondHit(FestivalMemoryContext.Meta, run, festival, recipient.Name, FestivalMemoryContext.Config,
                    FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltAfter, recipient.Name),
                    FestivalMemoryContext.IsForced(festival))) return;
            string text = FestivalMemoryContext.Line(run, festival, FestivalMemoryKeys.WinterStarAgain, recipient.Name, true);
            if (text == null) return;
            recipient.CurrentDialogue.Peek().dialogues.Add(new DialogueLine(text));
            FestivalMemoryContext.Spend(ev, festival, recipient, $"{recipient.Name} winterstar.again \"{text}\"");
        }
    }
}
