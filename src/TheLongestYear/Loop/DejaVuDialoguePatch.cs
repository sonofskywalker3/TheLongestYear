using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Deja-vu villager dialogue (spec 2026-08-27). Postfix on the method NPC.checkAction
    /// calls to choose today's line (NPC.cs:2832 -> 4009). After vanilla has chosen, and only on the
    /// LAST call it makes for this talk (noPreface, or the first call already succeeded), push a rare
    /// deja-vu line on top of CurrentDialogue so it plays first and the villager's own line follows.
    /// Never when the top line came from Farmer.activeDialogueEvents (the Introduction line the
    /// 0.16.8 fix re-seeds), never on festival days, never for a spouse.</summary>
    [HarmonyPatch(typeof(NPC), nameof(NPC.checkForNewCurrentDialogue))]
    internal static class DejaVuDialoguePatch
    {
        /// <summary>Mirrors GameplayConfig.EnableDejaVuDialogue; set by ModEntry at load and from GMCM.</summary>
        public static bool Enabled = true;

        /// <summary>Debug: the next talk with this villager injects regardless of chance and caps.</summary>
        public static string ForceNext;

        private const string TranslationKey = "TLY.dejavu";

        private static MetaState _meta;
        private static Func<RunState> _run;
        private static GameplayConfig _config;
        private static IMonitor _monitor;
        private static Func<IReadOnlyCollection<string>> _keys;

        public static void Connect(MetaState meta, Func<RunState> run, GameplayConfig config, IMonitor monitor,
            Func<IReadOnlyCollection<string>> translationKeys)
        {
            _meta = meta; _run = run; _config = config; _monitor = monitor; _keys = translationKeys;
        }

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(NPC __instance, bool noPreface, bool __result)
        {
            try
            {
                if (!Enabled || _meta == null || !RunActivation.IsActive) return;
                if (!(noPreface || __result)) return;                 // wait for checkAction's last call
                string npc = __instance.Name;
                bool force = ForceNext != null && ForceNext == npc;
                if (__instance.CurrentDialogue.Count == 0)             // nothing is about to play
                {
                    if (force) _monitor.Log($"Deja-vu: {npc} has no line to prepend to (empty dialogue stack, vanilla result={__result}).", LogLevel.Trace);
                    return;
                }
                if (Game1.isFestival()) return;
                if (__instance.getSpouse() == Game1.player) return;

                Dialogue top = __instance.CurrentDialogue.Peek();
                string tk = top?.TranslationKey ?? "";
                if (tk == TranslationKey) return;                      // already injected this talk
                foreach (string key in Game1.player.activeDialogueEvents.Keys)
                {
                    if (key.Length > 0 && tk.EndsWith(":" + key, StringComparison.Ordinal))
                    {
                        if (force) _monitor.Log($"Deja-vu: {npc} is playing the '{key}' event line; not touching it.", LogLevel.Trace);
                        return;
                    }
                }
                RunState run = _run();
                int daysPlayed = (int)Game1.stats.DaysPlayed;
                int tier = DejaVuRules.TryPick(_meta, run, npc, daysPlayed, _config,
                    max => Game1.random.Next(max), force);
                if (tier == 0) return;
                if (force) ForceNext = null;

                string text = DejaVuLines.Pick(npc, tier, _keys(), size => Game1.random.Next(size));
                if (text == null) return;
                __instance.CurrentDialogue.Push(new Dialogue(__instance, TranslationKey, text));
                _monitor.Log($"Deja-vu: {npc} tier {tier} on day {daysPlayed}{(force ? " (forced)" : "")}.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"DejaVuDialoguePatch failed for {__instance?.Name}: {ex}", LogLevel.Error);
            }
        }
    }
}
