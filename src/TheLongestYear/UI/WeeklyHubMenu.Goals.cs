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

        // ---------- lowered goals: "13 (was 27)" ----------
        // The theme week discount lowers a picked theme's goals for the week. The icon's stack badge
        // shows the lowered ask; a "was 27" label under the icon shows the full one, before the pick
        // and all week, so the number reads as temporary (Jeff, 2026-10-20).

        /// <summary>Per icon, the full ask to show under it, or null. Parallel to _leftBonus / _rightBonus.</summary>
        private readonly List<int?> _leftFullAsks = new List<int?>();
        private readonly List<int?> _rightFullAsks = new List<int?>();

        private List<int?> FullAsksFor(int slot) => slot == LeftSlot ? _leftFullAsks : _rightFullAsks;

        /// <summary>Extra card height for the label row, reserved whenever this run's difficulty lowers
        /// goals, so the panel keeps one size across rerolls.</summary>
        private const int LoweredLabelRowHeight = 38;
        private const int LoweredLabelTopGap = 16;   // clears the stack badge, which hangs below the icon
        private const int LoweredLabelSidePad = 20;
        /// <summary>How far an icon's stack badge reaches past its right edge; kept off the card frame.</summary>
        private const int StackBadgeOverhang = 12;
        private static readonly Color LoweredLabelColor = new Color(34, 110, 34);

        private bool AnyLoweredShown
            => _runController.WeekDiscount > 0
               || _leftFullAsks.Exists(f => f.HasValue) || _rightFullAsks.Exists(f => f.HasValue);

        private int CardHeight => BaseCardHeight + (AnyLoweredShown ? LoweredLabelRowHeight : 0);

        private int BonusBottomMargin => BaseBonusBottomMargin + (AnyLoweredShown ? LoweredLabelRowHeight : 0);

        private static string LoweredLabel(int full)
            => Strings.Get("goal.was-short", new Dictionary<string, string> { ["full"] = full.ToString() });

        /// <summary>" (was 27)" for the icon's hover, or "".</summary>
        private static string WasTag(List<int?> fullAsks, int index)
            => index < fullAsks.Count && fullAsks[index] is int full
                ? Strings.Get("goal.was-tag", new Dictionary<string, string> { ["full"] = full.ToString() })
                : "";

        /// <summary>Icon spacing for one card: wide enough for the widest label, never wider than the
        /// card's inner width allows (labels scale down to fit instead, see DrawLoweredLabels).</summary>
        private static int BonusIconPitch(List<int?> fullAsks, int count)
        {
            int pitch = BonusIconSize + BonusIconGap;
            if (count <= 1) return pitch;
            int widest = 0;
            foreach (int? full in fullAsks)
                if (full is int f)
                    widest = Math.Max(widest, (int)Math.Ceiling(Game1.smallFont.MeasureString(LoweredLabel(f)).X));
            int maxPitch = (CardWidth - CardInnerPad * 2 - BonusIconSize - StackBadgeOverhang * 2) / (count - 1);
            return Math.Max(pitch, Math.Min(widest + LoweredLabelSidePad, maxPitch));
        }

        /// <summary>"was 27" centred under each lowered icon, scaled down when the card is crowded.</summary>
        private static void DrawLoweredLabels(Microsoft.Xna.Framework.Graphics.SpriteBatch b,
            List<Rectangle> bounds, List<int?> fullAsks)
        {
            int pitch = bounds.Count > 1 ? bounds[1].X - bounds[0].X : CardWidth - CardInnerPad * 2;
            for (int i = 0; i < bounds.Count && i < fullAsks.Count; i++)
            {
                if (fullAsks[i] is not int full) continue;
                string label = LoweredLabel(full);
                Vector2 size = Game1.smallFont.MeasureString(label);
                float scale = Math.Min(1f, (pitch - 4) / Math.Max(1f, size.X));
                Rectangle icon = bounds[i];
                var pos = new Vector2(icon.X + (icon.Width - size.X * scale) / 2f, icon.Bottom + LoweredLabelTopGap);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, pos, LoweredLabelColor, scale);
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
