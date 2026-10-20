using System;

namespace TheLongestYear.Core;

/// <summary>Rewrites the ingredient field of one raw BundleData value through a stack or quality clamp,
/// leaving every other field byte for byte. Shared by the load-time repairs
/// (<see cref="UnstackableAsks"/>, <see cref="OncePerLoopAsks"/>, <see cref="ShallowFishStars"/>).</summary>
public static class BundleAskRewrite
{
    private const int IngredientFieldIndex = 2;
    private const int TokensPerIngredient = 3;
    private const int StackOffset = 1;
    private const int QualityOffset = 2;

    /// <summary>The value with every ingredient stack passed through <paramref name="clamp"/>
    /// (item id, stack), or null when no stack changed.</summary>
    public static string? LowerAsks(string value, Func<string, int, int> clamp)
        => Rewrite(value, StackOffset, clamp);

    /// <summary>The value with every ingredient quality passed through <paramref name="clamp"/>
    /// (item id, quality), or null when no quality changed.</summary>
    public static string? LowerQualities(string value, Func<string, int, int> clamp)
        => Rewrite(value, QualityOffset, clamp);

    private static string? Rewrite(string value, int offset, Func<string, int, int> clamp)
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
            if (!int.TryParse(tokens[i + offset], out int current))
                continue;
            int clamped = clamp(tokens[i], current);
            if (clamped == current)
                continue;
            tokens[i + offset] = clamped.ToString();
            changed = true;
        }
        if (!changed)
            return null;

        fields[IngredientFieldIndex] = string.Join(" ", tokens);
        return string.Join("/", fields);
    }
}
