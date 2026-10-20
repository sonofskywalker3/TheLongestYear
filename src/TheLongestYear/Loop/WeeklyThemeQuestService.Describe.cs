using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The weekly quest's goal lines as text.</summary>
    internal sealed partial class WeeklyThemeQuestService
    {
        /// <summary>"DisplayName x5 (gold) - Bundle Name": names the exact slot requirement. An egg
        /// slot needs no color tag: either color fills it (InterchangeableItems). Quality tags via
        /// <see cref="QualityTags"/>.</summary>
        private string DescribeSlot(BonusSlot slot)
        {
            string name = slot.ItemId;
            try
            {
                Item item = ItemRegistry.Create(slot.ItemId, 1, 0, allowNull: true);
                if (item != null) name = item.DisplayName;
            }
            catch (Exception ex)
            {
                // ItemRegistry may throw for malformed ids; fall back to the raw id.
                _monitor.Log($"Weekly quest: goal item '{slot.ItemId}' has no item data ({ex.GetType().Name}); showing the raw id.", LogLevel.Trace);
            }

            string qty = slot.Stack > 1
                ? Strings.Get("quest.weekly.qty", new Dictionary<string, string> { ["count"] = slot.Stack.ToString() })
                : "";
            // A line the theme week discount lowered names its full ask too: " x13 (was 27)".
            if (LoweredAsk.FullAsk(slot) is int full)
                qty += Strings.Get("goal.was-tag", new Dictionary<string, string> { ["full"] = full.ToString() });
            string quality = QualityTags.For(slot.Quality);
            string described = Strings.Get("quest.weekly.slot", new Dictionary<string, string>
            {
                ["item"] = name,
                ["qty"] = qty,
                ["quality"] = quality,
                ["bundle"] = slot.BundleName,
            });
            // Spec 2026-08-28-obtainable-board-2-stretch: a stretch goal asks for an item the
            // model wouldn't otherwise call obtainable yet - tag it so the player isn't confused.
            string tagged = slot.Stretch ? described + Strings.Get("goal.stretch-tag") : described;
            // The card's favoured line pays double per-item JP (spec 2026-10-19-focus-cards). The tag
            // goes once the slot is deposited, like the badge.
            if (FavouredGoal.ShowsBadge(slot)) tagged += Strings.Get("goal.favoured-tag");
            // Spec 2026-08-28-obtainable-board-4-boosts: tag a goal that routes through a Boost
            // (Year-Two Seeds, Sneak Peek) so the player knows why it's askable at all.
            return slot.RouteTag == null
                ? tagged
                : tagged + Strings.Get("goal.route-tag", new Dictionary<string, string> { ["tag"] = slot.RouteTag });
        }

        /// <summary>"DisplayName x5 - Junimo Shrine": a random shrine donation goal, any quality.</summary>
        private string DescribeShrineGoal(ShrineGoal goal)
        {
            string name = goal.ItemId;
            try
            {
                Item item = ItemRegistry.Create(goal.ItemId, 1, 0, allowNull: true);
                if (item != null) name = item.DisplayName;
            }
            catch (Exception ex)
            {
                // ItemRegistry may throw for malformed ids; fall back to the raw id.
                _monitor.Log($"Shrine goal '{goal.ItemId}' has no item data ({ex.GetType().Name}); showing the raw id.", LogLevel.Trace);
            }
            string qty = goal.Stack > 1
                ? Strings.Get("quest.weekly.qty", new Dictionary<string, string> { ["count"] = goal.Stack.ToString() })
                : "";
            return Strings.Get("quest.weekly.shrine-slot", new Dictionary<string, string>
            {
                ["item"] = name,
                ["qty"] = qty,
            });
        }
    }
}
