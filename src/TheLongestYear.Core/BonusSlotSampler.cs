using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Per-(run, week, theme) seeded sample of goal SLOTS from the open-slot pool (see
/// <see cref="SlotPoolBuilder"/>). Successor to <see cref="BonusItemSampler"/> (2026-07-09
/// slot redesign): the draw is per item id, and each drawn id resolves to ONE concrete slot,
/// seeded-random among the id's open slots, so the checklist entry names an exact
/// (bundle, line, stack, quality). Deterministic: same (seed, week, theme, pool) -> same slots.
///
/// With <see cref="GoalSamplingRules"/> (activity-themes spec 2026-08-28) the draw has two tiers:
///   - Rule A: ids with a Due slot (the day-28 gate wants them this season) are drawn first,
///     up to the season cap.
///   - Rule B: every other id is filler, at most <c>FillerAllowance</c> per week and at most
///     one filler goal per bundle.
///   - Rule E: the per-id weight is the effort tier x season table (EffortWeights); a zero
///     weight leaves the draw for the season.
/// Without rules the draw is the legacy one: every id eligible, inverse-rarity weights.
///
/// Each <see cref="GoalGroupCap"/> limits how many goals may come from one item group: the
/// fruit-tree fruits (Data/FruitTrees; a Spring week 2 Farming list once named three) and the
/// crab-pot catches (Data/Fish trap rows; a week 1 Fishing list was all crab pot, no fish).
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
    /// <summary>Rule B: a bundle holds at most this many filler goals per week.</summary>
    private const int MaxFillerPerBundle = 1;

    public static IReadOnlyList<BonusSlot> SampleSlots(
        int runSeed, int weekOfYear, Theme theme,
        IReadOnlyList<BonusSlot> openSlots,
        Func<string, Rarity> rarityOf,
        int maxCount,
        Func<int, int>? remainingNeedForBundle = null,
        IReadOnlyList<GoalGroupCap>? caps = null,
        GoalSamplingRules? rules = null)
    {
        if (openSlots is null) throw new ArgumentNullException(nameof(openSlots));
        if (rarityOf is null) throw new ArgumentNullException(nameof(rarityOf));
        if (maxCount <= 0 || openSlots.Count == 0) return Array.Empty<BonusSlot>();

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

        var draw = new DrawState(
            new Random(runSeed ^ (weekOfYear * WeekSaltPrime) ^ ((int)theme * ThemeSaltPrime)),
            slotsById, remainingNeedForBundle, caps);

        if (rules == null)
        {
            // Legacy path: every id eligible, inverse-rarity weight, one pass.
            List<(string Id, int Weight)> all = idPool
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => (id, BonusItemSampler.WeightFor(rarityOf(id))))
                .ToList();
            draw.Take(all, maxCount, filler: false, dueOnly: false);
            return draw.Result;
        }

        var weights = GoalWeighting.For(idPool, rules, rarityOf)
            .Where(w => w.Weight > 0)
            .ToList();
        List<(string Id, int Weight)> due = weights
            .Where(w => slotsById[w.ItemId].Any(s => s.Due))
            .Select(w => (w.ItemId, w.Weight))
            .ToList();
        List<(string Id, int Weight)> filler = weights
            .Where(w => !slotsById[w.ItemId].Any(s => s.Due))
            .Select(w => (w.ItemId, w.Weight))
            .ToList();

        draw.Take(due, maxCount, filler: false, dueOnly: true);
        int fillerBudget = Math.Min(maxCount - draw.Result.Count, rules.FillerAllowance);
        if (fillerBudget > 0)
            draw.Take(filler, fillerBudget, filler: true, dueOnly: false);
        return draw.Result;
    }

    /// <summary>One weighted draw pass. The rng is consumed exactly as the legacy sampler did:
    /// one Next(totalWeight) then one Next(candidates.Count) per pick, so the legacy call is
    /// byte-for-byte reproducible.</summary>
    private sealed class DrawState
    {
        private readonly Random _rng;
        private readonly Dictionary<string, List<BonusSlot>> _slotsById;
        private readonly Func<int, int>? _remainingNeedForBundle;
        private readonly IReadOnlyList<GoalGroupCap>? _caps;
        private readonly Dictionary<int, int> _takenPerBundle = new();
        private readonly Dictionary<int, int> _fillerPerBundle = new();

        public List<BonusSlot> Result { get; } = new();

        public DrawState(Random rng, Dictionary<string, List<BonusSlot>> slotsById,
            Func<int, int>? remainingNeedForBundle, IReadOnlyList<GoalGroupCap>? caps)
        {
            _rng = rng;
            _slotsById = slotsById;
            _remainingNeedForBundle = remainingNeedForBundle;
            _caps = caps;
        }

        public void Take(List<(string Id, int Weight)> remaining, int count, bool filler, bool dueOnly)
        {
            int take = Math.Min(count, remaining.Count);
            for (int n = 0; n < take; n++)
            {
                // Every ineligible id is removed from 'remaining' before the retry, so this terminates.
                if (remaining.Count == 0) break;
                int totalWeight = 0;
                for (int i = 0; i < remaining.Count; i++) totalWeight += remaining[i].Weight;
                if (totalWeight <= 0) break;

                int roll = _rng.Next(totalWeight);
                int cum = 0;
                for (int i = 0; i < remaining.Count; i++)
                {
                    cum += remaining[i].Weight;
                    if (roll >= cum) continue;

                    // Resolve the drawn id to one concrete slot: seeded-random among its open
                    // slots (deterministic order first so the rng pick reproduces). In the due
                    // pass only Due slots count.
                    List<BonusSlot> candidates = _slotsById[remaining[i].Id]
                        .Where(s => !dueOnly || s.Due)
                        .OrderBy(s => s.BundleIndex).ThenBy(s => s.IngredientIndex)
                        .ToList();
                    // Drop candidates in bundles that have already been asked for everything they
                    // can still take, and filler candidates in bundles that already hold a filler.
                    // Draw the rng ONCE either way so a capped bundle does not shift the sequence.
                    List<BonusSlot> allowed = candidates.Where(s => Allowed(s, filler)).ToList();
                    int pick = _rng.Next(Math.Max(1, candidates.Count));
                    remaining.RemoveAt(i);
                    if (allowed.Count == 0)
                    {
                        // Every slot for this id is blocked: this id cannot be a goal. Retry the
                        // draw so the week still gets its full complement from elsewhere.
                        n--;
                        break;
                    }
                    BonusSlot chosen = allowed[pick % allowed.Count];
                    Result.Add(chosen);
                    _takenPerBundle[chosen.BundleIndex] = _takenPerBundle.GetValueOrDefault(chosen.BundleIndex) + 1;
                    if (filler)
                        _fillerPerBundle[chosen.BundleIndex] = _fillerPerBundle.GetValueOrDefault(chosen.BundleIndex) + 1;
                    // Capped groups (Jeff, 2026-08-28: "limit fruit tree stuff to 1 per theme";
                    // same day: "why is the fishing goal all crab pot stuff"): once a group has
                    // its quota, the rest of it leaves the draw. No rng is consumed here.
                    if (_caps != null)
                        foreach (GoalGroupCap cap in _caps)
                            if (cap.Ids.Contains(chosen.ItemId)
                                && Result.Count(s => cap.Ids.Contains(s.ItemId)) >= cap.Max)
                                remaining.RemoveAll(r => cap.Ids.Contains(r.Id));
                    break;
                }
            }
        }

        private bool Allowed(BonusSlot slot, bool filler)
        {
            if (filler && _fillerPerBundle.GetValueOrDefault(slot.BundleIndex) >= MaxFillerPerBundle)
                return false;
            if (_remainingNeedForBundle == null) return true;
            int taken = _takenPerBundle.GetValueOrDefault(slot.BundleIndex);
            return taken < _remainingNeedForBundle(slot.BundleIndex);
        }
    }
}
