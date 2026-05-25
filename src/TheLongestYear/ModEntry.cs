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

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<GameplayConfig>();
            _meta = new MetaStore(helper.Data);
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
        }

        /// <summary>Load this playthrough's banked progress when a save opens.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _meta.Load();
            _reset = new WorldResetService(this.Monitor, _meta.State);
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
    }
}
