using System.Collections.Generic;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The planning shrine read aloud for a screen reader: the banked JP, the tabs, and
    /// whichever row, goal or inventory slot the snap is on. The Plan tab's forecast and cart
    /// stock have nothing to snap to, so the open Plan tab reads them.</summary>
    internal sealed partial class ShrinePreviewMenu : INarratedMenu
    {
        public string NarrationIntro()
            => NarrationText.Sentences(
                Strings.Get("menu.shrine-preview.title"),
                BankedLine(),
                Strings.Get("menu.shrine-preview.planning-note"),
                Strings.Get("narrate.preview.help"));

        /// <summary>The spoken text itself: a tab change, a purchase, a pin, a scroll or an opened
        /// Locked section all alter what the same slot says.</summary>
        public object NarrationFocusKey => NarrateFocus();

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            int id = focus.myID;

            if (id >= TabIdBase && id < TabIdBase + _tabOrder.Count)
                return NarrateTab(_tabOrder[id - TabIdBase]);
            if (id == RestartButtonId) return Strings.Get("shrine.restart.button");
            if (focus == upperRightCloseButton) return Strings.Get("narrate.shrine.close");

            if (_tab == ShrineTab.Donate)
            {
                if (id >= GoalSlotIdBase && id < GoalSlotIdBase + DonateGoals.Count)
                    return NarrationText.Sentences(
                        GoalHoverText(id - GoalSlotIdBase),
                        NarrationParts.Position(id - GoalSlotIdBase, DonateGoals.Count));
                if (id >= 0 && id < InventoryCapacity) return NarrateInventorySlot(id);
                return "";
            }

            if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
            {
                int index = _scrollIndex + (id - RowIdBase);
                if (index >= _rows.Count) return "";
                Row row = _rows[index];
                // A row that sells something ends with the JP left, so a purchase is heard.
                return NarrationText.Sentences(
                    NarrateRow(row),
                    NarrationParts.Position(index, _rows.Count),
                    HasButton(row) || row.Kind == RowKind.FocusCard ? BankedLine() : null);
            }
            if (id == ScrollUpId) return NarrationParts.Scroll(true, _scrollIndex, _rowsPerPage, _rows.Count);
            if (id == ScrollDownId) return NarrationParts.Scroll(false, _scrollIndex, _rowsPerPage, _rows.Count);
            return "";
        }

        private string BankedLine()
            => Strings.Get("menu.shrine-preview.banked", new Dictionary<string, string> { ["jp"] = _state.JunimoPoints.ToString() });

        private static string CostText(long cost)
            => Strings.Get("menu.shrine-preview.cost", new Dictionary<string, string> { ["cost"] = cost.ToString() });

        private string InsufficientUnless(long cost)
            => _state.JunimoPoints >= cost ? null : Strings.Get("menu.shrine.insufficient");

        /// <summary>A tab, and for the open one whatever it draws outside its list.</summary>
        private string NarrateTab(ShrineTab tab)
        {
            var parts = new List<string> { NarrationParts.Tab(TabLabel(tab), tab == _tab) };
            if (tab == _tab && tab == ShrineTab.Plan)
                parts.AddRange(ForesightLines());
            if (tab == _tab && tab == ShrineTab.Donate)
            {
                parts.Add(Strings.Get("shrine.donate.header"));
                parts.Add(DonateNote());
            }
            return NarrationText.Sentences(parts);
        }

        /// <summary>The Plan tab's calendar panel as words: the forecast days, then the cart.</summary>
        private IEnumerable<string> ForesightLines()
        {
            if (_weatherCells.Count > 0)
            {
                var days = new List<string>();
                foreach (var (_, day) in _weatherCells)
                    days.Add(WeatherDayText(day));
                yield return Strings.Get("menu.shrine-preview.weather-header") + ": " + string.Join(", ", days);
            }
            if (!_showCartBlock) yield break;
            yield return _cartHeader;
            if (_cartCells.Count > 0)
            {
                var items = new List<string>();
                foreach (var (_, _, price, name) in _cartCells)
                    items.Add(CartItemText(name, price).Replace('\n', ' '));
                yield return string.Join(", ", items);
            }
            else
                yield return _cartEmptyNote;
        }

        /// <summary>Everything a list row draws, plus its hover text.</summary>
        private string NarrateRow(Row row)
        {
            switch (row.Kind)
            {
                case RowKind.Running:
                    return NarrationText.Sentences(row.Text, row.Note, row.Tooltip);
                case RowKind.Boost:
                    return NarrateBoost(row);
                case RowKind.FocusCard:
                {
                    bool buyable = HasFocusButton(row);
                    return NarrationText.Sentences(
                        row.Face.Name,
                        FocusCardStrip.StatusText(row.Hand, row.Quote),
                        buyable ? InsufficientUnless(row.Quote.Price) : null,
                        row.Face.Description,
                        row.Hand.Bought ? null : row.Face.Note);
                }
                case RowKind.Upgrade when row.IsOwned:
                    return NarrationText.Sentences(row.Def.DisplayName, RowHoverText(row));
                case RowKind.Upgrade:
                {
                    long cost = UpgradePricing.EffectiveCost(row.Def, _priceFactor, _state);
                    return NarrationText.Sentences(row.Def.DisplayName, CostText(cost), InsufficientUnless(cost), RowHoverText(row));
                }
                case RowKind.Locked:
                    return NarrationText.Sentences(
                        LockedTitle(row),
                        CostText(UpgradePricing.EffectiveCost(row.Def, _priceFactor, _state)),
                        RowHoverText(row));
                case RowKind.LockedToggle:
                    return _expandedLocked.Contains(row.Category)
                        ? Strings.Get("narrate.preview.locked-open", new Dictionary<string, string> { ["text"] = row.Text })
                        : Strings.Get("narrate.preview.locked-closed", new Dictionary<string, string> { ["text"] = row.Text });
                default:   // Header, Note, FocusText: one drawn line
                    return row.Text;
            }
        }

        private string NarrateBoost(Row row)
        {
            BoostDefinition boost = row.Boost;
            if (!HasButton(row))   // the Crash Course label row above its skill rows
                return NarrationText.Sentences(Strings.Get(boost.NameKey), CrashCourseMultiplier(), row.Tooltip);

            BoostContext ctx = ContextFor(row);
            BoostRowState state = StateOf(row);
            long cost = BoostPricing.CostOf(boost, _run, ctx);
            return NarrationText.Sentences(
                BoostRowName(row, ctx),
                state == BoostRowState.NotAvailable ? null : CostText(cost),
                state == BoostRowState.Buy ? InsufficientUnless(cost) : BoostStateLabel(boost, state, ctx),
                row.Tooltip);
        }

        /// <summary>A slot of the Donate tab's inventory: the item, and whether a click donates it.</summary>
        private string NarrateInventorySlot(int slot)
        {
            IList<Item> items = _inventory?.actualInventory;
            Item item = items != null && slot < items.Count ? items[slot] : null;
            if (item == null) return Strings.Get("narrate.slot-empty");
            string name = Strings.Get("narrate.goal", new Dictionary<string, string>
            {
                ["count"] = item.Stack.ToString(),
                ["name"] = item.DisplayName,
            }) + QualityTags.For(item.Quality);
            return NarrationText.Sentences(
                name,
                CanDonate(item) ? Strings.Get("narrate.preview.can-donate") : Strings.Get("narrate.preview.cannot-donate"));
        }
    }
}
