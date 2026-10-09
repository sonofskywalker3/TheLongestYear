using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-10-09-bundle-count-dial: Easy one or two fewer (floor 2), Normal the
/// standard count (cap 6), Hard one or two more (cap 8), Extreme always 9.</summary>
public class BundleCountRuleTests
{
    private static IEnumerable<int> Targets(DifficultyStep step, int standard)
        => Enumerable.Range(0, 200).Select(s => BundleCountRule.For(step).Target(standard, new Random(s)));

    [Theory]
    [InlineData(6)]
    [InlineData(5)]
    [InlineData(4)]
    public void Easy_is_one_or_two_fewer(int standard)
    {
        var seen = Targets(DifficultyStep.Easy, standard).Distinct().OrderBy(t => t).ToList();
        Assert.Equal(new[] { standard - 2, standard - 1 }, seen);
    }

    [Fact]
    public void Easy_never_goes_below_two()
    {
        Assert.All(Targets(DifficultyStep.Easy, 3), t => Assert.Equal(2, t));
        Assert.All(Targets(DifficultyStep.Easy, 2), t => Assert.Equal(2, t));
    }

    [Fact]
    public void The_floor_never_grows_a_one_bundle_room()
    {
        Assert.All(Targets(DifficultyStep.Easy, 1), t => Assert.Equal(1, t));
        Assert.All(Targets(DifficultyStep.Normal, 1), t => Assert.Equal(1, t));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(6, 6)]
    [InlineData(7, 6)]
    [InlineData(10, 6)]
    public void Normal_keeps_the_standard_count_capped_at_six(int standard, int expected)
        => Assert.All(Targets(DifficultyStep.Normal, standard), t => Assert.Equal(expected, t));

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void Hard_is_one_or_two_more(int standard)
    {
        var seen = Targets(DifficultyStep.Hard, standard).Distinct().OrderBy(t => t).ToList();
        Assert.Equal(new[] { standard + 1, standard + 2 }, seen);
    }

    [Fact]
    public void Hard_caps_at_eight()
    {
        Assert.All(Targets(DifficultyStep.Hard, 7), t => Assert.Equal(8, t));
        Assert.All(Targets(DifficultyStep.Hard, 8), t => Assert.Equal(8, t));
        Assert.All(Targets(DifficultyStep.Hard, 12), t => Assert.Equal(8, t));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(14)]
    public void Extreme_is_always_nine(int standard)
        => Assert.All(Targets(DifficultyStep.Extreme, standard), t => Assert.Equal(9, t));

    /// <summary>Normal and Extreme roll nothing, so a stream shared with anything else would not
    /// move.</summary>
    [Theory]
    [InlineData(DifficultyStep.Normal)]
    [InlineData(DifficultyStep.Extreme)]
    public void Fixed_steps_consume_no_randomness(DifficultyStep step)
    {
        var used = new Random(7);
        BundleCountRule.For(step).Target(6, used);
        Assert.Equal(new Random(7).Next(), used.Next());
    }

    [Fact]
    public void Resolver_stamps_the_rule_for_the_step()
    {
        foreach (DifficultyStep step in Enum.GetValues<DifficultyStep>())
        {
            var p = DifficultyResolver.Resolve(new DifficultySettings { BundleCount = step }, new GameplayConfig());
            Assert.Equal(BundleCountRule.For(step), p.BundleCount);
        }
    }

    [Fact]
    public void A_profile_stamped_before_the_dial_has_no_rule()
        => Assert.Null(new DifficultyProfile().BundleCount);

    [Fact]
    public void Settings_plumbing_carries_the_dial()
    {
        var s = new DifficultySettings();
        Assert.Equal(DifficultyStep.Normal, s.BundleCount);
        Assert.True(s.IsAllNormal());

        s.BundleCount = DifficultyStep.Hard;
        Assert.False(s.IsAllNormal());
        Assert.True(s.AsksAllNormal()); // never applies to a vanilla board
        Assert.Equal(DifficultyStep.Hard, s.Clone().BundleCount);

        s.SetAll(DifficultyStep.Extreme);
        Assert.Equal(DifficultyStep.Extreme, s.BundleCount);
    }

    [Fact]
    public void Rule_round_trips_through_json()
    {
        var p = DifficultyResolver.Resolve(new DifficultySettings { BundleCount = DifficultyStep.Hard }, new GameplayConfig());
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(p);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<DifficultyProfile>(json)!;
        Assert.Equal(p.BundleCount, back.BundleCount);

        var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<DifficultyProfile>("{\"StackFactor\":1.0}")!;
        Assert.Null(legacy.BundleCount);
    }
}
