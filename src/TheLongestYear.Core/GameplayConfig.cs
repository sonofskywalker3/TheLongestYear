namespace TheLongestYear.Core;

/// <summary>Root config object read by the mod via SMAPI. All tuning dials hang off this.</summary>
public sealed class GameplayConfig
{
    public JpSettings Jp { get; set; } = new JpSettings();

    /// <summary>Gold the farmer starts each run with after a reset.</summary>
    public int StartingMoney { get; set; } = 500;

    /// <summary>Price cutoffs used to derive an item's rarity (and thus its JP value).</summary>
    public RarityThresholds RarityThresholds { get; set; } = new RarityThresholds();
}
