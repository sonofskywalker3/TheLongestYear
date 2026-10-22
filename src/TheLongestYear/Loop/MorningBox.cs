using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The darkness's morning messages in vanilla's corner message box (designer,
    /// 2026-10-08), the one "The spreading weeds have caused damage to the farm" uses
    /// (<c>Multiplayer.broadcastGlobalMessage</c> to <c>Game1.showGlobalMessage</c> to
    /// <c>HUDMessage.ForCornerTextbox</c>): big text, word-wrapped, no icon, gone on its own. The
    /// messages show one at a time, each after the last has faded, never stacked. None shows while
    /// anything holds the screen (a menu such as the planning hub or a level-up, a dialogue, an
    /// event, a strike scene, the Junimos' tamper scene, a fade); one already up when such a thing
    /// starts is taken down and shown again, in full, once it is over.</summary>
    internal static class MorningBox
    {
        private sealed class Entry
        {
            public string Text;
            /// <summary>Played the first time the box shows, then cleared so a re-show is silent.</summary>
            public string Sound;
        }

        private static readonly LinkedList<Entry> Waiting = new();
        private static HUDMessage _showing;
        private static Entry _showingEntry;

        /// <summary>Set by ModEntry: one of the mod's own scenes is running (the Junimos' tamper
        /// scene, a season turn), which vanilla's flags do not all show.</summary>
        internal static Func<bool> SceneRunning;

        internal static IMonitor Monitor;

        /// <summary>Messages not yet fully shown, the one on screen included.</summary>
        public static int Pending => Waiting.Count + (_showing != null ? 1 : 0);

        /// <summary>Queue a message. <paramref name="sound"/> plays when it first shows.</summary>
        public static void Enqueue(string text, string sound = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Waiting.AddLast(new Entry { Text = text, Sound = sound });
        }

        /// <summary>Forget everything waiting (day end, back to the title). A box on screen fades
        /// by itself.</summary>
        public static void Clear()
        {
            Waiting.Clear();
            _showing = null;
            _showingEntry = null;
        }

        /// <summary>Every tick: take the box down if something now holds the screen, else show the
        /// next one when the last has gone.</summary>
        public static void Tick()
        {
            if (!Context.IsWorldReady) return;
            if (_showing == null && Waiting.Count == 0) return;
            bool busy = Busy();
            if (_showing != null)
            {
                if (!Game1.hudMessages.Contains(_showing))
                {
                    _showing = null;
                    _showingEntry = null;
                }
                else if (busy)
                {
                    Game1.hudMessages.Remove(_showing);
                    Waiting.AddFirst(_showingEntry);
                    Monitor?.Log("Darkness: morning box taken down for something else on screen; it shows again after.", LogLevel.Trace);
                    _showing = null;
                    _showingEntry = null;
                    return;
                }
                else return;
            }
            if (busy || Waiting.Count == 0) return;
            Entry next = Waiting.First.Value;
            Waiting.RemoveFirst();
            HUDMessage box = HUDMessage.ForCornerTextbox(next.Text);
            box.timeLeft = MorningLines.BoxDurationMs(box.message.Split('\n').Length);
            Game1.addHUDMessage(box);
            _showing = box;
            _showingEntry = next;
            if (next.Sound != null) Game1.playSound(next.Sound);
            next.Sound = null;
            Monitor?.Log($"Darkness: morning box: {next.Text}", LogLevel.Trace);
        }

        private static bool Busy()
            => !Context.IsPlayerFree || Game1.eventUp || Game1.farmEvent != null || Game1.globalFade
               || Game1.fadeToBlack || Game1.activeClickableMenu != null || (SceneRunning?.Invoke() ?? false);
    }
}
