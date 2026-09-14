using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Items that never stack, so a bundle slot may never ask for more than one of them.
/// The game only accepts a deposit whose stack covers the whole ask (Bundle.canAcceptThisItem:
/// ingredient.stack &lt;= item.Stack), and a hat, a weapon or a ring is always a stack of one.
///
/// Nexus bug report, 2026-09-14: the Stack size dial on Hard turned Gil's Trophies' Skeleton Mask
/// x1 into x2, which no player can ever deposit, and it blocked the season. Gil gives each
/// trophy once per loop anyway, so one is the only ask that makes sense.</summary>
public static class UnstackableAsks
{
    private const string ObjectQualifier = "(O)";
    private const int IngredientFieldIndex = 2;
    private const int TokensPerIngredient = 3;

    /// <summary>True for any qualified non-Object item ((H) hats, (W) weapons and the like) and for
    /// Gil's trophy rings, which are Objects that still never stack.</summary>
    public static bool IsUnstackable(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId) || itemId[0] != '(')
            return false;
        return !itemId.StartsWith(ObjectQualifier, StringComparison.Ordinal)
               || AuthoredBundleCatalog.GilTrophies.Contains(itemId);
    }

    /// <summary>One for an unstackable item, the stack unchanged for anything else.</summary>
    public static int ClampStack(string? itemId, int stack)
        => IsUnstackable(itemId) && stack > 1 ? 1 : stack;

    /// <summary>Repairs one live BundleData value written before this rule existed: every
    /// unstackable ask above one comes back as one, every other field survives byte for byte.
    /// Null when nothing needed changing.</summary>
    public static string? RepairBundleValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        string[] fields = value.Split('/');
        if (fields.Length <= IngredientFieldIndex)
            return null;

        string[] tokens = fields[IngredientFieldIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < TokensPerIngredient || tokens.Length % TokensPerIngredient != 0)
            return null;

        bool changed = false;
        for (int i = 0; i < tokens.Length; i += TokensPerIngredient)
        {
            if (!int.TryParse(tokens[i + 1], out int stack))
                continue;
            int clamped = ClampStack(tokens[i], stack);
            if (clamped == stack)
                continue;
            tokens[i + 1] = clamped.ToString();
            changed = true;
        }
        if (!changed)
            return null;

        fields[IngredientFieldIndex] = string.Join(" ", tokens);
        return string.Join("/", fields);
    }
}
