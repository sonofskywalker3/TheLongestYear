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
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    internal sealed partial class ShrinePreviewMenu
    {
        // ------------------------------------------------------------------ input

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _tabs.Count > 0 ? _tabs[(int)_tab] : getComponentWithID(RowIdBase);
            snapCursorToCurrentSnappedComponent();
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                int id = currentlySnappedComponent.myID;
                if (id >= TabIdBase && id < TabIdBase + _tabs.Count) { SetTab((ShrineTab)(id - TabIdBase)); return; }
                if (id == RestartButtonId && _restartButton != null) { RequestRestart(); return; }
                if (id == ScrollUpId) { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                {
                    int idx = _scrollIndex + (id - RowIdBase);
                    if (idx < _rows.Count) ActivateRow(_rows[idx]);
                    return;
                }
            }
            base.receiveGamePadButton(b);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);   // handles the close button
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].containsPoint(x, y)) { SetTab((ShrineTab)i); return; }
            }
            if (_restartButton != null && _restartButton.containsPoint(x, y)) { RequestRestart(); return; }
            if (_tab == ShrineTab.Donate) { DonateClick(x, y); return; }
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                Row row = _rows[idx];
                int rowY = _listY + i * RowHeight;
                if (row.Kind == RowKind.LockedToggle && _rowSlots[i].containsPoint(x, y))
                {
                    ActivateRow(row);
                    return;
                }
                if (HasButton(row) && BoostButtonBounds(rowY).Contains(x, y))
                {
                    ActivateRow(row);
                    return;
                }
            }
        }

        /// <summary>Gamepad A / click on a row: toggle a Locked section, or buy a boost.</summary>
        private void ActivateRow(Row row)
        {
            if (row.Kind == RowKind.LockedToggle)
            {
                if (!_expandedLocked.Remove(row.Category)) _expandedLocked.Add(row.Category);
                Game1.playSound("shwip");
                BuildRows();
                ClampScroll();
                return;
            }
            if (!HasButton(row) || StateOf(row) != BoostRowState.Buy) return;
            _buyBoost(row.Boost.Id, row.Skill);   // sound, HUD and logging all live in the callback
            BuildRows();                          // the row's control flips to Active on success
            ClampScroll();
        }

        /// <summary>Close the shrine and hand over to the mod's yes/no. The question box would
        /// replace this menu anyway; closing first keeps the hand-off clean. Answering No reopens
        /// the shrine (RunController.AskVoluntaryRestart).</summary>
        private void RequestRestart()
        {
            Game1.playSound("smallSelect");
            exitThisMenuNoSound();
            _requestRestart();
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";
            if (_tab == ShrineTab.Donate) { DonateHover(x, y); return; }

            foreach (var (bounds, item, price, name) in _cartCells)
            {
                if (bounds.Contains(x, y))
                {
                    _hoverText = Strings.Get("menu.shrine-preview.cart-item-hover", new Dictionary<string, string>
                    {
                        ["name"] = name,
                        ["price"] = price.ToString(),
                    });
                    return;
                }
            }
            foreach (var (bounds, day) in _weatherCells)
            {
                if (bounds.Contains(x, y))
                {
                    _hoverText = Strings.Get("menu.shrine-preview.weather-day-hover", new Dictionary<string, string>
                    {
                        ["day"] = day.DayOfMonth.ToString(),
                        ["weather"] = WeatherIcons.Label(day.Weather),
                    });
                    return;
                }
            }

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                if (_rows[idx].Tooltip != null && _rowSlots[i].containsPoint(x, y))
                {
                    _hoverText = _rows[idx].Tooltip;
                    return;
                }
            }
        }
    }
}
