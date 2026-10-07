namespace TheLongestYear.Core;

/// <summary>One random shrine donation goal of the current week (Randomizer, shrine donations).
/// <see cref="ListIndex"/> 0 is the first theme, 1 the double-week second theme.</summary>
public sealed class ShrineGoal
{
    public string ItemId { get; set; } = "";
    public int Stack { get; set; } = 1;
    public int ListIndex { get; set; }
    public bool Deposited { get; set; }
    public bool Paid { get; set; }
}
