using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class IconCountRowTests
{
    private const float Sprite = 48f;
    private const float Inner = 440f;
    private const float Gap = 10f;
    private const float MinGap = 6f;
    /// <summary>"100 (134)" at the hub's count scale.</summary>
    private const float WideLowered = 86.25f;
    /// <summary>"15 (20)".</summary>
    private const float Lowered = 66.25f;
    /// <summary>"1".</summary>
    private const float One = 11.25f;

    private static IconCountRow.Plan Fit(params float[] widths)
        => IconCountRow.Fit(s => widths.Select(w => w * s).ToArray(), Sprite, Inner, Gap, MinGap);

    /// <summary>Clear space between neighbouring icons' footprints (wider of sprite and count).</summary>
    private static IEnumerable<float> Gaps(IconCountRow.Plan plan, float[] widths)
    {
        for (int i = 1; i < widths.Length; i++)
        {
            float prev = System.Math.Max(Sprite, widths[i - 1] * plan.Shrink) / 2f;
            float next = System.Math.Max(Sprite, widths[i] * plan.Shrink) / 2f;
            yield return plan.Centres[i] - plan.Centres[i - 1] - prev - next;
        }
    }

    [Fact]
    public void Plain_icons_keep_the_old_spacing()
    {
        var plan = Fit(One, One, One);
        Assert.Equal(new[] { 24f, 82f, 140f }, plan.Centres);
        Assert.Equal(1f, plan.Shrink);
    }

    [Fact]
    public void A_one_next_to_a_lowered_count_gets_the_full_gap()
    {
        float[] widths = { One, One, One, One, Lowered };
        var plan = Fit(widths);
        Assert.All(Gaps(plan, widths), g => Assert.True(g >= Gap - 0.01f));
        Assert.Equal(1f, plan.Shrink);
    }

    [Fact]
    public void Five_goals_with_a_wide_lowered_count_fit_at_full_size()
    {
        float[] widths = { One, WideLowered, One, WideLowered, One };
        var plan = Fit(widths);
        Assert.Equal(1f, plan.Shrink);
        Assert.Equal(Gap, plan.Gap);
        Assert.True(plan.Width <= Inner);
    }

    [Fact]
    public void Five_wide_lowered_counts_keep_a_visible_gap_and_stay_inside()
    {
        float[] widths = { WideLowered, WideLowered, WideLowered, WideLowered, WideLowered };
        var plan = Fit(widths);
        Assert.True(plan.Width <= Inner);
        Assert.True(plan.Shrink < 1f);
        Assert.All(Gaps(plan, widths), g => Assert.True(g >= MinGap - 0.01f));
    }

    [Fact]
    public void Crowded_row_closes_the_gap_before_shrinking_counts()
    {
        // 4 wide counts: 345 + 3 x 10 = 375 fits; 5 sprites-plus-wide: 4 x 86.25 + 48 = 393 + 40 = 433 fits.
        float[] widths = { WideLowered, WideLowered, WideLowered, WideLowered, One };
        var plan = Fit(widths);
        Assert.Equal(1f, plan.Shrink);
        Assert.True(plan.Width <= Inner);
    }

    [Fact]
    public void Empty_row_is_empty()
    {
        var plan = Fit();
        Assert.Empty(plan.Centres);
        Assert.Equal(0f, plan.Width);
    }
}
