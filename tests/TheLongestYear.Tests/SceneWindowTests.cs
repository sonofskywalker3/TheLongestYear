using System;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: the hall's windows glow like firelight and one shadow figure stands in one,
/// and the shapes must never spill outside the glass. The flicker, the figure's step and the cut are pure
/// arithmetic, so they are tested here.</summary>
public class SceneWindowTests
{
    // ------------------------------------------------------------------ the flicker

    [Fact]
    public void The_flicker_stays_between_the_declared_bounds()
    {
        for (int ms = 0; ms < 8000; ms += 7)
        {
            for (int window = 0; window < 6; window++)
            {
                float alpha = SceneWindow.Flicker(ms, window);
                // Jeff, 2026-09-23: the light was too bright. The glow is 0.35 plus or minus 0.10.
                Assert.InRange(alpha, 0.25f, 0.45f);
            }
        }
    }

    [Fact]
    public void Two_windows_do_not_flicker_in_step()
    {
        Assert.NotEqual(SceneWindow.Flicker(0, 0), SceneWindow.Flicker(0, 1), 4);
    }

    [Fact]
    public void The_flicker_comes_back_round_after_a_whole_period()
    {
        // Two pi periods of 180 ms is about 1131 ms, so a full turn lands within a millisecond.
        float start = SceneWindow.Flicker(0, 0);
        float round = SceneWindow.Flicker((int)Math.Round(2 * Math.PI * 180.0), 0);
        Assert.Equal(start, round, 2);
    }

    // ------------------------------------------------------------------ the figure (Jeff, 2026-10-07)

    [Fact]
    public void The_figure_waits_out_of_sight_then_steps_across_and_stands()
    {
        Assert.Equal(-64, SceneWindow.FigureX(0, 900, 2400, -64, 0, out bool walking));
        Assert.False(walking);
        int mid = SceneWindow.FigureX(1650, 900, 2400, -64, 0, out walking);
        Assert.True(walking);
        Assert.InRange(mid, -40, -24);
        Assert.Equal(0, SceneWindow.FigureX(2400, 900, 2400, -64, 0, out walking));
        Assert.False(walking);
        Assert.Equal(0, SceneWindow.FigureX(9000, 900, 2400, -64, 0, out walking));
        Assert.False(walking);
    }

    [Fact]
    public void The_figure_only_ever_moves_toward_where_it_stands()
    {
        int last = int.MinValue;
        for (int ms = 0; ms < 4000; ms += 17)
        {
            int x = SceneWindow.FigureX(ms, 900, 2400, -64, 0, out _);
            Assert.InRange(x, -64, 0);
            Assert.True(x >= last);
            last = x;
        }
    }

