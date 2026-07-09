using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleClassifierTests
{
    private static ParsedBundle Make(string name, int x, params string[] ingredientIds)
    {
        var ings = ingredientIds.Select(id => new BundleIngredient(id, 1, 0)).ToList();
        return new ParsedBundle("CraftsRoom", 0, name, ings, x);
    }

    private static readonly IReadOnlyDictionary<string, Season> NoPins =
        new Dictionary<string, Season>();

    private static readonly IReadOnlyDictionary<string, int[]> NoQuotas =
        new Dictionary<string, int[]>();

    // ----- KIND 1 (Seasonal) name detection -----

    [Theory]
    [InlineData("Spring Foraging", "Spring")]
    [InlineData("Summer Foraging", "Summer")]
    [InlineData("Fall Foraging",   "Fall")]
    [InlineData("Winter Foraging", "Winter")]
    [InlineData("Spring Crops",    "Spring")]
    [InlineData("Summer Crops",    "Summer")]
    [InlineData("Fall Crops",      "Fall")]
    public void Seasonal_bundles_classified_by_name(string bundleName, string expectedSeason)
    {
        var parsed = Make(bundleName, 4, "(O)16", "(O)18", "(O)20", "(O)22");
        var req = BundleClassifier.Classify(parsed, Theme.Foraging, NoPins, NoQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Seasonal, req!.Kind);
        Assert.Equal(System.Enum.Parse<Season>(expectedSeason), req.SeasonalSeason);
    }

    [Fact]
    public void Seasonal_name_match_is_case_insensitive()
    {
        var parsed = Make("spring foraging", 4, "(O)16", "(O)18");
        var req = BundleClassifier.Classify(parsed, Theme.Foraging, NoPins, NoQuotas);
        Assert.Equal(BundleKind.Seasonal, req!.Kind);
    }

    [Fact]
    public void Winter_crops_is_not_a_known_seasonal_in_vanilla_but_pattern_still_matches()
    {
        // Vanilla doesn't ship a Winter Crops bundle, but if one appeared (mod), it'd classify.
        var parsed = Make("Winter Crops", 4, "(O)100", "(O)101", "(O)102", "(O)103");
        var req = BundleClassifier.Classify(parsed, Theme.Farming, NoPins, NoQuotas);
        Assert.Equal(BundleKind.Seasonal, req!.Kind);
        Assert.Equal(Season.Winter, req.SeasonalSeason);
    }

    // ----- KIND 3 (Percentage) by quota lookup -----

    [Fact]
    public void Quota_match_classifies_as_percentage()
    {
        var parsed = Make("Crab Pot", 5,
            "(O)715", "(O)372", "(O)717", "(O)718", "(O)719",
            "(O)720", "(O)721", "(O)716", "(O)722", "(O)723");
        var quotas = new Dictionary<string, int[]>
        {
            ["Crab Pot"] = new[] { 1, 3, 5, 5 }
        };

        var req = BundleClassifier.Classify(parsed, Theme.Fishing, NoPins, quotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Percentage, req!.Kind);
        Assert.Equal(5, req.NumberOfSlots);
        Assert.Equal(10, req.Ingredients.Count);
        Assert.Equal(new[] { 1, 3, 5, 5 }, req.CumulativeRequiredBySeason);
    }

    [Fact]
    public void Quota_lookup_with_X_GE_Y_falls_through_to_perItem()
    {
        // CB2: SVE-edited save data can leave a Chef's-style bundle with X=Y=7 (quota table
        // matches by name but the structural Percentage shape — X < Y — no longer holds).
        // The classifier must fall through to PerItem instead of throwing.
        var parsed = Make("Chef's", 7,
            "(O)1", "(O)2", "(O)3", "(O)4", "(O)5", "(O)6", "(O)7");
        var quotas = new Dictionary<string, int[]> { ["Chef's"] = new[] { 1, 2, 3, 4 } };

        var req = BundleClassifier.Classify(parsed, Theme.Farming, NoPins, quotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.PerItem, req!.Kind);
        Assert.Equal(7, req.NumberOfSlots);
        Assert.Equal(7, req.Ingredients.Count);
        // Quota array is ignored on the PerItem path.
        Assert.Null(req.CumulativeRequiredBySeason);
    }

    [Fact]
    public void Quota_lookup_with_X_LT_Y_still_classifies_as_percentage()
    {
        // Defensive: the X < Y path still picks the quota when present (the vanilla case).
        var parsed = Make("Crab Pot", 5,
            "(O)715", "(O)372", "(O)717", "(O)718", "(O)719",
            "(O)720", "(O)721", "(O)716");
        var quotas = new Dictionary<string, int[]> { ["Crab Pot"] = new[] { 1, 3, 5, 5 } };

        var req = BundleClassifier.Classify(parsed, Theme.Fishing, NoPins, quotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Percentage, req!.Kind);
    }

    // ----- KIND 2 (PerItem) by X == Y -----

    [Fact]
    public void X_equals_Y_classifies_as_perItem()
    {
        var parsed = Make("Blacksmith's", 3, "(O)334", "(O)335", "(O)336");
        var pins = new Dictionary<string, Season>
        {
            ["(O)334"] = Season.Spring,
            ["(O)335"] = Season.Summer,
            ["(O)336"] = Season.Fall
        };

        var req = BundleClassifier.Classify(parsed, Theme.Mining, pins, NoQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.PerItem, req!.Kind);
        Assert.Equal(3, req.Ingredients.Count);
        Assert.Equal(3, req.ItemSeasonPins!.Count);
    }

    [Fact]
    public void Unpinned_perItem_ingredients_still_count_toward_full_completion()
    {
        // Bundle has 3 ingredients but only 2 are pinned. The unpinned 3rd doesn't gate any
        // season, but IsFullyComplete still demands all 3 distinct donations.
        var parsed = Make("Half-pinned", 3, "(O)1", "(O)2", "(O)3");
        var pins = new Dictionary<string, Season>
        {
            ["(O)1"] = Season.Spring,
            ["(O)2"] = Season.Spring
            // (O)3 has no pin
        };

        var req = BundleClassifier.Classify(parsed, Theme.Mixed, pins, NoQuotas);

        var donated2 = new HashSet<string> { "(O)1", "(O)2" };
        Assert.False(req!.IsFullyComplete(donated2));
        donated2.Add("(O)3");
        Assert.True(req.IsFullyComplete(donated2));
    }

    [Fact]
    public void Duplicate_ingredients_dedupe_to_distinct_set()
    {
        // Construction-style: vanilla has Wood twice in the ingredient string. After dedup
        // we treat it as 1 entry for Wood.
        var parsed = Make("Construction", 3, "(O)388", "(O)388", "(O)390", "(O)709");
        var pins = new Dictionary<string, Season>
        {
            ["(O)388"] = Season.Spring,
            ["(O)390"] = Season.Spring,
            ["(O)709"] = Season.Summer
        };

        var req = BundleClassifier.Classify(parsed, Theme.Foraging, pins, NoQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.PerItem, req!.Kind);
        Assert.Equal(3, req.Ingredients.Count);
        Assert.Contains("(O)388", req.Ingredients);
        Assert.Contains("(O)390", req.Ingredients);
        Assert.Contains("(O)709", req.Ingredients);
    }

    [Fact]
    public void Vanilla_construction_with_X_gt_Y_classifies_as_perItem()
    {
        // CB1: vanilla Data/Bundles Construction is `(O)388 99 0 (O)388 99 0 (O)390 99 0 (O)709 10 0`
        // — 4 raw slots, Wood listed twice, deduping to 3 distinct ingredients. parsed.NumberOfSlots
        // is 4 (parser preserves the raw slot count); ingredients.Count is 3 (deduped). The
        // structural PerItem condition is "X >= Y", which holds here. Before the fix this fell
        // through to "didn't match any classification rule" because the strict X==Y check failed.
        var parsed = Make("Construction", 4, "(O)388", "(O)388", "(O)390", "(O)709");
        var pins = new Dictionary<string, Season>
        {
            ["(O)388"] = Season.Spring,
            ["(O)390"] = Season.Spring,
            ["(O)709"] = Season.Summer
        };

        var req = BundleClassifier.Classify(parsed, Theme.Foraging, pins, NoQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.PerItem, req!.Kind);
        // numberOfSlots is the deduped ingredient count (set-based donation ledger).
        Assert.Equal(3, req.NumberOfSlots);
        Assert.Equal(3, req.Ingredients.Count);
    }

    // ----- Derived-quota fallback (5th-sweep fix: remixed/mod bundles must never drop) -----

    [Fact]
    public void Unknown_xLtY_bundle_with_no_quota_classifies_with_derived_quota()
    {
        // Remixed saves generate pick-X-of-Y bundles with names outside DefaultBundleQuotas
        // (Rare Crops, Brewer's, Wild Medicine, ...). These must NOT be dropped — they'd
        // silently vanish from season checkpoints, the win gate, and weekly-theme pools
        // (khauser13's premature win + blank themes, xsansara's log WARNs, 2026-07-09 sweep).
        var parsed = Make("Mystery", 3, "(O)1", "(O)2", "(O)3", "(O)4", "(O)5");
        var req = BundleClassifier.Classify(parsed, Theme.Mixed, NoPins, NoQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Percentage, req!.Kind);
        Assert.Equal(3, req.NumberOfSlots);
        Assert.Equal(5, req.Ingredients.Count);
        // Derived ramp = floor(X * [0.25, 0.5, 0.75, 1.0]) — matches the curated Chef's
        // ramp exactly at X=3.
        Assert.Equal(new[] { 0, 1, 2, 3 }, req.CumulativeRequiredBySeason);
    }

    [Theory]
    [InlineData(1, new[] { 0, 0, 0, 1 })]
    [InlineData(2, new[] { 0, 1, 1, 2 })]
    [InlineData(3, new[] { 0, 1, 2, 3 })]
    [InlineData(5, new[] { 1, 2, 3, 5 })]
    [InlineData(6, new[] { 1, 3, 4, 6 })]
    public void Derived_quota_ramp_is_quarter_half_threequarter_full(int x, int[] expected)
    {
        Assert.Equal(expected, BundleClassifier.DerivedDefaultQuota(x));
    }

    [Fact]
    public void Named_quota_entry_wins_over_derived_fallback()
    {
        var parsed = Make("Chef's", 3, "(O)1", "(O)2", "(O)3", "(O)4", "(O)5", "(O)6");
        var quotas = new Dictionary<string, int[]> { ["Chef's"] = new[] { 1, 1, 2, 3 } };

        var req = BundleClassifier.Classify(parsed, Theme.Farming, NoPins, quotas);

        Assert.Equal(new[] { 1, 1, 2, 3 }, req!.CumulativeRequiredBySeason);
    }

    // ----- Skip cases -----

    [Fact]
    public void Bundle_with_only_category_ingredients_returns_null()
    {
        var parsed = Make("Animal", 5, "-5", "-6", "-14", "-18");
        var req = BundleClassifier.Classify(parsed, Theme.Farming, NoPins, NoQuotas);
        Assert.Null(req);
    }

    [Fact]
    public void Bare_numeric_ids_get_qualified_to_O_prefix()
    {
        var parsed = Make("Spring Foraging", 4, "16", "18", "20", "22");
        var req = BundleClassifier.Classify(parsed, Theme.Foraging, NoPins, NoQuotas);

        Assert.NotNull(req);
        Assert.Contains("(O)16", req!.Ingredients);
        Assert.Contains("(O)22", req.Ingredients);
    }
}
