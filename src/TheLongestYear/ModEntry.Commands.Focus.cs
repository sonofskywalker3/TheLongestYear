using System;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Focus;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        private FocusService _focus;

        private const string FocusUsage =
            "Usage: tly_focus show | deal | pick <tools|buildings|knacks> <chainId> | buy <tools|buildings|knacks> [keeponly] | reset";

        /// <summary>tly_focus: inspect and drive the focus cards headlessly (debug only).</summary>
        private void CmdFocus(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (!Context.IsMainPlayer) { this.Monitor.Log("tly_focus: host only.", LogLevel.Warn); return; }
            if (_focus == null) { this.Monitor.Log("tly_focus: focus service not wired yet.", LogLevel.Warn); return; }

            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "show";
            switch (verb)
            {
                case "show":
                    _focus.EnsureDealt();
                    LogFocusHands();
                    break;
                case "deal":
                    _focus.ForceDeal();
                    this.Monitor.Log("tly_focus: hands re-dealt (debug).", LogLevel.Info);
                    LogFocusHands();
                    break;
                case "pick":
                {
                    if (args.Length < 3 || !TryParseFocusTheme(args[1], out FocusTheme theme))
                    { this.Monitor.Log(FocusUsage, LogLevel.Warn); return; }
                    _focus.EnsureDealt();
                    bool picked = FocusPicks.Choose(_meta.Run, theme, args[2]);
                    this.Monitor.Log(picked
                        ? $"tly_focus: picked '{args[2]}' for {theme}."
                        : $"tly_focus: cannot pick '{args[2]}' for {theme} (not dealt, or {theme} already picked).",
                        picked ? LogLevel.Info : LogLevel.Warn);
                    break;
                }
                case "buy":
                {
                    if (args.Length < 2 || !TryParseFocusTheme(args[1], out FocusTheme theme))
                    { this.Monitor.Log(FocusUsage, LogLevel.Warn); return; }
                    _focus.EnsureDealt();
                    bool keepOnly = args.Length > 2 && args[2].Equals("keeponly", StringComparison.OrdinalIgnoreCase);
                    if (keepOnly) _focus.BuyKeepOnly(theme);
                    else _focus.Buy(theme);
                    break;
                }
                case "reset":
                    _meta.Run.ResetFocus();
                    this.Monitor.Log("tly_focus: hands and picks cleared; the next tly_focus show deals again.", LogLevel.Info);
                    break;
                default:
                    this.Monitor.Log(FocusUsage, LogLevel.Warn);
                    break;
            }
        }

        private static bool TryParseFocusTheme(string text, out FocusTheme theme)
            => Enum.TryParse(text, ignoreCase: true, out theme) && Enum.IsDefined(typeof(FocusTheme), theme);

        private void LogFocusHands()
        {
            RunState run = _meta.Run;
            this.Monitor.Log(
                $"Focus cards: run {run.RunNumber}, dealt for run {run.FocusDealtRun}, JP {_meta.State.JunimoPoints}.",
                LogLevel.Info);
            foreach (FocusHand hand in run.FocusHands.OrderBy(h => h.Theme))
            {
                string status = hand.Chosen == null ? "no pick" : $"picked {hand.Chosen}";
                if (hand.Bought) status += $", bought {hand.BoughtUpgradeId}";
                this.Monitor.Log($"  {hand.Theme}: {(hand.Chains.Count == 0 ? "(empty hand)" : status)}", LogLevel.Info);
                foreach (string chainId in hand.Chains)
                {
                    FocusQuote q = _focus.QuoteFor(chainId);
                    string mark = chainId == hand.Chosen ? "*" : " ";
                    this.Monitor.Log(
                        q == null
                            ? $"   {mark} {chainId}: unknown chain"
                            : $"   {mark} {chainId,-14} world {FocusWorldReader.Rung(chainId)}  {q.Kind,-8} rung {q.TargetRung} " +
                              $"{q.UpgradeId ?? "-"}  {q.Price} JP  grants [{string.Join(", ", q.Grants)}]",
                        LogLevel.Info);
                }
            }
        }
    }
}
