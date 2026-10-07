using System;

namespace TheLongestYear.Core;

/// <summary>Tab strip math for the farm statue's menu. Three tabs keep their fixed width; the
/// fourth tab (Donate, only on weeks with shrine goals) shrinks the tabs just enough to fit
/// beside the restart button.</summary>
public static class ShrineTabLayout
{
    /// <summary>Tab counts at or below this keep the preferred width untouched.</summary>
    public const int FixedWidthTabCount = 3;

    /// <summary>Width of each tab. <paramref name="availableWidth"/> is the room the whole strip
    /// may take (gaps included). Never wider than <paramref name="preferredWidth"/>, never narrower
    /// than <paramref name="minWidth"/>.</summary>
    public static int TabWidth(int tabCount, int preferredWidth, int gap, int availableWidth, int minWidth)
    {
        if (tabCount <= FixedWidthTabCount) return preferredWidth;
        int fit = (availableWidth - gap * (tabCount - 1)) / tabCount;
        return Math.Max(minWidth, Math.Min(preferredWidth, fit));
    }

    /// <summary>Right edge of the last tab for a strip starting at 0.</summary>
    public static int StripWidth(int tabCount, int tabWidth, int gap)
        => tabCount <= 0 ? 0 : tabCount * tabWidth + (tabCount - 1) * gap;
}
