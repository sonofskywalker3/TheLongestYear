using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The year's three chosen focus cards on the planning hub, display only. With a
    /// weather forecast the strip sits beside the calendar, in the space it leaves free; without
    /// one it gets its own band under the theme cards.</summary>
    internal sealed partial class WeeklyHubMenu
    {
        private const int FocusBandGap = 16;
        private const int FocusBesideWeatherGap = 32;

        /// <summary>The chosen cards with their live quotes. Same static-hook idiom as the shrine's
        /// hooks; unattached or empty, the hub draws no strip and keeps its old size.</summary>
        internal static Func<IReadOnlyList<(FocusHand Hand, FocusQuote Quote)>> FocusCards;

        /// <summary>Read once per open: nothing can be bought while the hub is up.</summary>
        private IReadOnlyList<(FocusHand Hand, FocusQuote Quote)> _focusCards;
        private Rectangle _focusArea = Rectangle.Empty;

        private bool HasFocusStrip
        {
            get
            {
                _focusCards ??= FocusCards?.Invoke() ?? Array.Empty<(FocusHand, FocusQuote)>();
                return _focusCards.Count > 0;
            }
        }

        /// <summary>Beside the weather calendar the strip needs no room of its own.</summary>
        private bool FocusBesideWeather => _weatherSageSlots > 0;

        /// <summary>The panel height the strip adds: a band under the cards when there is no
        /// weather calendar to sit beside.</summary>
        private int FocusBandHeight() => HasFocusStrip && !FocusBesideWeather ? FocusBandGap + FocusCardStrip.CellHeight : 0;

        /// <summary>Places the strip. <paramref name="bandTop"/> is where its own band starts when
        /// there is no weather calendar to sit beside.</summary>
        private void LayoutFocusStrip(int bandTop)
        {
            _focusArea = Rectangle.Empty;
            if (!HasFocusStrip) return;
            int rowX = xPositionOnScreen + PanelPadding;
            int rowRight = xPositionOnScreen + width - PanelPadding;
            if (FocusBesideWeather)
            {
                int left = _weatherBlockX + _weatherForecast.Length * WeatherCellWidth + FocusBesideWeatherGap;
                _focusArea = new Rectangle(left, _weatherBlockY, Math.Max(0, rowRight - left),
                    WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH);
                return;
            }
            _focusArea = new Rectangle(rowX, bandTop, rowRight - rowX, FocusCardStrip.CellHeight);
        }

        private void DrawFocusStrip(SpriteBatch b)
        {
            if (_focusArea.IsEmpty) return;
            // Beside the calendar there is room for icons and prices only; the names show on hover.
            FocusCardStrip.Draw(b, _focusArea, _focusCards, compact: FocusBesideWeather);
        }

        private string FocusStripHover(int x, int y)
            => _focusArea.IsEmpty ? null : FocusCardStrip.HoverAt(_focusArea, _focusCards, x, y);
    }
}
