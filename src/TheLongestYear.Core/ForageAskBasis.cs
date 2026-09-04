using System;

namespace TheLongestYear.Core;

/// <summary>How many of a forage item a bundle may build an ask on, for <see cref="AskBands"/>.
/// Jeff's ruling, 2026-09-04, putting forage on the same basis x band rule as fish.
///
/// The basis is the MEASURED seasonal mean from <see cref="ForageAskLimits"/> (three full-year
/// sweeps of every map, 2026-08-30), reachable by the slot's deadline: the most generous mean
/// among the seasons up to and including it, or the whole year when there is no per-item
/// deadline. A ruling in <see cref="ForageAskLimits.RuledMaxAsk"/> is a ceiling, so it stands in
/// as ceiling / 0.8. A Wild Seed crop whose seeds grow by the deadline is farmable, its supply
/// is land and time rather than spawns, and it takes a full 99-stack as its basis; before its
/// season it is measured like anything else (the 90 Common Mushrooms on a first Spring).</summary>
public static class ForageAskBasis
{
    /// <summary>The basis for something the player can farm without limit: one inventory stack.</summary>
    public const double FarmableBasis = StackScaling.MaxStack;

    /// <summary>True when the item can carry a banded ask at all: measured in some season, ruled,
    /// or growable from Wild Seeds. The stack multiplier leaves such a slot alone.</summary>
    public static bool Covers(string? itemId)
    {
        if (itemId == null) return false;
        string id = BundleParsing.NormalizeItemId(itemId);
        if (ForageAskLimits.IsWildSeedGrowable(id) || ForageAskLimits.RuledMaxAsk(id) != null) return true;
        for (Season s = Season.Spring; s <= Season.Winter; s++)
            if (ForageAskLimits.MeanFor(s, id) != null) return true;
        return false;
    }

    public static double? BasisByDeadline(string itemId, Season? deadline)
    {
        if (itemId == null) return null;
        string id = BundleParsing.NormalizeItemId(itemId);
        Season last = deadline ?? Season.Winter;
        if (ForageAskLimits.IsWildSeedGrowableBy(id, last))
            return FarmableBasis;
        int? ruled = ForageAskLimits.RuledMaxAsk(id);
        if (ruled != null)
            return ruled.Value / AskBands.Ceiling;
        double? best = null;
        for (Season s = Season.Spring; s <= last; s++)
        {
            double? mean = ForageAskLimits.MeanFor(s, id);
            if (mean != null && (best == null || mean > best)) best = mean;
        }
        return best;
    }
}
