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
        /// <summary>One debug command: its name, its console help and its handler.</summary>
        private readonly record struct DebugCommand(string Name, string Help, Action<string, string[]> Run);

        /// <summary>Every tly_* command, registered with the SMAPI console AND served by the debug
        /// file bridge from this one table, so the two can never drift apart again (the hygiene
        /// review found three console-only and three bridge-only commands).</summary>
        private IEnumerable<DebugCommand> DebugCommandTable()
        {
            yield return new DebugCommand("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            yield return new DebugCommand("tly_loadsave", "Load a save by folder name from the title screen (debug/automation). Usage: tly_loadsave <saveFolderName>", this.CmdLoadSave);
            yield return new DebugCommand("tly_totitle", "Exit to the title screen without saving (debug/automation), so tly_newgame / tly_loadsave can run next.", this.CmdToTitle);
            yield return new DebugCommand("tly_buildings", "List every building on the farm with its type and tile (read-only; for keep-building audits).", this.CmdBuildings);
            yield return new DebugCommand("tly_newgame", "Create a new TLY farm from the title screen without the character screen (debug/automation). Usage: tly_newgame <standard|riverland|forest|hilltop|wilderness|fourcorners|beach|meadowlands> [skipintro] [custom|standard|remixed] [name]", this.CmdNewGame);
            yield return new DebugCommand("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            yield return new DebugCommand("tly_addmoney", "Add gold to the loaded farmer (debug). Usage: tly_addmoney <amount>", this.AddMoney);
            yield return new DebugCommand("tly_additem", "Grant an item to the farmer (debug). Usage: tly_additem <qualifiedId> [count]", this.CmdAddItem);
            yield return new DebugCommand("tly_removehorse", "Remove the stable + horse, clear the carryover snapshot, and drop the Keep Horse upgrade so it's re-buyable (debug — clean slate for a Keep-Horse carryover test).", this.CmdRemoveHorse);
            yield return new DebugCommand("tly_reset", "Force an in-place reset to Spring 1 (debug). An optional seed loop pins the board the new run generates (same number tly_genbundles takes), so two runs can be played on the same board. Usage: tly_reset [seedLoop]", this.ForceReset);
            yield return new DebugCommand("tly_setday", "Jump the in-game date to <day> of the current season so you can sleep straight into that day's gate (e.g. day 28) without grinding a month. Sleep to trigger it. Usage: tly_setday <day>", this.CmdSetDay);
            yield return new DebugCommand("tly_failreset", "Simulate a day-28 gate-miss reset: opens the JP shrine, then resets to Spring 1 on close (debug — exercises the natural loop-reset path the JP-refund bug lived in).", this.CmdFailReset);
            yield return new DebugCommand("tly_restart", "Debug: press the Junimo Shrine's Restart the year button. Opens the same yes/no (tly_answer 0 = Yes, 1 = No); refuses and logs why when the button would be hidden.", this.CmdRestart);
            yield return new DebugCommand("tly_answer", "Pick a response on the open question dialogue without the mouse (debug). Usage: tly_answer <n> (0-based), or tly_answer key [n] for the Escape/N key path.", this.CmdAnswer);
            yield return new DebugCommand("tly_win", "Open the basic win screen, then the JP shrine + keep-playing choice (debug — bypasses the first-win-only gate, re-runnable).", this.CmdForceWin);
            yield return new DebugCommand("tly_resetif", "Reset only if the loaded farmer's name matches. Usage: tly_resetif <name>", this.ResetIfNameMatches);
            yield return new DebugCommand("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
            yield return new DebugCommand("tly_select", "Select a theme. With the planning hub open this is the card click (any theme, hub closes); otherwise it forces the theme for the current week. Usage: tly_select <theme> [left|right]", this.CmdSelect);
            yield return new DebugCommand("tly_offer", "Show this week's selection offer.", this.CmdOffer);
            yield return new DebugCommand("tly_skipscene", "Finish the open day-28 Junimo scene as if clicked through (debug/automation).", this.CmdSkipScene);
            yield return new DebugCommand("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            yield return new DebugCommand("tly_runstate", "Print the current run state.", this.CmdRunState);
            yield return new DebugCommand("tly_netstate", "Print the NetWorldState fields the keep/wipe audit rules, for smoking a reset.", this.CmdNetState);
            yield return new DebugCommand("tly_gatecheck", "Audit the live board's season gates: for every bundle and every season, what the gate demands against what is actually obtainable by then. Flags anything IMPOSSIBLE (would brick the run) and anything FREE (gate demands nothing). Read-only.", this.CmdGateCheck);
            yield return new DebugCommand("tly_gateneeds", "Print, per bundle, what the current season's day-28 gate still needs (the same numbers the Season Goals page shows) plus the vault. Read-only.", this.CmdGateNeeds);
            yield return new DebugCommand("tly_crabpots", "Measure real crab pot yields: 'place [n]' drops n baited pots per water zone (default 10), bare runs the daily collect-and-rebait into that season's crab chest, 'report' prints them. Usage: tly_crabpots [place|report] [count]", this.CmdCrabPots);
            yield return new DebugCommand("tly_sweepforage", "Take every spawned forage item on every map and put it in the sweep chest on the Farm (a real harvest, for measuring what a season actually yields). Run 'clear' first, then once a day, then 'report'. Usage: tly_sweepforage [clear|report]", this.CmdSweepForage);
            yield return new DebugCommand("tly_forageyield", "Simulate how much of each forage item a player could gather by a cutoff day if every reachable map were cleared every day, and print the 20-80% band a requirement should sit in. Forage only. Read-only. Usage: tly_forageyield [spring|summer|fall|winter|<day>] [itemId]", this.CmdForageYield);
            yield return new DebugCommand("tly_playseason", "Debug: simulate a minimal compliant player for the current season (donate exactly what every gate demands by day 28, pay the vault; 'goals' also deposits this week's goal slots; 'goalsonly' deposits only the goal slots; 'quarter <k>' donates only the first k/4 of the season's share, cumulative across k=1..4, and pays the vault on k=4). Real CC slot flips. Follow with tly_setday 28 and a sleep. Usage: tly_playseason [goals|goalsonly|quarter <1-4>]", this.CmdPlaySeason);
            yield return new DebugCommand("tly_goals", "Log the weekly goals every theme would offer on the LIVE board for a season (the same sample and counts the planning hub shows, theme week discount included). Read-only. Usage: tly_goals [spring|summer|fall|winter] [weekOfYear]", this.CmdGoals);
            yield return new DebugCommand("tly_themepool", "Print each theme's askable weekly-goal count for the current week (rule C's number), or, with a theme, every candidate line with due/filler, effort, tier and weight. Read-only. Usage: tly_themepool [theme]", this.CmdThemePool);
            yield return new DebugCommand("tly_dumpbundles", "Write a Markdown catalogue of every bundle the engine can produce, with every item each one can ask for and how its quantity is decided. Reads LIVE game data, so it covers whatever content mods are installed. Usage: tly_dumpbundles [fileName]", this.CmdDumpBundles);
            yield return new DebugCommand(
                "tly_warpgraph",
                "Print every loaded location and its warp targets, for verifying reachability derivation. Usage: tly_warpgraph [filter]",
                this.CmdWarpGraph);
            yield return new DebugCommand("tly_dumpavailability", "Write a Markdown listing of every item in every bundle on the LIVE board with the earliest season the engine says it can exist, why, and the season its gate demands it. Usage: tly_dumpavailability [fileName]", this.CmdDumpAvailability);
            yield return new DebugCommand("tly_itemmodel", "Print the derived availability model for one item id or every ingredient of a bundle. Usage: tly_itemmodel <itemId|bundleName>", this.CmdItemModel);
            yield return new DebugCommand(TheLongestYear.DebugCommands.ModelDumpCommand.Name, TheLongestYear.DebugCommands.ModelDumpCommand.Description,
                (c, a) => this.CmdDumpModel(a));
            yield return new DebugCommand("tly_dumpeffort", "Write a Markdown review of the derived item effort model: every pool item by theme with its effort, tier (quartile within the theme's pool), source and game-data basis. Usage: tly_dumpeffort [fileName]", this.CmdDumpEffort);
            yield return new DebugCommand("tly_difficulty", "Read-only: print the ten configured difficulty steps, the ten this loop is actually running under, and every resolved value. Attach this to any balance report.", this.CmdDifficulty);
            yield return new DebugCommand("tly_catalog", "Print the bundle-derived CC catalog summary.", this.CmdCatalog);
            yield return new DebugCommand("tly_classify", "Re-run bundle classification over the live BundleData and log the summary (diagnostics only — does not touch the active run). Pairs with 'debug ShuffleBundles' to exercise remixed classification in memory.", this.CmdClassify);
            yield return new DebugCommand("tly_genbundles", "Generate (diagnostics only) the engine bundle set for a loop: nothing written or persisted. Logs each room's picked bundles + slot counts, the manifest classification summary, and a determinism self-check (regenerates off the same seed and diffs). Requires a loaded save (the seed uses Game1.player.UniqueMultiplayerID). Usage: tly_genbundles [seedLoop] [custom|standard|remixed] (default: the current board's seed loop, custom = the TLY engine set; standard/remixed audit the board vanilla would build for that Advanced Options choice)", this.CmdGenBundles);
            yield return new DebugCommand("tly_trophytest", "Diagnostics-only proof that the weapon/hat donation patches accept (W)13/(H)8/(O)520 as valid Gil's Trophies ingredients. Builds ephemeral items + a detached synthetic Bundle (never touches the real CC board) and logs PASS/FAIL per id. Requires a loaded save.", this.CmdTrophyTest);
            yield return new DebugCommand("tly_testdonate", "Simulate a CC donation through the JP service. Usage: tly_testdonate <qualifiedId> [count]", this.CmdTestDonate);
            yield return new DebugCommand("tly_hubcards", "Log each planning hub card: slot, theme (? if face down), drawback, goal multiplier (debug).", this.CmdHubCards);
            yield return new DebugCommand("tly_shrinegoals", "List this week's random shrine donation goals: index, list, item, stack, deposited, paid (debug).", this.CmdShrineGoals);
            yield return new DebugCommand("tly_shrinedonate", "Donate shrine goal N through the statue's donate path, spawning the stack into the inventory if missing (debug). Usage: tly_shrinedonate <index>", this.CmdShrineDonate);
            yield return new DebugCommand("tly_openhub", "Open the weekly planning hub menu (debug).", this.CmdOpenHub);
            yield return new DebugCommand("tly_reroll", "Press the planning hub's re-roll button N times, or close and reopen the hub (debug). Usage: tly_reroll [count|reopen|paid]", this.CmdReroll);
            yield return new DebugCommand("tly_seasongoals", "Open the Season Goals page, the same one the Bundle Log book opens (debug).", this.CmdSeasonGoals);
            yield return new DebugCommand("tly_driedprobe", "Diagnostics: what each mushroom and fruit dries into, and whether vanilla's PreserveType names resolve as item ids. Read-only.", this.CmdDriedProbe);
            yield return new DebugCommand("tly_flavors", "Diagnostics: for every flavored bundle slot on the live board (Dried Fruit, Dried Mushrooms, Smoked Fish), show which fruit/mushroom/fish it names and how it reads. Read-only.", this.CmdFlavors);
            yield return new DebugCommand("tly_bundlesource", "Diagnostics: show or set the loaded save's bundle source / vanilla type in memory (persists on the next save). Usage: tly_bundlesource [Engine|Vanilla] [Default|Remixed] — also sets the save's chosen source so the next reset honours it.", this.CmdBundleSource);
            yield return new DebugCommand("tly_jpbudget", "Diagnostics only: log the maximum JP the CURRENT loop's board can pay out, per season + total (earliest-obtainable-season model) and a hoard-for-Winter ceiling. Baseline economy, no jp_boost. Usage: tly_jpbudget [verbose]", this.CmdJpBudget);
            yield return new DebugCommand("tly_openshop", "Open the Junimo Shrine upgrade shop (debug).", this.CmdOpenShop);
            yield return new DebugCommand("tly_listupgrades", "List the upgrade catalog grouped by category.", this.CmdListUpgrades);
            yield return new DebugCommand("tly_dumpevents", "Audit Data/Events for furnace/cave/early-scene ids (debug — logs candidates so the event-gating tables use real ids, not guesses).", this.CmdDumpEvents);
            yield return new DebugCommand("tly_dumpreplayable", "Audit which Data/Events cutscenes the loop treats as REPLAYABLE (re-fire each loop): logs each unlock-granting event id, the matched grant command, whether it's excluded, and the active exclusion set (debug — diagnoses 'an event keeps replaying').", this.CmdDumpReplayable);
            yield return new DebugCommand("tly_buyupgrade", "Buy an upgrade by id (debug). Usage: tly_buyupgrade <id>", this.CmdBuyUpgrade);
            yield return new DebugCommand("tly_boost", "Buy a shrine boost today (debug, the same purchase the shrine's Buy button makes), or list the roster with each row's state. Usage: tly_boost list | tly_boost <id> [farming|fishing|foraging|mining|combat]", this.CmdBoost);
            yield return new DebugCommand("tly_wildcard", "Debug: show this week's wildcard day and twist, or set today's twist (headless twist checks). Usage: tly_wildcard [twistId|clear]", this.CmdWildcard);
            yield return new DebugCommand("tly_boostexpire", "Debug: run the boosts' day-start pass now (prune expired entries, re-apply buffs, lucky day).", (cmd, a) => _boostEffects?.OnDayStarted());
            yield return new DebugCommand("tly_dismiss", "Debug: dismiss the active menu headlessly (a LevelUpMenu via its OK button, anything else via exitThisMenu). Lets the bridge get past end-of-night menus.", this.CmdDismiss);
            yield return new DebugCommand("tly_openshrine", "Debug: open the planning shrine on a tab (active|boosts|plan|donate) exactly as the statue does, so every tab's rows build and draw headlessly. Donate shows only on weeks with shrine goals. Usage: tly_openshrine [active|boosts|plan|donate]", this.CmdOpenShrine);
            yield return new DebugCommand("tly_tv", "Debug: run the Queen of Sauce weekly-recipe lookup the TV uses (no mouse needed) and log the returned dialogue plus whether the recipe landed in cookingRecipes. Exercises the Sneak Peek boost patch. NOT read-only: this is the real grant path, so it teaches the player that episode's recipe exactly as watching the TV would.", this.CmdTv);
            yield return new DebugCommand("tly_dejavu", "Deja-vu dialogue debug. Usage: tly_dejavu [status | set <npc> <n> | force <npc> | reset]", this.CmdDejaVu);
            yield return new DebugCommand(TheLongestYear.DebugCommands.FestivalMemoryCommand.Name, TheLongestYear.DebugCommands.FestivalMemoryCommand.Description,
                (cmd, a) => TheLongestYear.DebugCommands.FestivalMemoryCommand.Run(this.Monitor, _meta?.State, () => _meta?.Run, _config, a));
            yield return new DebugCommand("tly_readbook","Debug: mark a power book as read (sets its Book_* stat). No args lists every Book_* stat. Usage: tly_readbook [Book_Id]", this.CmdReadBook);
            yield return new DebugCommand("tly_ordersboard", TheLongestYear.DebugCommands.OrdersBoardCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.OrdersBoardCommand.Run(this.Monitor, a));
            yield return new DebugCommand("tly_bundlecount", TheLongestYear.DebugCommands.BundleCountDebugCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.BundleCountDebugCommand.Run(this.Monitor, _config, a));
            yield return new DebugCommand("tly_wallet", TheLongestYear.DebugCommands.WalletDebugCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.WalletDebugCommand.Run(this.Monitor, a));
            yield return new DebugCommand("tly_cropprobe", TheLongestYear.DebugCommands.CropProbeCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.CropProbeCommand.Run(this.Monitor, a));
            yield return new DebugCommand("tly_spawnprobe", TheLongestYear.DebugCommands.SpawnProbeCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.SpawnProbeCommand.Run(this.Monitor, a));
            yield return new DebugCommand(TheLongestYear.DebugCommands.MineSweepCommand.Name, TheLongestYear.DebugCommands.MineSweepCommand.Description,
                (cmd, a) => TheLongestYear.DebugCommands.MineSweepCommand.Run(this.Monitor, this.Helper, a));
            yield return new DebugCommand(TheLongestYear.DebugCommands.BankRecipesDebugCommand.Name, TheLongestYear.DebugCommands.BankRecipesDebugCommand.Description,
                (cmd, a) => TheLongestYear.DebugCommands.BankRecipesDebugCommand.Run(this.Monitor, _meta?.State, a));
            yield return new DebugCommand("tly_payvault", "Mark a Vault bundle as paid this run (debug; a real payment is picked up by VaultPaymentSync). Usage: tly_payvault <season|index>", this.CmdPayVault);
            yield return new DebugCommand("tly_hold", "Debug: apply the Fail-night hold choice in memory without a fail night. Usage: tly_hold keep|reshuffle|status. keep deducts JP per the config curve; the next reset (tly_reset) then honours it. Must be followed by tly_reset before sleeping; a real Fail night after tly_hold keep charges the next tier again.", this.CmdHold);
            yield return new DebugCommand("tly_here", "Print the player's current tile coords (debug — useful for tuning interactable tile coords).", this.CmdHere);
            yield return new DebugCommand("tly_opencookbook",
                "Open the Cookbook menu directly (debug).",
                this.CmdOpenCookbook);
            yield return new DebugCommand("tly_opencraftbook",
                "Open the Craftbook menu directly (debug).",
                this.CmdOpenCraftbook);
            yield return new DebugCommand("tly_openherdbook",
                "Open the Herd Book menu directly (debug).",
                this.CmdOpenHerdBook);
            yield return new DebugCommand(TheLongestYear.DebugCommands.AnimalPowersDebugCommand.Name, TheLongestYear.DebugCommands.AnimalPowersDebugCommand.Description,
                (cmd, a) => TheLongestYear.DebugCommands.AnimalPowersDebugCommand.Run(this.Monitor, _meta?.State, a));
            yield return new DebugCommand(TheLongestYear.DebugCommands.HerdBookDebugCommand.Name, TheLongestYear.DebugCommands.HerdBookDebugCommand.Description,
                (cmd, a) => TheLongestYear.DebugCommands.HerdBookDebugCommand.Run(this.Monitor, _meta?.State, a));
            yield return new DebugCommand("tly_activeeffects",
                "Print the currently active theme bonus and liability.",
                this.CmdActiveEffects);
            yield return new DebugCommand("tly_setstash",
                "Anchor the Junimo Stash chest to the tile you are facing on the Farm. Writes config.json.",
                this.CmdSetStash);
            yield return new DebugCommand("tly_openstash",
                "Open the Junimo Stash chest directly (debug).",
                this.CmdOpenStash);
            yield return new DebugCommand("tly_stashclear",
                "Clear all items from the Junimo Stash MetaState (debug — DESTRUCTIVE).",
                this.CmdStashClear);
            yield return new DebugCommand("tly_wipemeta",
                "Wipe MetaState (JP, owned upgrades, stash contents, dismissed indicators) without " +
                "deleting the save. Persists immediately. Reload the save to fully apply " +
                "(some services cache the MetaState reference). DESTRUCTIVE.",
                this.CmdWipeMeta);
            yield return new DebugCommand("tly_replayintro",
                "Clear MetaState.HasSeenIntro + per-run intro mail flags so the day-1 Lewis+Junimo " +
                "intro chain re-fires on the next Spring 1. Pair with tly_reset to test immediately.",
                this.CmdReplayIntro);
            yield return new DebugCommand("tly_addpet",
                "Debug: add a pet to the Farm, or list every pet with its location and bowl. " +
                "Usage: tly_addpet <Cat|Dog> [name] [breed] | tly_addpet check",
                this.CmdAddPet);
            yield return new DebugCommand("tly_fixbridge",
                "Debug: mark the beach bridge repaired (Beach.bridgeFixed), or report the flag + the " +
                "bridge tiles so a reset can be checked to un-fix it. Usage: tly_fixbridge | tly_fixbridge check",
                this.CmdFixBridge);
            yield return new DebugCommand("tly_stashrod",
                "Debug: drop an Iridium Rod with bait, a spinner and an Auto-Hook enchantment into the " +
                "Junimo Stash chest, or print every stashed tool's slots + enchantments. " +
                "Usage: tly_stashrod | tly_stashrod check",
                this.CmdStashRod);
            yield return new DebugCommand("tly_stashnest",
                "Debug: exercise the stash nesting rule and item identity. Usage: tly_stashnest <hats|ring|gear|legacy|fill|wear|worn|check>",
                (cmd, a) => { if (Context.IsWorldReady) StashNestingDebug.Run(a, this.Monitor, _stashService); });
            yield return new DebugCommand("tly_decor",
                "Debug: set up and inspect Keep Farm Decor cases. Usage: tly_decor <clumps|path x y|fence x y|check x y>",
                (cmd, a) => { if (Context.IsWorldReady) FarmDecorDebug.Run(a, this.Monitor); });
            yield return new DebugCommand("tly_housefurn",
                "Debug: set up and inspect Keep Farmhouse Furniture cases. Usage: tly_housefurn <list|place id x y [rotations]|fill x y|check>",
                (cmd, a) => { if (Context.IsWorldReady) FarmhouseFurnitureDebug.Run(a, _meta.State, this.Monitor); });
            yield return new DebugCommand("tly_giftbox",
                "Debug: report or open a vanilla one-time gift box. Usage: tly_giftbox <Location> <x> <y> [warp|open]",
                this.CmdGiftBox);
            yield return new DebugCommand("tly_festival",
                "Debug: 'state' logs the festival clock gates (timer, control sequence, shouldTimePass, tool state); 'contest' starts the ice fishing contest on the current festival; 'mainevent' answers the host's start question with yes (exercises the once-per-day block and its leave offer).",
                this.CmdFestival);
            yield return new DebugCommand("tly_stashmenu",
                "Debug: open the Junimo stash and log what the menu carries (context, source item, Chests Anywhere keys).",
                this.CmdStashMenu);
            yield return new DebugCommand("tly_ringtest",
                "Debug: open the Community Center note and log whether <qualifiedId> (default (O)529) would highlight for pickup. Usage: tly_ringtest [id]",
                this.CmdRingTest);
            yield return new DebugCommand("tly_day28continue",
                "Debug: queue the day-28 CONTINUE (gate passed) Junimo cutscene now and roll into the next season after it, without a real passing day 28.",
                this.CmdDay28Continue);
            yield return new DebugCommand(TheLongestYear.DebugCommands.RarityStepCommand.Name,
                "Debug: re-stamp this loop's rarity difficulty step in memory and rebuild the availability model for it. " + TheLongestYear.DebugCommands.RarityStepCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.RarityStepCommand.Run(this.Monitor, _config, _meta?.State,
                    BuildAvailabilityModelFor, _enginePools, _catalog.Select(c => c.Id), DisplayName,
                    this.Helper.DirectoryPath, a));
            yield return new DebugCommand(TheLongestYear.DebugCommands.RecipeBookBackOutCommand.Name,
                "Debug: check every way of backing out of the Cookbook and Craftbook (click, Escape, menu key, pad B). " + TheLongestYear.DebugCommands.RecipeBookBackOutCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.RecipeBookBackOutCommand.Run(this.Monitor,
                    () => _launcher?.OpenCookbook(), () => _launcher?.OpenCraftbook(), a));
        }

        /// <summary>The bridge's view of <see cref="DebugCommandTable"/>, built once in Entry.</summary>
        private Dictionary<string, Action<string, string[]>> _debugCommands;

        /// <summary>Register every command with the console and build the bridge's lookup.</summary>
        private void RegisterDebugCommands(IModHelper helper)
        {
            _debugCommands = new Dictionary<string, Action<string, string[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (DebugCommand c in DebugCommandTable())
            {
                helper.ConsoleCommands.Add(c.Name, c.Help, c.Run);
                _debugCommands[c.Name] = c.Run;
            }
        }

        /// <summary>Debug: the day-28 CONTINUE cutscene without a passing day 28.</summary>
        private void CmdDay28Continue(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController?.DebugForceContinueCutscene();
        }
    }
}
