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
        // ------------------------------------------------------------------ foresight data

        private void BuildForesight()
        {
            int weatherTier = _state.HighestKeptTier("weather_sage_", 6);
            _weatherDays = weatherTier > 0
                ? WeatherForecast.Build(
                    (int)Game1.uniqueIDForThisGame, (int)Game1.stats.DaysPlayed,
                    Game1.dayOfMonth, (int)Game1.season, weatherTier,
                    GreenRainDay.VanillaSummerDay())
                : Array.Empty<ForecastDay>();
            // Rain Dance / Storm Call: slot 0 is tomorrow; show the bought weather, not the schedule.
            if (_run != null && _weatherDays.Length > 0 && _run.WeatherOverride != null
                && _run.WeatherOverrideDay == Today + 1)
                _weatherDays[0] = _weatherDays[0] with { Weather = _run.WeatherOverride };

            _cartItems.Clear();
            _cartHeader = null;
            _cartEmptyNote = null;
            _showCartBlock = _state.HasUpgrade("cart_whisper_1")
                && CartCatalogIntegration.Available(Game1.player);
            if (!_showCartBlock)
                return;

            bool catalogAnyDay = CartCatalogIntegration.Available(Game1.player);
            bool cartInTown = TravelingCartVisitsToday(Game1.dayOfMonth);
            if (!cartInTown && !catalogAnyDay)
            {
                int? nextDay = NextCartVisitDay(Game1.dayOfMonth);
                _cartHeader = nextDay == null
                    ? Strings.Get("menu.shrine-preview.cart-away-season")
                    : Strings.Get("menu.shrine-preview.cart-away", new Dictionary<string, string>
                    {
                        ["day"] = ShortDayName(nextDay.Value),
                    });
                _cartEmptyNote = "";
                return;
            }

            try
            {
                var stock = StardewValley.Internal.ShopBuilder.GetShopStock("Traveler");
                foreach (var pair in stock)
                {
                    if (pair.Key is not Item item) continue;
                    if (!BundleRelevanceIndex.IsRelevant(item)) continue;
                    _cartItems.Add((pair.Key, pair.Value.Price, pair.Key.DisplayName));
                }
            }
            catch (Exception ex)
            {
                TheLongestYear.Loop.PatchLog.WarnOnce("shrine-cart-stock",
                    $"Planning shrine: the Traveling Cart stock could not be built ({ex.GetType().Name}: {ex.Message}); the Foresight tab shows no cart items.");
                _cartItems.Clear();
            }

            _cartHeader = (catalogAnyDay && !cartInTown)
                ? Strings.Get("menu.shrine-preview.cart-catalog-header")
                : Strings.Get("menu.shrine-preview.cart-traveling-header");
            if (_cartItems.Count == 0)
                _cartEmptyNote = Strings.Get("menu.shrine-preview.cart-nothing");
        }

        /// <summary>Cart days for the week starting at <paramref name="weekStart"/>: this week's stored
        /// roll when random, a computed (not stored) roll for later weeks, vanilla when off.</summary>
        private IReadOnlyList<int> CartDaysForWeek(int weekStart)
        {
            int seasonIndex = Game1.seasonIndex;
            if (!CartDaysPatch.RandomOn(seasonIndex, weekStart))
                return CartSchedule.VanillaDaysInWeek(weekStart);
            if (weekStart == CartSchedule.WeekStartOf(Game1.dayOfMonth))
                return CartDaysPatch.DaysFor(seasonIndex, weekStart);
            int week = Calendar.WeekOfYear(seasonIndex, weekStart);
            return CartSchedule.RandomDaysInWeek(CartDaysPatch.RunProvider().Seed, week, weekStart, CartSchedule.BlockedDays(seasonIndex));
        }

        private bool TravelingCartVisitsToday(int dayOfMonth)
            => CartDaysForWeek(CartSchedule.WeekStartOf(dayOfMonth)).Contains(dayOfMonth);

        private static string ShortDayName(int dayOfMonth) => Game1.shortDayDisplayNameFromDayOfSeason(dayOfMonth);

        /// <summary>The next day this season the cart is in town, or null when none are left.</summary>
        private int? NextCartVisitDay(int today)
        {
            for (int weekStart = CartSchedule.WeekStartOf(today); weekStart <= WeatherScheduler.DaysPerMonth; weekStart += 7)
            {
                foreach (int d in CartDaysForWeek(weekStart))
                    if (d > today) return d;
            }
            return null;
        }

        private int ForesightPanelHeight()
        {
            if (!ShowForesight) return 0;
            int h = 0;
            if (_weatherDays.Length > 0)
                h += WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH + ForesightBlockGap;
            if (_showCartBlock)
                h += CartHeaderH + CartIconRowH + ForesightBlockGap;
            return h;
        }

        private void LayoutForesight()
        {
            _weatherCells.Clear();
            _cartCells.Clear();
            _weatherHeaderY = -1;
            _cartHeaderY = -1;
            if (!ShowForesight) return;

            int fy = yPositionOnScreen + TabsTop + TabStripH;

            if (_weatherDays.Length > 0)
            {
                _weatherHeaderY = fy;
                int numY = fy + WeatherHeaderH;
                for (int i = 0; i < _weatherDays.Length; i++)
                {
                    int cellX = _listX + i * WeatherCellWidth;
                    var bounds = new Rectangle(cellX, numY, WeatherCellWidth, WeatherNumberRowH + WeatherIconRowH);
                    _weatherCells.Add((bounds, _weatherDays[i]));
                }
                fy += WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH + ForesightBlockGap;
            }

            if (_showCartBlock)
            {
                _cartHeaderY = fy;
                int iconY = fy + CartHeaderH;
                for (int i = 0; i < _cartItems.Count; i++)
                {
                    int cellX = _listX + i * CartIconCell;
                    var bounds = new Rectangle(cellX, iconY, CartIconPx, CartIconPx);
                    _cartCells.Add((bounds, _cartItems[i].Item, _cartItems[i].Price, _cartItems[i].Name));
                }
            }
        }

        private void DrawForesight(SpriteBatch b)
        {
            if (_weatherCells.Count > 0)
            {
                Utility.drawTextWithShadow(b, Strings.Get("menu.shrine-preview.weather-header"), Game1.dialogueFont,
                    new Vector2(_listX, _weatherHeaderY), Game1.textColor);
                int numY = _weatherHeaderY + WeatherHeaderH;
                int iconY = numY + WeatherNumberRowH;
                foreach (var (bounds, day) in _weatherCells)
                {
                    DrawCell(b, new Rectangle(bounds.X + 2, bounds.Y, bounds.Width - 4, bounds.Height));

                    string num = day.DayOfMonth.ToString();
                    Vector2 ns = Game1.smallFont.MeasureString(num);
                    Utility.drawTextWithShadow(b, num, Game1.smallFont,
                        new Vector2(bounds.X + (WeatherCellWidth - ns.X) / 2f, numY), Game1.textColor);

                    var (tex, src) = WeatherIcons.Source(day.Weather);
                    float iconX = bounds.X + (WeatherCellWidth - WeatherIconPx) / 2f;
                    b.Draw(tex, new Vector2(iconX, iconY), src, Color.White, 0f,
                        Vector2.Zero, WeatherIconScale, SpriteEffects.None, 0.9f);
                }
            }

            if (ShowForesight && _showCartBlock)
            {
                Utility.drawTextWithShadow(b, _cartHeader, Game1.dialogueFont,
                    new Vector2(_listX, _cartHeaderY), Game1.textColor);
                if (_cartCells.Count > 0)
                {
                    foreach (var (bounds, item, price, name) in _cartCells)
                        item.drawInMenu(b, new Vector2(bounds.X, bounds.Y), CartIconScale, 1f, 0.9f,
                            StackDrawType.Hide, Color.White, drawShadow: true);
                }
                else if (!string.IsNullOrEmpty(_cartEmptyNote))
                {
                    Utility.drawTextWithShadow(b, _cartEmptyNote, Game1.smallFont,
                        new Vector2(_listX + 24, _cartHeaderY + CartHeaderH + 8), Game1.textColor * 0.8f);
                }
            }
        }

        /// <summary>A faint filled cell with a thin border, drawn from the 1x1 white pixel
        /// (<c>Game1.staminaRect</c>): the calendar-grid backing for a weather column.</summary>
        private static void DrawCell(SpriteBatch b, Rectangle r)
        {
            Color fill = Color.SaddleBrown * 0.10f;
            Color border = Color.SaddleBrown * 0.40f;
            b.Draw(Game1.staminaRect, r, fill);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, 2), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, 2, r.Height), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.Right - 2, r.Y, 2, r.Height), border);
        }
    }
}
