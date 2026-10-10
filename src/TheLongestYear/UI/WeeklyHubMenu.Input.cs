using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    internal sealed partial class WeeklyHubMenu
    {
        // ---------- input ----------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            // No card-hover tooltip — the bonus/liability text on the card itself is now
            // self-explanatory (2026-05-28 playtest: "remove the tooltip for the benefit
            // section, and just be very clear about what the benefit and drawback are").
            // Per-item bonus-icon tooltips still surface below so the player can see the
            // exact donation quantities.

            CheckBonusIconHover(x, y, _leftBonus, _leftBonusBounds);
            CheckBonusIconHover(x, y, _rightBonus, _rightBonusBounds);

            // Weather calendar cells: "Day N - Weather", same as the planning-shrine board.
            if (string.IsNullOrEmpty(_hoverText))
            {
                foreach (var (bounds, day) in _weatherCells)
                {
                    if (bounds.Contains(x, y))
                    {
                        _hoverText = Strings.Get("menu.hub.day-label", new Dictionary<string, string>
                        {
                            ["day"] = day.DayOfMonth.ToString(),
                            ["weather"] = WeatherIcons.Label(day.Weather),
                        });
                        return;
                    }
                }
            }
        }

        private void CheckBonusIconHover(int x, int y, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                if (items[i] != null && bounds[i].Contains(x, y))
                {
                    // Show quantity in the hover so the player sees "Wood x99 (1.5x)" not just "Wood (1.5x)".
                    int hoverStack = items[i].Stack;
                    string qty = hoverStack > 1
                        ? Strings.Get("menu.hub.qty-suffix", new Dictionary<string, string> { ["count"] = hoverStack.ToString() })
                        : "";
                    _hoverText = Strings.Get("menu.hub.bonus-hover", new Dictionary<string, string>
                    {
                        ["name"] = items[i].DisplayName,
                        ["qty"] = qty,
                    });
                    return;
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_rerollButton != null && _rerollButton.containsPoint(x, y))
            {
                Game1.playSound(TryRerollFromButton(out _) ? "smallSelect" : "cancel");
                return;
            }
            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmSelection(_offer[0], LeftSlot);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmSelection(_offer[1], RightSlot);
        }

        /// <summary>The card click, by theme name, for the tly_select console command: the same
        /// commit path as the mouse (current-week pick or day-28 pre-pick), then the menu closes.
        /// Any theme is accepted, on or off the cards (it is a debug command).</summary>
        public bool ConfirmByName(string themeName)
        {
            if (!System.Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
                return false;
            _forcedPick = !_offer.Contains(theme);
            // A debug pick pays 1x, like every other pick made off the cards.
            ConfirmSelection(theme, NoSlot);
            return true;
        }

        private bool _forcedPick;

        /// <summary>True when the last <see cref="ConfirmSelection"/> actually recorded the pick
        /// (SelectByName can reject it), so debug logs never claim a pick that did not happen.</summary>
        public bool LastPickTook { get; private set; }

        /// <param name="slot">The card position picked (0 left, 1 right); it sets the goal multiplier.</param>
        private void ConfirmSelection(Theme theme, int slot)
        {
            _themePicked = true;
            // Double week: either card (mouse, A button, or a console pick of a card on offer)
            // takes both, left card first (slot 0) and right card second (slot 1).
            if (_double && !_forcedPick)
            {
                _runController.SelectBoth(_offer[LeftSlot], _offer[RightSlot], skipOfferCheck: _rerollCounter > 0);
                LastPickTook = _run.CurrentSelection == _offer[LeftSlot] && _run.SecondSelection == _offer[RightSlot];
                Game1.playSound("smallSelect");
                this.exitThisMenu();
                return;
            }
            if (_isPreSelectForNextMonth)
                _runController.PreSelectForNextMonth(theme, slot);
            else
                // skipOfferCheck whenever the menu has rerolled so picks off the rerolled
                // offer aren't rejected by RunController's canonical OfferForWeek validation.
                // The reroll path already excludes already-selected-this-month themes, so the
                // gameplay rule that matters is preserved. A console pick off the cards is forced.
                _runController.SelectByName(theme.ToString(), skipOfferCheck: _rerollCounter > 0 || _forcedPick, slot: slot);
            LastPickTook = _isPreSelectForNextMonth
                ? _run.NextMonthSelection == theme
                : _run.CurrentSelection == theme;
            Game1.playSound("smallSelect");
            this.exitThisMenu();
        }
    }
}
