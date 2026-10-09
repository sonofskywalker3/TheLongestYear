using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Shared state and helpers for the festival memory glue (deja-vu phase 2, spec
    /// 2026-10-09): the recorder writes what happened, the injector places the lines. Every entry point
    /// checks <see cref="CanRecord"/> or <see cref="CanSpeak"/>, so the whole feature is dormant on a
    /// non-TLY save, in multiplayer and when switched off.</summary>
    internal static class FestivalMemoryContext
    {
        /// <summary>Dialogue translation key for a pushed memory; the festival id follows the colon so
        /// the heard stamp knows which festival it spends.</summary>
        public const string KeyPrefix = "TLY.festmem:";

        /// <summary>Debug (<c>tly_festmem force</c>): a festival id, or "all", whose rolls all hit and
        /// whose budget is ignored. Null = off.</summary>
        public static string ForceFestival;
        public const string ForceAll = "all";

        public static MetaState Meta { get; private set; }
        public static GameplayConfig Config { get; private set; }
        public static IMonitor Monitor { get; private set; }
        private static Func<RunState> _run;
        private static Func<IReadOnlyCollection<string>> _keys;

        public static void Connect(MetaState meta, Func<RunState> run, GameplayConfig config, IMonitor monitor,
            Func<IReadOnlyCollection<string>> translationKeys)
        {
            Meta = meta; _run = run; Config = config; Monitor = monitor; _keys = translationKeys;
        }

        public static RunState Run => _run?.Invoke();
        public static IReadOnlyCollection<string> Keys => _keys?.Invoke() ?? Array.Empty<string>();

        public static bool IsForced(string festival)
            => ForceFestival != null && (ForceFestival == ForceAll || ForceFestival == festival);

        /// <summary>Record outcomes: a live TLY run, the feature switch on, single player.</summary>
        public static bool CanRecord
            => Meta != null && Run != null && RunActivation.IsActive && Config != null
            && Config.EnableDejaVuFestivalMemories && !Game1.IsMultiplayer;

        /// <summary>Speak memories: as <see cref="CanRecord"/>, plus phase 1 on.</summary>
        public static bool CanSpeak => CanRecord && FestivalMemoryRules.Active(Config);

        /// <summary>The tracked festival this event is, or null.</summary>
        public static string FestivalOf(Event ev)
        {
            if (ev == null || !ev.isFestival) return null;
            string id = FestivalIds.FromEventId(ev.id);
            return FestivalIds.IsTracked(id) ? id : null;
        }

        private static readonly AccessTools.FieldRef<Event, NPC> HostRef = AccessTools.FieldRefAccess<Event, NPC>("festivalHost");

        public static NPC Host(Event ev)
        {
            try { return HostRef(ev); }
            catch (Exception) { return null; }
        }

        /// <summary>The event's actors as the rules see them.</summary>
        public static List<FestivalActor> Actors(Event ev)
        {
            NPC host = Host(ev);
            string spouse = Game1.player?.spouse;
            string secretFriend = ev.secretSantaRecipient?.Name;
            var list = new List<FestivalActor>();
            foreach (NPC npc in ev.actors)
            {
                if (npc == null) continue;
                bool canDance = npc.GetData()?.FlowerDanceCanDance ?? npc.datable.Value;
                list.Add(new FestivalActor(npc.Name,
                    IsHost: host != null && ReferenceEquals(host, npc),
                    IsSpouse: spouse != null && spouse == npc.Name,
                    IsChild: npc is Child,
                    CanSocialize: npc.IsVillager && npc.CanSocialize,
                    CanDance: canDance,
                    IsSecretFriend: secretFriend != null && secretFriend == npc.Name));
            }
            return list;
        }

        public static NPC Actor(Event ev, string name)
            => ev?.actors?.FirstOrDefault(a => a != null && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        public static string ItemName(string qualifiedId)
        {
            if (string.IsNullOrEmpty(qualifiedId)) return null;
            try { return ItemRegistry.GetData(qualifiedId)?.DisplayName; }
            catch (Exception) { return null; }
        }

        /// <summary>Pick a line for this villager, stable for the loop.</summary>
        public static string Line(RunState run, string festival, string memory, string npc, bool after,
            string itemId = null, string partnerName = null)
            => FestivalMemoryLines.Pick(memory, npc, after, Keys,
                size => FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltLine, npc) % size,
                ItemName(itemId), partnerName);

        public static bool IsMemory(Dialogue d) => d?.TranslationKey != null && d.TranslationKey.StartsWith(KeyPrefix, StringComparison.Ordinal);

        public static bool IsMemoryOf(Dialogue d, string festival) => d?.TranslationKey == KeyPrefix + festival;

        /// <summary>Add a page to the dialogue on top of the villager's stack (the one the open box is
        /// showing). The old last line is marked as continued, or the box closes after it and the new page
        /// is never shown (DialoguePaging).</summary>
        public static void AppendPage(NPC npc, string text)
        {
            Dialogue d = npc.CurrentDialogue.Peek();
            int marker = DialoguePaging.MarkerIndex(d.currentDialogueIndex, d.dialogues.Count);
            if (marker == DialoguePaging.SetLiveFlag) d.isCurrentStringContinuedOnNextScreen = true;
            else d.dialogues[marker].Text += DialoguePaging.ContinueMarker;
            d.dialogues.Add(new DialogueLine(text));
        }

        /// <summary>Put a memory on top of the villager's stack, so it plays on the next talk and the
        /// festival line after it. One memory per villager.</summary>
        public static bool Push(NPC npc, string festival, string text)
        {
            if (npc == null || string.IsNullOrEmpty(text)) return false;
            if (npc.CurrentDialogue.Any(IsMemory)) return false;
            npc.CurrentDialogue.Push(new Dialogue(npc, KeyPrefix + festival, text));
            return true;
        }

        /// <summary>Remove every unheard memory of this festival from every actor. With
        /// <paramref name="speaking"/>, that villager's top line (the one on screen) is left alone.</summary>
        public static int Pull(Event ev, string festival, NPC speaking = null)
        {
            int removed = 0;
            if (ev?.actors == null) return 0;
            foreach (NPC npc in ev.actors)
            {
                if (npc?.CurrentDialogue == null || !npc.CurrentDialogue.Any(d => IsMemoryOf(d, festival))) continue;
                Dialogue[] items = npc.CurrentDialogue.ToArray();     // top first
                var kept = new List<Dialogue>();
                for (int i = 0; i < items.Length; i++)
                {
                    bool onScreen = i == 0 && ReferenceEquals(npc, speaking);
                    if (!onScreen && IsMemoryOf(items[i], festival)) { removed++; continue; }
                    kept.Add(items[i]);
                }
                npc.CurrentDialogue.Clear();
                for (int i = kept.Count - 1; i >= 0; i--) npc.CurrentDialogue.Push(kept[i]);
            }
            return removed;
        }

        /// <summary>A memory of this festival was heard: spend the festival and pull the rest.</summary>
        public static void Spend(Event ev, string festival, NPC speaking, string what)
        {
            RunState run = Run;
            if (run == null) return;
            bool first = FestivalMemoryStore.MarkHeard(run, festival);
            int pulled = Pull(ev, festival, speaking);
            if (first)
                Monitor?.Log($"Festival memory heard: {what} at {festival}; festival spent for loop {run.RunNumber} ({pulled} other line(s) dropped).", LogLevel.Info);
        }

        /// <summary>Insert a speak command into the event script at <paramref name="index"/>. Quotes are
        /// stripped from the text because the command is quote-delimited.</summary>
        public static void InsertSpeak(Event ev, int index, NPC speaker, string text)
        {
            string command = $"speak {speaker.Name} \"{text.Replace("\"", "")}\"";
            List<string> commands = ev.eventCommands.ToList();
            index = Math.Clamp(index, 0, commands.Count);
            commands.Insert(index, command);
            ev.eventCommands = commands.ToArray();
        }
    }
}
