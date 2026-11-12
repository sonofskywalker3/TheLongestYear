using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoardRequirementsTests
{
    private static BundleSpec Spec(string room, int index, string name, int slots, params string[] itemIds) =>
        new(room, index, name, name, "O 495 30", 0, slots,
            itemIds.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    private static GeneratedBundleSet Set() => new(new[]
    {
        Spec("Pantry", 0, "Quality Crops", 3, "(O)24", "(O)188", "(O)190", "(O)192"),
        Spec("Pantry", 1, "Totally Unknown Bundle", 2, "(O)24", "(O)188", "(O)190"),
        Spec("Fish Tank", 9, "Night Fishing", 3, "(O)800", "(O)132", "(O)155"),
        Spec("Vault", 23, "2,500g", 1, "-1"),
    });

    [Fact]
    public void Stored_board_rebuilds_the_same_requirements_as_the_generated_set()
    {
        GeneratedBundleSet set = Set();
        var pins = new Dictionary<string, Season>();
        IReadOnlyList<BundleRequirement> fromSet = set.BuildRequirements(pins, GameplayConfig.DefaultBundleQuotas);

        // Round-trip the way MetaState persists it: a plain dictionary of the written strings.
        var stored = new Dictionary<string, string>(set.ToBundleData());
        IReadOnlyList<BundleRequirement> fromStored = BoardRequirements.Build(stored, pins, GameplayConfig.DefaultBundleQuotas);

        Assert.Equal(fromSet.Select(r => r.Name), fromStored.Select(r => r.Name));
        Assert.Equal(fromSet.Select(r => r.Kind), fromStored.Select(r => r.Kind));
        Assert.Equal(fromSet.Select(r => r.BundleIndex), fromStored.Select(r => r.BundleIndex));
        Assert.Equal(fromSet.Select(r => string.Join(",", r.Ingredients)), fromStored.Select(r => string.Join(",", r.Ingredients)));
        Assert.DoesNotContain(fromStored, r => r.Name == "2,500g"); // Vault skipped, as before
    }

    /// <summary>A Sneak Peek dish asked on a board (Hard, or a banked recipe) is due no earlier than
    /// the season its recipe can first be learned: Pink Cake airs on the Summer 17 rerun, so a
    /// per-item bundle beside a Spring dish still gives it Summer or later (Jeff, 2026-11-10).</summary>
    [Fact]
    public void A_sneak_peek_dish_is_never_due_before_the_season_its_recipe_can_be_learned()
    {
        const string pinkCake = "(O)221";
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(),
            effortDerived: new Dictionary<string, ItemEffort>
            {
                [pinkCake] = new(3, "dish Pink Cake: recipe week 7, " + TheLongestYear.Core.Availability.CookedDishAvailability.SneakPeekBasisMarker, 7, Season.Summer),
                ["(O)194"] = new(1, "dish Fried Egg", 6, Season.Spring),
                ["(O)195"] = new(1, "dish Omelet", 6, Season.Spring),
            },
            step: DifficultyStep.Hard);
        var board = new GeneratedBundleSet(new[] { Spec("Pantry", 0, "Home Cook's Feast", 3, "(O)194", "(O)195", pinkCake) });

        BundleRequirement req = BoardRequirements.Build(board.ToBundleData(), new Dictionary<string, Season>(),
            GameplayConfig.DefaultBundleQuotas, model).Single();

        Assert.Equal(BundleKind.PerItem, req.Kind);
        Assert.True(req.ItemSeasonPins![pinkCake] >= model.For(pinkCake).Gate, $"due {req.ItemSeasonPins[pinkCake]}");
    }

    [Fact]
    public void Season_pins_round_trip_through_their_stored_int_form()
    {
        var pins = new Dictionary<string, Season> { ["(O)800"] = Season.Fall, ["(O)24"] = Season.Spring };
        Dictionary<string, int> stored = BoardRequirements.PinsToStored(pins);
        Assert.Equal(pins, BoardRequirements.PinsFromStored(stored));
        Assert.Empty(BoardRequirements.PinsFromStored(null));
    }

    [Fact]
    public void RoomOf_strips_the_index()
    {
        Assert.Equal("Fish Tank", BoardRequirements.RoomOf("Fish Tank/9"));
        Assert.Equal("Vault", BoardRequirements.RoomOf("Vault"));
    }

    /// <summary>A pick-some bundle's season quota counts a flavored slot from the season the fruit
    /// it names can be had, not the season the good can be made. Dried Fruit is a Summer good, but
    /// a slot that names Cranberries cannot be filled before Fall, and with two Fall items beside
    /// it the Summer gate used to demand one slot nobody could fill.</summary>
    [Theory]
    [InlineData(false, new[] { 0, 1, 2, 2 })]
    [InlineData(true, new[] { 0, 0, 2, 2 })]
    public void A_flavored_slot_is_reachable_from_the_season_of_the_input_it_names(bool named, int[] quota)
    {
        const string cranberries = "(O)282";
        (string Id, Season Gate)[] items =
        {
            ("(O)DriedFruit", Season.Summer), (cranberries, Season.Fall), ("(O)410", Season.Fall), ("(O)408", Season.Fall),
        };
        var model = new ItemAvailabilityModel(items.ToDictionary(i => i.Id,
            i => new ItemAvailability(i.Gate, 1, "test", EffortSource.Derived, AvailabilityWeeks.FirstWeekOf(i.Gate), i.Gate),
            System.StringComparer.Ordinal));
        var board = new GeneratedBundleSet(new[] { Spec("Pantry", 5, "Sweet Things", 2, "DriedFruit", "(O)410", "(O)408") });
        var flavors = named
            ? new Dictionary<string, string> { [FlavoredSlotPass.KeyFor(5, 0)] = cranberries }
            : null;

        BundleRequirement req = BoardRequirements.Build(board.ToBundleData(), new Dictionary<string, Season>(),
            GameplayConfig.DefaultBundleQuotas, model, flavors).Single();

        Assert.Equal(quota, req.CumulativeRequiredBySeason);
    }

    /// <summary>In a bundle that needs every slot, a flavored slot is never due before the season
    /// the input it names can be had, even when the good itself is due sooner.</summary>
    [Fact]
    public void A_flavored_slot_is_never_due_before_the_input_it_names()
    {
        const string cranberries = "(O)282";
        (string Id, Season Gate)[] items = { ("(O)DriedFruit", Season.Summer), (cranberries, Season.Fall), ("(O)24", Season.Spring) };
        var model = new ItemAvailabilityModel(items.ToDictionary(i => i.Id,
            i => new ItemAvailability(i.Gate, 1, "test", EffortSource.Derived, AvailabilityWeeks.FirstWeekOf(i.Gate), i.Gate),
            System.StringComparer.Ordinal));
        var board = new GeneratedBundleSet(new[] { Spec("Pantry", 5, "Sweet Things", 2, "DriedFruit", "(O)24") });
        var flavors = new Dictionary<string, string> { [FlavoredSlotPass.KeyFor(5, 0)] = cranberries };

        BundleRequirement req = BoardRequirements.Build(board.ToBundleData(), new Dictionary<string, Season>(),
            GameplayConfig.DefaultBundleQuotas, model, flavors).Single();

        Assert.Equal(BundleKind.PerItem, req.Kind);
        Assert.True(req.ItemSeasonPins!["(O)DriedFruit"] >= Season.Fall, $"due {req.ItemSeasonPins["(O)DriedFruit"]}");
    }
}
