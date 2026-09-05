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
    /// <summary>What a Wild Seed loop adds on top of the wild spawns in a week (Jeff, 2026-09-04,
    /// after the Codex review: a flat 99 had a Normal Spring Foraging bundle asking Leek x50 and
    /// Daffodil x50, the whole measured wild mean, for a "20 to 50%" ask). Four forage make ten
    /// seeds, each seed one forage a week later; 30 is a tilled patch kept in rotation.</summary>
    public const double FarmableAllowance = 30;

    /// <summary>Kept for callers and tests that ask what "farmable" meant: the allowance alone,
    /// which is the basis when nothing was measured for the item.</summary>
    public const double FarmableBasis = FarmableAllowance;

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
        int? ruled = ForageAskLimits.RuledMaxAsk(id);
        double? best = ruled != null ? ruled.Value / AskBands.Ceiling : null;
        if (best == null)
            for (Season s = Season.Spring; s <= last; s++)
            {
                double? mean = ForageAskLimits.MeanFor(s, id);
                if (mean != null && (best == null || mean > best)) best = mean;
            }
        if (ForageAskLimits.IsWildSeedGrowableBy(id, last))
            return Math.Min(StackScaling.MaxStack, (best ?? 0) + FarmableAllowance);
        return best;
    }
}
