using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Curated per-name ramps added 2026-08-21 (user ruling) for the remix-pool + authored
/// pick-X-of-Y bundles whose derived ramp was impossible or plainly harsh/lax.</summary>
public class CuratedQuotaRampTests
{
    // name → (X, expected ramp)
    public static IEnumerable<object[]> Curated => new[]
    {
        new object[] { "Winter Star",          2, new[] { 0, 0, 0, 2 } },
        new object[] { "Forager's",            2, new[] { 0, 0, 2, 2 } },
        new object[] { "Gil's Trophies",       2, new[] { 0, 0, 1, 2 } },
        new object[] { "Brewer's",             4, new[] { 0, 1, 2, 4 } },
        new object[] { "Preserver's",          4, new[] { 0, 1, 2, 4 } },
        new object[] { "Mineral",              4, new[] { 0, 1, 2, 4 } },
        new object[] { "Home Cook's Feast",    4, new[] { 0, 1, 2, 4 } },
        new object[] { "Fish Farmer's",        2, new[] { 0, 0, 1, 2 } },
        new object[] { "Artifact",             4, new[] { 0, 1, 2, 4 } },
        new object[] { "Four Seasons Sampler", 5, new[] { 1, 3, 4, 5 } },
        new object[] { "Rare Crops",           1, new[] { 0, 0, 1, 1 } },
        new object[] { "Garden",               4, new[] { 1, 2, 4, 4 } },
    };

    [Theory]
    [MemberData(nameof(Curated))]
    public void Curated_ramp_is_present_monotone_and_ends_at_X(string name, int x, int[] expected)
    {
        Assert.True(GameplayConfig.DefaultBundleQuotas.TryGetValue(name, out int[] ramp), $"{name} missing");
        Assert.Equal(expected, ramp);
        Assert.Equal(4, ramp.Length);
        for (int i = 1; i < ramp.Length; i++)
            Assert.True(ramp[i] >= ramp[i - 1], $"{name} ramp not monotone");
        Assert.Equal(x, ramp[^1]);
    }

    [Fact]
    public void Winter_Star_no_longer_asks_for_anything_before_winter()
    {
        // 2-of-4 Winter Star: Holly, Plum Pudding, Stuffing, Powdermelon (vanilla remix).
        var parsed = BundleParsing.Parse("Bulletin Board/31",
            "Winter Star/O 74 1/283 5 0 604 1 0 239 1 0 Powdermelon 5 0/2/2/0/Winter Star");
        var req = BundleClassifier.Classify(parsed, Theme.Mixed,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Percentage, req!.Kind);
        Assert.Equal(new[] { 0, 0, 0, 2 }, req.CumulativeRequiredBySeason);
        Assert.True(req.IsSatisfiedAtSeasonEnd(Season.Summer, new HashSet<string>()));
        Assert.True(req.IsSatisfiedAtSeasonEnd(Season.Fall, new HashSet<string>()));
        Assert.False(req.IsSatisfiedAtSeasonEnd(Season.Winter, new HashSet<string>()));
    }

    [Fact]
    public void Gils_Trophies_curated_ramp_wins_over_the_derived_one()
    {
        var parsed = BundleParsing.Parse("Boiler Room/22",
            "Gil's Trophies/O 879 5/(H)27 1 0 (W)13 1 0 (O)522 1 0 (O)810 1 0/4/2/0/Gil's Trophies");
        var req = BundleClassifier.Classify(parsed, Theme.Mining,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);

        Assert.Equal(new[] { 0, 0, 1, 2 }, req!.CumulativeRequiredBySeason);
        // The late-leaning derived ramp (2026-08-28) happens to coincide at X=2; the curated
        // entry is still the one in force (DefaultBundleQuotas carries it).
        Assert.True(GameplayConfig.DefaultBundleQuotas.ContainsKey("Gil's Trophies"));
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
