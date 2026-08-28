namespace TheLongestYear.Core;

/// <summary>Rule E (activity-themes spec): the per-id draw weight of a weekly goal is a function
/// of its effort tier and the season. A zero means "not this season" (an Extreme item is never a
/// Spring goal); by Winter the hard items carry the weight, when they pay 4x.</summary>
public static class EffortWeights
{
    private static readonly int[,] Table =
    {
        // Easy, Medium, Hard, Extreme
        { 8, 3, 1, 0 },   // Spring
        { 6, 4, 2, 1 },   // Summer
        { 3, 4, 4, 2 },   // Fall
        { 1, 2, 4, 8 },   // Winter
    };

    public static int For(Season season, EffortTier tier) => Table[(int)season, (int)tier];
}
