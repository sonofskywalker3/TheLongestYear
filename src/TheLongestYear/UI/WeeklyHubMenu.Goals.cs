using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Each card's goal preview: the item icons and which one carries the favoured x2
    /// (spec 2026-10-19-focus-cards, "The favoured goal (x2)").</summary>
    internal sealed partial class WeeklyHubMenu
    {
        /// <summary>Per icon, true when it shows the x2 badge. Parallel to _leftBonus / _rightBonus.</summary>
        private readonly List<bool> _leftFavoured = new List<bool>();
        private readonly List<bool> _rightFavoured = new List<bool>();

        private List<bool> FavouredFor(int slot) => slot == LeftSlot ? _leftFavoured : _rightFavoured;

        /// <summary>The goals a card shows: the stored list when its theme is already picked this
        /// week (a fresh sample could land on a different favoured line once lines are donated),
        /// otherwise the preview sample.</summary>
        private IReadOnlyList<BonusSlot> ShownGoalsFor(Theme theme, IReadOnlyList<BonusSlot> ownedByOtherCard)
        {
            if (!_isPreSelectForNextMonth && FavouredGoal.StoredListFor(_run, theme) is IReadOnlyList<BonusSlot> stored)
                return stored;

            IReadOnlyList<BonusSlot> sample = PreviewFor(theme);
            if (ownedByOtherCard == null) return sample;
            List<BonusSlot> kept = GoalLists.Dedupe(ownedByOtherCard, sample);
            // Same re-mark as the commit, so the preview shows the line the pick will pay double.
            _runController.MarkFavoured(kept);
            return kept;
        }

        private void ResolveBonusItemsForTheme(Theme? theme, List<Item> dest, List<bool> favouredDest,
            IReadOnlyList<BonusSlot> ownedByOtherCard = null)
        {
            dest.Clear();
            favouredDest.Clear();
            if (theme == null) return;

            foreach (BonusSlot slot in ShownGoalsFor(theme.Value, ownedByOtherCard))
            {
                // Each icon shows ITS slot's true requirement (stack badge + quality star) —
                // 2026-07-09 slot redesign; replaces the global per-id MAX maps here.
                Item item = null;
                try { item = ItemRegistry.Create(slot.ItemId, slot.Stack, slot.Quality, allowNull: true); }
                catch (Exception ex)
                {
                    TheLongestYear.Loop.PatchLog.TraceOnce("hub-item:" + slot.ItemId,
                        $"Planning hub: goal item '{slot.ItemId}' could not be created ({ex.GetType().Name}); its icon is left empty.");
                    item = null;
                }
                dest.Add(item);
                favouredDest.Add(FavouredGoal.ShowsBadge(slot));
            }
        }

        /// <summary>The favoured icon's hover gets the Junimos' line under the item name.</summary>
        private static string WithFavouredLine(string hover, List<bool> favoured, int index)
            => index < favoured.Count && favoured[index]
                ? hover + "\n" + Strings.Get("goal.favoured-line")
                : hover;

        private static void DrawFavouredBadge(Microsoft.Xna.Framework.Graphics.SpriteBatch b,
            List<bool> favoured, int index, Rectangle icon)
        {
            if (index < favoured.Count && favoured[index])
                FavouredBadge.Draw(b, icon, FavouredBadge.HubScale);
        }
    }
}
