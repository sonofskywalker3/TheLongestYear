using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Merge the run's seen vanilla events into the cross-loop SeenEventsEver memory so a
        /// scene watched in any run stays suppressed on later loops (event-gating Phase 1). Called
        /// from OnSaving before the meta-state persists; FarmerReset re-seeds eventsSeen from it.</summary>
        /// <summary>Scan Data/Events for the events whose scripts grant the Furnace recipe or run the
        /// cave (bats/mushrooms) choice, logging their real ids + a snippet. The ids live in compiled
        /// content (not in code), so this audit is how the EventGatingTables get real ids rather than
        /// guesses. Loadable at the title or in-game.</summary>
        private void CmdDumpEvents(string command, string[] args)
        {
            string[] locations =
            {
                "Farm", "FarmHouse", "Town", "Mountain", "Beach", "Forest", "BusStop", "Backwoods",
                "Railroad", "Saloon", "SeedShop", "Blacksmith", "AnimalShop", "Hospital", "ScienceHouse",
                "JoshHouse", "HaleyHouse", "SamHouse", "Tent", "Trailer", "ManorHouse", "WizardHouse",
                "Sewer", "Mine", "Tunnel", "Woods", "CommunityCenter", "ArchaeologyHouse", "FishShop",
                "Sunroom", "AdventureGuild", "Greenhouse", "Cellar", "Desert", "Summit",
            };
            string[] tokens = { "Furnace", "cave", "mushroom", "fruitBat", "caveChoice" };

            int total = 0, hits = 0;
            foreach (string loc in locations)
            {
                System.Collections.Generic.Dictionary<string, string> data;
                try
                {
                    data = this.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>($"Data/Events/{loc}");
                }
                catch (System.Exception ex)
                {
                    // Location has no event data file.
                    this.Monitor.Log($"Data/Events/{loc}: not loaded ({ex.GetType().Name}); skipped.", LogLevel.Trace);
                    continue;
                }
                if (data == null) continue;

                foreach (System.Collections.Generic.KeyValuePair<string, string> kv in data)
                {
                    total++;
                    string script = kv.Value ?? "";
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    foreach (string tok in tokens)
                    {
                        if (script.IndexOf(tok, System.StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        hits++;
                        string snippet = script.Length > 140 ? script.Substring(0, 140) : script;
                        this.Monitor.Log($"[dumpevents] {loc} id={id} match='{tok}' :: {snippet}", LogLevel.Info);
                        break;
                    }
                }
            }
            this.Monitor.Log(
                $"[dumpevents] scanned {total} events across {locations.Length} locations; {hits} candidate(s) matched.",
                LogLevel.Info);
        }

        /// <summary>Audit the replayable-cutscene detection: scan the live save's events, log every
        /// unlock-granting cutscene with the matched grant command + whether the exclusion set drops it,
        /// then the resulting flagged-id set. Requires a loaded save (reads Game1.locations).</summary>
        private void CmdDumpReplayable(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            System.Collections.Generic.HashSet<string> exclude = BuildReplayableExclude();
            int total = 0, grants = 0, excluded = 0;

            foreach (GameLocation loc in Game1.locations)
            {
                if (string.IsNullOrEmpty(loc?.Name)) continue;

                System.Collections.Generic.Dictionary<string, string> data;
                try
                {
                    data = this.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>($"Data/Events/{loc.Name}");
                }
                catch (System.Exception ex)
                {
                    this.Monitor.Log($"Data/Events/{loc.Name}: not loaded ({ex.GetType().Name}); skipped.", LogLevel.Trace);
                    continue;
                }
                if (data == null) continue;

                foreach (System.Collections.Generic.KeyValuePair<string, string> kv in data)
                {
                    total++;
                    string script = kv.Value ?? "";
                    string token = EventGatingTables.MatchedGrantToken(script);
                    if (token == null) continue;

                    grants++;
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    bool isExcluded = exclude.Contains(id);
                    if (isExcluded) excluded++;
                    string snippet = script.Length > 120 ? script.Substring(0, 120) : script;
                    this.Monitor.Log(
                        $"[dumpreplayable] {loc.Name} id={id} grant='{token}' excluded={isExcluded} :: {snippet}",
                        LogLevel.Info);
                }
            }

            this.Monitor.Log(
                $"[dumpreplayable] scanned {total} events; {grants} grant-cutscene(s), {excluded} excluded, " +
                $"{grants - excluded} flagged replayable (config enabled={_config.AutoDetectReplayableUnlockCutscenes}). " +
                $"Exclusion set has {exclude.Count} id(s). Vanilla base always-replayable: " +
                $"[{string.Join(",", EventGatingTables.Default.ReplayableEventIds)}].",
                LogLevel.Info);
        }

        /// <summary>Debug: festival clock gates (0.18.5 verification) and a way to start the ice
        /// fishing contest without talking to Lewis.</summary>
        private void CmdFestival(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string mode = args.Length > 0 ? args[0] : "state";
            Event ev = Game1.CurrentEvent;
            if (mode == "contest")
            {
                if (ev == null || !ev.isFestival) { this.Monitor.Log("tly_festival: no festival running.", LogLevel.Warn); return; }
                // Vanilla reaches the contest through a switchEvent, which marks the event as past
                // its header; without the flag the after-contest script re-parses the header and fails.
                ev.eventSwitched = true;
                ev.setUpPlayerControlSequence("iceFishing");
                this.Monitor.Log("tly_festival: ice fishing contest started.", LogLevel.Info);
                return;
            }
            if (mode == "mainevent")
            {
                // Answer the host's "start the main event?" question with yes, without the click on the
                // host: the same Event.answerDialogueQuestion the host's dialogue goes through, so the
                // once-per-day block (and its leave offer) is exercised headlessly.
                if (ev == null || !ev.isFestival) { this.Monitor.Log("tly_festival: no festival running.", LogLevel.Warn); return; }
                NPC host = HarmonyLib.AccessTools.Field(typeof(Event), "festivalHost")?.GetValue(ev) as NPC
                    ?? Game1.getCharacterFromName("Lewis");
                this.Monitor.Log($"tly_festival: answering the host ({host?.Name ?? "none"}) with yes.", LogLevel.Info);
                ev.answerDialogueQuestion(host, "yes");
                return;
            }
            if (mode == "click")
            {
                if (Game1.activeClickableMenu is DialogueBox box) { box.receiveLeftClick(0, 0); this.Monitor.Log("tly_festival: clicked the dialogue box.", LogLevel.Info); }
                else this.Monitor.Log("tly_festival: no dialogue box open.", LogLevel.Info);
                return;
            }
            Farmer p = Game1.player;
            this.Monitor.Log(
                $"tly_festival state: time={Game1.timeOfDay} isFestival={Game1.isFestival()} event={(ev == null ? "none" : ev.id)} " +
                $"timer={(ev?.festivalTimer ?? -1)} control={(ev?.playerControlSequence ?? false)} id={(ev?.playerControlSequenceID ?? "-")} " +
                $"shouldTimePass={Game1.shouldTimePass()} autoEnd={FestivalTimeFlow.ShouldAutoEnd()} " +
                $"usingTool={p.UsingTool} tool={(p.CurrentTool?.GetType().Name ?? "none")} temp={(p.TemporaryItem?.Name ?? "none")} canMove={p.CanMove} " +
                $"eventUp={Game1.eventUp} freeze={Game1.freezeControls} fade={Game1.fadeToBlack}/{Game1.globalFade} dialogue={Game1.dialogueUp} menu={(Game1.activeClickableMenu?.GetType().Name ?? "none")} " +
                $"farmEvent={(Game1.farmEvent != null)} paused={Game1.paused}/{Game1.isTimePaused} festDay={Utility.isFestivalDay()} where={Game1.whereIsTodaysFest ?? "-"} loc={Game1.currentLocation?.Name}",
                LogLevel.Info);
        }

        /// <summary>Debug: invoke the TV's protected <c>getWeeklyRecipe()</c> directly so the
        /// headless bridge can exercise the Queen of Sauce path (and the Sneak Peek boost patch)
        /// without walking to a TV and clicking it. Logs the two dialogue lines the TV would show
        /// and reports whether the episode's recipe is now in the player's cookingRecipes.</summary>
        private void CmdTv(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int week = (int)(Game1.stats.DaysPlayed % 224 / 7);
            this.Monitor.Log($"tly_tv: DaysPlayed={Game1.stats.DaysPlayed} day={Game1.dayOfMonth} ({Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}) vanilla week={week}", LogLevel.Info);

            var before = new System.Collections.Generic.HashSet<string>(Game1.player.cookingRecipes.Keys);
            var tv = new StardewValley.Objects.TV();
            var method = HarmonyLib.AccessTools.Method(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new System.Type[0]);
            if (method == null) { this.Monitor.Log("tly_tv: getWeeklyRecipe() not found.", LogLevel.Warn); return; }

            string[] result;
            try
            {
                result = method.Invoke(tv, new object[0]) as string[];
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                this.Monitor.Log($"tly_tv: getWeeklyRecipe threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}", LogLevel.Warn);
                return;
            }

            if (result == null) { this.Monitor.Log("tly_tv: getWeeklyRecipe returned null.", LogLevel.Warn); return; }
            for (int i = 0; i < result.Length; i++)
                this.Monitor.Log($"tly_tv: line[{i}] = {result[i]}", LogLevel.Info);

            var after = new System.Collections.Generic.HashSet<string>(Game1.player.cookingRecipes.Keys);
            foreach (string key in after)
            {
                if (!before.Contains(key))
                    this.Monitor.Log($"tly_tv: cookingRecipes gained '{key}'", LogLevel.Info);
            }
            this.Monitor.Log($"tly_tv: cookingRecipes count {before.Count} -> {after.Count}; Pizza present={after.Contains("Pizza")}", LogLevel.Info);
        }

        private void CmdDejaVu(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            RunState run = _meta.Run;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "set" when args.Length >= 3 && int.TryParse(args[2], out int n):
                    s.VillagerFamiliarity[args[1]] = n;
                    this.Monitor.Log($"tly_dejavu: {args[1]} familiarity = {n}.", LogLevel.Info);
                    break;
                case "force" when args.Length >= 2:
                    TheLongestYear.Loop.DejaVuDialoguePatch.ForceNext = args[1];
                    this.Monitor.Log($"tly_dejavu: next talk with {args[1]} will inject a line (Introduction day excepted).", LogLevel.Info);
                    break;
                case "reset":
                    run.DejaVuShownTo.Clear();
                    run.DejaVuLastDay = -1;
                    this.Monitor.Log("tly_dejavu: loop caps cleared.", LogLevel.Info);
                    break;
                default:
                    int day = (int)Game1.stats.DaysPlayed;
                    var sb = new System.Text.StringBuilder(
                        $"tly_dejavu status: enabled={_config.EnableDejaVuDialogue} resets={s.CompletedResets} threshold={_config.DejaVuThreshold} " +
                        $"chance={_config.DejaVuChancePercent}% day={day} lastDay={run.DejaVuLastDay} " +
                        $"shownThisLoop=[{string.Join(",", run.DejaVuShownTo)}] force={TheLongestYear.Loop.DejaVuDialoguePatch.ForceNext ?? "-"}");
                    foreach (var kv in s.VillagerFamiliarity.OrderByDescending(k => k.Value))
                        sb.Append($"\n  {kv.Key}={kv.Value} tier={DejaVuRules.Tier(kv.Value, _config.DejaVuThreshold)} eligible={DejaVuRules.IsEligible(s, run, kv.Key, day, _config.DejaVuThreshold)}");
                    this.Monitor.Log(sb.ToString(), LogLevel.Info);
                    break;
            }
        }

        private void CmdReadBook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            if (args.Length == 0)
            {
                var sb = new System.Text.StringBuilder("tly_readbook: ");
                foreach (BookKeep book in BookKeepTable.Entries)
                    sb.Append(book.StatKey).Append('=').Append(p.stats.Get(book.StatKey)).Append(' ');
                this.Monitor.Log(sb.ToString().TrimEnd(), LogLevel.Info);
                return;
            }
            string key = args[0];
            if (!key.StartsWith(BookKeepTable.StatKeyPrefix, System.StringComparison.Ordinal))
            {
                this.Monitor.Log($"tly_readbook: '{key}' is not a Book_* stat key.", LogLevel.Warn);
                return;
            }
            p.stats.Set(key, 1);
            this.Monitor.Log($"tly_readbook: {key}=1 (reach '{BookKeepTable.ReachFor(key)}' now met; buy {BookKeepTable.UpgradeIdFor(key)} at the shrine or via tly_buyupgrade).", LogLevel.Info);
        }
    }
}
