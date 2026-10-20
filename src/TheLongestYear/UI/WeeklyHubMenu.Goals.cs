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
        /// <summary>Top of the vanilla stack count below the icon's top (Item.DrawMenuIcons: 64 - 18 x scale + 1).</summary>
        private const float CountTop = 64f - 18f * BonusIconScale + 1f;
        /// <summary>Top of a lowered count on an icon with a quality star: the star sits where the wide
        /// count would start, so the count drops just below it.</summary>
        private const int StarredCountTop = 60;
        /// <summary>Extra card height so a dropped count clears the card frame. Reserved whenever this
        /// run lowers goals so the panel keeps one size across rerolls.</summary>
        private const int LoweredCountDrop = 10;
        /// <summary>Clear space wanted between two neighbouring lowered counts.</summary>
        private const int LoweredCountGap = 10;
        /// <summary>Least clear space before a crowded card scales its counts down.</summary>
        private const int LoweredCountMinGap = 2;

        private bool AnyLoweredShown
            => _runController.WeekDiscount > 0
               || _leftFullAsks.Exists(f => f.HasValue) || _rightFullAsks.Exists(f => f.HasValue);

        private int CardHeight => BaseCardHeight + (AnyLoweredShown ? LoweredCountDrop : 0);

        private int BonusBottomMargin => BaseBonusBottomMargin + (AnyLoweredShown ? LoweredCountDrop : 0);

        private static int? FullAskAt(List<Item> items, List<int?> fullAsks, int index)
            => index < fullAsks.Count && index < items.Count && items[index] != null ? fullAsks[index] : null;

        /// <summary>" (27)" for the icon's hover, or "".</summary>
        private static string FullTag(List<int?> fullAsks, int index)
            => index < fullAsks.Count && fullAsks[index] is int full
                ? Strings.Get("goal.full-tag", new Dictionary<string, string> { ["full"] = full.ToString() })
                : "";

        /// <summary>Width of the widest "13 (27)" count on a card, 0 when none is lowered.</summary>
        private static float WidestLoweredCount(List<Item> items, List<int?> fullAsks)
        {
            float widest = 0f;
            for (int i = 0; i < items.Count; i++)
                if (FullAskAt(items, fullAsks, i) is int full)
                    widest = Math.Max(widest, TinyCount.Width(items[i].Stack, full, CountScale));
            return widest;
        }

        /// <summary>Icon spacing for one card: wide enough that neighbouring "13 (27)" counts don't
        /// touch, never wider than the card's inner width allows (counts scale down to fit instead,
        /// see DrawLoweredCount). A 5-goal card with 3-digit counts on both sides ("100 (134)", 86 px)
        /// still fits at full size: pitch 88, row 438 of the 440 inner width.</summary>
        private static int BonusIconPitch(List<Item> items, List<int?> fullAsks, int count)
        {
            int pitch = BonusIconSize + BonusIconGap;
            float widest = WidestLoweredCount(items, fullAsks);
            if (count <= 1 || widest <= 0f) return pitch;
            int wanted = (int)Math.Ceiling(widest) + LoweredCountGap;
            int maxPitch = (int)((CardWidth - CardInnerPad * 2 - widest) / (count - 1));
            return Math.Max(pitch, Math.Min(wanted, maxPitch));
        }

        /// <summary>"13 (27)" centred on a lowered icon's sprite at the vanilla count's height (just under
        /// the quality star when the icon has one), scaled down only when the card is too crowded.</summary>
        private static void DrawLoweredCount(Microsoft.Xna.Framework.Graphics.SpriteBatch b,
            Rectangle icon, int pitch, int now, int full, bool starred)
        {
            float width = TinyCount.Width(now, full, CountScale);
            float scale = CountScale * Math.Min(1f, (pitch - LoweredCountMinGap) / Math.Max(1f, width));
            width = TinyCount.Width(now, full, scale);
            var pos = new Vector2((int)(icon.X + IconFrameCentre - width / 2f), icon.Y + (starred ? StarredCountTop : CountTop));
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