    [Fact]
    public void A_figure_that_arrives_before_it_enters_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWindow.FigureX(0, 900, 900, 0, 1, out _));
    }

    [Fact]
    public void The_working_poses_loop_in_order_from_when_it_arrives()
    {
        Assert.Equal(0, SceneWindow.Cycle(0, 2400, 4, 320));
        Assert.Equal(0, SceneWindow.Cycle(2400, 2400, 4, 320));
        Assert.Equal(1, SceneWindow.Cycle(2400 + 320, 2400, 4, 320));
        Assert.Equal(3, SceneWindow.Cycle(2400 + 3 * 320 + 10, 2400, 4, 320));
        Assert.Equal(0, SceneWindow.Cycle(2400 + 4 * 320, 2400, 4, 320));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWindow.Cycle(0, 0, 0, 320));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWindow.Cycle(0, 0, 4, 0));
    }

    private static bool[,] Mask(params string[] rows)
    {
        var mask = new bool[rows[0].Length, rows.Length];
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                mask[x, y] = rows[y][x] == '#';
        return mask;
    }

    [Fact]
    public void Eyes_and_a_mouth_inside_the_outline_are_filled_black()
    {
        bool[,] solid = SceneWindow.FillHoles(Mask(
            ".....",
            ".###.",
            ".#.#.",
            ".###.",
            "....."));
        Assert.True(solid[2, 2], "the enclosed hole should be filled");
        Assert.False(solid[0, 0], "the outside stays clear");
        Assert.False(solid[2, 4], "the outside stays clear");
    }

    [Fact]
    public void A_gap_open_to_the_outside_is_not_filled()
    {
        bool[,] solid = SceneWindow.FillHoles(Mask(
            ".....",
            ".#.#.",
            ".#.#.",
            ".###.",
            "....."));
        Assert.False(solid[2, 1]);
        Assert.False(solid[2, 2]);
        Assert.True(solid[1, 1]);
    }

    // ------------------------------------------------------------------ the cut

    [Fact]
    public void A_shape_wholly_inside_the_pane_is_drawn_whole()
    {
        bool hit = SceneWindow.Clip(10, 10, 20, 20, 0, 0, 100, 100, 16, 8, out SceneWindow.ClippedDraw draw);
        Assert.True(hit);
        Assert.Equal(10, draw.DestX);
        Assert.Equal(10, draw.DestY);
        Assert.Equal(20, draw.DestWidth);
        Assert.Equal(20, draw.DestHeight);
        Assert.Equal(0, draw.SourceX);
        Assert.Equal(0, draw.SourceY);
        Assert.Equal(16, draw.SourceWidth);
        Assert.Equal(8, draw.SourceHeight);
    }

    [Fact]
    public void A_shape_hanging_off_the_left_of_the_pane_is_cut_at_the_frame()
    {
        // Half the shape is left of the pane, so the right half of the texture is drawn.
        bool hit = SceneWindow.Clip(-10, 0, 20, 10, 0, 0, 100, 10, 16, 8, out SceneWindow.ClippedDraw draw);
        Assert.True(hit);
        Assert.Equal(0, draw.DestX);
        Assert.Equal(10, draw.DestWidth);
        Assert.Equal(8, draw.SourceX);
        Assert.Equal(8, draw.SourceWidth);
    }

    [Fact]
    public void A_shape_hanging_off_the_right_of_the_pane_is_cut_at_the_frame()
    {
        bool hit = SceneWindow.Clip(90, 0, 20, 10, 0, 0, 100, 10, 16, 8, out SceneWindow.ClippedDraw draw);
        Assert.True(hit);
        Assert.Equal(90, draw.DestX);
        Assert.Equal(10, draw.DestWidth);
        Assert.Equal(0, draw.SourceX);
        Assert.Equal(8, draw.SourceWidth);
    }

    [Fact]
    public void A_shape_taller_than_the_pane_is_cut_top_and_bottom()
    {
        bool hit = SceneWindow.Clip(0, -5, 10, 30, 0, 0, 10, 20, 16, 8, out SceneWindow.ClippedDraw draw);
        Assert.True(hit);
        Assert.Equal(0, draw.DestY);
        Assert.Equal(20, draw.DestHeight);
        Assert.True(draw.SourceY > 0);
        Assert.True(draw.SourceY + draw.SourceHeight <= 8);
    }

    [Fact]
    public void A_shape_that_misses_the_pane_is_not_drawn_at_all()
    {
        Assert.False(SceneWindow.Clip(200, 0, 20, 20, 0, 0, 100, 100, 16, 8, out _));
        Assert.False(SceneWindow.Clip(-50, 0, 20, 20, 0, 0, 100, 100, 16, 8, out _));
        Assert.False(SceneWindow.Clip(0, 300, 20, 20, 0, 0, 100, 100, 16, 8, out _));
    }

    [Fact]
    public void A_shape_touching_the_pane_edge_only_is_not_drawn()
    {
        Assert.False(SceneWindow.Clip(100, 0, 20, 20, 0, 0, 100, 100, 16, 8, out _));
    }

    [Fact]
    public void The_cut_never_leaves_the_pane_or_the_texture()
    {
        const int windowX = 40, windowY = 20, windowW = 64, windowH = 48;
        const int sourceW = 12, sourceH = 7;
        for (int x = -200; x < 300; x++)
        {
            if (!SceneWindow.Clip(x, 10, 96, 70, windowX, windowY, windowW, windowH, sourceW, sourceH, out SceneWindow.ClippedDraw draw))
                continue;
            Assert.InRange(draw.DestX, windowX, windowX + windowW);
            Assert.InRange(draw.DestX + draw.DestWidth, windowX, windowX + windowW);
            Assert.InRange(draw.DestY, windowY, windowY + windowH);
            Assert.InRange(draw.DestY + draw.DestHeight, windowY, windowY + windowH);
            Assert.InRange(draw.SourceX, 0, sourceW - 1);
            Assert.InRange(draw.SourceX + draw.SourceWidth, 1, sourceW);
            Assert.InRange(draw.SourceY, 0, sourceH - 1);
            Assert.InRange(draw.SourceY + draw.SourceHeight, 1, sourceH);
        }
    }

    [Fact]
    public void A_shape_or_a_pane_or_a_texture_with_no_size_is_not_drawn()
    {
        Assert.False(SceneWindow.Clip(0, 0, 0, 20, 0, 0, 100, 100, 16, 8, out _));
        Assert.False(SceneWindow.Clip(0, 0, 20, 0, 0, 0, 100, 100, 16, 8, out _));
        Assert.False(SceneWindow.Clip(0, 0, 20, 20, 0, 0, 0, 100, 16, 8, out _));
        Assert.False(SceneWindow.Clip(0, 0, 20, 20, 0, 0, 100, 0, 16, 8, out _));
        Assert.False(SceneWindow.Clip(0, 0, 20, 20, 0, 0, 100, 100, 0, 8, out _));
        Assert.False(SceneWindow.Clip(0, 0, 20, 20, 0, 0, 100, 100, 16, 0, out _));
    }

    // ------------------------------------------------------------------ the texel grid

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 4)]
    [InlineData(7, 4)]
    [InlineData(-1, -4)]
    [InlineData(-4, -4)]
    [InlineData(-5, -8)]
    public void A_position_snaps_down_to_the_texel_grid(int pixels, int snapped)
    {
        Assert.Equal(snapped, SceneWindow.SnapToTexel(pixels, 4));
    }
}
