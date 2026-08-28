using System.Text;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_wallet: set a wallet item, power event or Stardrop source marker the way the
    /// game would, so the keep_wallet_* / keep_stardrop_* rows can be smoked from the console.
    /// No args prints every marker in WalletKeepTable with its current state.</summary>
    internal static class WalletDebugCommand
    {
        public const string Usage =
            "Debug: set a wallet/Stardrop marker. Usage: tly_wallet [<MailFlag> | event:<id> | stardrop:<source>]. No args lists all.";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            if (args.Length == 0) { Print(monitor, p); return; }

            string arg = args[0];
            if (arg.StartsWith("event:", System.StringComparison.Ordinal))
            {
                string id = arg.Substring("event:".Length);
                p.eventsSeen.Add(id);
                monitor.Log($"tly_wallet: event {id} marked seen.", LogLevel.Info);
                return;
            }
            if (arg.StartsWith("stardrop:", System.StringComparison.Ordinal))
            {
                string source = arg.Substring("stardrop:".Length);
                WalletKeep? keep = WalletKeepTable.TryGet(WalletKeepTable.StardropIdPrefix + source);
                if (keep == null || keep.Kind != WalletKeepKind.Stardrop)
                {
                    monitor.Log($"tly_wallet: unknown Stardrop source '{source}' (fair, fish, mines, sewer, spouse, statue, museum).", LogLevel.Warn);
                    return;
                }
                foreach (string flag in keep.MailFlags) p.mailReceived.Add(flag);
                p.maxStamina.Value += WalletKeepTable.StardropStamina;
                p.stamina = p.maxStamina.Value;
                monitor.Log($"tly_wallet: Stardrop '{source}' claimed ({string.Join(",", keep.MailFlags)}), max stamina now {p.maxStamina.Value}.", LogLevel.Info);
                return;
            }
            p.mailReceived.Add(arg);
            monitor.Log($"tly_wallet: mail '{arg}' added.", LogLevel.Info);
        }

        private static void Print(IMonitor monitor, Farmer p)
        {
            var sb = new StringBuilder("tly_wallet: ");
            foreach (WalletKeep keep in WalletKeepTable.Entries)
            {
                sb.Append(keep.UpgradeId).Append('=');
                if (keep.EventId != null)
                    sb.Append(p.eventsSeen.Contains(keep.EventId) ? "seen" : "unseen");
                else
                    foreach (string flag in keep.MailFlags)
                        sb.Append(flag).Append(p.mailReceived.Contains(flag) ? "+" : "-");
                sb.Append(' ');
            }
            sb.Append("chest100=").Append(p.chestConsumedMineLevels.GetValueOrDefault(100, false))
              .Append(" maxStamina=").Append(p.maxStamina.Value);
            monitor.Log(sb.ToString(), LogLevel.Info);
        }
    }
}
