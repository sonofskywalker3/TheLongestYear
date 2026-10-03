using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SpriteEyesTests
{
    // The head rows of frame 0 (down-facing idle) of the vanilla farmer sheets, read from
    // Characters/Farmer/farmer_base and farmer_girl_base. A outline, B skin shade, C lash,
    // D skin, E shaded white, F iris, G white.
    private static readonly string[] MaleHead =
    {
        "................",
        "................",
        "................",
        "................",
        "................",
        ".....AAAAAA.....",
        "....ABBBBBBA....",
        "....ABBBBBBA....",
        "...ABBBBBBBBA...",
        "...ABBBBBBBBA...",
        "...ACCCDDCCCA...",
        "...ACEFDDFECA...",
        "...ABGFDDFGBA...",
        "....ADDDDDDA....",
    };

    private static readonly string[] FemaleHead =
    {
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "......AAAA......",
        ".....ABBBBA.....",
        "....ABBBBBBA....",
        "...ABBBBBBBBA...",
        "...ABBBBBBBBA...",
        "...AACCDDCCAA...",
        "...ACEFDDFECA...",
        "...ADGFDDFGDA...",
        "....ADDDDDDA....",
    };

    private static readonly uint Iris = SpriteEyes.Pack(104, 43, 15);
    private static readonly uint DarkIris = SpriteEyes.Pack(45, 18, 6);

    private static readonly Dictionary<char, uint> Palette = new()
    {
        ['.'] = 0,
        ['A'] = SpriteEyes.Pack(107, 0, 58),
        ['B'] = SpriteEyes.Pack(224, 107, 101),
        ['C'] = SpriteEyes.Pack(15, 10, 8),
        ['D'] = SpriteEyes.Pack(249, 174, 137),
        ['E'] = SpriteEyes.Pack(190, 168, 168),
        ['F'] = Iris,
        ['G'] = SpriteEyes.Pack(255, 253, 252),
    };

    private static uint[] Sheet(string[] rows, int padLeft, out int width)
    {
        width = rows[0].Length + padLeft;
        var px = new uint[width * rows.Length];
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                px[y * width + padLeft + x] = Palette[rows[y][x]];
        return px;
    }

    private static IReadOnlyList<PixelBox> FindIn(string[] rows, int padLeft = 0)
    {
        uint[] px = Sheet(rows, padLeft, out int w);
        return SpriteEyes.Find(px, w, padLeft, 0, rows[0].Length, rows.Length, new[] { Iris, DarkIris });
    }

    [Fact]
    public void Male_eyes_cover_iris_and_white_on_rows_11_and_12()
    {
        var eyes = FindIn(MaleHead);

        Assert.Equal(new[] { new PixelBox(5, 11, 2, 2), new PixelBox(9, 11, 2, 2) }, eyes);
    }

    [Fact]
    public void Female_eyes_sit_one_row_lower()
    {
        var eyes = FindIn(FemaleHead);

        Assert.Equal(new[] { new PixelBox(5, 12, 2, 2), new PixelBox(9, 12, 2, 2) }, eyes);
    }

    [Fact]
    public void Boxes_are_relative_to_the_frame_not_the_sheet()
    {
        var eyes = FindIn(MaleHead, padLeft: 32);

        Assert.Equal(new[] { new PixelBox(5, 11, 2, 2), new PixelBox(9, 11, 2, 2) }, eyes);
    }

    [Fact]
    public void Skin_and_lashes_are_never_eye_white()
    {
        Assert.False(SpriteEyes.IsEyeWhite(Palette['D']));
        Assert.False(SpriteEyes.IsEyeWhite(Palette['B']));
        Assert.False(SpriteEyes.IsEyeWhite(Palette['C']));
        Assert.True(SpriteEyes.IsEyeWhite(Palette['E']));
        Assert.True(SpriteEyes.IsEyeWhite(Palette['G']));
    }

    [Fact]
    public void Whites_diagonal_to_the_iris_join_the_eye()
    {
        // Morris's vanilla eyes (Characters/Morris frame 0, rows 8-9): white up and out from the iris.
        string[] rows = { "...GDDDDDG...", "....FDDDF...." };
        var eyes = FindIn(rows);

        Assert.Equal(new[] { new PixelBox(3, 0, 2, 2), new PixelBox(8, 0, 2, 2) }, eyes);
    }

    [Fact]
    public void Glow_pixel_is_the_top_of_the_column_nearer_the_nose()
    {
        Assert.Equal((6, 11), SpriteEyes.GlowPixel(new PixelBox(5, 11, 2, 2), 16));
        Assert.Equal((9, 11), SpriteEyes.GlowPixel(new PixelBox(9, 11, 2, 2), 16));
        Assert.Equal((6, 12), SpriteEyes.GlowPixel(new PixelBox(5, 12, 2, 2), 16));
    }

    [Fact]
    public void A_one_pixel_eye_is_its_own_glow_pixel()
    {
        Assert.Equal((7, 9), SpriteEyes.GlowPixel(new PixelBox(7, 9, 1, 1), 16));
        Assert.Equal((9, 9), SpriteEyes.GlowPixel(new PixelBox(9, 9, 1, 1), 16));
    }

    [Fact]
    public void Ring_is_the_eight_neighbours_of_one_glow_pixel()
    {
        var ring = SpriteEyes.Ring(new[] { (7, 9) });

        Assert.Equal(8, ring.Count);
        Assert.DoesNotContain((7, 9), ring);
        Assert.Contains((6, 8), ring);
        Assert.Contains((8, 10), ring);
    }

    [Fact]
    public void Touching_rings_darken_each_pixel_once_and_never_a_glow_pixel()
    {
        // Morris's eyes, two pixels apart: the rings share column 8.
        var ring = SpriteEyes.Ring(new[] { (7, 9), (9, 9) });

        Assert.Equal(13, ring.Count);
        Assert.Equal(ring.Count, new HashSet<(int, int)>(ring).Count);
        Assert.DoesNotContain((7, 9), ring);
        Assert.DoesNotContain((9, 9), ring);
    }

    [Fact]
    public void No_iris_on_the_frame_finds_nothing()
    {
        Assert.Empty(FindIn(new[] { "....", ".GG.", "...." }));
    }
}
