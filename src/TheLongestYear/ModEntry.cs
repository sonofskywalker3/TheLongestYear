using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    public sealed class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<GameplayConfig>();
            _meta = new MetaStore(helper.Data);

            this.Monitor.Log(
                $"The Longest Year loaded. JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state.", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points (debug). Usage: tly_addjp <amount>", this.AddJp);
        }

        private void PrintMeta(string command, string[] args)
        {
            MetaState s = _meta.State;
            this.Monitor.Log(
                $"JP={s.JunimoPoints}, StashTier={s.StashCapacityTier}, Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
                LogLevel.Info);
        }

        private void AddJp(string command, string[] args)
        {
            if (args.Length < 1 || !long.TryParse(args[0], out long amount))
            {
                this.Monitor.Log("Usage: tly_addjp <amount>", LogLevel.Warn);
                return;
            }

            _meta.State.JunimoPoints += amount;
            _meta.Save();
            this.Monitor.Log($"JP is now {_meta.State.JunimoPoints} (saved).", LogLevel.Info);
        }
    }
}
