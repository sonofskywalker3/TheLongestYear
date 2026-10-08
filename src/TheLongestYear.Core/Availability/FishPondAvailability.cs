using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for a fish pond output (Data/FishPondData): the cheapest fish
/// any matching pond entry accepts, plus the pond itself, plus one step per three fish of
/// population the product needs beyond the first. The week is the fish's week plus a season
/// (AvailabilityWeeks.PondDelayWeeks) to build and populate a 5,000g pond.
///
/// A product row whose daily <c>Chance</c> is below the Item Rarity step's minimum is not a route
/// (<see cref="CountsAsRoute"/>): SVE's Goldenfish pond lists Golden Pumpkin at 0.01, which put it
/// at week 5 instead of the Spirit's Eve maze in week 12. Jeff, 2026-10-08: 0.10 on Easy and
/// Normal, 0.05 on Hard and Extreme.</summary>
public static class FishPondAvailability
{
    private const int PondCost = 2;
    private const int PopulationStepSize = 3;
    private const string FishType = "Fish";

    /// <summary>Lowest row Chance that counts on Easy and Normal Item Rarity: about ten days'
    /// wait at a full pond.</summary>
    public const double MinRouteChanceEasyNormal = 0.10;

    /// <summary>Lowest row Chance that counts on Hard and Extreme Item Rarity: about twenty days'
    /// wait at a full pond.</summary>
    public const double MinRouteChanceHardExtreme = 0.05;

    /// <summary>Slack for game data's single-precision Chance (0.05f widens to 0.0500000007).</summary>
    private const double ChanceTolerance = 1e-6;

    public static double MinRouteChance(DifficultyStep step)
        => step >= DifficultyStep.Hard ? MinRouteChanceHardExtreme : MinRouteChanceEasyNormal;

    /// <summary>True when a product row's daily chance is at least the step's minimum.</summary>
    public static bool CountsAsRoute(double chance, DifficultyStep step)
        => chance + ChanceTolerance >= MinRouteChance(step);

    public static int PopulationSteps(int requiredPopulation)
        => requiredPopulation <= 1 ? 0 : (requiredPopulation - 2) / PopulationStepSize + 1;

    public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf,
        Func<string, int?>? weekOf = null, DifficultyStep step = DifficultyStep.Normal)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (effortOf == null) throw new ArgumentNullException(nameof(effortOf));
        weekOf ??= _ => null;
        ItemEffort? best = null;
        foreach (RawFishPondRule rule in data.FishPonds)
        {
            foreach (RawFishPondProduct product in rule.Products)
            {
                if (product.ItemId != qualifiedId || !CountsAsRoute(product.Chance, step)) continue;
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
