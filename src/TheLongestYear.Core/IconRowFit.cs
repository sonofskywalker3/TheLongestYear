using System;

namespace TheLongestYear.Core
{
    /// <summary>How many item icons a Bundle Log row shows before its "+N more" note, so icons and
    /// note both stay inside the row's box. Game-free so the fit can be tested.</summary>
    public static class IconRowFit
    {
        /// <summary>Icons to draw out of <paramref name="total"/> in a row <paramref name="room"/> wide,
        /// each icon <paramref name="iconSize"/> wide and <paramref name="pitch"/> from the next. All of
        /// them when they fit; otherwise the most that leave room after them for the note, whose width
        /// for a given hidden count <paramref name="noteWidth"/> gives. Never below zero.</summary>
        public static int Shown(int total, int room, int iconSize, int pitch, Func<int, int> noteWidth)
        {
            if (total <= 0) return 0;
            if ((total - 1) * pitch + iconSize <= room) return total;
            for (int n = Math.Min(total - 1, room / Math.Max(1, pitch)); n > 0; n--)
                if (n * pitch + (noteWidth?.Invoke(total - n) ?? 0) <= room) return n;
            return 0;
        }
    }
}
