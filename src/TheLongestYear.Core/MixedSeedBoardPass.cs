using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Sizes a finished board's asks for the crops only another mod's Mixed Seeds pool gives, once
/// it is known which seasons the board asks such crops from (Jeff, 2026-11-06: saved bags carry over,
/// and a bag is promised to one ask only).
/// <para>
/// The quantity pass sizes each such slot as its bundle is made, from the basis for a board that asks
/// in every season, where each bag is shared the most ways. Here, with every bundle in hand, the
/// seasons asked from are read off the board and each of those slots is rolled again from the stored
/// basis for exactly that combination (<see cref="ItemAvailabilityModel.MixedSeedBoardBases"/>): a
/// board that asks only a Fall crop gets every saved bag in Fall. Nothing is worked out here; the
/// bases were built with the answers.
/// </para>
/// <para>
/// An Artichoke on the board counts as a Fall ask: it grows from the same bags. Its own count is not
/// touched (<see cref="YearTwoCrops"/>). A dish or an artisan good made from one of the crops keeps the
/// every-season size. Returns the same list when nothing changed.
/// </para></summary>
public static class MixedSeedBoardPass
{
    private const int QualityGold = 2;
    private const int SlotSaltPrime = 7919;
    private const int Salt = 0x4D53_4244;

    /// <summary>The seasons this board asks pool-only crops from: each asked crop's seasons with a
    /// basis, and Fall when Artichoke is asked. Empty when the board asks none of them.</summary>
    public static IReadOnlyList<Season> AskedSeasons(IReadOnlyList<BundleSpec> board, ItemAvailabilityModel model)
    {
        if (board == null) throw new ArgumentNullException(nameof(board));
        if (model == null) throw new ArgumentNullException(nameof(model));
        var seasons = new SortedSet<Season>();
        bool anyPoolCrop = false;
        foreach (BundleSlotSpec slot in board.SelectMany(b => b.Slots))
        {
            if (slot.ItemId == null) continue;
            string id = BundleParsing.NormalizeItemId(slot.ItemId);
            if (!model.MixedSeedCropBases.TryGetValue(id, out double[]? basis)) continue;
            anyPoolCrop = true;
            for (int s = 0; s < basis.Length; s++)
                if (basis[s] > 0) seasons.Add((Season)s);
        }
        if (anyPoolCrop && board.SelectMany(b => b.Slots).Any(s => s.ItemId != null && BundleParsing.NormalizeItemId(s.ItemId) == YearTwoCrops.Artichoke))
            seasons.Add(Season.Fall);
        return seasons.ToList();
    }

    public static IReadOnlyList<BundleSpec> Apply(IReadOnlyList<BundleSpec> board, ItemAvailabilityModel? model, DifficultyProfile profile, int seed)
    {
        if (board == null) throw new ArgumentNullException(nameof(board));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (model == null || model.MixedSeedBoardBases.Count == 0) return board;
        IReadOnlyList<Season> asked = AskedSeasons(board, model);
        if (asked.Count == 0) return board;
        string combination = MixedSeedSupply.Combination(asked);

        List<BundleSpec>? resized = null;
        for (int b = 0; b < board.Count; b++)
        {
            BundleSpec spec = board[b];
            List<BundleSlotSpec>? slots = null;
            for (int i = 0; i < spec.Slots.Count; i++)
            {
                BundleSlotSpec slot = spec.Slots[i];
                if (slot.ItemId == null) continue;
                string id = BundleParsing.NormalizeItemId(slot.ItemId);
                if (!model.MixedSeedBoardBases.TryGetValue(id, out var byCombination)
                    || !byCombination.TryGetValue(combination, out double[]? bySeason)
                    || SeasonalAskBasis.BestUpTo(bySeason, null) is not double basis)
                    continue;
                var rng = new Random(seed ^ (spec.Index * SlotSaltPrime) ^ (i + 1) ^ Salt);
                int stack = AskBands.Roll(basis, profile, rng);
                if (slot.Quality >= QualityGold) stack = AskBands.ForGold(stack, AskBands.CropGoldFactor);
                if (stack == slot.Stack) continue;
                slots ??= spec.Slots.ToList();
                slots[i] = slot with { Stack = stack };
            }
            if (slots == null) continue;
            resized ??= board.ToList();
            resized[b] = spec with { Slots = slots };
        }
        return resized ?? board;
    }
}
