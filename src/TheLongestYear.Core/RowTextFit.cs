using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>An axis-aligned box in screen pixels (X, Y is the top-left corner).</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>A free horizontal span [<see cref="Start"/>, <see cref="End"/>) on one text line.</summary>
public readonly record struct FreeSpan(int Start, int End)
{
    public int Width => End - Start;
}

/// <summary>Finds room for a line of text inside a list row that already has things drawn in it
/// (the Herd Book's "Needs keep" note next to the hearts, pet and cracker icons). Pure geometry,
/// so the game side only measures its fonts and icons and asks where the text can go.</summary>
public static class RowTextFit
{
    /// <summary>The free spans between <paramref name="xMin"/> and <paramref name="xMax"/> on the
    /// line from <paramref name="lineTop"/> to <paramref name="lineBottom"/>, left to right. Every
    /// obstacle that touches the line blocks its own width plus <paramref name="padding"/> on each side.</summary>
    public static List<FreeSpan> FreeSpans(IEnumerable<PixelBox> obstacles, int lineTop, int lineBottom, int xMin, int xMax, int padding)
    {
        var spans = new List<FreeSpan>();
        int cursor = xMin;
        foreach (PixelBox box in obstacles
                     .Where(o => o.Width > 0 && o.Height > 0 && o.Y < lineBottom && o.Bottom > lineTop)
                     .OrderBy(o => o.X))
        {
            int blockStart = box.X - padding;
            int blockEnd = box.Right + padding;
            if (blockStart > cursor)
                spans.Add(new FreeSpan(cursor, Math.Min(blockStart, xMax)));
            cursor = Math.Max(cursor, blockEnd);
            if (cursor >= xMax) break;
        }
        if (cursor < xMax)
            spans.Add(new FreeSpan(cursor, xMax));
        spans.RemoveAll(s => s.Width <= 0);
        return spans;
    }

    /// <summary>The leftmost span at least <paramref name="width"/> wide, or null.</summary>
    public static FreeSpan? FirstFitting(IReadOnlyList<FreeSpan> spans, int width)
        => spans.Where(s => s.Width >= width).Select(s => (FreeSpan?)s).FirstOrDefault();

    /// <summary>The rightmost span at least <paramref name="width"/> wide, or null.</summary>
    public static FreeSpan? LastFitting(IReadOnlyList<FreeSpan> spans, int width)
        => spans.Where(s => s.Width >= width).Select(s => (FreeSpan?)s).LastOrDefault();

    /// <summary>The widest span, or null when there is none.</summary>
    public static FreeSpan? Widest(IEnumerable<FreeSpan> spans)
        => spans.OrderByDescending(s => s.Width).Select(s => (FreeSpan?)s).FirstOrDefault();
}
