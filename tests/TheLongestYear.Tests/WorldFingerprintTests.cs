using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WorldFingerprintTests
{
    private static WorldFingerprint Baseline() => new WorldFingerprint
    {
        Year = 1, Season = Season.Spring, DayOfMonth = 1,
        Money = 500, Stamina = 270, InventoryItemCount = 0, TotalSkillXp = 0,
        CropCount = 0, PlacedObjectCount = 0, BuildingCount = 1,
        CompletedBundleCount = 0, FriendshipCount = 0, MailReceivedCount = 0,
        EventsSeenCount = 1, LowestMineLevel = -1
    };

    [Fact]
    public void Identical_fingerprints_have_no_diff_and_match()
    {
        var a = Baseline();
        var b = Baseline();
        Assert.Empty(a.Diff(b));
        Assert.True(a.Matches(b));
    }

    [Fact]
    public void A_single_difference_is_named_and_breaks_match()
    {
        var a = Baseline();
        var b = Baseline();
        b.CropCount = 3;

        var diff = a.Diff(b);
        Assert.Single(diff);
        Assert.Contains("CropCount", diff[0]);
        Assert.False(a.Matches(b));
    }

    [Fact]
    public void PlacedObjectCount_difference_is_ignored_world_gen_is_nondeterministic()
    {
        var a = Baseline();
        var b = Baseline();
        b.PlacedObjectCount = a.PlacedObjectCount + 9; // world-gen/mod spawn noise, not a leak

        Assert.Empty(a.Diff(b));
        Assert.True(a.Matches(b));
    }

    [Fact]
    public void Multiple_differences_are_all_named()
    {
        var a = Baseline();
        var b = Baseline();
        b.Money = 999;
        b.LowestMineLevel = 40;

        var diff = a.Diff(b);
        Assert.Equal(2, diff.Count);
        Assert.Contains(diff, d => d.Contains("Money"));
        Assert.Contains(diff, d => d.Contains("LowestMineLevel"));
    }
}
