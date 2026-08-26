using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Per-(run, week, theme) seeded sample of goal SLOTS from the open-slot pool (see
/// <see cref="SlotPoolBuilder"/>). Successor to <see cref="BonusItemSampler"/> (2026-07-09
/// slot redesign): the draw is still per item id with inverse-rarity weighting and the week-1/2
/// early-game filter, but each drawn id resolves to ONE concrete slot — seeded-random among the
/// id's open slots — so the checklist entry names an exact (bundle, line, stack, quality).
/// Deterministic: same (seed, week, theme, pool) -> same slots.
///
/// A bundle never contributes more goals than it can still accept. Bundles that require only SOME
/// of their listed items put every open line in the pool, so without a cap a week could ask for
/// three items from a bundle that needs two - and since 0.14.0 requires a real deposit per goal,
/// the extra one is impossible (Jeff, 2026-08-26, from emmalution's stream). Pass
/// <c>remainingNeedForBundle</c> (bundle index -> lines it still needs); null means no cap, for
/// preview callers that cannot see live completion state.
/// </summary>
public static class BonusSlotSampler
{
    private const int WeekSaltPrime = 7919;
    private const int ThemeSaltPrime = 1031;
    private const int EarlyGameMaxWeek = 2;

    public static IReadOnlyList<BonusSlot> SampleSlots(
        int runSeed, int weekOfYear, Theme theme,
        IReadOnlyList<BonusSlot> openSlots,
        Func<string, Rarity> rarityOf,
        int maxCount,
        Func<int, int>? remainingNeedForBundle = null)
    {
        if (openSlots is null) throw new ArgumentNullException(nameof(openSlots));
        if (rarityOf is null) throw new ArgumentNullException(nameof(rarityOf));
        if (maxCount <= 0 || openSlots.Count == 0) return Array.Empty<BonusSlot>();

        // Group open slots by item id; per-id weight = inverse rarity (unchanged from
        // BonusItemSampler.WeightFor).
        Dictionary<string, List<BonusSlot>> slotsById = new(StringComparer.Ordinal);
        foreach (BonusSlot slot in openSlots)
        {
            if (!slotsById.TryGetValue(slot.ItemId, out List<BonusSlot>? list))
                slotsById[slot.ItemId] = list = new List<BonusSlot>();
            list.Add(slot);
        }

        // Week 1-2: drop late-game-infrastructure ids unless that empties the pool.
        IEnumerable<string> idPool = slotsById.Keys;
        if (weekOfYear <= EarlyGameMaxWeek)
        {
            var filtered = slotsById.Keys.Where(id => !CcItemCatalog.EarlyGameAvoid.Contains(id)).ToList();
            if (filtered.Count > 0)
                idPool = filtered;
        }

        // Stable input order keyed by id so the seeded weighted draws are reproducible.
        List<(string Id, int Weight)> remaining = idPool
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => (id, BonusItemSampler.WeightFor(rarityOf(id))))
            .ToList();

        Random rng = new Random(runSeed ^ (weekOfYear * WeekSaltPrime) ^ ((int)theme * ThemeSaltPrime));
        int take = Math.Min(maxCount, remaining.Count);
        List<BonusSlot> result = new(take);
        // How many goals this week already point at each bundle.
        Dictionary<int, int> takenPerBundle = new();
        for (int n = 0; n < take; n++)
        {
            // Every ineligible id is removed from 'remaining' before the retry, so this terminates.
            if (remaining.Count == 0) break;
            int totalWeight = 0;
            for (int i = 0; i < remaining.Count; i++) totalWeight += remaining[i].Weight;

            int draw = rng.Next(totalWeight);
            int cum = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                cum += remaining[i].Weight;
                if (draw < cum)
                {
                    // Resolve the drawn id to one concrete slot: seeded-random among its open
                    // slots (deterministic order first so the rng pick reproduces).
                    List<BonusSlot> candidates = slotsById[remaining[i].Id]
                        .OrderBy(s => s.BundleIndex).ThenBy(s => s.IngredientIndex)
                        .ToList();
                    // Drop candidates in bundles that have already been asked for everything they
                    // can still take. Draw the rng ONCE either way so a capped bundle does not
                    // shift the sequence for the slots that do get picked.
                    List<BonusSlot> allowed = remainingNeedForBundle == null
                        ? candidates
                        : candidates.Where(s =>
                            takenPerBundle.TryGetValue(s.BundleIndex, out int taken)
                                ? taken < remainingNeedForBundle(s.BundleIndex)
                                : remainingNeedForBundle(s.BundleIndex) > 0).ToList();
                    int pick = rng.Next(candidates.Count);
                    remaining.RemoveAt(i);
                    if (allowed.Count == 0)
                    {
                        // Every slot for this id is in a full bundle: this id cannot be a goal.
                        // Retry the draw so the week still gets its full complement from elsewhere.
                        n--;
                        break;
                    }
                    BonusSlot chosen = allowed[pick % allowed.Count];
                    result.Add(chosen);
                    takenPerBundle[chosen.BundleIndex] =
                        takenPerBundle.TryGetValue(chosen.BundleIndex, out int prior) ? prior + 1 : 1;
                    break;
                }
            }
        }

        return result;
    }
}
