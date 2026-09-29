using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The theme week discount (spec 2026-09-29-theme-week-discount): picking a theme lowers
    /// its week's goal lines on the board (Easy 50%, Normal 25%, from the stamped Stack size dial),
    /// and every un-donated line goes back to its full ask when the week ends, on a re-pick and at
    /// the month start. Every board write is mirrored into <see cref="MetaState.WrittenBoard"/> so the
    /// load-time manifest check still recognises the board. Host only.</summary>
    internal sealed partial class RunController
    {
        /// <summary>Called after the discount rewrites the board. ModEntry re-reads the vanilla-mode
        /// fingerprint here so ReclassifyIfBoardChanged does not take our own write for another mod's.</summary>
        public System.Action AfterBoardWrite { get; set; }

        /// <summary>Lower this week's goal lines. Runs right after the goal slots are committed.</summary>
        private void ApplyWeekDiscount()
        {
            if (!Context.IsMainPlayer || Run.CurrentWeekBonusSlots.Count == 0) return;
            var board = Game1.netWorldState?.Value?.BundleData;
            if (board == null) return;

            double discount = _store.State.BoardDifficulty(_config).EffectiveWeeklyGoalStackDiscount();
            IReadOnlyList<WeeklyGoalDiscount.StackEdit> edits =
                WeeklyGoalDiscount.Apply(Run.CurrentWeekBonusSlots, board, discount);
            if (edits.Count == 0)
            {
                _monitor.Log($"Theme week discount: nothing to lower for week {Run.WeekOfYear} (discount {discount:P0}).", LogLevel.Trace);
                return;
            }
            Run.DiscountWeek = Run.WeekOfYear;
            WriteBoard(edits);
            _monitor.Log(
                $"Theme week discount ({discount:P0}) for week {Run.WeekOfYear}: " +
                string.Join(", ", Run.CurrentWeekBonusSlots.Where(s => s.OriginalStack > 0)
                    .Select(s => $"{s.ItemId}@{s.BundleName}#{s.IngredientIndex} {s.OriginalStack}->{s.Stack}")) + ".",
                LogLevel.Info);
        }

        /// <summary>Put every un-donated discounted line back to its full ask. A donated line stays
        /// done at the discounted stack.</summary>
        private void RevertWeekDiscount(string why)
        {
            if (!Context.IsMainPlayer) return;
            bool anyDiscounted = Run.CurrentWeekBonusSlots.Any(s => s.OriginalStack > 0);
            if (Run.DiscountWeek < 0 && !anyDiscounted) return;

            int week = Run.DiscountWeek;
            Run.DiscountWeek = -1;
            var board = Game1.netWorldState?.Value?.BundleData;
            IReadOnlyList<WeeklyGoalDiscount.StackEdit> edits =
                WeeklyGoalDiscount.Revert(Run.CurrentWeekBonusSlots, board, IsSlotDonated);
            if (edits.Count > 0)
                WriteBoard(edits);
            _monitor.Log(
                $"Theme week discount for week {week} ended ({why}): {edits.Count} un-donated line(s) back to the full ask.",
                LogLevel.Info);
        }

        /// <summary>Day start: the discount belongs to one week, so a new week (or month) takes it off.</summary>
        private void RevertWeekDiscountIfStale(int todayWeekOfYear)
        {
            if (Run.DiscountWeek >= 0 && Run.DiscountWeek != todayWeekOfYear)
                RevertWeekDiscount($"week {todayWeekOfYear} started");
        }

        private static bool IsSlotDonated(BonusSlot slot)
        {
            if (slot.Deposited) return true;
            bool[] state = SlotStateForBundle(slot.BundleIndex);
            return state != null && slot.IngredientIndex < state.Length && state[slot.IngredientIndex];
        }

        /// <summary>Same write as BoardRepairService.ClampUnstackableAsks: the live board, the stored
        /// copy of the written board, then the CC's ingredient cache.</summary>
        private void WriteBoard(IReadOnlyList<WeeklyGoalDiscount.StackEdit> edits)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return;

            Dictionary<string, string> stored = _store.State.WrittenBoard;
            if (stored != null)
                foreach (KeyValuePair<string, string> update in WeeklyGoalDiscount.ApplyEdits(stored, edits))
                    stored[update.Key] = update.Value;

            Dictionary<string, string> updates = WeeklyGoalDiscount.ApplyEdits(worldState.BundleData, edits);
            if (updates.Count == 0) return;
            worldState.SetBundleData(updates);
            (Game1.getLocationFromName("CommunityCenter") as CommunityCenter)?.refreshBundlesIngredientsInfo();
            AfterBoardWrite?.Invoke();
        }
    }
}
