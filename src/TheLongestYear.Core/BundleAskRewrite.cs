using System;

namespace TheLongestYear.Core;

/// <summary>Rewrites the ingredient field of one raw BundleData value through a stack clamp,
/// leaving every other field byte for byte. Shared by the load-time repairs
/// (<see cref="UnstackableAsks"/>, <see cref="OncePerLoopAsks"/>).</summary>
public static class BundleAskRewrite
{
    private const int IngredientFieldIndex = 2;
    private const int TokensPerIngredient = 3;

    /// <summary>The value with every ingredient stack passed through <paramref name="clamp"/>
    /// (item id, stack), or null when no stack changed.</summary>
    public static string? LowerAsks(string value, Func<string, int, int> clamp)
    {
        if (clamp == null) throw new ArgumentNullException(nameof(clamp));
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
            int clamped = clamp(tokens[i], stack);
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
