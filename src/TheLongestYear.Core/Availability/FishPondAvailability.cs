using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for a fish pond output (Data/FishPondData): the cheapest fish
/// any matching pond entry accepts, plus the pond itself, plus one step per three fish of
/// population the product needs beyond the first. The week is the fish's week plus a season
/// (AvailabilityWeeks.PondDelayWeeks) to build and populate a 5,000g pond.</summary>
public static class FishPondAvailability
{
    private const int PondCost = 2;
    private const int PopulationStepSize = 3;
    private const string FishType = "Fish";

    public static int PopulationSteps(int requiredPopulation)
        => requiredPopulation <= 1 ? 0 : (requiredPopulation - 2) / PopulationStepSize + 1;

    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf,
        Func<string, int?>? weekOf = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        weekOf ??= _ => null;
        ItemEffort? best = null;
        foreach (RawFishPondRule rule in data.FishPonds)
        {
            foreach (RawFishPondProduct product in rule.Products)
            {
                if (product.ItemId != qualifiedId) continue;
                int? fishEffort = null;
                int? fishWeek = null;
                string fishId = "";
                foreach (string id in ContextTagMatcher.IdsMatchingAll(data.Objects, rule.RequiredTags))
                {
                    if (!data.Objects.TryGetValue(BundleParsing.StripQualifier(id), out RawObjectEntry? obj)
                        || !string.Equals(obj.Type, FishType, StringComparison.OrdinalIgnoreCase))
                        continue;
                    int? e = effortOf(id);
                    if (e != null && (fishEffort == null || e < fishEffort)) { fishEffort = e; fishId = id; }
                    int? w = weekOf(id);
                    if (w != null && (fishWeek == null || w < fishWeek)) fishWeek = w;
                }
                if (fishEffort == null) continue;
                int steps = PopulationSteps(product.RequiredPopulation);
                int effort = fishEffort.Value + PondCost + steps;
                int? week = fishWeek == null ? null : Math.Min(fishWeek.Value + AvailabilityWeeks.PondDelayWeeks, Calendar.WeeksPerYear);
                bool better = best == null
                    || (week ?? int.MaxValue) < (best.EarliestWeek ?? int.MaxValue)
                    || (week == best.EarliestWeek && effort < best.Effort);
                if (better)
                    best = new ItemEffort(effort,
                        $"fish pond, {fishId} ({fishEffort}) + pond {PondCost} + population {product.RequiredPopulation} (+{steps}), "
                        + $"week {(week?.ToString() ?? "unknown")}, effort {effort}",
                        week, week == null ? null : AvailabilityWeeks.SeasonOf(week.Value));
            }
        }
        return best;
    }
}
