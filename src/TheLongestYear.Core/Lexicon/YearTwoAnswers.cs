using System;
using System.Collections.Generic;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Turns a layer's year-2 week and effort rulings into the answers' year-2 table. The table
/// is read-only to the year-1 model: <see cref="Build"/> reads the year-1 availability and never writes to it.</summary>
public static class YearTwoAnswers
{
    public const string BasisPrefix = "year 2 ruling: ";

    /// <summary>One <see cref="ItemAvailability"/> per accepted ruling (week, effort, season of the year-2 week).
    /// A ruling is refused, with a warning, for an id that already has a year-1 week: any entry in a key's
    /// <see cref="AvailabilityAnswers.Derived"/>, or an <see cref="AvailabilityAnswers.EffortDerived"/> entry with a week.</summary>
    public static IReadOnlyDictionary<string, ItemAvailability> Build(
        IReadOnlyDictionary<string, YearTwoRuling> rulings,
        IReadOnlyDictionary<string, AvailabilityAnswers> availability, Action<string> warn)
    {
        var table = new Dictionary<string, ItemAvailability>(StringComparer.Ordinal);
        foreach (var (id, ruling) in rulings)
        {
            if (YearOneWeek(id, availability) is var (week, key))
            {
                warn($"year 2 ruling for {id} refused: it already has a year-1 week ({week}, {key})");
                continue;
            }
            Season season = AvailabilityWeeks.SeasonOf(((ruling.Week - 1) % Calendar.WeeksPerYear) + 1);
            table[id] = new ItemAvailability(
                season, ruling.Effort, BasisPrefix + ruling.Basis, EffortSource.Ruling, ruling.Week, season);
        }
        return table;
    }

    private static (int Week, string Key)? YearOneWeek(string id, IReadOnlyDictionary<string, AvailabilityAnswers> availability)
    {
        foreach (var (key, answers) in availability)
        {
            if (answers.Derived.TryGetValue(id, out ItemAvailability d)) return (d.Week, key);
            if (answers.EffortDerived.TryGetValue(id, out ItemEffort e) && e.EarliestWeek != null) return (e.EarliestWeek.Value, key);
        }
        return null;
    }
}
