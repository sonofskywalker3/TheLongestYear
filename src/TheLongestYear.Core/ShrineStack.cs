using System;

namespace TheLongestYear.Core;

/// <summary>The stack a random shrine goal asks for.</summary>
public static class ShrineStack
{
    private const int MinStack = 1;
    private const int MaxStack = 99;

    /// <summary>A basis (the item's usual ask) rolls inside the profile's ask band; with none the
    /// ask is one scaled by the Stack Size factor. <paramref name="clamps"/> applies caller rules
    /// (once-per-loop items), then the result is held to 1..99.</summary>
    public static int For(double? basis, DifficultyProfile profile, Random rng, Func<int, int> clamps)
    {
        int stack = basis.HasValue
            ? AskBands.Roll(basis.Value, profile, rng)
            : StackScaling.ScaleStack(MinStack, profile.StackFactor);
        return Math.Clamp(clamps(stack), MinStack, MaxStack);
    }
}
