using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One composed bundle as the no-repeat pass sees it. <see cref="Match"/> is the
/// domain the bundle re-rolled from; an authored bundle carries its <see cref="Authored"/> def
/// instead. A bundle with neither (a money bundle, vanilla slots kept because the pool could not
/// fill) never moves, and the others work around it.</summary>
public sealed record NoRepeatEntry(
    BundleSpec Composed,
    BundleSpec Pick,
    DomainMatch? Match = null,
    PoolRecipe? Recipe = null,
    AuthoredBundleDef? Authored = null);

/// <summary>The engine's last word on "no item asked twice across the board": runs on the
/// composed board after every fill and swaps repeats out with <see cref="BoardNoRepeats"/>.
/// A swapped slot asks for one of its new item (the quantity passes band it later, as they do
/// every slot), keeps its quality ask where the new item can carry one, and keeps vanilla's
/// stack and quality when the new item is one of the bundle's own vanilla items, as
/// <see cref="BundleSlotFiller.Fill"/> does. Deterministic for a given seed.</summary>
public static class BoardNoRepeatPass
{
    private const string MoneySlotId = "-1";
    private const int NoRepeatSalt = 0x2E9E;
    private const int IndexSaltPrime = 7877;
    private const int SlotSaltPrime = 104729;

    public static IReadOnlyList<BundleSpec> Apply(
        IReadOnlyList<NoRepeatEntry> entries, ItemPools pools, ItemAvailabilityModel? availability,
        BundleGenerationTuning tuning, bool nonObjectDonationsEnabled, IReadOnlySet<string>? authoredBanned,
        int seed, Action<string>? log, out IReadOnlyList<string> stillRepeated)
    {
        List<IReadOnlyList<string?>> board = entries
            .Select(e => (IReadOnlyList<string?>)e.Composed.Slots.Select(s => ItemOf(s.ItemId)).ToList())
            .ToList();

        IReadOnlyList<string>? Options(BoardSlotRef slot)
        {
            NoRepeatEntry entry = entries[slot.Bundle];
            var rng = new Random(seed ^ NoRepeatSalt
                                 ^ unchecked(entry.Composed.Index * IndexSaltPrime)
                                 ^ unchecked((slot.Slot + 1) * SlotSaltPrime));
            if (entry.Authored != null)
                return AuthoredBundleComposer.ReplacementOptions(
                    entry.Authored, entry.Composed, slot.Slot, pools, nonObjectDonationsEnabled, authoredBanned, rng);
            if (entry.Match == null || entry.Match.Domain == PoolDomain.None)
                return null;
            return BundleSlotFiller.ReplacementOptions(
                entry.Composed, slot.Slot, entry.Match, pools, availability, entry.Recipe, rng);
        }

        IReadOnlyDictionary<BoardSlotRef, string> moves = BoardNoRepeats.Resolve(board, Options, out stillRepeated);
        if (moves.Count == 0)
            return entries.Select(e => e.Composed).ToList();

        var result = new List<BundleSpec>(entries.Count);
        for (int b = 0; b < entries.Count; b++)
        {
            NoRepeatEntry entry = entries[b];
            var slots = entry.Composed.Slots.ToList();
            bool changed = false;
            for (int s = 0; s < slots.Count; s++)
            {
                if (!moves.TryGetValue(new BoardSlotRef(b, s), out string? id)) continue;
                BundleSlotSpec old = slots[s];
                slots[s] = SlotFor(entry, id, old, pools, tuning);
                changed = true;
                log?.Invoke($"'{entry.Composed.Name}' slot {s}: {ItemOf(old.ItemId)} is asked elsewhere on the board; asks for {id} instead.");
            }
            result.Add(changed ? entry.Composed with { Slots = slots } : entry.Composed);
        }
        return result;
    }

    /// <summary>The normalized item a slot asks for, or null for a money or category slot.</summary>
    private static string? ItemOf(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId) || itemId == MoneySlotId || BundleParsing.IsCategoryRef(itemId))
            return null;
        string id = BundleParsing.NormalizeItemId(itemId);
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private static BundleSlotSpec SlotFor(
        NoRepeatEntry entry, string id, BundleSlotSpec old, ItemPools pools, BundleGenerationTuning tuning)
    {
        if (entry.Authored == null)
            foreach (BundleSlotSpec vanilla in entry.Pick.Slots)
                if (ItemOf(vanilla.ItemId) == id)
                    return new BundleSlotSpec(id, vanilla.Stack, LegendaryFishRules.ClampQuality(id, vanilla.Quality));
        int quality = CanCarryQuality(id, pools, tuning) ? old.Quality : 0;
        return new BundleSlotSpec(id, 1, LegendaryFishRules.ClampQuality(id, quality));
    }

    private static bool CanCarryQuality(string id, ItemPools pools, BundleGenerationTuning tuning)
    {
        if (BundleSlotFiller.BuiltInQualityIneligibleItemIds.Contains(id)) return false;
        if (tuning?.QualityIneligibleItemIds != null && tuning.QualityIneligibleItemIds.Contains(id)) return false;
        if (pools.ShallowWaterFishIds != null && pools.ShallowWaterFishIds.Contains(id)) return false;
        return pools.QualityEligibleIds == null || pools.QualityEligibleIds.Contains(id);
    }
}
