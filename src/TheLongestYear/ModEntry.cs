using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear
{
    public sealed class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;
        private WorldResetService _reset;
        private RunController _runController;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<GameplayConfig>();
            _meta = new MetaStore(helper.Data);
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
            helper.ConsoleCommands.Add("tly_champion", "Champion one of this week's offered themes. Usage: tly_champion <theme>", this.CmdChampion);
            helper.ConsoleCommands.Add("tly_offer", "Show this week's champion offer.", this.CmdOffer);
            helper.ConsoleCommands.Add("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            helper.ConsoleCommands.Add("tly_runstate", "Print the current run state.", this.CmdRunState);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
        }

        /// <summary>Load this playthrough's banked progress when a save opens.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _meta.Load();
            _reset = new WorldResetService(this.Monitor, _meta.State);
            _runController = new RunController(this.Monitor, _meta, _config, _reset);
            _runController.OnRunLoaded();
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
    }
}
