using System;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: the hall's windows glow like firelight and shadow shapes cross them,
/// and the shapes must never spill outside the glass. The flicker, the slide and the cut are pure
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
                Assert.InRange(alpha, 0.40f, 0.70f);
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

    // ------------------------------------------------------------------ the slide

    [Fact]
    public void Every_shape_starts_and_stays_within_its_own_journey()
    {
        const int spanLeft = 3000, spanWidth = 768, shapeWidth = 96;
        for (int shape = 0; shape < SceneWindow.ShapeCount; shape++)
        {
            for (int ms = 0; ms < 20000; ms += 13)
            {
                int x = SceneWindow.SlideX(ms, shape, spanLeft, spanWidth, shapeWidth);
                Assert.InRange(x, spanLeft - shapeWidth, spanLeft + spanWidth + shapeWidth);
            }
        }
    }

    [Fact]
    public void The_shapes_are_not_on_top_of_each_other_when_the_scene_opens()
    {
        int first = SceneWindow.SlideX(0, 0, 0, 768, 96);
        int second = SceneWindow.SlideX(0, 1, 0, 768, 96);
        int third = SceneWindow.SlideX(0, 2, 0, 768, 96);
        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void A_shape_moves_to_the_right_as_time_passes()
    {
        Assert.True(SceneWindow.SlideX(500, 0, 0, 768, 96) > SceneWindow.SlideX(0, 0, 0, 768, 96));
    }

    [Fact]
    public void A_negative_clock_is_treated_as_the_start()
    {
        Assert.Equal(SceneWindow.SlideX(0, 0, 0, 768, 96), SceneWindow.SlideX(-500, 0, 0, 768, 96));
    }

    [Fact]
    public void Asking_for_a_shape_that_does_not_exist_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWindow.SlideX(0, SceneWindow.ShapeCount, 0, 768, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWindow.SlideX(0, -1, 0, 768, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWindow.SlideX(0, 0, 0, 768, 0));
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
}
