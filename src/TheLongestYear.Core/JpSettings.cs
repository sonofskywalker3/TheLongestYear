namespace TheLongestYear.Core;

/// <summary>Tunable Junimo Point values. Serialized as part of the mod config.</summary>
public sealed class JpSettings
{
    public int CommonJp { get; set; } = 1;
    public int UncommonJp { get; set; } = 3;
    public int RareJp { get; set; } = 10;
    public int VeryRareJp { get; set; } = 25;

    /// <summary>Per-week-of-year multiplier step. Multiplier = 1 + (week-1) * step.</summary>
    public double WeekDepthStep { get; set; } = 0.1;

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
