using System;
using System.Linq;
using System.Text;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear.DebugCommands
{
    /// <summary><c>tly_festmem</c>: festival memory readouts and setters for live checks (deja-vu phase 2,
    /// spec "Debug commands"). Writes go to meta or the run log exactly as the rewind would.</summary>
    internal static class FestivalMemoryCommand
    {
        public const string Name = "tly_festmem";
        public const string Description =
            "Debug: festival memories (deja-vu phase 2). Usage: tly_festmem [status] | set <festival> <outcome> [item-or-npc] [score] | " +
            "partner <npc> [run] | recipient <npc> <item> <outcome> [run] | clear [festival|partners|recipients] | commit | force <festival|all|off>";

        public static void Run(IMonitor monitor, MetaState meta, Func<RunState> runProvider, GameplayConfig config, string[] args)
        {
            RunState run = runProvider?.Invoke();
            if (!Context.IsWorldReady || meta == null || run == null) { monitor.Log("Load a TLY save first.", LogLevel.Warn); return; }
            string sub = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
            int previousLoop = Math.Max(0, run.RunNumber - 1);
            try
            {
                switch (sub)
                {
                    case "status": Status(monitor, meta, run, config); break;
                    case "set" when args.Length >= 3:
                    {
                        string festival = args[1];
                        if (!FestivalIds.IsTracked(festival)) { monitor.Log($"{Name}: unknown festival '{festival}' (one of {string.Join(", ", FestivalIds.All)}).", LogLevel.Warn); return; }
                        string extra = args.Length >= 4 ? args[3] : "";
                        bool isItem = extra.StartsWith("(", StringComparison.Ordinal);
                        meta.FestivalMemories[festival] = new FestivalMemory
                        {
                            Festival = festival, AttendedRun = previousLoop, OutcomeRun = previousLoop, Outcome = args[2],
                            ItemId = isItem ? extra : "", Npc = isItem ? "" : extra,
                            Score = args.Length >= 5 && int.TryParse(args[4], out int score) ? score : 0,
                        };
                        monitor.Log($"{Name}: meta {festival} = {args[2]} {extra} (loop {previousLoop}, not heard).", LogLevel.Info);
                        break;
                    }
                    case "partner" when args.Length >= 2:
                        meta.DancePartners.Add(new BondMemory { Npc = args[1], Run = RunArg(args, 2, previousLoop), Outcome = FestivalOutcome.Danced });
                        monitor.Log($"{Name}: dance partner {args[1]} added ({meta.DancePartners.Count} kept).", LogLevel.Info);
                        break;
                    case "recipient" when args.Length >= 4:
                        meta.WinterStarRecipients.Add(new BondMemory { Npc = args[1], ItemId = args[2], Outcome = args[3], Run = RunArg(args, 4, previousLoop) });
                        monitor.Log($"{Name}: secret friend {args[1]} ({args[2]}, {args[3]}) added ({meta.WinterStarRecipients.Count} kept).", LogLevel.Info);
                        break;
                    case "clear":
                        Clear(monitor, meta, run, args.Length >= 2 ? args[1] : null);
                        break;
                    case "commit":
                        FestivalMemoryStore.Commit(meta, run);
                        run.FestivalLog.Clear();
                        run.FestivalMemoryHeard.Clear();
                        monitor.Log($"{Name}: this loop's log committed to meta (as at a rewind) and cleared.", LogLevel.Info);
                        break;
                    case "force" when args.Length >= 2:
                        FestivalMemoryContext.ForceFestival = args[1] == "off" ? null : args[1];
                        monitor.Log($"{Name}: force = {FestivalMemoryContext.ForceFestival ?? "off"} (every roll hits, budget ignored; speaker guards kept).", LogLevel.Info);
                        break;
                    default: monitor.Log(Description, LogLevel.Warn); break;
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"{Name} {sub}: threw {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            }
        }

        private static int RunArg(string[] args, int index, int fallback)
            => args.Length > index && int.TryParse(args[index], out int n) ? n : fallback;

        private static void Clear(IMonitor monitor, MetaState meta, RunState run, string what)
        {
            switch (what)
            {
                case null:
                    meta.FestivalMemories.Clear(); meta.DancePartners.Clear(); meta.WinterStarRecipients.Clear();
                    run.FestivalLog.Clear(); run.FestivalMemoryHeard.Clear();
                    break;
                case "partners": meta.DancePartners.Clear(); break;
                case "recipients": meta.WinterStarRecipients.Clear(); break;
                case "heard": run.FestivalMemoryHeard.Clear(); break;
                default: meta.FestivalMemories.Remove(what); run.FestivalMemoryHeard.Remove(what); break;
            }
            monitor.Log($"{Name}: cleared {what ?? "everything"}.", LogLevel.Info);
        }

        private static string Describe(FestivalMemory m)
            => $"attended={m.AttendedRun} outcome={(m.HasOutcome ? m.Outcome : "-")}@{m.OutcomeRun} item={m.ItemId} npc={m.Npc} score={m.Score} heard={m.HeardRun} memory={FestivalOutcomes.MemoryFor(m) ?? "-"}";

        private static void Status(IMonitor monitor, MetaState meta, RunState run, GameplayConfig config)
        {
            var sb = new StringBuilder();
            sb.Append($"{Name} status: active={FestivalMemoryRules.Active(config)} loop={run.RunNumber} chance={config.DejaVuFestivalChancePercent}% " +
                $"bond={config.DejaVuFestivalBondChancePercent}% eggGuarantee={FestivalMemoryStore.EggGuaranteeArmed(meta)} force={FestivalMemoryContext.ForceFestival ?? "off"}");
            sb.Append("\n meta:");
            foreach (var kv in meta.FestivalMemories.OrderBy(k => k.Key)) sb.Append($"\n  {kv.Key}: {Describe(kv.Value)}");
            sb.Append($"\n dance partners: [{string.Join(", ", meta.DancePartners.Select(b => $"{b.Npc}@{b.Run}"))}]");
            sb.Append($"\n secret friends: [{string.Join(", ", meta.WinterStarRecipients.Select(b => $"{b.Npc}@{b.Run}:{b.ItemId}:{b.Outcome}"))}]");
            sb.Append("\n this loop:");
            foreach (var kv in run.FestivalLog.OrderBy(k => k.Key)) sb.Append($"\n  {kv.Key}: {Describe(kv.Value)}");
            sb.Append($"\n heard this loop: [{string.Join(", ", run.FestivalMemoryHeard)}]");

            Event ev = Game1.CurrentEvent;
            string festival = FestivalMemoryContext.FestivalOf(ev);
            if (festival != null)
            {
                sb.Append($"\n at {festival} now:");
                foreach (FestivalActor a in FestivalMemoryContext.Actors(ev))
                {
                    int roll = FestivalMemoryRules.StableRoll(run, festival, FestivalMemoryRules.SaltFestivalDay, a.Name);
                    NPC npc = FestivalMemoryContext.Actor(ev, a.Name);
                    string carried = npc?.CurrentDialogue.FirstOrDefault(FestivalMemoryContext.IsMemory)?.getCurrentDialogue();
                    sb.Append($"\n  {a.Name} roll={roll}{(a.IsHost ? " host" : "")}{(a.CanDance ? " dancer" : "")}{(a.IsSecretFriend ? " secret-friend" : "")}" +
                        $"{(!a.CanSocialize ? " no-social" : "")}{(carried != null ? $" carries \"{carried}\"" : "")}");
                }
            }
            monitor.Log(sb.ToString(), LogLevel.Info);
        }
    }
}
