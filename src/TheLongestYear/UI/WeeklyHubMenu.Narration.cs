using System.Collections.Generic;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The hub read aloud for a screen reader: everything the panel and the focused card
    /// draw, as text.</summary>
    internal sealed partial class WeeklyHubMenu : INarratedMenu
    {
        public string NarrationIntro()
        {
            var parts = new List<string>
            {
                _double ? Strings.Get("menu.hub.double-week") : Strings.Get("menu.hub.pick-theme"),
                Strings.Get("menu.hub.banking-tip"),
                Strings.Get("menu.hub.season-mult", new Dictionary<string, string> { ["mult"] = SeasonMultiplierDisplay() }),
            };
            if (_weatherForecast.Length > 0)
            {
                var days = new List<string>();
                foreach (ForecastDay day in _weatherForecast)
                    days.Add(Strings.Get("menu.hub.day-label", new Dictionary<string, string>
                    {
                        ["day"] = day.DayOfMonth.ToString(),
                        ["weather"] = WeatherIcons.Label(day.Weather),
                    }));
                parts.Add(Strings.Get("menu.hub.weather-header") + ": " + string.Join(", ", days));
            }
            parts.Add(Strings.Get(_double ? "narrate.hub.help-double" : "narrate.hub.help"));
            return NarrationText.Sentences(parts);
        }

        /// <summary>The snapped component and the reroll count: a reroll changes both cards in place.</summary>
        public object NarrationFocusKey => (currentlySnappedComponent?.myID ?? -1, _rerollCounter);

        public string NarrateFocus()
        {
            ClickableComponent focus = currentlySnappedComponent;
            if (focus == null) return "";
            if (focus == _leftCard) return NarrateCard(LeftSlot, _leftBonus, _leftFavoured, _leftFullAsks);
            if (focus == _rightCard) return NarrateCard(RightSlot, _rightBonus, _rightFavoured, _rightFullAsks);
            if (focus == _rerollButton) return RerollLabel();
            return "";
        }

        private string NarrateCard(int slot, List<Item> goals, List<bool> favoured, List<int?> fullAsks)
        {
            string side = slot == LeftSlot ? "left" : "right";
            if (_offer.Count <= slot)
                return NarrationText.Sentences(CardName(side, Strings.Get("menu.hub.no-offer")));

            Theme theme = _offer[slot];
            string mult = CardMultiplier.Format(CardMultiplier.ForCard(_run.Seed, OfferWeek, theme, slot, _rand, _double));
            if (IsSealed(slot))
                return NarrationText.Sentences(
                    Strings.Get("narrate.hub.card-" + side + "-sealed"),
                    Strings.Get("menu.hub.mystery-mult", new Dictionary<string, string> { ["mult"] = mult }));

            var parts = new List<string>
            {
                CardName(side, ThemeDisplay.Name(theme)),
                Strings.Get("narrate.hub.bonus", new Dictionary<string, string>
                    { ["text"] = ThemeModifiers.DisplayNameFor(ThemeModifiers.For(theme).BonusId) }),
                Strings.Get("narrate.hub.drawback", new Dictionary<string, string>
                    { ["text"] = ThemeModifiers.DisplayNameFor(DrawbackFor(slot)) }),
            };
            if (ShowsMultiplier)
                parts.Add(Strings.Get("menu.hub.card-mult", new Dictionary<string, string> { ["mult"] = mult }));
            parts.Add(goals.Count == 0
                ? Strings.Get("menu.hub.bonus-none")
                : Strings.Get("menu.hub.bonus-week") + " " + NarrateGoals(goals, favoured, fullAsks));
            return NarrationText.Sentences(parts);
        }

        private static string CardName(string side, string name)
            => Strings.Get("narrate.hub.card-" + side, new Dictionary<string, string> { ["name"] = name });

        /// <summary>"5 Parsnip, 13 (27) gold Wood, The Junimos want this one..." in icon order.</summary>
        private static string NarrateGoals(List<Item> goals, List<bool> favoured, List<int?> fullAsks)
        {
            var lines = new List<string>();
            for (int i = 0; i < goals.Count; i++)
            {
                Item item = goals[i];
                if (item == null) continue;
                string line = Strings.Get(
                    item.Quality > 0 ? "narrate.goal-quality" : "narrate.goal",
                    new Dictionary<string, string>
                    {
                        ["count"] = item.Stack + FullTag(fullAsks, i),
                        ["quality"] = Strings.Get("narrate.quality." + item.Quality),
                        ["name"] = item.DisplayName,
                    });
                if (i < favoured.Count && favoured[i])
                    line += " (" + Strings.Get("goal.favoured-line") + ")";
                lines.Add(line);
            }
            return string.Join(", ", lines);
        }
    }
}
