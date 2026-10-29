namespace TheLongestYear.Core;

/// <summary>Vertical centring of a text label in a box by its drawn ink, not its line height.
/// A font's line height includes room below the baseline and the line gap, so centring that
/// height leaves the letters sitting high with a gap underneath (Jeff, 2026-10-29).</summary>
public static class TextCentering
{
    /// <summary>The y to draw the text line at so its ink is centred in the box.</summary>
    /// <param name="boxTop">Top of the box.</param>
    /// <param name="boxHeight">Height of the box.</param>
    /// <param name="inkTop">Distance from the line's top to the top of the highest glyph, unscaled.</param>
    /// <param name="inkHeight">From the top of the highest glyph to the bottom of the lowest, unscaled.</param>
    /// <param name="scale">Draw scale of the text.</param>
    public static float InkCentredY(float boxTop, float boxHeight, float inkTop, float inkHeight, float scale)
        => boxTop + (boxHeight - inkHeight * scale) / 2f - inkTop * scale;

    /// <summary>The ink extent of a line from each glyph's top offset and height (unscaled):
    /// (top of the highest glyph, height to the bottom of the lowest). Null when nothing is drawn.</summary>
    public static (float Top, float Height)? InkExtent(System.Collections.Generic.IEnumerable<(int Top, int Height)> glyphs)
    {
        int? top = null, bottom = null;
        foreach ((int t, int h) in glyphs)
        {
            if (h <= 0) continue;   // spaces draw nothing
            top = top == null ? t : System.Math.Min(top.Value, t);
            bottom = bottom == null ? t + h : System.Math.Max(bottom.Value, t + h);
        }
        return top == null ? null : (top.Value, bottom!.Value - top.Value);
    }
}
