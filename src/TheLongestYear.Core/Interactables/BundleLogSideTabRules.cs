using System.Collections.Generic;

namespace TheLongestYear.Core.Interactables
{
    /// <summary>Rules for the Bundle Log side tab on the game menu's Collections page: one more tab at
    /// the bottom of the column of side tabs down the page's left edge (Items Shipped, Fish, Artifacts
    /// and the rest). Selecting it shows the Bundle Log in place of the collection grid. Game-free so
    /// the placement and the tab's picture can be tested.</summary>
    public static class BundleLogSideTabRules
    {
        /// <summary>The tab's key in the page's side-tab and collection tables. Vanilla reads a side
        /// tab's name back as this number when it is clicked, so the name is this key as text. Vanilla
        /// uses 0-7; this stays clear of them and of any small numbers another mod may add.</summary>
        public const int TabKey = 91_400;

        /// <summary>Snap id of the tab. Vanilla's side tabs are 7001-7008, collection items count up
        /// from 0, the page buttons are 706/707 and the game menu's tabs 12340-12349.</summary>
        public const int TabComponentId = 91_400;

        /// <summary>Side tab width and height (16px sprite at 4x, like vanilla's).</summary>
        public const int TabSize = 64;

        /// <summary>Pixel size of a vanilla side-tab sprite.</summary>
        public const int SpriteSize = 16;

        /// <summary>The tab's picture is drawn at twice the sprite's resolution so the Bundle Log's 16px
        /// cover fits inside the frame at half the frame's pixel size.</summary>
        public const int IconScale = 2;

        /// <summary>The tab's component name: its key as text.</summary>
        public static string TabName => TabKey.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>True when the tab belongs on a freshly built Collections page: a run is active and
        /// the tab is not already there.</summary>
        public static bool ShouldAdd(bool runActive, bool alreadyPresent) => runActive && !alreadyPresent;

        /// <summary>Top edge of the tab: one tab height under the lowest tab in the column
        /// (<paramref name="tabTops"/>), so it ends the column the way vanilla spaces its own tabs.
        /// With no tabs it takes <paramref name="firstSlotY"/>.</summary>
        public static int TabY(IReadOnlyList<int> tabTops, int firstSlotY)
        {
            if (tabTops == null || tabTops.Count == 0) return firstSlotY;
            int lowest = int.MinValue;
            foreach (int top in tabTops)
                if (top > lowest) lowest = top;
            return lowest + TabSize;
        }

        /// <summary>True when a tab whose top is <paramref name="tabY"/> ends inside both the menu box
        /// (bottom edge <paramref name="menuBottom"/>) and the screen (<paramref name="screenHeight"/>).</summary>
        public static bool Fits(int tabY, int menuBottom, int screenHeight)
            => tabY + TabSize <= menuBottom && tabY + TabSize <= screenHeight;

        /// <summary>Least distance between tab tops the column is squeezed to; below this the tabs would
        /// hide too much of each other, and the column is left to run long instead.</summary>
        public const int MinPitch = 48;

        /// <summary>Distance between the tops of neighbouring tabs in a column of <paramref name="count"/>
        /// tabs starting at <paramref name="firstTop"/> that must end by <paramref name="bottomLimit"/>
        /// (the menu box's bottom, or the screen's if that is higher up). Vanilla's spacing (one tab
        /// height, tabs touching) whenever the column fits that way; otherwise just tight enough that
        /// the last tab ends at the limit, never under <see cref="MinPitch"/>.</summary>
        public static int Pitch(int count, int firstTop, int bottomLimit)
        {
            if (count <= 1) return TabSize;
            int room = bottomLimit - firstTop - TabSize;
            int pitch = room / (count - 1);
            if (pitch >= TabSize) return TabSize;
            return pitch < MinPitch ? MinPitch : pitch;
        }

        /// <summary>Top edge of each of <paramref name="count"/> tabs, spaced by <see cref="Pitch"/>.</summary>
        public static int[] ColumnTops(int count, int firstTop, int bottomLimit)
        {
            int pitch = Pitch(count, firstTop, bottomLimit);
            var tops = new int[System.Math.Max(0, count)];
            for (int i = 0; i < tops.Length; i++) tops[i] = firstTop + i * pitch;
            return tops;
        }

        /// <summary>A vanilla side-tab frame with its picture taken out. <paramref name="tabs"/> are the
        /// vanilla side-tab sprites (packed colours, <see cref="SpriteSize"/> squared, row by row): they
        /// share one frame and differ only where their pictures are, so a pixel all of them agree on is
        /// kept and any other is filled with the colour that most often borders the pictures (the
        /// frame's inside). Null when fewer than two sprites are given.</summary>
        public static uint[] BlankFrame(IReadOnlyList<uint[]> tabs)
        {
            const int n = SpriteSize * SpriteSize;
            if (tabs == null || tabs.Count < 2) return null;
            foreach (uint[] t in tabs)
                if (t == null || t.Length != n) return null;

            var frame = new uint[n];
            var picture = new bool[n];
            for (int i = 0; i < n; i++)
            {
                frame[i] = tabs[0][i];
                for (int k = 1; k < tabs.Count; k++)
                    if (tabs[k][i] != frame[i]) { picture[i] = true; break; }
            }

            var counts = new Dictionary<uint, int>();
            for (int i = 0; i < n; i++)
            {
                if (picture[i] || !TouchesPicture(picture, i)) continue;
                counts.TryGetValue(frame[i], out int c);
                counts[frame[i]] = c + 1;
            }
            if (counts.Count == 0) return frame;
            uint fill = 0;
            int best = -1;
            foreach (KeyValuePair<uint, int> kv in counts)
                if (kv.Value > best) { best = kv.Value; fill = kv.Key; }
            for (int i = 0; i < n; i++)
                if (picture[i]) frame[i] = fill;
            return frame;
        }

        private static bool TouchesPicture(bool[] picture, int i)
        {
            int x = i % SpriteSize, y = i / SpriteSize;
            return (x > 0 && picture[i - 1]) || (x < SpriteSize - 1 && picture[i + 1])
                || (y > 0 && picture[i - SpriteSize]) || (y < SpriteSize - 1 && picture[i + SpriteSize]);
        }

        /// <summary>The tab's picture: <paramref name="frame"/> (<see cref="SpriteSize"/> squared) blown
        /// up by <see cref="IconScale"/>, with <paramref name="cover"/> (also <see cref="SpriteSize"/>
        /// squared) centred on it at its own size. Fully transparent cover pixels (alpha 0 in the top
        /// byte, as packed XNA colours store it) leave the frame showing.</summary>
        public static uint[] Compose(uint[] frame, uint[] cover)
        {
            int size = SpriteSize * IconScale;
            var icon = new uint[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    icon[y * size + x] = frame[(y / IconScale) * SpriteSize + x / IconScale];
            if (cover == null) return icon;
            int offset = (size - SpriteSize) / 2;
            for (int y = 0; y < SpriteSize; y++)
                for (int x = 0; x < SpriteSize; x++)
                {
                    uint c = cover[y * SpriteSize + x];
                    if (c >> 24 == 0) continue;
                    icon[(y + offset) * size + x + offset] = c;
                }
            return icon;
        }
    }
}
