using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Spacing for a row of goal icons whose counts sit centred under each icon. Each icon
/// takes the wider of its sprite and its count, and neighbours keep a clear gap, so every count
/// reads as its own icon's. A crowded row closes the gap down to a minimum, then shrinks the counts.</summary>
public static class IconCountRow
{
    /// <summary>Smallest count shrink tried before giving up and letting the row run wide.</summary>
    public const float MinShrink = 0.5f;
    private const float ShrinkStep = 0.01f;

    /// <summary>The row's layout: each icon's centre from the row's left edge, the row's width,
    /// the gap used, and the factor the counts are drawn at (1 = full size).</summary>
    public sealed record Plan(IReadOnlyList<float> Centres, float Width, float Gap, float Shrink);

    /// <summary>Lays out <paramref name="labelWidthsAt"/>(shrink) counts under sprites
    /// <paramref name="spriteWidth"/> wide inside <paramref name="available"/>: the wanted gap when it
    /// fits, else a smaller gap no less than <paramref name="minGap"/>, else smaller counts.</summary>
    public static Plan Fit(Func<float, IReadOnlyList<float>> labelWidthsAt, float spriteWidth,
        float available, float wantedGap, float minGap)
    {
        IReadOnlyList<float> labels = labelWidthsAt(1f);
        int n = labels.Count;
        if (n == 0) return new Plan(Array.Empty<float>(), 0f, wantedGap, 1f);

        float gap = wantedGap;
        if (Width(labels, spriteWidth, gap) > available && n > 1)
            gap = Math.Max(minGap, (available - Footprints(labels, spriteWidth)) / (n - 1));

        float shrink = 1f;
        while (Width(labels, spriteWidth, gap) > available && shrink - ShrinkStep >= MinShrink)
        {
            shrink -= ShrinkStep;
            labels = labelWidthsAt(shrink);
        }
        return new Plan(Centres(labels, spriteWidth, gap), Width(labels, spriteWidth, gap), gap, shrink);
    }

    /// <summary>Each icon's centre from the row's left edge.</summary>
    public static float[] Centres(IReadOnlyList<float> labelWidths, float spriteWidth, float gap)
    {
        var centres = new float[labelWidths.Count];
        float x = 0f;
        for (int i = 0; i < labelWidths.Count; i++)
        {
            float footprint = Math.Max(spriteWidth, labelWidths[i]);
            if (i > 0) x += gap;
            centres[i] = x + footprint / 2f;
            x += footprint;
        }
        return centres;
    }

    /// <summary>Row width: every icon's footprint plus the gaps between them.</summary>
    public static float Width(IReadOnlyList<float> labelWidths, float spriteWidth, float gap)
        => labelWidths.Count == 0 ? 0f : Footprints(labelWidths, spriteWidth) + gap * (labelWidths.Count - 1);

    private static float Footprints(IReadOnlyList<float> labelWidths, float spriteWidth)
    {
        float sum = 0f;
        foreach (float w in labelWidths) sum += Math.Max(spriteWidth, w);
        return sum;
    }
}
