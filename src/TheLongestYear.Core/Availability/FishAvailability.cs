using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Earliest season and effort for a rod or trap fish.
///
/// Why this exists: the fish bundles re-roll their slots from a 52 item pool, while the old hand
/// written pin table named 15 specific fish. A re-rolled bundle was therefore gated only on
/// whichever slots happened to land on one of those 15, and roughly a quarter of boards came out
/// with no season pressure at all.</summary>
public static class FishAvailability
{
    private const int DifficultyBandSize = 20;
    private const int LevelBandSize = 3;
    private const int RestrictedWeatherCost = 2;
    private const int NarrowWindowCost = 2;
    private const int ShortWindowCost = 1;
    private const int DeepCastCost = 1;
    private const int FewSeasonsCost = 1;
    private const int DeepCastDepth = 4;
    private const int NarrowWindowHours = 8;
    private const int ShortWindowHours = 14;
    private const int FewSeasonsThreshold = 2;
    private const int FullDayHours = 24;

    /// <summary>Stardew's clock runs 600 to 2600 and the hundreds digit is the hour, so an hour
    /// is 100 units and the span arithmetic is plain subtraction.</summary>
    private const int ClockUnitsPerHour = 100;

    public static ItemAvailability Derive(PoolItem item, RawFishEntry? row)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        int spawnWeek = item.Seasons.Count == 0 ? 1 : AvailabilityWeeks.FirstWeekOf(item.Seasons.Min());
        int locationWeek = LocationGating.WeekForAny(item.Locations);
        int week = Math.Max(spawnWeek, locationWeek);
        Season floor = AvailabilityWeeks.SeasonOf(week);
        int hardWeek = Math.Max(spawnWeek, LocationGating.HardWeekForAny(item.Locations));
        if (AvailabilityWeeks.MineFishWeeks.TryGetValue(item.ItemId, out (int Week, Season Gate) mineFish))
        {
            week = Math.Max(week, mineFish.Week);
            floor = mineFish.Gate > AvailabilityWeeks.SeasonOf(week) ? mineFish.Gate : AvailabilityWeeks.SeasonOf(week);
            hardWeek = Math.Max(hardWeek, mineFish.Week);
        }

        string locationNote = locationWeek > 1
            ? $", gated by location ({string.Join(", ", item.Locations)})"
            : "";

        if (row == null)
        {
            int rowEffort = AvailabilityWeeks.FishEffortRows.TryGetValue(item.ItemId, out int effortRow)
                ? effortRow
                : ItemAvailabilityModel.UnrecognisedEffort;
            return new ItemAvailability(floor, rowEffort,
                $"fish, no Data/Fish row, week {week}, spawns {SeasonList(item.Seasons)}{locationNote}",
                EffortSource.Derived, week, floor, HardWeek: hardWeek);
        }

        int effort =
            row.Difficulty / DifficultyBandSize
            + row.MinFishingLevel / LevelBandSize
            + WeatherCost(row.Weather)
            + WindowCost(row.RawTimeSpans)
            + (row.MaxDepth >= DeepCastDepth ? DeepCastCost : 0)
            + (item.Seasons.Count > 0 && item.Seasons.Count < FewSeasonsThreshold ? FewSeasonsCost : 0);

        return new ItemAvailability(floor, effort,
            $"fish, week {week}, spawns {SeasonList(item.Seasons)}{locationNote}, "
            + $"difficulty {row.Difficulty}, level {row.MinFishingLevel}, weather {WeatherLabel(row.Weather)}, "
            + $"window {OpenHours(row.RawTimeSpans)}h, effort {effort}", EffortSource.Derived, week, floor,
            HardWeek: hardWeek);
    }

    private static string SeasonList(IReadOnlyList<Season> seasons)
        => seasons.Count == 0 ? "any season" : string.Join("/", seasons);

    private static string WeatherLabel(string weather)
        => string.IsNullOrEmpty(weather) ? "any" : weather;

    private static int WeatherCost(string weather)
        => weather == "rainy" || weather == "sunny" ? RestrictedWeatherCost : 0;

    private static int WindowCost(string rawTimeSpans)
    {
        int hours = OpenHours(rawTimeSpans);
        if (hours < NarrowWindowHours) return NarrowWindowCost;
        if (hours < ShortWindowHours) return ShortWindowCost;
        return 0;
    }

    /// <summary>Total hours the fish is biting, summed over every start/end pair. A row with no
    /// parseable span reads as open all day, which is the lenient direction.</summary>
    private static int OpenHours(string rawTimeSpans)
    {
        string[] parts = (rawTimeSpans ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int units = 0;
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (int.TryParse(parts[i], out int start) && int.TryParse(parts[i + 1], out int end)
                && end > start)
                units += end - start;
        }
        return units == 0 ? FullDayHours : units / ClockUnitsPerHour;
    }
}
