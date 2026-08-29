using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Shared constants for the weekly bonus-slot system: the inverse-rarity weight table and the
/// per-season bonus-list size cap. The actual sampling (seeded weighted draw of goal slots from
/// the open-slot pool) lives in <see cref="BonusSlotSampler"/> as of the 2026-07-09 slot redesign;
/// this class now only owns the shared tuning data both the hub preview and the sampler read.
/// </summary>
public static class BonusItemSampler
{
    /// <summary>
    /// How many bonus items to show per (season, theme) preview card. Flat across the year (Jeff,
    /// 2026-08-28: goals follow the gate with no look-ahead, so there is no longer a Winter surge
    /// of extra lines to make room for). Indices match the <see cref="Season"/> enum
    /// (Spring=0..Winter=3). Spec round 6 lived in <c>GameplayConfig.BonusListSizeBySeason</c>;
    /// moved here in spec round 7 so the sampler owns its own default cap.
    /// </summary>
    public static readonly IReadOnlyList<int> DefaultMaxCountBySeason = new[] { 5, 5, 5, 5 };

    /// <summary>
    /// Inverse-rarity weight: Common shows up most often, VeryRare least. Tuned per the 2026-05-26
    /// playtest feedback (user expected Chub/Sardine to dominate Spring fishing samples, not Crab
    /// Pot items; expected to NOT see Solar/Void essences in Spring mining samples).
    /// </summary>
    public static int WeightFor(Rarity r) => r switch
    {
        Rarity.Common   => 8,
        Rarity.Uncommon => 4,
        Rarity.Rare     => 2,
        Rarity.VeryRare => 1,
        _ => 1
    };
}
