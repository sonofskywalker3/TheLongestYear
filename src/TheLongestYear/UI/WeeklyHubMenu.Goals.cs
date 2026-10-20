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
            List<int?> fullAskDest, IReadOnlyList<BonusSlot> ownedByOtherCard = null)
        {
            dest.Clear();
            favouredDest.Clear();
            fullAskDest.Clear();
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
                fullAskDest.Add(item == null ? null : LoweredAsk.FullAsk(slot));
            }
        }

        // ---------- lowered goals: "13 (27)" ----------
        // The theme week discount lowers a picked theme's goals for the week. A lowered icon's stack
        // count reads "13 (27)": the lowered ask, then the full one in brackets, in the count's own
        // pixel digits, before the pick and all week, so the number reads as temporary (Jeff, 2026-10-20).

        /// <summary>Per icon, the full ask to show beside its count, or null. Parallel to _leftBonus / _rightBonus.</summary>
        private readonly List<int?> _leftFullAsks = new List<int?>();
        private readonly List<int?> _rightFullAsks = new List<int?>();

        private List<int?> FullAsksFor(int slot) => slot == LeftSlot ? _leftFullAsks : _rightFullAsks;

        /// <summary>The stack count's digit scale at the hub's icon size (Item.DrawMenuIcons uses 3 x scale).</summary>
        private const float CountScale = 3f * BonusIconScale;
        /// <summary>drawInMenu centres the sprite in a 64-pixel frame from the icon's top-left.</summary>
        private const int IconFrameCentre = 32;
        /// <summary>Top of every count below the icon's top: centred under the sprite and below the
        /// quality star (sprite ends at 56, star at 61), so each count reads as its own icon's caption.</summary>
        private const int CountTop = 60;
        /// <summary>Extra card height so the counts under the icons clear the card frame.</summary>
        private const int CountRoom = 10;
        /// <summary>Clear space wanted between neighbouring icons (sprite or count, whichever is wider).</summary>
        private const int CountGap = BonusIconGap;
        /// <summary>Least clear space before a crowded card scales its counts down.</summary>
        private const int CountMinGap = 6;
        /// <summary>Extra room a count wider than its sprite takes, so two side-by-side counts get
        /// CountGap plus this between them (a wide count reads as one word otherwise).</summary>
        private const int CountPad = 8;

        /// <summary>Per card, the factor its counts are drawn at (1 unless the row is crowded).</summary>
        private float _leftCountShrink = 1f;
        private float _rightCountShrink = 1f;

        private float CountShrinkFor(int slot) => slot == LeftSlot ? _leftCountShrink : _rightCountShrink;

        private const int CardHeight = BaseCardHeight + CountRoom;

        private const int BonusBottomMargin = BaseBonusBottomMargin + CountRoom;

        private static int? FullAskAt(List<Item> items, List<int?> fullAsks, int index)
            => index < fullAsks.Count && index < items.Count && items[index] != null ? fullAsks[index] : null;

        /// <summary>" (27)" for the icon's hover, or "".</summary>
        private static string FullTag(List<int?> fullAsks, int index)
            => index < fullAsks.Count && fullAsks[index] is int full
                ? Strings.Get("goal.full-tag", new Dictionary<string, string> { ["full"] = full.ToString() })
                : "";

        /// <summary>Room each icon's count ("13 (27)" or a plain "5") takes at <paramref name="scale"/>,
        /// with <see cref="CountPad"/> added; 0 for an empty icon.</summary>
        private static IReadOnlyList<float> CountWidths(List<Item> items, List<int?> fullAsks, float scale)
        {
            var widths = new float[items.Count];
            for (int i = 0; i < items.Count; i++)
                widths[i] = items[i] == null ? 0f : TinyCount.Width(items[i].Stack, FullAskAt(items, fullAsks, i), scale) + CountPad;
            return widths;
        }

        /// <summary>One card's icon row: every icon takes the wider of its sprite and its count, with a
        /// clear gap between neighbours, so a count never runs into the next icon's. A crowded card
        /// closes the gap to <see cref="CountMinGap"/>, then shrinks its counts to stay inside the frame.</summary>
        private static IconCountRow.Plan PlanBonusRow(List<Item> items, List<int?> fullAsks)
            => IconCountRow.Fit(shrink => CountWidths(items, fullAsks, CountScale * shrink), BonusIconSize,
                CardWidth - CardInnerPad * 2, CountGap, CountMinGap);

        /// <summary>A goal's count ("13 (27)" when lowered, else the plain number, "1" included)
        /// centred under its icon's sprite.</summary>
        private static void DrawGoalCount(Microsoft.Xna.Framework.Graphics.SpriteBatch b,
            Rectangle icon, int now, int? full, float shrink)
        {
            float scale = CountScale * shrink;
            float width = TinyCount.Width(now, full, scale);
            var pos = new Vector2((int)(icon.X + IconFrameCentre - width / 2f), icon.Y + CountTop);
            TinyCount.Draw(b, now, full, pos, scale, 0.87f, Color.White);
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
