using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The theme-week economy rules the goal sampler applies (activity-themes spec
/// 2026-08-28): the season (rule E weights), how many filler goals the season allows (rule B),
/// and the effort resolver (null = no rule placed the id, use the price bucket).</summary>
/// <param name="Season">Season the goals are for.</param>
/// <param name="FillerAllowance">Rule B: at most this many non-due goals this week.</param>
/// <param name="EffortOf">Derived effort for an id, or null when no rule claims it.</param>
public sealed record GoalSamplingRules(Season Season, int FillerAllowance, Func<string, int?> EffortOf)
{
    /// <summary>The Winter allowance in the default config: as many fillers as the cap permits.</summary>
    public const int UnlimitedFiller = 99;
}

/// <summary>One candidate id's tier and draw weight for the week.</summary>
public sealed record GoalWeight(string ItemId, int? Effort, EffortTier Tier, int Weight);

/// <summary>Rule E: per-id weights from effort tier and season. Tiers are quartiles over the
/// recognised efforts of the ids being sampled; an id no rule placed takes the price bucket.</summary>
public static class GoalWeighting
{
    public static IReadOnlyList<GoalWeight> For(
        IEnumerable<string> ids, GoalSamplingRules rules, Func<string, Rarity> rarityOf)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (rarityOf == null) throw new ArgumentNullException(nameof(rarityOf));

        List<string> ordered = ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var efforts = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (string id in ordered)
            efforts[id] = rules.EffortOf(id);

        TierCutoffs cutoffs = EffortTiers.Cutoffs(
            efforts.Values.Where(e => e.HasValue).Select(e => e!.Value).ToList());

        var result = new List<GoalWeight>(ordered.Count);
        foreach (string id in ordered)
        {
            int? effort = efforts[id];
            EffortTier tier = effort.HasValue
                ? EffortTiers.Tier(effort.Value, cutoffs)
                : EffortTiers.FromRarity(rarityOf(id));
            result.Add(new GoalWeight(id, effort, tier, EffortWeights.For(rules.Season, tier)));
        }
        return result;
    }
}
