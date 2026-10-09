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
            "partner <npc> [run] | recipient <npc> <item> <outcome> [run] | clear [festival|partners|recipients|heard] | commit | force <festival|all|off> | talk <npc> | start | click | pot | give <item> [quality] | score <n>";

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
                    case "talk" when args.Length >= 2: Talk(monitor, args[1]); break;
                    case "start": Start(monitor); break;
                    case "click": Click(monitor); break;
                    case "pot": Pot(monitor); break;
                    case "give" when args.Length >= 2:
                        Give(monitor, args[1], args.Length >= 3 && int.TryParse(args[2], out int quality) ? quality : 0);
                        break;
                    case "score" when args.Length >= 2 && int.TryParse(args[1], out int festivalScore):
                        Game1.player.festivalScore = festivalScore;
                        monitor.Log($"{Name} score: festivalScore = {festivalScore} (eggs or fish found).", LogLevel.Info);
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

        /// <summary>Talk to a festival actor through the festival's own checkAction (Event.cs 11631), the
        /// path a click on the villager takes, so the heard stamp runs as in play.</summary>
        private static void Talk(IMonitor monitor, string name)
        {
            Event ev = Game1.CurrentEvent;
            NPC npc = FestivalMemoryContext.Actor(ev, name);
            if (ev == null || !ev.isFestival || npc == null) { monitor.Log($"{Name} talk: no festival actor '{name}'.", LogLevel.Warn); return; }
            string top = npc.CurrentDialogue.Count > 0 ? npc.CurrentDialogue.Peek().TranslationKey : "(empty)";
            bool handled = ev.checkAction(new xTile.Dimensions.Location(npc.TilePoint.X, npc.TilePoint.Y), Game1.viewport, Game1.player);
            string shown = (Game1.activeClickableMenu as StardewValley.Menus.DialogueBox)?.getCurrentString();
            monitor.Log($"{Name} talk {npc.Name}: handled={handled} top was {top}; box shows \"{shown ?? "-"}\".", LogLevel.Info);
        }

        /// <summary>The host's "yes": start the festival's main event (egg hunt, soup, dance...).</summary>
        private static void Start(IMonitor monitor)
        {
            Event ev = Game1.CurrentEvent;
            NPC host = ev == null ? null : FestivalMemoryContext.Host(ev);
            if (ev == null || !ev.isFestival || host == null) { monitor.Log($"{Name} start: no festival host here.", LogLevel.Warn); return; }
            ev.answerDialogueQuestion(host, "yes");
            monitor.Log($"{Name} start: answered yes to {host.Name} at {ev.id}.", LogLevel.Info);
        }

        /// <summary>One left click on the open dialogue box: finishes the typing, or turns the page.</summary>
        private static void Click(IMonitor monitor)
        {
            if (Game1.activeClickableMenu is StardewValley.Menus.DialogueBox box)
            {
                string before = box.getCurrentString();
                box.receiveLeftClick(box.xPositionOnScreen + 10, box.yPositionOnScreen + 10);
                monitor.Log($"{Name} click: \"{before}\"", LogLevel.Info);
            }
            else monitor.Log($"{Name} click: no dialogue box (menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}, command {Game1.CurrentEvent?.CurrentCommand}: {CurrentCommand()}).", LogLevel.Info);
        }

        private const string LuauSoupAction = "LuauSoup";

        /// <summary>Act on the Luau soup pot tile (the festival map's "Action LuauSoup"), the path a click on
        /// the pot takes; vanilla opens its ingredient menu (Event.cs 11915).</summary>
        private static void Pot(IMonitor monitor)
        {
            Event ev = Game1.CurrentEvent;
            GameLocation here = Game1.currentLocation;
            var layer = here?.map?.GetLayer("Buildings");
            if (ev == null || !ev.isFestival || layer == null) { monitor.Log($"{Name} pot: not at a festival.", LogLevel.Warn); return; }
            for (int x = 0; x < layer.LayerWidth; x++)
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    string action = here.doesTileHaveProperty(x, y, "Action", "Buildings");
                    if (action == null || !action.StartsWith(LuauSoupAction, StringComparison.Ordinal)) continue;
                    bool handled = ev.checkAction(new xTile.Dimensions.Location(x, y), Game1.viewport, Game1.player);
                    monitor.Log($"{Name} pot: soup pot at ({x},{y}), handled={handled}, menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}.", LogLevel.Info);
                    return;
                }
            monitor.Log($"{Name} pot: no LuauSoup tile on {here.Name}.", LogLevel.Warn);
        }

        /// <summary>Pick an item in the open item menu (the Luau pot, the secret gift): puts the item in the
        /// inventory if missing, then calls the menu's own click callback with it, as a click on it does.</summary>
        private static void Give(IMonitor monitor, string qualifiedId, int quality)
        {
            if (Game1.activeClickableMenu is not StardewValley.Menus.ItemGrabMenu menu || menu.behaviorFunction == null)
            {
                monitor.Log($"{Name} give: no item menu open (menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}).", LogLevel.Warn);
                return;
            }
            Item item = Game1.player.Items.FirstOrDefault(i => i?.QualifiedItemId == qualifiedId && i.Quality == quality);
            if (item == null)
            {
                item = ItemRegistry.Create(qualifiedId, 1, quality);
                Game1.player.addItemToInventory(item);
                item = Game1.player.Items.FirstOrDefault(i => i?.QualifiedItemId == qualifiedId && i.Quality == quality) ?? item;
            }
            Game1.player.removeItemFromInventory(item);
            menu.behaviorFunction(item, Game1.player);
            string shown = (Game1.activeClickableMenu as StardewValley.Menus.DialogueBox)?.getCurrentString();
            monitor.Log($"{Name} give: {item.QualifiedItemId} q{item.Quality} ({item.DisplayName}) given; box shows \"{shown ?? "-"}\".", LogLevel.Info);
        }

        private static string CurrentCommand()
        {
            Event ev = Game1.CurrentEvent;
            if (ev?.eventCommands == null || ev.CurrentCommand < 0 || ev.CurrentCommand >= ev.eventCommands.Length) return "-";
            return ev.eventCommands[ev.CurrentCommand];
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
