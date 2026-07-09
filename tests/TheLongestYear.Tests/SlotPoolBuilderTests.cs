using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SlotPoolBuilderTests
{
    // Pantry room maps to Theme.Farming via RoomThemeMap.
    private static Dictionary<string, string> BundleData(params (int index, string name, string ingredients, int slots)[] bundles)
    {
        var d = new Dictionary<string, string>();
        foreach (var (index, name, ingredients, slots) in bundles)
            d[$"Pantry/{index}"] = $"{name}/O 465 1/{ingredients}/0/{slots}/0/{name}";
        return d;
    }

    private static IReadOnlyList<BundleRequirement> Reqs(params BundleRequirement[] reqs) => reqs;

    private static BundleRequirement SeasonalReq(string name, params string[] ids)
        => BundleRequirement.CreateSeasonal(name, Theme.Farming, ids, Season.Spring,
            new Dictionary<string, int>(), new Dictionary<string, int>());

    [Fact]
    public void Open_slots_of_an_in_play_bundle_are_pooled_with_stack_and_quality()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0 188 5 2", 2));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24", "(O)188"));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { false, false }, reqs,
            Theme.Farming, Season.Spring, _ => true);

        Assert.Equal(2, pool.Count);
        var green = pool.Single(s => s.ItemId == "(O)188");
        Assert.Equal(3, green.BundleIndex);
        Assert.Equal(1, green.IngredientIndex);
        Assert.Equal(5, green.Stack);
        Assert.Equal(2, green.Quality);
        Assert.Equal("Spring Crops", green.BundleName);
    }

    [Fact]
    public void Completed_slots_are_excluded()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0 188 5 2", 2));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24", "(O)188"));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { true, false }, reqs,
            Theme.Farming, Season.Spring, _ => true);

        Assert.Single(pool);
        Assert.Equal("(O)188", pool[0].ItemId);
    }

    [Fact]
    public void Bundle_with_enough_completed_slots_is_fully_excluded()
    {
        // Pick-1-of-2: one slot done ⇒ the bundle is complete; its remaining line is dead.
        var data = BundleData((3, "Rare Crops", "24 1 0 188 5 2", 1));
        var reqs = Reqs(BundleRequirement.CreatePercentage(
            "Rare Crops", Theme.Farming, new[] { "(O)24", "(O)188" },
            numberOfSlots: 1, cumulativeRequiredBySeason: new[] { 1, 1, 1, 1 }));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { true, false }, reqs,
            Theme.Farming, Season.Spring, _ => true);

        Assert.Empty(pool);
    }

    [Fact]
    public void Other_theme_and_off_season_and_category_slots_are_excluded()
    {
        var data = BundleData(
            (3, "Spring Crops", "24 1 0 -5 1 0", 2),     // -5 = category ref
            (4, "Summer Crops", "256 1 0", 1));
        var reqs = Reqs(
            SeasonalReq("Spring Crops", "(O)24"),
            BundleRequirement.CreateSeasonal("Summer Crops", Theme.Farming, new[] { "(O)256" }, Season.Summer,
                new Dictionary<string, int>(), new Dictionary<string, int>()));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs,
            Theme.Farming, Season.Spring, _ => true);

        // Summer Crops not in play in Spring; category ref skipped; only (O)24 remains.
        Assert.Single(pool);
        Assert.Equal("(O)24", pool[0].ItemId);
    }

    [Fact]
    public void Null_slot_state_means_all_open()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0", 1));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24"));
        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs, Theme.Farming, Season.Spring, _ => true);
        Assert.Single(pool);
    }
}
