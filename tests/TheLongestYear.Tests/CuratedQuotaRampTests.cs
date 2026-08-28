using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Since the even-year spec (2026-08-28) the curated per-name ramp table is empty: a
/// pick-X-of-Y ramp follows the bundle's own items. What remains here: the table is empty, and a
/// user quota still overrides the derived ramp.</summary>
public class CuratedQuotaRampTests
{
    [Fact]
    public void The_default_quota_table_is_empty()
        => Assert.Empty(GameplayConfig.DefaultBundleQuotas);

    [Fact]
    public void Winter_Star_asks_for_nothing_before_winter_because_its_items_are_winter()
    {
        // 2-of-4 Winter Star: Holly, Plum Pudding, Stuffing, Powdermelon (vanilla remix). With an
        // empty model every item is unknown, which gates at Winter.
        var parsed = BundleParsing.Parse("Bulletin Board/31",
            "Winter Star/O 74 1/283 5 0 604 1 0 239 1 0 Powdermelon 5 0/2/2/0/Winter Star");
        var req = BundleClassifier.Classify(parsed, Theme.Mixed,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas,
            new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>()));

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Percentage, req!.Kind);
        Assert.Equal(new[] { 0, 0, 0, 2 }, req.CumulativeRequiredBySeason);
        Assert.True(req.IsSatisfiedAtSeasonEnd(Season.Summer, new HashSet<string>()));
        Assert.True(req.IsSatisfiedAtSeasonEnd(Season.Fall, new HashSet<string>()));
        Assert.False(req.IsSatisfiedAtSeasonEnd(Season.Winter, new HashSet<string>()));
    }
}

/// <summary>The ramp has to move with X, or a bundle made HARDER by the required-slots difficulty
/// modifier ends up demanding a SMALLER fraction of itself at every checkpoint. Jeff's ruling
/// 2026-08-27, with his own worked example: Animal [1,3,5,5] at X=5 becomes [2,4,6,6] at X=6.</summary>
public class QuotaRampShiftTests
{
    [Fact]
    public void Jeffs_Worked_Example_Animal_At_Hard()
        => Assert.Equal(new[] { 2, 4, 6, 6 },
            BundleClassifier.ShiftRampToSlotCount(new[] { 1, 3, 5, 5 }, 6));

    [Fact]
    public void An_Unchanged_Slot_Count_Leaves_The_Ramp_Alone()
        => Assert.Equal(new[] { 1, 3, 5, 5 },
            BundleClassifier.ShiftRampToSlotCount(new[] { 1, 3, 5, 5 }, 5));

    [Fact]
    public void Easy_Shifts_The_Ramp_Down()
        => Assert.Equal(new[] { 0, 2, 4, 4 },
            BundleClassifier.ShiftRampToSlotCount(new[] { 1, 3, 5, 5 }, 4));

    /// <summary>Extreme drives X all the way to Y; the endpoint follows and the rest moves with it.</summary>
    [Fact]
    public void Extreme_Pulls_The_Whole_Ramp_Up()
        => Assert.Equal(new[] { 3, 5, 7, 7 },
            BundleClassifier.ShiftRampToSlotCount(new[] { 1, 3, 5, 5 }, 7));

    [Fact]
    public void No_Entry_Can_Exceed_The_Slot_Count()
        => Assert.All(BundleClassifier.ShiftRampToSlotCount(new[] { 1, 3, 5, 5 }, 6),
            n => Assert.InRange(n, 0, 6));

    [Fact]
    public void The_Ramp_Stays_Monotonic()
    {
        int[] r = BundleClassifier.ShiftRampToSlotCount(new[] { 0, 0, 1, 2 }, 6);
        for (int i = 1; i < r.Length; i++)
            Assert.True(r[i] >= r[i - 1], $"ramp went backwards at {i}: {string.Join(",", r)}");
    }

    /// <summary>A deliberately never-gated bundle (endpoint zero) must not have a Spring demand
    /// invented for it out of nothing.</summary>
    [Fact]
    public void A_Never_Gated_Ramp_Is_Left_Alone()
        => Assert.Equal(new[] { 0, 0, 0, 0 },
            BundleClassifier.ShiftRampToSlotCount(new[] { 0, 0, 0, 0 }, 6));

    /// <summary>The lean-late shape survives: Winter Star is Fall/Winter items only, so its early
    /// zeros must stay zero rather than becoming impossible early demands.</summary>
    [Fact]
    public void A_Lean_Late_Ramp_Keeps_Its_Early_Zeros_Low()
    {
        int[] r = BundleClassifier.ShiftRampToSlotCount(new[] { 0, 0, 0, 2 }, 3);
        Assert.Equal(3, r[3]);
        Assert.True(r[0] <= 1, $"Spring demand jumped to {r[0]}");
    }
}
