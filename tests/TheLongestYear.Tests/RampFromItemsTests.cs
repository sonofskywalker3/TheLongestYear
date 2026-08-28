using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-08-28-even-year: a pick-X-of-Y ramp is an even quarter split of X, never
/// above what the bundle's own items can supply by each season's gate.</summary>
public class RampFromItemsTests
{
    private static ItemAvailabilityModel Model(params (string Id, Season Gate)[] items)
        => new(items.ToDictionary(i => i.Id,
            i => new ItemAvailability(i.Gate, 1, "test", EffortSource.Derived, AvailabilityWeeks.FirstWeekOf(i.Gate), i.Gate),
            System.StringComparer.Ordinal));

    [Fact]
    public void Even_split_when_everything_is_spring()
    {
        var model = Model(("a", Season.Spring), ("b", Season.Spring), ("c", Season.Spring), ("d", Season.Spring), ("e", Season.Spring), ("f", Season.Spring));
        Assert.Equal(new[] { 1, 2, 3, 4 }, BundleClassifier.RampFromItems(4, new[] { "a", "b", "c", "d", "e", "f" }, model));
    }

    [Fact]
    public void Ramp_never_asks_for_more_than_is_reachable()
    {
        var model = Model(("a", Season.Spring), ("b", Season.Spring), ("c", Season.Summer), ("d", Season.Summer), ("e", Season.Fall), ("f", Season.Fall));
        Assert.Equal(new[] { 1, 2, 3, 4 }, BundleClassifier.RampFromItems(4, new[] { "a", "b", "c", "d", "e", "f" }, model));
        var late = Model(("a", Season.Fall), ("b", Season.Fall), ("c", Season.Winter), ("d", Season.Winter));
        Assert.Equal(new[] { 0, 0, 2, 2 }, BundleClassifier.RampFromItems(2, new[] { "a", "b", "c", "d" }, late));
    }

    [Fact]
    public void Winter_always_demands_x_and_the_ramp_is_monotone()
    {
        var model = Model(("a", Season.Winter), ("b", Season.Winter), ("c", Season.Winter));
        Assert.Equal(new[] { 0, 0, 0, 2 }, BundleClassifier.RampFromItems(2, new[] { "a", "b", "c" }, model));
    }

    [Fact]
    public void Unknown_items_count_as_winter()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>());
        Assert.Equal(new[] { 0, 0, 0, 3 }, BundleClassifier.RampFromItems(3, new[] { "x", "y", "z", "w" }, model));
    }

    [Fact]
    public void Classify_uses_the_items_when_a_model_is_given_and_a_user_quota_still_wins()
    {
        var parsed = BundleParsing.Parse("Pantry/5",
            "Preserver's/O 24 1/344 1 0 346 1 0 342 1 0 445 1 0/4/2/0/Preserver's");
        var model = Model(("(O)344", Season.Spring), ("(O)346", Season.Summer), ("(O)342", Season.Spring), ("(O)445", Season.Fall));
        var derived = BundleClassifier.Classify(parsed, Theme.Farming, new Dictionary<string, Season>(),
            new Dictionary<string, int[]>(), model)!;
        Assert.Equal(BundleKind.Percentage, derived.Kind);
        Assert.Equal(new[] { 1, 1, 2, 2 }, derived.CumulativeRequiredBySeason);

        var user = new Dictionary<string, int[]> { ["Preserver's"] = new[] { 0, 0, 0, 2 } };
        var overridden = BundleClassifier.Classify(parsed, Theme.Farming, new Dictionary<string, Season>(), user, model)!;
        Assert.Equal(new[] { 0, 0, 0, 2 }, overridden.CumulativeRequiredBySeason);
    }
}
