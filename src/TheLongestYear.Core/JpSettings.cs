namespace TheLongestYear.Core;

/// <summary>Tunable Junimo Point values. Serialized as part of the mod config.</summary>
public sealed class JpSettings
{
    public int CommonJp { get; set; } = 1;
    public int UncommonJp { get; set; } = 3;
    public int RareJp { get; set; } = 10;
    public int VeryRareJp { get; set; } = 25;

    /// <summary>
    /// Per-season JP multiplier (Spring..Winter). Spec 2026-05-26 replaces the linear
    /// WeekDepthStep ramp with explicit per-season tiers: roguelite-style acceleration
    /// gives late-season donations meaningfully more value than early-season ones.
    /// Bundle/room completion bonuses also scale by this multiplier.
    /// </summary>
    public double[] SeasonMultipliers { get; set; } = { 1.0, 1.5, 2.5, 4.0 };

    public int BundleCompletionBonus { get; set; } = 15;
    public int RoomCompletionBonus { get; set; } = 60;

    public int BaseFor(Rarity rarity) => rarity switch
    {
        Rarity.Common => CommonJp,
        Rarity.Uncommon => UncommonJp,
        Rarity.Rare => RareJp,
        Rarity.VeryRare => VeryRareJp,
        _ => 0
    };
}
