using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Turns a PerItem bundle's ingredient list into a per-ingredient season deadline.
///
/// Replaces the hand written GameplayConfig.DefaultItemSeasonPins lookup, which covered 40 items
/// across 12 bundles and left every other ingredient with no deadline at all. An ingredient with
/// no deadline applies no checkpoint pressure, so a bundle whose ingredients were all unlisted
/// could be ignored for three seasons. The engine re-rolls eight of those 12 bundles from pools
/// far larger than the table, so most re-rolled boards were partly or wholly ungated.
///
/// Pacing is Jeff's ruling of 2026-08-27: an even spread across the four checkpoints, easiest
/// first, weighted so a hard item slides later and a trivial one slides earlier.</summary>
public static class BundleDeadlines
{
    private const int CheckpointCount = 4;

    /// <summary>At or above this effort an ingredient slides one checkpoint later.</summary>
    public const int HighEffortThreshold = 8;

    /// <summary>At or below this effort an ingredient slides one checkpoint earlier.</summary>
    public const int TrivialEffortThreshold = 1;

    public static IReadOnlyDictionary<string, Season> For(
        IReadOnlyList<string> ingredients, ItemAvailabilityModel model,
        IReadOnlyDictionary<string, Season>? stretchLines = null)
    {
        if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
        if (model == null) throw new ArgumentNullException(nameof(model));

        var result = new Dictionary<string, Season>(StringComparer.Ordinal);
        if (ingredients.Count == 0)
            return result;

        // Rank easiest first. The id tiebreak keeps the output reproducible from a seed, which
        // matters because a held or reshuffled board must classify the same way twice.
        List<(string Id, ItemAvailability Availability)> ranked = ingredients
            .Select(id => (Id: id, Availability: model.For(id)))
            .OrderBy(pair => pair.Availability.Effort)
            .ThenBy(pair => pair.Id, StringComparer.Ordinal)
            .ToList();

        for (int rank = 0; rank < ranked.Count; rank++)
        {
            (string id, ItemAvailability availability) = ranked[rank];

            int index = BaseCheckpoint(rank, ranked.Count);
            index += EffortShift(availability.Effort);
            index = Math.Clamp(index, 0, CheckpointCount - 1);

            var deadline = (Season)index;
            // A stretch line (spec 2026-08-28-obtainable-board-2-stretch) pins the item to its
            // stretch season instead of clamping up to the gate: the whole point of a stretch
            // line is to demand an item slightly before its own gate would otherwise allow.
            if (stretchLines != null && stretchLines.TryGetValue(id, out Season stretch))
                deadline = stretch;
            // The safety step. A deadline earlier than the season the item can first exist in is
            // unsatisfiable, and an unsatisfiable gate loses the year every loop.
            else if (availability.Gate > deadline)
                deadline = availability.Gate;

            result[id] = deadline;
        }

        return result;
    }

    /// <summary>A bundle with four or fewer ingredients backs its spread against Winter, so two
    /// ingredients land on Fall and Winter rather than Spring and Fall. A larger bundle spreads
    /// proportionally across the four checkpoints.</summary>
    private static int BaseCheckpoint(int rank, int count)
        => count <= CheckpointCount
            ? CheckpointCount - count + rank
            : rank * CheckpointCount / count;

    private static int EffortShift(int effort)
    {
        if (effort >= HighEffortThreshold) return 1;
        if (effort <= TrivialEffortThreshold) return -1;
        return 0;
    }
}
