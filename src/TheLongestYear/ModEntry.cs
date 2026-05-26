using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;
        private CommunityCenterUnlock _ccUnlock;
        private WorldResetService _reset;
        private RunController _runController;
        private UpgradePurchaseService _purchases;
        private MenuLauncher _launcher;
        private SeasonResolver _seasonResolver;
        private IReadOnlyList<CcItem> _catalog = new List<CcItem>();

        // Debug command-file bridge: lets the developer trigger tly_ actions by writing lines into a file
        // in the mod folder, so PC in-game testing needs no console typing (the mod polls + executes them).
        private const string DebugCommandFileName = "tly_commands.txt";
        private const int DebugPollTicks = 30;
        private string _commandFilePath;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<GameplayConfig>();
            _meta = new MetaStore(helper.Data);
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();

            _commandFilePath = Path.Combine(helper.DirectoryPath, DebugCommandFileName);

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
            helper.ConsoleCommands.Add("tly_champion", "Champion one of this week's offered themes. Usage: tly_champion <theme>", this.CmdChampion);
            helper.ConsoleCommands.Add("tly_offer", "Show this week's champion offer.", this.CmdOffer);
            helper.ConsoleCommands.Add("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            helper.ConsoleCommands.Add("tly_runstate", "Print the current run state.", this.CmdRunState);
            helper.ConsoleCommands.Add("tly_catalog", "Print the bundle-derived CC catalog summary.", this.CmdCatalog);
            helper.ConsoleCommands.Add("tly_testdonate", "Simulate a CC donation through the JP service. Usage: tly_testdonate <qualifiedId> [count]", this.CmdTestDonate);
            helper.ConsoleCommands.Add("tly_openhub", "Open the weekly planning hub menu (debug).", this.CmdOpenHub);
            helper.ConsoleCommands.Add("tly_openshop", "Open the Junimo Shrine upgrade shop (debug).", this.CmdOpenShop);
            helper.ConsoleCommands.Add("tly_listupgrades", "List the upgrade catalog grouped by category.", this.CmdListUpgrades);
            helper.ConsoleCommands.Add("tly_buyupgrade", "Buy an upgrade by id (debug). Usage: tly_buyupgrade <id>", this.CmdBuyUpgrade);
            helper.ConsoleCommands.Add("tly_reroll", "Re-roll the year plan (new seed) and re-open the planning hub (debug).", this.CmdReroll);
            helper.ConsoleCommands.Add("tly_dumpplan", "Dump the per-(season,theme) item assignment table to the log.", this.CmdDumpPlan);
            helper.ConsoleCommands.Add("tly_payvault", "Mark a Vault bundle as paid this run (debug — Harmony hookup is Plan 06). Usage: tly_payvault <season|index>", this.CmdPayVault);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
        }

        /// <summary>Load this playthrough's banked progress when a save opens.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _meta.Load();
            _ccUnlock = new CommunityCenterUnlock(this.Monitor);
            _ccUnlock.Apply();
            _reset = new WorldResetService(this.Monitor, _meta.State, _ccUnlock);

            _seasonResolver = new SeasonResolver();
            _catalog = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                ParseThemeOverrides()).Build();
            DonationService.Active = new DonationService(this.Monitor, _meta, _config);

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog);
            _runController.OnRunLoaded();
            _purchases = new UpgradePurchaseService(this.Monitor, _meta);
            _launcher = new MenuLauncher(this.Monitor, _config, _meta, _runController, _purchases);
            _runController.AttachLauncher(_launcher);
            this.Monitor.Log(
                $"Run {_meta.Run.RunNumber} loaded ({_meta.Run.Season} {_meta.Run.DayOfMonth}). JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>Commit meta-state as part of the game's save — never eagerly, to prevent save-scumming.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            _meta.Save();
            this.Monitor.Log($"Meta-state saved with the game. JP banked: {_meta.State.JunimoPoints}.", LogLevel.Trace);
        }

        private void PrintMeta(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            MetaState s = _meta.State;
            this.Monitor.Log(
                $"JP={s.JunimoPoints}, StashTier={s.StashCapacityTier}, Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
                LogLevel.Info);
        }

        private void AddJp(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !long.TryParse(args[0], out long amount))
            {
                this.Monitor.Log("Usage: tly_addjp <amount>", LogLevel.Warn);
                return;
            }

            _meta.State.JunimoPoints += amount;
            this.Monitor.Log($"JP is now {_meta.State.JunimoPoints} (in memory — persists on next save).", LogLevel.Info);
        }

        private void ForceReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset(_config.StartingMoney);
        }

        private void LeakTest(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset(_config.StartingMoney);
            var first = WorldStateProbe.Capture();

            _reset.PerformReset(_config.StartingMoney);
            var second = WorldStateProbe.Capture();

            this.Monitor.Log(
                $"Leak test object counts (informational, non-deterministic world-gen): {first.PlacedObjectCount} vs {second.PlacedObjectCount}.",
                LogLevel.Info);

            var diff = first.Diff(second);
            if (diff.Count == 0)
            {
                this.Monitor.Log("Leak test PASSED: two consecutive resets produced an identical baseline.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Leak test FAILED: {diff.Count} field(s) leaked between runs:", LogLevel.Error);
                foreach (string d in diff)
                    this.Monitor.Log($"  - {d}", LogLevel.Error);
            }
        }

        private void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
            => _runController?.OnDayStarted(sender, e);

        private void OnDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
            => _runController?.OnDayEnding(sender, e);

        /// <summary>
        /// Poll the debug command file (mod folder) and execute any queued lines once. Lets the developer
        /// drive tly_ actions by writing the file while the player only plays — no in-game console needed.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.IsMultipleOf(DebugPollTicks))
                return;
            if (string.IsNullOrEmpty(_commandFilePath) || !File.Exists(_commandFilePath))
                return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(_commandFilePath);
                File.Delete(_commandFilePath); // consume once; the file may be re-written for the next batch
            }
            catch (IOException)
            {
                return; // file is mid-write — retry on the next poll
            }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                this.Monitor.Log($"Debug bridge: executing '{line}'.", LogLevel.Info);
                this.ExecuteDebugLine(line);
            }
        }

        /// <summary>Parse one "tly_command arg1 arg2" line and route it to the matching command handler.</summary>
        private void ExecuteDebugLine(string line)
        {
            string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();
            string[] args = parts.Skip(1).ToArray();

            switch (command)
            {
                case "tly_meta": this.PrintMeta(command, args); break;
                case "tly_addjp": this.AddJp(command, args); break;
                case "tly_reset": this.ForceReset(command, args); break;
                case "tly_leaktest": this.LeakTest(command, args); break;
                case "tly_champion": this.CmdChampion(command, args); break;
                case "tly_offer": this.CmdOffer(command, args); break;
                case "tly_donate": this.CmdDonate(command, args); break;
                case "tly_runstate": this.CmdRunState(command, args); break;
                case "tly_catalog": this.CmdCatalog(command, args); break;
                case "tly_testdonate": this.CmdTestDonate(command, args); break;
                case "tly_openhub": this.CmdOpenHub(command, args); break;
                case "tly_openshop": this.CmdOpenShop(command, args); break;
                case "tly_listupgrades": this.CmdListUpgrades(command, args); break;
                case "tly_buyupgrade": this.CmdBuyUpgrade(command, args); break;
                case "tly_reroll": this.CmdReroll(command, args); break;
                case "tly_dumpplan": this.CmdDumpPlan(command, args); break;
                case "tly_payvault": this.CmdPayVault(command, args); break;
                default:
                    this.Monitor.Log($"Debug bridge: unknown command '{command}'.", LogLevel.Warn);
                    break;
            }
        }

        private void CmdChampion(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_champion <theme>", LogLevel.Warn); return; }
            _runController.ChampionByName(args[0]);
        }

        private void CmdOffer(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PresentOffer();
        }

        private void CmdDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_donate <itemId>", LogLevel.Warn); return; }
            _runController.Donate(args[0]);
        }

        private void CmdRunState(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PrintRunState();
        }

        private void CmdCatalog(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var byTheme = new Dictionary<TheLongestYear.Core.Theme, int>();
            foreach (CcItem item in _catalog)
                byTheme[item.Theme] = byTheme.TryGetValue(item.Theme, out int n) ? n + 1 : 1;

            this.Monitor.Log($"CC catalog: {_catalog.Count} items.", LogLevel.Info);
            foreach (var kvp in byTheme)
                this.Monitor.Log($"  {kvp.Key}: {kvp.Value}", LogLevel.Info);
        }

        private void CmdTestDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_testdonate <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            DonationService.Active?.OnItemDonated(args[0], count);
        }

        private void CmdOpenHub(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenWeeklyHub();
        }

        private void CmdOpenShop(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenShrineShop();
        }

        private void CmdListUpgrades(string command, string[] args)
        {
            this.Monitor.Log($"Upgrade catalog: {UpgradeCatalog.All.Count} entries.", LogLevel.Info);
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                var rows = UpgradeCatalog.ByCategory(cat);
                this.Monitor.Log($"  {cat} ({rows.Count}):", LogLevel.Info);
                foreach (var u in rows)
                {
                    string owned = _meta != null && _meta.State.HasUpgrade(u.Id) ? " [OWNED]" : "";
                    string prereq = u.PrerequisiteId != null ? $" (req {u.PrerequisiteId})" : "";
                    this.Monitor.Log($"    - {u.Id}: {u.DisplayName} — {u.Cost} JP{prereq}{owned}", LogLevel.Info);
                }
            }
        }

        private void CmdBuyUpgrade(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_buyupgrade <id>", LogLevel.Warn); return; }
            _purchases?.TryPurchase(args[0]);
        }

        private void CmdReroll(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController?.Reroll();
        }

        private void CmdDumpPlan(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController?.DumpAssignmentTable("on demand");
        }

        private void CmdPayVault(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: tly_payvault <Spring|Summer|Fall|Winter|34|35|36|37>", LogLevel.Warn);
                return;
            }

            int bundleIndex;
            if (int.TryParse(args[0], out bundleIndex))
            {
                // direct index
            }
            else if (System.Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.Season s))
            {
                bundleIndex = TheLongestYear.Core.VaultRules.BundleIndexForSeason(s);
            }
            else
            {
                this.Monitor.Log($"Unknown argument '{args[0]}'.", LogLevel.Warn);
                return;
            }

            if (!_meta.Run.VaultBundlesPaid.Contains(bundleIndex))
                _meta.Run.VaultBundlesPaid.Add(bundleIndex);
            this.Monitor.Log(
                $"Vault bundle {bundleIndex} marked paid. Paid this run: [{string.Join(", ", _meta.Run.VaultBundlesPaid)}]",
                LogLevel.Info);
        }

        /// <summary>Merge GameplayConfig.DefaultThemeOverrides + user ThemeOverrides for the catalog builder.</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Theme> ParseThemeOverrides()
        {
            var merged = new System.Collections.Generic.Dictionary<string, TheLongestYear.Core.Theme>();

            foreach (var kv in TheLongestYear.Core.GameplayConfig.DefaultThemeOverrides)
                if (System.Enum.TryParse(kv.Value, ignoreCase: true, out TheLongestYear.Core.Theme t))
                    merged[kv.Key] = t;

            if (_config?.ThemeOverrides != null)
            {
                foreach (var kv in _config.ThemeOverrides)
                {
                    if (System.Enum.TryParse(kv.Value, ignoreCase: true, out TheLongestYear.Core.Theme t))
                        merged[kv.Key] = t;
                    else
                        this.Monitor.Log(
                            $"ThemeOverrides: '{kv.Value}' is not a valid theme for id '{kv.Key}' — ignoring.",
                            LogLevel.Warn);
                }
            }

            return merged;
        }
    }
}
