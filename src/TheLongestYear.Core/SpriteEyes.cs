using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Finds the eyes on one frame of a character sheet: each eye is an iris pixel group
/// plus the eye-white pixels touching it, as a <see cref="PixelBox"/> in sprite pixels relative
/// to the frame's top-left. Used by the Joja Game Over screen to put the red glow
/// on the farmer's eyes for any base sprite (male and female heads sit one row apart on the
/// vanilla sheets; note 5, 2026-10-02).
///
/// Pixels are packed like XNA's <c>Color.PackedValue</c>: R in the low byte, then G, B, A.</summary>
public static class SpriteEyes
{
    // An eye white is opaque, light and nearly grey. The vanilla whites are (255,253,252) and the
    // shaded (190,168,168) / (211,192,184); skin, even the lightest tone's highlight, is far more
    // saturated (vanilla base highlight (249,174,137): spread 112).
    private const int WhiteMinChannel = 150;
    private const int WhiteMaxSpread = 48;
    private const int Opaque = 255;

    /// <summary>The eyes on the frame at (<paramref name="frameX"/>, <paramref name="frameY"/>)
    /// of size <paramref name="frameW"/> x <paramref name="frameH"/>, left to right, as boxes
    /// relative to the frame. Empty when no iris colour appears on the frame.</summary>
    /// <param name="pixels">The whole sheet, row-major, <paramref name="sheetWidth"/> wide.</param>
    /// <param name="irisColours">The packed iris colours (the farmer sheets keep them at pixels
    /// 276 and 277 of row 0, the swatches FarmerRenderer recolours to the eye colour).</param>
    public static IReadOnlyList<PixelBox> Find(
        uint[] pixels, int sheetWidth, int frameX, int frameY, int frameW, int frameH, IReadOnlyCollection<uint> irisColours)
    {
        var iris = new HashSet<uint>(irisColours);
        var seen = new bool[frameW * frameH];
        var eyes = new List<PixelBox>();
        var queue = new Queue<(int X, int Y)>();

        for (int y = 0; y < frameH; y++)
            for (int x = 0; x < frameW; x++)
            {
                if (seen[y * frameW + x] || !iris.Contains(At(x, y))) continue;

                // Flood out from this iris pixel through iris and eye-white pixels, diagonals
                // included (some sheets, Morris's for one, put the white above and beside the
                // iris): one eye.
                int minX = x, minY = y, maxX = x, maxY = y;
                seen[y * frameW + x] = true;
                queue.Enqueue((x, y));
                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    minX = Math.Min(minX, cx); maxX = Math.Max(maxX, cx);
                    minY = Math.Min(minY, cy); maxY = Math.Max(maxY, cy);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= frameW || ny >= frameH || seen[ny * frameW + nx]) continue;
                        uint c = At(nx, ny);
                        if (!iris.Contains(c) && !IsEyeWhite(c)) continue;
                        seen[ny * frameW + nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
                eyes.Add(new PixelBox(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }

        eyes.Sort((a, b) => a.X.CompareTo(b.X));
        return eyes;

        uint At(int x, int y) => pixels[(frameY + y) * sheetWidth + frameX + x];
    }

    /// <summary>Opaque, every channel light, and close to grey.</summary>
    public static bool IsEyeWhite(uint packed)
    {
        int r = (int)(packed & 0xFF), g = (int)((packed >> 8) & 0xFF), b = (int)((packed >> 16) & 0xFF), a = (int)(packed >> 24);
        int min = Math.Min(r, Math.Min(g, b)), max = Math.Max(r, Math.Max(g, b));
        return a == Opaque && min >= WhiteMinChannel && max - min <= WhiteMaxSpread;
    }

    /// <summary>Packs a colour the way XNA's <c>Color.PackedValue</c> does.</summary>
    public static uint Pack(byte r, byte g, byte b, byte a = Opaque) => (uint)(r | g << 8 | b << 16 | a << 24);
}
