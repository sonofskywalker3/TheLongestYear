using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Suggested keeps in the rewind shop (spec 2026-11-10 shrine planning, feature 1): the
    /// keeps the loop recap suggested sit right below the pinned rows of their own tab, with a green
    /// "!" badge and the reason line. Read from the outgoing-loop summary the recap built
    /// (<see cref="MetaStore.OutgoingLoop"/>), so the shop and the recap always agree. Only the
    /// rewind shrine (the one with banish rows) has one.</summary>
    internal sealed partial class JunimoShrineMenu
    {
        private const string SuggestedBadge = "!";
        private const int BadgeInset = 16;
        private const int ReasonLineY = 56;
        private const int CostToReasonGap = 24;

        private LoopSummary Outgoing => _banishRows ? _store.OutgoingLoop : null;

        /// <summary>A plain buyable keep row the recap suggested. A pinned chain is already on top.</summary>
        private bool IsSuggested(ShrineRow row)
            => row.Def != null && !row.IsFocus && !row.IsBanish && row.Buyable && !IsPinned(row)
               && Outgoing?.SuggestionFor(row.Def.Id) != null;

        private IReadOnlyList<ShrineRow> SuggestedAfterPinned(IReadOnlyList<ShrineRow> rows)
        {
            LoopSummary outgoing = Outgoing;
            if (outgoing == null || outgoing.Suggestions.Count == 0) return rows;
            return KeepSuggestions.AfterPinned(rows, IsPinned, IsSuggested, r => outgoing.RankOf(r.Def?.Id));
        }

        /// <summary>The green "!" in the row's top-right corner and the reason line on the right of
        /// the cost line, shrunk to fit beside it.</summary>
        private void DrawSuggestedMark(SpriteBatch b, ClickableComponent slot, ShrineRow row)
        {
            if (!IsSuggested(row)) return;
            KeepSuggestion suggestion = Outgoing.SuggestionFor(row.Def.Id);

            Vector2 badgeSize = Game1.dialogueFont.MeasureString(SuggestedBadge);
            Utility.drawTextWithShadow(b, SuggestedBadge, Game1.dialogueFont,
                new Vector2(slot.bounds.Right - BadgeInset - badgeSize.X, slot.bounds.Y + 8), LoopRecapMenu.SuggestedGreen);

            string reason = LoopRecapText.Reason(suggestion, Outgoing.HeldBoard);
            long cost = _purchases.EffectiveCost(row.Def);
            string costLine = Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = cost.ToString() })
                + (_store.State.JunimoPoints < cost ? Strings.Get("menu.shrine.insufficient") : "");
            float left = slot.bounds.X + BadgeInset + Game1.smallFont.MeasureString(costLine).X + CostToReasonGap;
            float right = slot.bounds.Right - BadgeInset;
            float width = Game1.smallFont.MeasureString(reason).X;
            float scale = Math.Min(1f, Math.Max(0f, right - left) / Math.Max(1f, width));
            Utility.drawTextWithShadow(b, reason, Game1.smallFont,
                new Vector2(right - width * scale, slot.bounds.Y + ReasonLineY), LoopRecapMenu.SuggestedGreen, scale);
        }
    }
}
