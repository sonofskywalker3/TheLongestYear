using System;
using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08: one crow lands ON one scarecrow, and the flock pecks at random.</summary>
public class CrowPerchTests
{
    private const int Reach = 12;
    private const int FrameW = 20;
    private const int FrameH = 11;

    [Fact]
    public void Several_scarecrows_give_one_the_nearest_to_the_patch()
    {
        var scarecrows = new List<(int X, int Y)> { (30, 10), (22, 11), (10, 10) };
        Assert.Equal((22, 11), CrowPerch.ChooseScarecrow(scarecrows, (20, 10), Reach, FrameW, FrameH));
    }

    [Fact]
    public void Equally_near_scarecrows_keep_the_first()
    {
        var scarecrows = new List<(int X, int Y)> { (23, 10), (17, 10) };
        Assert.Equal((23, 10), CrowPerch.ChooseScarecrow(scarecrows, (20, 10), Reach, FrameW, FrameH));
    }

    [Fact]
    public void A_scarecrow_out_of_reach_is_no_scarecrow()
        => Assert.Null(CrowPerch.ChooseScarecrow(new List<(int X, int Y)> { (40, 10) }, (20, 10), Reach, FrameW, FrameH));

    [Fact]
    public void A_scarecrow_that_cannot_share_the_frame_is_passed_over_for_one_that_can()
    {
        // (20, 20) is 10 tiles straight down: within reach, but too tall a pair for an 11 tile frame.
        var scarecrows = new List<(int X, int Y)> { (20, 20), (31, 10) };
        Assert.Equal((31, 10), CrowPerch.ChooseScarecrow(scarecrows, (20, 10), Reach, FrameW, FrameH));
    }

    [Fact]
    public void No_scarecrows_gives_none()
        => Assert.Null(CrowPerch.ChooseScarecrow(new List<(int X, int Y)>(), (0, 0), Reach, FrameW, FrameH));

    [Fact]
    public void The_crow_stands_on_the_hat_above_the_scarecrow_tile()
    {
        (float x, float y) = CrowPerch.OnHat(10, 5, 64);
        Assert.Equal(10 * 64 + 32f, x);
        // Above the scarecrow's own tile, near the top of its two-tile sprite.
        Assert.True(y < 5 * 64 - 32, $"y {y}");
        Assert.True(y >= 5 * 64 - 64, $"y {y}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    public void Pecks_fit_the_time_on_the_ground_and_never_overlap(int seed)
    {
        IReadOnlyList<int> pecks = CrowPerch.PeckTimes(new Random(seed), 2200, 6400);
        Assert.NotEmpty(pecks);
        Assert.True(pecks[0] >= 2200 + CrowPerch.FirstPeckMinMs);
        for (int i = 0; i < pecks.Count; i++)
        {
            Assert.True(pecks[i] + CrowPerch.PeckLengthMs <= 6400);
            if (i > 0) Assert.True(pecks[i] >= pecks[i - 1] + CrowPerch.PeckLengthMs);
        }
    }

    [Fact]
    public void Two_crows_do_not_peck_in_step()
    {
        var rng = new Random(7);
        IReadOnlyList<int> a = CrowPerch.PeckTimes(rng, 2200, 6400);
        IReadOnlyList<int> b = CrowPerch.PeckTimes(rng, 2200, 6400);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void A_short_stay_still_gets_one_peck()
        => Assert.Single(CrowPerch.PeckTimes(new Random(1), 1000, 1000 + CrowPerch.PeckLengthMs + 50));

    [Fact]
    public void Too_short_to_peck_gets_none()
        => Assert.Empty(CrowPerch.PeckTimes(new Random(1), 1000, 1100));

    [Fact]
    public void The_pose_follows_the_peck()
    {
        var pecks = new[] { 1000, 2000 };
        Assert.Equal(CrowPerch.PeckPose.Standing, CrowPerch.PoseAt(pecks, 999));
        Assert.Equal(CrowPerch.PeckPose.HeadDown, CrowPerch.PoseAt(pecks, 1000));
        Assert.Equal(CrowPerch.PeckPose.Strike, CrowPerch.PoseAt(pecks, 1000 + CrowPerch.PeckLengthMs / 2));
        Assert.Equal(CrowPerch.PeckPose.Standing, CrowPerch.PoseAt(pecks, 1000 + CrowPerch.PeckLengthMs));
        Assert.Equal(CrowPerch.PeckPose.HeadDown, CrowPerch.PoseAt(pecks, 2010));
    }
}
