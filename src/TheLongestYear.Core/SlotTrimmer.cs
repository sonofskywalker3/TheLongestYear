using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Restores vanilla remix's Pick-based ingredient trim at GENERATION time (the
/// pool builder has no seed, so it keeps every candidate ingredient and records the
/// intended shown-count in BundleSpec.PickCount — see VanillaBundlePool's class doc).
/// Without this trim an untrimmed RandomBundles pick shows MORE candidate items than
/// vanilla's own remix while requiring the same donations — strictly easier, unacceptable
/// for the Normal difficulty bar (review-carried requirement, SDD ledger ENG Task 5).</summary>
public static class SlotTrimmer
{
    public static BundleSpec Trim(BundleSpec spec, Random rng)
    {
        if (spec.PickCount <= 0 || spec.PickCount >= spec.Slots.Count)
            return spec;

        // Partial Fisher-Yates over the index list: choose PickCount distinct indices.
        var indices = Enumerable.Range(0, spec.Slots.Count).ToArray();
        var chosen = new List<int>(spec.PickCount);
        for (int i = 0; i < spec.PickCount; i++)
        {
            int j = rng.Next(i, indices.Length);
            (indices[i], indices[j]) = (indices[j], indices[i]);
            chosen.Add(indices[i]);
        }
        chosen.Sort(); // keep the surviving slots in their original order

        var slots = chosen.Select(ix => spec.Slots[ix]).ToList();
        return spec with
        {
            Slots = slots,
            NumberOfSlots = Math.Min(spec.NumberOfSlots, slots.Count),
        };
    }
}
