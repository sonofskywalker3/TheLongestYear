using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
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

    private static Dictionary<string, string> TwoRoomBoard() => new()
    {
        ["Boiler Room/20"] = "Blacksmith's/O 334 1/80 1 0 334 1 0/3/2/20/Blacksmith's",
        ["Pantry/0"] = "Animal/O 176 1/176 1 0 24 1 0/2/2/0/Animal",
    };

    private static IReadOnlyList<BundleRequirement> TwoRoomReqs() => Reqs(
        BundleRequirement.CreatePerItem("Blacksmith's", Theme.Mining,
            new Dictionary<string, Season> { ["(O)80"] = Season.Spring, ["(O)334"] = Season.Summer }),
        BundleRequirement.CreatePerItem("Animal", Theme.Farming,
            new Dictionary<string, Season> { ["(O)176"] = Season.Spring, ["(O)24"] = Season.Spring }));

    private static ItemKind Kind(string id) => id switch
    {
        "(O)80" => ItemKind.Gem, "(O)176" => ItemKind.Egg, _ => ItemKind.Other,
    };

    [Fact]
    public void Activity_themes_match_lines_by_kind_across_every_room()
    {
        var spelunking = SlotPoolBuilder.OpenSlotsForTheme(TwoRoomBoard(), _ => null, TwoRoomReqs(), Theme.Spelunking, Season.Spring, _ => true, weekOfYear: 1, Kind);
        Assert.Equal(new[] { "(O)80" }, spelunking.Select(s => s.ItemId));
        Assert.True(spelunking[0].Due);

        var kitchen = SlotPoolBuilder.OpenSlotsForTheme(TwoRoomBoard(), _ => null, TwoRoomReqs(), Theme.Kitchen, Season.Summer, _ => true, weekOfYear: 5, Kind);
        Assert.Equal(new[] { "(O)176" }, kitchen.Select(s => s.ItemId));
        Assert.False(kitchen[0].Due);   // pinned Spring, now Summer: filler

        var mixed = SlotPoolBuilder.OpenSlotsForTheme(TwoRoomBoard(), _ => null, TwoRoomReqs(), Theme.Mixed, Season.Spring, _ => true, weekOfYear: 1, Kind);
        Assert.Equal(4, mixed.Count);   // Mixed means anything on the board

        var mining = SlotPoolBuilder.OpenSlotsForTheme(TwoRoomBoard(), _ => null, TwoRoomReqs(), Theme.Mining, Season.Spring, _ => true, weekOfYear: 1, Kind);
        Assert.Equal(2, mining.Count);  // room themes stay bundle-level
        Assert.Equal(new[] { true, false }, mining.Select(s => s.Due));
    }

    [Fact]
    public void Without_a_classifier_mixed_stays_the_bulletin_board_room()
        => Assert.Empty(SlotPoolBuilder.OpenSlotsForTheme(TwoRoomBoard(), _ => null, TwoRoomReqs(), Theme.Mixed, Season.Spring, _ => true, weekOfYear: 1));

    [Fact]
    public void Open_slots_of_an_in_play_bundle_are_pooled_with_stack_and_quality()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0 188 5 2", 2));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24", "(O)188"));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { false, false }, reqs,
            Theme.Farming, Season.Spring, _ => true, weekOfYear: 1);

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
            Theme.Farming, Season.Spring, _ => true, weekOfYear: 1);

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
            Theme.Farming, Season.Spring, _ => true, weekOfYear: 1);

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
            Theme.Farming, Season.Spring, _ => true, weekOfYear: 1);

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
            data, _ => null, reqs, Theme.Farming, Season.Spring, _ => true, weekOfYear: 1);
        Assert.Single(pool);
    }

    [Fact]
    public void A_spring_stretch_line_is_a_goal_from_spring_week_4()
    {
        // "(O)s" is pinned Winter normally (not due in Spring) but stretched to Spring: it must
        // become an in-play, due, Stretch goal once weekOfYear reaches Spring's last week (4),
        // regardless of the obtainability predicate (here: not truly obtainable until week 6).
        var data = BundleData((3, "Stretchy", "s 1 0", 1));
        var reqs = Reqs(BundleRequirement.CreatePerItem(
            "Stretchy", Theme.Mixed,
            new Dictionary<string, Season> { ["(O)s"] = Season.Winter },
            stretchLines: new Dictionary<string, Season> { ["(O)s"] = Season.Spring }));

        static bool Obtainable(string id, int week) => id != "(O)s" || week >= 6;
        static ItemKind KindOf(string id) => ItemKind.Other;

        var week3 = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs, Theme.Mixed, Season.Spring,
            id => Obtainable(id, 3), weekOfYear: 3, KindOf);
        var week4 = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs, Theme.Mixed, Season.Spring,
            id => Obtainable(id, 4), weekOfYear: 4, KindOf);

        Assert.DoesNotContain(week3, s => s.ItemId == "(O)s");
        BonusSlot slot = Assert.Single(week4, s => s.ItemId == "(O)s");
        Assert.True(slot.Stretch);
        Assert.True(slot.Due);
    }

    [Fact]
    public void Slots_carry_the_boost_route_tag()
    {
        // Garlic ((O)248) is a year-2 crop: always "Boost: Year-Two Seeds", regardless of
        // routeTagOf. A dish whose availability basis names Sneak Peek gets that tag instead. An
        // item with neither gets no tag at all.
        var data = BundleData((3, "Boosted", "248 1 0 611 1 0 24 1 0", 3));
        var reqs = Reqs(SeasonalReq("Boosted", "(O)248", "(O)611", "(O)24"));

        static string? BasisOf(string id) => id == "(O)611"
            ? "dish Blackberry Cobbler: recipe week 10, " + CookedDishAvailability.SneakPeekBasisMarker
            : null;

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs, Theme.Farming, Season.Spring, _ => true, weekOfYear: 1,
            routeTagOf: BasisOf);

        Assert.Equal("Boost: Year-Two Seeds", pool.Single(s => s.ItemId == "(O)248").RouteTag);
        Assert.Equal("Boost: Sneak Peek", pool.Single(s => s.ItemId == "(O)611").RouteTag);
        Assert.Null(pool.Single(s => s.ItemId == "(O)24").RouteTag);
    }
    [Fact]
    public void A_doubled_id_offers_its_second_slot_once_the_first_is_filled()
    {
        // Construction shape: Wood, Wood, Stone, Hardwood. Slot 0 (Wood) already filled on the board.
        // One slot, one goal (spec 2026-08-29-per-slot-ledger): the second Wood slot is its own goal.
        var board = new Dictionary<string, string>
        {
            ["Crafts Room/13"] = "Construction/O 388 1/388 99 0 388 99 0 390 99 0 709 10 0/4/4/13/Construction",
        };
        var req = BundleRequirement.CreatePerItem("Construction", Theme.Foraging,
            new[] { "(O)388", "(O)390", "(O)709" },
            new Dictionary<string, Season> { ["(O)388"] = Season.Spring, ["(O)390"] = Season.Spring, ["(O)709"] = Season.Spring },
            bundleIndex: 13,
            slots: new[] { new BundleSlot(0, "(O)388"), new BundleSlot(1, "(O)388"), new BundleSlot(2, "(O)390"), new BundleSlot(3, "(O)709") });
        bool[] state = { true, false, false, false };

        var pool = SlotPoolBuilder.OpenSlotsForTheme(board, _ => state, Reqs(req), Theme.Foraging, Season.Spring, _ => true, weekOfYear: 1);

        Assert.Contains(pool, s => s.BundleIndex == 13 && s.IngredientIndex == 1 && s.ItemId == "(O)388");
        Assert.DoesNotContain(pool, s => s.IngredientIndex == 0);
        Assert.Equal(3, pool.Count);   // Wood slot 1, Stone, Hardwood
    }
}
