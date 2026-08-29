using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The stretch rule (spec 2026-08-28-obtainable-board, section 2). A bundle that gains
/// no reachable item in a season may have that season's gate reach an item whose hard week is
/// inside the season and whose pacing week is at most WindowWeeks past it. Never on Easy, never
/// into Winter. Pure: the same ingredients and model always give the same lines.</summary>
public static class StretchRule
{
    public const int WindowWeeks = 2;

    public static bool Applies(DifficultyStep step) => step != DifficultyStep.Easy;

    public static bool IsReachable(ItemAvailability a, Season season)
        => a.Week <= AvailabilityWeeks.LastWeekOf(season);

    public static bool IsStretchFor(ItemAvailability a, Season season)
        => season != Season.Winter
           && !IsReachable(a, season)
           && a.HardWeekOrPacing <= AvailabilityWeeks.LastWeekOf(season)
           && a.Week <= AvailabilityWeeks.LastWeekOf(season) + WindowWeeks;

    public static IReadOnlyDictionary<string, Season> Lines(IReadOnlyList<string> ingredients, ItemAvailabilityModel model)
    {
        if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
        if (model == null) throw new ArgumentNullException(nameof(model));
        var lines = new Dictionary<string, Season>(StringComparer.Ordinal);
        if (!Applies(model.Step)) return lines;
        foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall })
        {
            bool gainsSomething = ingredients.Any(id =>
            {
                ItemAvailability a = model.For(id);
                bool now = IsReachable(a, season);
                bool before = season != Season.Spring && IsReachable(a, season - 1);
                return now && !before;
            });
            if (gainsSomething) continue;
            string? pick = ingredients
                .Where(id => !lines.ContainsKey(id) && IsStretchFor(model.For(id), season))
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pick != null) lines[pick] = season;
        }
        return lines;
    }
}
