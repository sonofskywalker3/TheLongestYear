using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>What a game-state-query string says about WHEN. See Task 2's intro in the plan for the
/// three clause classes: temporal (narrows weeks or sets a flag), prerequisite (a note), unknown (a note
/// plus <see cref="Unresolved"/>).</summary>
public sealed record ConditionReading(
    WeekMask Weeks, bool YearTwo, bool FewDays, bool Chance, bool RainOnly, bool Unresolved, bool IslandHint,
    IReadOnlyList<string> Other, IReadOnlySet<int>? DaysOfYear = null);

public static class ConditionSeasons
{
    private const int YearOne = 1;
    private const string IslandMarker = "Island";

    /// <summary>Vanilla queries (GameStateQuery.cs) that gate on the player's progress, the item, the
    /// location or the clock rather than the calendar. The model does not judge them; they become notes.
    /// Exact names, so an unknown key that merely starts the same way stays unresolved.</summary>
    private static readonly HashSet<string> PrerequisiteQueries = new(StringComparer.Ordinal)
    {
        "IS_COMMUNITY_CENTER_COMPLETE", "IS_JOJA_MART_COMPLETE", "IS_GREEN_RAIN_DAY", "IS_HOST", "IS_CUSTOM_FARM_TYPE",
        "IS_EVENT", "IS_ISLAND_NORTH_BRIDGE_FIXED", "IS_VISITING_ISLAND", "IS_MULTIPLAYER", "IS_LOST_BOOK_FOUND",
        "MINE_LOWEST_LEVEL_REACHED", "WORLD_STATE_FIELD", "WORLD_STATE_ID", "LOCATION_ACCESSIBLE",
        "CAN_BUILD_CABIN", "CAN_BUILD_FOR_CABINS", "FARM_CAVE", "FARM_NAME", "FARM_TYPE", "HAS_TARGET_LOCATION",
        "LOCATION_CONTEXT", "LOCATION_HAS_CUSTOM_FIELD", "LOCATION_IS_INDOORS", "LOCATION_IS_OUTDOORS",
        "LOCATION_IS_MINES", "LOCATION_IS_SKULL_CAVE", "LOCATION_NAME", "LOCATION_UNIQUE_NAME", "TIME",
        "ITEM_CATEGORY", "ITEM_CONTEXT_TAG", "ITEM_EDIBILITY", "ITEM_HAS_EXPLICIT_OBJECT_CATEGORY", "ITEM_ID",
        "ITEM_ID_PREFIX", "ITEM_NUMERIC_ID", "ITEM_OBJECT_TYPE", "ITEM_PRICE", "ITEM_QUALITY", "ITEM_STACK", "ITEM_TYPE",
    };

    /// <summary>Prefixes of whole query families that are all prerequisites (PLAYER_HEARTS, BUILDINGS_CONSTRUCTED...).</summary>
    private static readonly string[] PrerequisitePrefixes = { "PLAYER_", "BUILDINGS_" };

    private static readonly string[] WetWeather = { "Rain", "Storm", "GreenRain" };

