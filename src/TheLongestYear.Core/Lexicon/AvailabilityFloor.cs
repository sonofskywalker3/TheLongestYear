using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Holds answers built from live data to the shipped ones: no item lands earlier than the
/// earliest answer vanilla or an installed layer ships for it (spec 2026-11-12-ignore-unsupported-mods,
/// rule 4). A build only runs when the data differs from what shipped (a mod with no layer edited a
/// known entry, or supported mods are installed in a combination nothing was written for), and such
/// data may only make the board more cautious. A later answer, or a missing one, is kept as built.
/// A vanilla item no shipped answer places is taken out again, so it stays unplaced as on an
/// unmodded game.</summary>
public static class AvailabilityFloor
{
    public const string HeldNote = " (held to the shipped week)";

    /// <param name="vetted">The shipped answers for the same step and kitchen: vanilla's, and each
    /// installed layer's. With none, <paramref name="built"/> is returned as it is.</param>
    /// <param name="held">The ids whose answer was moved later or taken out.</param>
    public static AvailabilityAnswers Apply(
        AvailabilityAnswers built, IReadOnlyList<AvailabilityAnswers> vetted, out IReadOnlyList<string> held)
    {
        if (built == null) throw new ArgumentNullException(nameof(built));
        var changed = new List<string>();
        held = changed;
        if (vetted == null || vetted.Count == 0) return built;

        var derived = new Dictionary<string, ItemAvailability>(StringComparer.Ordinal);
        foreach (var (id, mine) in built.Derived)
        {
            List<ItemAvailability> shipped = vetted.Select(v => v.Derived.TryGetValue(id, out ItemAvailability? a) ? a : null)
                .Where(a => a != null).Select(a => a!).ToList();
            if (shipped.Count == 0)
            {
                if (Unvetted(id, vetted)) { changed.Add(id); continue; }
                derived[id] = mine;
                continue;
            }
            int pacing = shipped.Min(a => a.PacingWeek);
            int hard = shipped.Min(a => a.HardWeekOrPacing);
            Season season = shipped.Min(a => a.EarliestSeason);
            Season gate = shipped.Min(a => a.GateSeason ?? a.EarliestSeason);
            bool late = mine.PacingWeek >= pacing && mine.HardWeekOrPacing >= hard && mine.EarliestSeason >= season
                && (mine.GateSeason ?? mine.EarliestSeason) >= gate;
            if (late) { derived[id] = mine; continue; }
            changed.Add(id);
            Season heldSeason = mine.EarliestSeason < season ? season : mine.EarliestSeason;
            Season mineGate = mine.GateSeason ?? mine.EarliestSeason;
            derived[id] = mine with
            {
                EarliestSeason = heldSeason,
                EarliestWeek = Math.Max(mine.PacingWeek, pacing),
                HardWeek = Math.Max(mine.HardWeekOrPacing, hard),
                GateSeason = mineGate < gate ? gate : mine.GateSeason,
                Basis = mine.Basis + HeldNote,
            };
        }

        var effort = new Dictionary<string, ItemEffort>(StringComparer.Ordinal);
        foreach (var (id, mine) in built.EffortDerived)
        {
            List<ItemEffort> shipped = vetted.Select(v => v.EffortDerived.TryGetValue(id, out ItemEffort? e) ? e : null)
                .Where(e => e != null).Select(e => e!).ToList();
            if (shipped.Count == 0)
            {
                if (Unvetted(id, vetted)) { changed.Add(id); continue; }
                effort[id] = mine;
                continue;
            }
            // A null week is "not placed" (the model floors it at Winter), the latest there is.
            int? week = Earliest(shipped.Select(e => e.EarliestWeek));
            int? hard = Earliest(shipped.Select(e => e.HardWeek));
            Season? gate = shipped.Any(e => e.GateSeason == null) && shipped.All(e => e.EarliestWeek == null)
                ? null : shipped.Where(e => e.GateSeason != null).Select(e => e.GateSeason).DefaultIfEmpty(null).Min();
            bool weekEarly = mine.EarliestWeek != null && (week == null || mine.EarliestWeek < week);
            bool hardEarly = mine.HardWeek != null && hard != null && mine.HardWeek < hard;
            bool gateEarly = mine.GateSeason != null && gate != null && mine.GateSeason < gate;
            if (!weekEarly && !hardEarly && !gateEarly) { effort[id] = mine; continue; }
            changed.Add(id);
            effort[id] = mine with
            {
                EarliestWeek = weekEarly ? week : mine.EarliestWeek,
                HardWeek = hardEarly ? hard : mine.HardWeek,
                GateSeason = gateEarly ? gate : mine.GateSeason,
                Basis = mine.Basis + HeldNote,
            };
        }
        return changed.Count == 0 ? built : built with { Derived = derived, EffortDerived = effort };
    }

    /// <summary>A vanilla item that no shipped answer places at all, of either kind.</summary>
    private static bool Unvetted(string id, IReadOnlyList<AvailabilityAnswers> vetted)
        => VanillaItemIds.All.Contains(id)
           && !vetted.Any(v => v.Derived.ContainsKey(id) || v.EffortDerived.ContainsKey(id));

    private static int? Earliest(IEnumerable<int?> weeks)
    {
        var placed = weeks.Where(w => w != null).Select(w => w!.Value).ToList();
        return placed.Count == 0 ? null : placed.Min();
    }
}
