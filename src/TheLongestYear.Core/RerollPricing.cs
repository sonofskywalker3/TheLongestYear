namespace TheLongestYear.Core;

/// <summary>Spec section 1. Flat on purpose, not season-scaled (Jeff, 2026-09-28): in Spring one reroll
/// costs more than the week's whole bonus, so paid rerolls are a real choice.</summary>
public static class RerollPricing
{
    public const long BaseCost = 50;
    /// <summary>Doubling stops here so a long reroll streak cannot overflow.</summary>
    public const long MaxCost = 1_000_000;
    private const int MaxDoublings = 40;

    public static long CostOf(RerollMode mode, int rerollsAlreadyThisWeek)
    {
        if (mode != RerollMode.CostsJp) return 0;
        int n = System.Math.Clamp(rerollsAlreadyThisWeek, 0, MaxDoublings);
        return System.Math.Min(MaxCost, BaseCost << n);
    }
}
