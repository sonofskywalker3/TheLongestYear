using System;

namespace TheLongestYear.Core;

/// <summary>
/// Applies the jp_boost_1..5 upgrade chain multiplier to a base JP amount. Chain is
/// linear: each tier owned adds +5% (tier 5 = +25%). Highest owned tier wins (no
/// stacking — owning III is +15%, not +5+10+15).
///
/// 2026-05-29 user spec: "I want a jp boost 5-25% that boosts all jp sources" —
/// applies uniformly to item donations, bundle completions, room completions,
/// weekly theme quest awards, and any interim run-end awards. The debug
/// <c>tly_addjp</c> command is intentionally NOT routed through here so the
/// command stays as a raw +N grant for testing.
///
/// Pure Core (no Stardew dependencies) so it can be unit-tested deterministically
/// and reused by any future JP-awarding path.
/// </summary>
public static class JpBoostHelper
{
    /// <summary>Returns <paramref name="baseAmount"/> scaled by the highest owned
    /// jp_boost tier. Zero / negative bases pass through unchanged (the math is
    /// defined but a "boosted zero" log line would be misleading).</summary>
    public static long Apply(MetaState meta, long baseAmount)
    {
        if (meta == null || baseAmount <= 0) return baseAmount;
        int tier = HighestTier(meta);
        if (tier == 0) return baseAmount;
        double mult = 1.0 + 0.05 * tier;
        return (long)Math.Round(baseAmount * mult, MidpointRounding.AwayFromZero);
    }

    /// <summary>Highest owned jp_boost tier (1..5), or 0 if none owned.</summary>
    public static int HighestTier(MetaState meta)
    {
        if (meta == null) return 0;
        for (int t = 5; t >= 1; t--)
            if (meta.HasUpgrade("jp_boost_" + t)) return t;
        return 0;
    }
}
