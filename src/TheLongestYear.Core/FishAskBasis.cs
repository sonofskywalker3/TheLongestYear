using System;
using System.Collections.Generic;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

/// <summary>How many of a fish a bundle may build an ask on, per season: what a level-10 player
/// with plain bait lands on the best ten-hour day for that fish, times seven days (Jeff's ruling,
/// 2026-09-04). The numbers are the stored catch model (<see cref="FishCatchRates"/>, worked out in
/// the lexicon's cold build and served with its answers, vanilla's in vanilla.json and a mod's in its
/// layer), so they cover every item that bites, a mod's fish or a seed in a fish row alike, and
/// nothing is computed on a served load.
///
/// A rain-only fish takes the larger of the rain-weighted week and a rainy-day catch times the
/// season's expected rainy days. A catch landed less than twice a week has no basis and stays a
/// single ask, as do the five legendaries (LegendaryFishRules) and the mine-floor fish
/// MineShaft.getFish hard-codes outside the data tables.</summary>
public static class FishAskBasis
{
    /// <summary>True when the item has a basis in at least one season, so its ask is banded and
    /// the flat stack multiplier must leave it alone.</summary>
    public static bool Covers(IReadOnlyDictionary<string, CatchRate>? rates, string? itemId)
    {
        if (rates == null || itemId == null || !rates.TryGetValue(BundleParsing.NormalizeItemId(itemId), out CatchRate? rate)) return false;
        foreach (double week in rate.Weekly)
            if (week > 0) return true;
        return false;
    }

    /// <summary>The week's catch in this season, or null when it is not caught then (or too rarely
    /// to build a stack on).</summary>
    public static double? Basis(IReadOnlyDictionary<string, CatchRate>? rates, Season season, string itemId)
    {
        if (rates == null || itemId == null || !rates.TryGetValue(BundleParsing.NormalizeItemId(itemId), out CatchRate? rate)) return null;
        int index = (int)season;
        return index < rate.Weekly.Count && rate.Weekly[index] > 0 ? rate.Weekly[index] : null;
    }

    /// <summary>The most generous basis among the seasons a bundle due in
    /// <paramref name="deadline"/> can reach (Spring up to and including the deadline), or the
    /// whole year when there is no per-item deadline. Null when no reachable season has one.</summary>
    public static double? BasisByDeadline(IReadOnlyDictionary<string, CatchRate>? rates, string itemId, Season? deadline)
    {
        double? best = null;
        Season last = deadline ?? Season.Winter;
        for (Season s = Season.Spring; s <= last; s++)
        {
            double? b = Basis(rates, s, itemId);
            if (b != null && (best == null || b > best)) best = b;
        }
        return best;
    }
}
