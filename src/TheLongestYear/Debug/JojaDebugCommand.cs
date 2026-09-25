using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;
using TheLongestYear.Loop;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug: Morris's offer letters (spec 2026-09-25-joja-offer-design). status prints
    /// every Joja field plus the Morris line; letter/seen/unseen/reject/unreject poke the state
    /// directly so the letter cadence and rejection path can be exercised without waiting real days.</summary>
    internal static class JojaDebugCommand
    {
        public const string Name = "tly_joja";
        public const string Description =
            "Debug: Morris's offer. Usage: tly_joja status | letter come <n> | letter decide <n> | seen | unseen | reject | unreject | counter | cashier | scene";

        private const int MinArgsLetter = 3;

        public static void Run(IMonitor m, MetaStore meta, string[] args, Integration.JojaOfferDriver offerDriver = null)
        {
            if (!Context.IsWorldReady) { m.Log("Load a save first.", LogLevel.Warn); return; }
            if (meta == null || args.Length < 1) { m.Log(Description, LogLevel.Warn); return; }

            RunState run = meta.Run;
            MetaState state = meta.State;

            switch (args[0].ToLowerInvariant())
            {
                case "status":
                    Status(m, run, state);
                    break;
                case "letter" when args.Length >= MinArgsLetter && int.TryParse(args[2], out int n):
                    Letter(m, args[1].ToLowerInvariant(), n);
                    break;
                case "seen":
                    JojaOffer.MarkSceneSeen(run, state, Calendar.DayOfYear((int)run.Season, run.DayOfMonth));
                    m.Log("tly_joja: scene marked seen today.", LogLevel.Info);
                    break;
                case "unseen":
                    run.JojaSceneSeenDay = -1;
                    m.Log("tly_joja: JojaSceneSeenDay reset to -1.", LogLevel.Info);
                    break;
                case "reject":
                    JojaOffer.Reject(state, run.RunNumber);
                    m.Log($"tly_joja: rejected in loop {state.JojaRejectedLoop}.", LogLevel.Info);
                    break;
                case "unreject":
                    state.JojaRejectedLoop = 0;
                    m.Log("tly_joja: JojaRejectedLoop reset to 0.", LogLevel.Info);
                    break;
                case "counter":
                    JojaCounterPatch.DebugCounter();
                    break;
                case "cashier":
                    JojaCounterPatch.DebugCashier();
                    break;
                case "scene" when offerDriver != null:
                    offerDriver.DebugReplay();
                    break;
                default:
                    m.Log(Description, LogLevel.Warn);
                    break;
            }
        }

        private static void Letter(IMonitor m, string which, int n)
        {
            string key = which switch
            {
                "come" => JojaLetterService.ComePrefix + n,
                "decide" => JojaLetterService.DecidePrefix + n,
                _ => null,
            };
            if (key == null) { m.Log(Description, LogLevel.Warn); return; }
            Game1.player.mailReceived.Remove(key);
            if (!Game1.mailbox.Contains(key)) Game1.mailbox.Add(key);
            m.Log($"tly_joja: letter '{key}' added to mailbox.", LogLevel.Info);
        }

        private static void Status(IMonitor m, RunState run, MetaState state)
        {
            m.Log(
                $"tly_joja: JojaOfferEverSeen={state.JojaOfferEverSeen} JojaRejectedLoop={state.JojaRejectedLoop} " +
                $"JojaSceneSeenDay={run.JojaSceneSeenDay} JojaLettersSent={run.JojaLettersSent} " +
                $"JojaDecisionLettersSent={run.JojaDecisionLettersSent} " +
                $"JojaLetterDays=[{string.Join(", ", run.JojaLetterDays ?? new List<int>())}] " +
                $"MorrisLine={JojaOffer.MorrisLine(run, state, run.RunNumber)}",
                LogLevel.Info);
        }
    }
}
