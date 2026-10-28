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
            "Usage: tly_focus show | deal | pick <tools|buildings|knacks> <chainId> | card <n> | buy <tools|buildings|knacks> [keeponly] | reset";

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
                case "card":
                {
                    // The open pick menu's own click path, so the menu advances and closes as in play.
                    if (args.Length < 2 || !int.TryParse(args[1], out int index))
                    { this.Monitor.Log(FocusUsage, LogLevel.Warn); return; }
                    if (StardewValley.Game1.activeClickableMenu is not TheLongestYear.UI.FocusPickMenu menu)
                    { this.Monitor.Log("tly_focus card: no focus card pick is open.", LogLevel.Warn); return; }
                    if (!menu.PickAt(index))
                        this.Monitor.Log($"tly_focus card: there is no card {index} on the open pick.", LogLevel.Warn);
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

        /// <summary>tly_favour [itemId|clear]: force this week's favoured goal (debug). An item id
        /// flags the matching committed line in whichever list holds it and clears the rest of that
        /// list; clear removes every flag; no argument only logs. Logs both lists' flags after.</summary>
        private void CmdFavour(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (!Context.IsMainPlayer) { this.Monitor.Log("tly_favour: host only.", LogLevel.Warn); return; }

            RunState run = _meta.Run;
            if (args.Length > 0)
            {
                if (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (BonusSlot s in run.CurrentWeekBonusSlots) s.Favoured = false;
                    foreach (BonusSlot s in run.SecondWeekBonusSlots ?? new System.Collections.Generic.List<BonusSlot>()) s.Favoured = false;
                    this.Monitor.Log("tly_favour: every favoured flag cleared.", LogLevel.Info);
                }
                else if (!FavourIn(run.CurrentWeekBonusSlots, args[0]) && !FavourIn(run.SecondWeekBonusSlots, args[0]))
                {
                    this.Monitor.Log($"tly_favour: '{args[0]}' is not one of this week's committed goals.", LogLevel.Warn);
                }
            }

            LogCommittedGoals("This week's goals", run.CurrentSelection, run.CurrentGoalMultiplier,
                run.LiabilitySuppressedThisWeek, run.CurrentWeekBonusSlots);
            if (run.SecondSelection.HasValue)
                LogCommittedGoals("Double week second list", run.SecondSelection, run.SecondGoalMultiplier,
                    run.SecondLiabilitySuppressedThisWeek, run.SecondWeekBonusSlots);
        }

        /// <summary>Flags the first line in <paramref name="slots"/> whose item is <paramref name="itemId"/>
        /// (qualified, or bare and read as an object) and clears the others. False when none matches.</summary>
        private static bool FavourIn(System.Collections.Generic.List<BonusSlot> slots, string itemId)
        {
            if (slots == null) return false;
            BonusSlot match = slots.FirstOrDefault(s => SameItem(s.ItemId, itemId));
            if (match == null) return false;
            foreach (BonusSlot s in slots) s.Favoured = ReferenceEquals(s, match);
            return true;
        }

        private static bool SameItem(string slotItemId, string typed)
            => string.Equals(slotItemId, typed, StringComparison.OrdinalIgnoreCase)
               || string.Equals(slotItemId, "(O)" + typed, StringComparison.OrdinalIgnoreCase);

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
