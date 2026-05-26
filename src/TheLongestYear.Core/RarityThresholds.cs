namespace TheLongestYear.Core;

/// <summary>Sale-price cutoffs (gold) that bucket an item into a <see cref="Rarity"/>. All tunable.</summary>
public sealed class RarityThresholds
{
    public int UncommonAtLeast { get; set; } = 50;
    public int RareAtLeast { get; set; } = 200;
    public int VeryRareAtLeast { get; set; } = 600;
}