    public static ConditionReading Read(string? condition, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        WeekMask weeks = WeekMask.All;
        bool yearTwo = false, fewDays = false, chance = false, rainOnly = false, unresolved = false, island = false;
        var other = new List<string>();
        HashSet<int>? days = null;
        void PinDays(IEnumerable<int> d)
        {
            if (days == null) days = new HashSet<int>(d);
            else days.IntersectWith(d);
        }
        if (string.IsNullOrWhiteSpace(condition))
            return new ConditionReading(weeks, false, false, false, false, false, false, other);

        foreach (string rawClause in SplitClauses(condition))
        {
            string clause = rawClause.Trim();
            if (clause.Length == 0) continue;
            bool negated = clause.StartsWith("!", StringComparison.Ordinal);
            if (!negated && clause.Contains(IslandMarker, StringComparison.OrdinalIgnoreCase)) island = true;
            string[] tokens = (negated ? clause.Substring(1) : clause).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string key = tokens[0].ToUpperInvariant();

            WeekMask? clauseWeeks = null;
            switch (key)
            {
                case "TRUE":
                    if (negated) weeks = WeekMask.None;
                    break;
                case "FALSE":
                    if (!negated) weeks = WeekMask.None;
                    break;
                case "SEASON":
                    clauseWeeks = SeasonsIn(tokens.Skip(1));
                    break;
                case "LOCATION_SEASON":
                    clauseWeeks = SeasonsIn(tokens.Skip(2));
                    break;
                case "SEASON_DAY":
                    clauseWeeks = SeasonDays(tokens);
                    fewDays = true;
                    if (!negated) PinDays(SeasonDayPins(tokens));
                    break;
                case "DAY_OF_MONTH":
                    fewDays = true;
                    if (negated) { other.Add(clause); unresolved = true; break; }
                    // "even" / "odd" (GameStateQuery.cs 282-289): every week still has such days.
                    if (tokens.Skip(1).Any(t => t.Equals("even", StringComparison.OrdinalIgnoreCase) || t.Equals("odd", StringComparison.OrdinalIgnoreCase)))
                        break;
                    clauseWeeks = DaysOfMonth(tokens.Skip(1));
                    PinDays(DaysOfMonthPins(tokens.Skip(1)));
                    break;
                case "DAY_OF_WEEK":
                    fewDays = true;
                    break;
                case "DAYS_PLAYED":
                    // "DAYS_PLAYED min [max]" (GameStateQuery.cs 310-322); in a loop, days played is the day of the year.
                    if (negated) { other.Add(clause); unresolved = true; break; }
                    if (tokens.Length > 1 && int.TryParse(tokens[1], out int minDays))
                    {
                        int maxDays = tokens.Length > 2 && int.TryParse(tokens[2], out int m) ? m : Calendar.DaysPerYear;
                        int clampedMin = Math.Max(1, minDays);
                        int clampedMax = Math.Clamp(maxDays, 1, Calendar.DaysPerYear);
                        weeks &= WeekMask.Range(WeekMask.WeekOfDay(clampedMin), WeekMask.WeekOfDay(clampedMax));
                        if (clampedMin <= clampedMax) PinDays(Enumerable.Range(clampedMin, clampedMax - clampedMin + 1));
                    }
                    break;
                case "IS_PASSIVE_FESTIVAL_OPEN":
                case "IS_PASSIVE_FESTIVAL_TODAY":
                    if (tokens.Length > 1 && festivals.TryGetValue(tokens[1], out FestivalDates? festival))
                    {
                        clauseWeeks = festival.Weeks;
                        fewDays = true;
                        if (!negated)
                        {
                            int startDoy = Calendar.DayOfYear((int)festival.Season, festival.StartDay);
                            int endDoy = Calendar.DayOfYear((int)festival.Season, festival.EndDay);
                            PinDays(Enumerable.Range(startDoy, endDoy - startDoy + 1));
                        }
                    }
                    else { other.Add(clause); unresolved = true; }
                    break;
                case "YEAR":
                    // "YEAR min [max]" (GameStateQuery.cs 392-404).
                    if (tokens.Length > 1 && int.TryParse(tokens[1], out int minYear))
                    {
                        int maxYear = tokens.Length > 2 && int.TryParse(tokens[2], out int my) ? my : int.MaxValue;
                        bool yearOnePasses = minYear <= YearOne && maxYear >= YearOne;
                        if (negated ? yearOnePasses : !yearOnePasses)
                        {
                            if (negated || maxYear < YearOne) weeks = WeekMask.None; else yearTwo = true;
                        }
                    }
                    break;
                case "WEATHER":
                    if (!negated && tokens.Length > 2 && tokens.Skip(2).All(w => WetWeather.Contains(w, StringComparer.OrdinalIgnoreCase)))
                        rainOnly = true;
                    else other.Add(clause);
                    break;
                case "RANDOM":
                case "SYNCED_RANDOM":
                case "SYNCED_CHOICE":
                case "SYNCED_SUMMER_RAIN_RANDOM":
                case "SYNCED_DAY_RANDOM":
                    chance = true;
                    break;
                default:
                    other.Add(clause);
                    if (!PrerequisiteQueries.Contains(key) && !PrerequisitePrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                        unresolved = true;
                    break;
            }

            if (clauseWeeks is WeekMask w)
                weeks &= negated ? WeekMask.All.Except(w) : w;
        }
        return new ConditionReading(weeks, yearTwo, fewDays, chance, rainOnly, unresolved, island, other, days);
    }

    /// <summary>Whether one clause reading is open on <paramref name="day"/>: within
    /// <paramref name="alsoWithin"/> and the reading's own weeks, and, when the reading pins exact
    /// days, only on those days. A caller combining this with a further day-range restriction (a
    /// festival's exact dates) should AND it into the same predicate rather than intersect two
    /// tables afterward: <see cref="DayTable.Latest"/> is not an intersection, so it can report a
    /// day where only one side is open.</summary>
    public static bool IsAvailableOn(ConditionReading reading, WeekMask alsoWithin, int day)
    {
        int week = WeekMask.WeekOfDay(day);
        return alsoWithin.Contains(week) && reading.Weeks.Contains(week)
               && (reading.DaysOfYear == null || reading.DaysOfYear.Contains(day));
    }

    /// <summary>A same-day table for one clause reading: available on a day when
    /// <see cref="IsAvailableOn"/> says so.</summary>
    public static DayTable Availability(ConditionReading reading, WeekMask alsoWithin)
        => DayTable.Available(day => IsAvailableOn(reading, alsoWithin, day));

    public static ObtainConditions Apply(ObtainConditions conditions, ConditionReading reading)
        => conditions with
        {
            Requires = conditions.Requires.Concat(reading.Other).ToList(),
            YearTwo = conditions.YearTwo || reading.YearTwo,
            FewDays = conditions.FewDays || reading.FewDays,
            RainOnly = conditions.RainOnly || reading.RainOnly,
            Unresolved = conditions.Unresolved || reading.Unresolved,
            GingerIsland = conditions.GingerIsland || reading.IslandHint,
        };

    /// <summary>Splits on commas outside double quotes (ANY "a, b" "c" keeps its arguments together).</summary>
    private static IEnumerable<string> SplitClauses(string condition)
    {
        int start = 0;
        bool quoted = false;
        for (int i = 0; i < condition.Length; i++)
        {
            if (condition[i] == '"') quoted = !quoted;
            else if (condition[i] == ',' && !quoted)
            {
                yield return condition.Substring(start, i - start);
                start = i + 1;
            }
        }
        yield return condition.Substring(start);
    }

    private static WeekMask SeasonsIn(IEnumerable<string> tokens)
    {
        WeekMask mask = WeekMask.None;
        foreach (string token in tokens)
            if (Enum.TryParse(token, ignoreCase: true, out Season season))
                mask |= WeekMask.ForSeason(season);
        return mask;
    }

    /// <summary>"SEASON_DAY season day [season day...]": each pair is one day of one season.</summary>
    private static WeekMask SeasonDays(string[] tokens)
    {
        WeekMask mask = WeekMask.None;
        for (int i = 1; i + 1 < tokens.Length; i += 2)
            if (Enum.TryParse(tokens[i], ignoreCase: true, out Season season)
                && int.TryParse(tokens[i + 1], out int day) && day is >= 1 and <= Calendar.DaysPerMonth)
                mask |= WeekMask.Of(WeekMask.WeekOfDay(Calendar.DayOfYear((int)season, day)));
        return mask;
    }

    /// <summary>"DAY_OF_MONTH day [day...]": those days in every season.</summary>
    private static WeekMask DaysOfMonth(IEnumerable<string> tokens)
    {
        WeekMask mask = WeekMask.None;
        foreach (string token in tokens)
            if (int.TryParse(token, out int day) && day is >= 1 and <= Calendar.DaysPerMonth)
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    mask |= WeekMask.Of(WeekMask.WeekOfDay(Calendar.DayOfYear(season, day)));
        return mask;
    }

    /// <summary>The exact days "SEASON_DAY season day [season day...]" pins, one per pair.</summary>
    private static IEnumerable<int> SeasonDayPins(string[] tokens)
    {
        for (int i = 1; i + 1 < tokens.Length; i += 2)
            if (Enum.TryParse(tokens[i], ignoreCase: true, out Season season)
                && int.TryParse(tokens[i + 1], out int day) && day is >= 1 and <= Calendar.DaysPerMonth)
                yield return Calendar.DayOfYear((int)season, day);
    }

    /// <summary>The exact days "DAY_OF_MONTH day [day...]" pins: that day of every season.</summary>
    private static IEnumerable<int> DaysOfMonthPins(IEnumerable<string> tokens)
    {
        foreach (string token in tokens)
            if (int.TryParse(token, out int day) && day is >= 1 and <= Calendar.DaysPerMonth)
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    yield return Calendar.DayOfYear(season, day);
    }
}
