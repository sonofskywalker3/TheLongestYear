using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Holds answers built from live data to the shipped ones: no item lands earlier than the
/// earliest answer vanilla or an installed layer ships for it (spec 2026-11-12-ignore-unsupported-mods,
/// rule 4). A build only runs when the data differs from what shipped (a mod with no layer edited a
/// known entry, or supported mods are installed in a combination nothing was written for), and such
/// data may only make the board more cautious.
/// <para>Answers are compared as the model reads them, in every week mode, so it does not matter
/// which of the two tables holds an item or which of its weeks are left unset. A built answer that
/// is earlier in any mode is replaced whole by the earliest shipped answer for that item. A later
/// answer is kept as built. A vanilla item no shipped answer holds is taken out again, so it stays
/// unplaced as on an unmodded game; an item of a supported mod that none holds is kept as built.</para></summary>
public static class AvailabilityFloor
{
    private static readonly WeekMode[] Modes = Enum.GetValues<WeekMode>();

    /// <param name="vetted">The shipped answers for the same step and kitchen: vanilla's, and each
    /// installed layer's. With none, <paramref name="built"/> is returned as it is.</param>
    /// <param name="step">The difficulty step the answers are for.</param>
    /// <param name="held">The ids whose answer was replaced or taken out.</param>
    public static AvailabilityAnswers Apply(
        AvailabilityAnswers built, IReadOnlyList<AvailabilityAnswers> vetted, DifficultyStep step, out IReadOnlyList<string> held)
    {
        if (built == null) throw new ArgumentNullException(nameof(built));
        var changed = new List<string>();
        held = changed;
        if (vetted == null || vetted.Count == 0) return built;

        Reader mine = new(built, step);
        List<Reader> shipped = vetted.Select(v => new Reader(v, step)).ToList();
        var derived = new Dictionary<string, ItemAvailability>(built.Derived, StringComparer.Ordinal);
        var effort = new Dictionary<string, ItemEffort>(built.EffortDerived, StringComparer.Ordinal);

        foreach (string id in built.Derived.Keys.Concat(built.EffortDerived.Keys).Distinct(StringComparer.Ordinal).ToList())
        {
            List<Reader> holders = shipped.Where(s => s.Holds(id)).ToList();
            if (holders.Count == 0)
            {
                if (!VanillaItemIds.All.Contains(id)) continue;
                derived.Remove(id);
                effort.Remove(id);
                changed.Add(id);
                continue;
            }
            Reader earliest = holders.OrderBy(h => h.Weeks(id), WeeksOrder.Instance).First();
            if (!Earlier(mine.Weeks(id), holders.Select(h => h.Weeks(id)).ToList())) continue;
            derived.Remove(id);
            effort.Remove(id);
            if (earliest.Answers.Derived.TryGetValue(id, out ItemAvailability? a)) derived[id] = a;
            if (earliest.Answers.EffortDerived.TryGetValue(id, out ItemEffort? e)) effort[id] = e;
            changed.Add(id);
        }
        return changed.Count == 0 ? built : built with { Derived = derived, EffortDerived = effort };
    }

    /// <summary>True when the built answer is earlier than every shipped one in some reading: a
    /// mode's gate week, goal week or gate season, or the season floor.</summary>
    private static bool Earlier(int[] mine, IReadOnlyList<int[]> shipped)
    {
        for (int i = 0; i < mine.Length; i++)
            if (mine[i] < shipped.Min(s => s[i]))
                return true;
        return false;
    }

    /// <summary>One answer set as the model reads it, in every week mode.</summary>
    private sealed class Reader
    {
        private readonly ItemAvailabilityModel[] _models;

        public Reader(AvailabilityAnswers answers, DifficultyStep step)
        {
            Answers = answers;
            _models = Modes.Select(m => ItemAvailabilityBuilder.FromAnswers(answers, null, null, m, step)).ToArray();
        }

        public AvailabilityAnswers Answers { get; }

        public bool Holds(string id) => Answers.Derived.ContainsKey(id) || Answers.EffortDerived.ContainsKey(id);

        /// <summary>Per mode: the gate week, the goal week and the gate season; then the season floor.</summary>
        public int[] Weeks(string id)
        {
            var weeks = new List<int>(Modes.Length * 3 + 1);
            foreach (ItemAvailabilityModel model in _models)
            {
                ItemAvailability a = model.For(id);
                weeks.Add(a.Week);
                weeks.Add(a.GoalWeek);
                weeks.Add((int)a.Gate);
            }
            weeks.Add((int)_models[0].For(id).EarliestSeason);
            return weeks.ToArray();
        }
    }

    /// <summary>Orders readings by their sum: the answer that is earliest overall comes first.</summary>
    private sealed class WeeksOrder : IComparer<int[]>
    {
        public static readonly WeeksOrder Instance = new();

        public int Compare(int[]? x, int[]? y) => (x?.Sum() ?? 0).CompareTo(y?.Sum() ?? 0);
    }
}
