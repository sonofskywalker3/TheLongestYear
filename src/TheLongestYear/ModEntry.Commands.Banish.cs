using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Debug: tly_banish status | open | list &lt;bundles|items&gt; | pick &lt;bundles|items&gt; &lt;n&gt; | cancel &lt;n&gt;.
        /// open/list/pick need the Junimo Shrine up (tly_openshop or tly_failreset); pick buys exactly as a click.</summary>
        private void CmdBanish(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "status":
                    this.Monitor.Log(
                        $"tly_banish: offerable {BanishRules.IsOfferable(s)}, next costs {_purchases?.BanishCost()} JP (honoured so far {s.BanishesHonoured}), " +
                        $"pending [{string.Join(", ", s.PendingBanishes.Select(p => $"{p.Kind} {p.Key} paid {p.Paid}"))}], " +
                        $"this board [{string.Join(", ", s.BoardBanishes.Select(p => $"{p.Kind} {p.Key}"))}], " +
                        $"refund notice {(s.BanishRefundNotice?.Count ?? 0)}.",
                        LogLevel.Info);
                    return;
                case "cancel":
                    if (args.Length > 1 && int.TryParse(args[1], out int cancelIndex)) _purchases?.CancelBanish(cancelIndex);
                    else this.Monitor.Log("Usage: tly_banish cancel <n>", LogLevel.Warn);
                    return;
            }

            if (Game1.activeClickableMenu is not JunimoShrineMenu shrine)
            {
                this.Monitor.Log("tly_banish: open the Junimo Shrine first (tly_openshop or tly_failreset).", LogLevel.Warn);
                return;
            }
            BanishPickMenu pick = shrine.GetChildMenu() as BanishPickMenu ?? shrine.OpenBanishPick();
            int tab = args.Length > 1 && args[1].ToLowerInvariant().StartsWith("item") ? 1 : 0;
            switch (mode)
            {
                case "open":
                    this.Monitor.Log("tly_banish: banish pick open.", LogLevel.Info);
                    return;
                case "list":
                    this.Monitor.Log($"tly_banish {(tab == 0 ? "bundles" : "items")}:\n  " + string.Join("\n  ", pick.DebugList(tab)), LogLevel.Info);
                    return;
                case "pick":
                    if (args.Length > 2 && int.TryParse(args[2], out int index))
                        this.Monitor.Log($"tly_banish pick: {(pick.DebugPick(tab, index) ? "picked" : "no such row")}.", LogLevel.Info);
                    else
                        this.Monitor.Log("Usage: tly_banish pick <bundles|items> <n>", LogLevel.Warn);
                    return;
                default:
                    this.Monitor.Log("Usage: tly_banish status | open | list <bundles|items> | pick <bundles|items> <n> | cancel <n>", LogLevel.Warn);
                    return;
            }
        }
    }
}
