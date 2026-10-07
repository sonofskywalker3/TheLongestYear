using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer ruling 2026-10-07: the tamper only picks an item that is asked in exactly one
/// slot on the whole board, the item is the exact item (id plus flavour), and the aura marks that
/// exact item only.</summary>
public class TamperExactItemTests
{
    private const string Potato = "(O)192";
    private const string Pickles = "(O)342";
    private const string Dried = "(O)DriedFruit";
    private const string Apple = "613";
    private const string Cucumber = "(O)Cucumber";

    private static BundleRequirement Bundle(string name, Theme theme, int index, int slots, params string[] ids)
        => BundleRequirement.CreatePercentage(name, theme, ids, slots, new[] { 0, 0, 0, slots }, bundleIndex: index);

    private static Func<int, int, string?> Flavors(Dictionary<string, string> map)
        => (b, i) => map.TryGetValue(FlavoredSlotPass.KeyFor(b, i), out string? f) ? f : null;

    // ---------------------------------------------------------------- targets

    [Fact]
    public void An_item_asked_in_three_bundles_is_never_a_target()
    {
        var crops = Bundle("Spring Crops", Theme.Farming, 1, 3, Potato, "(O)24", "(O)188");
        var quality = Bundle("Quality Crops", Theme.Farming, 2, 3, Potato, "(O)190", "(O)250");
        var chef = Bundle("Chef's", Theme.Mixed, 3, 2, Potato, "(O)262");
        var ledger = new SlotLedger();
        ledger.Add(3, 0, Potato);   // one of the three is already filled; it still counts

        IReadOnlyList<TamperTarget> targets = TamperRule.Targets(ledger, new[] { crops, quality, chef });

        Assert.DoesNotContain(targets, t => t.ItemId == Potato);
        Assert.Contains(targets, t => t.ItemId == "(O)24");
        for (int seed = 0; seed < 40; seed++)
        {
            TamperTarget? pick = TamperRule.PickTarget(targets, id => id == Potato, new Random(seed));
            Assert.NotEqual(Potato, pick!.ItemId);
        }
    }

    [Fact]
    public void An_item_asked_in_one_slot_can_be_picked()
    {
        var crops = Bundle("Spring Crops", Theme.Farming, 1, 2, Potato, "(O)24");
        var quality = Bundle("Quality Crops", Theme.Farming, 2, 1, "(O)24");
        IReadOnlyList<TamperTarget> targets = TamperRule.Targets(new SlotLedger(), new[] { crops, quality });
        TamperTarget only = Assert.Single(targets);
        Assert.Equal(Potato, only.ItemId);
        Assert.Equal(Potato, TamperRule.PickTarget(targets, _ => false, new Random(1))!.ItemId);
    }

    [Fact]
    public void A_filled_copy_elsewhere_still_makes_an_item_multi_slot()
    {
        var crops = Bundle("Spring Crops", Theme.Farming, 1, 2, Potato, "(O)24");
        var chef = Bundle("Chef's", Theme.Mixed, 3, 2, Potato, "(O)262");
        var ledger = new SlotLedger();
        ledger.Add(3, 0, Potato);
        Assert.DoesNotContain(TamperRule.Targets(ledger, new[] { crops, chef }), t => t.ItemId == Potato);
    }

    [Fact]
    public void A_doubled_id_in_one_bundle_is_two_slots()
    {
        var crops = Bundle("Spring Crops", Theme.Farming, 1, 3, Potato, Potato, "(O)24");
        Assert.DoesNotContain(TamperRule.Targets(new SlotLedger(), new[] { crops }), t => t.ItemId == Potato);
    }

    [Fact]
    public void Dried_apple_and_dried_cucumber_are_each_single_and_carry_their_flavour()
    {
        var artisan = Bundle("Artisan", Theme.Farming, 4, 2, Dried, "(O)24");
        var pantry = Bundle("Pantry", Theme.Farming, 5, 2, Dried, "(O)188");
        var map = new Dictionary<string, string> { ["4:0"] = Apple, ["5:0"] = Cucumber };

        IReadOnlyList<TamperTarget> targets = TamperRule.Targets(new SlotLedger(), new[] { artisan, pantry }, Flavors(map));

        TamperTarget apple = Assert.Single(targets, t => t.Bundle.BundleIndex == 4 && t.ItemId == Dried);
        TamperTarget cucumber = Assert.Single(targets, t => t.Bundle.BundleIndex == 5 && t.ItemId == Dried);
        Assert.Equal(Apple, apple.Flavor);
        Assert.Equal(Cucumber, cucumber.Flavor);
    }

    [Fact]
    public void The_same_flavour_in_two_slots_is_one_item_asked_twice()
    {
        var artisan = Bundle("Artisan", Theme.Farming, 4, 2, Dried, "(O)24");
        var pantry = Bundle("Pantry", Theme.Farming, 5, 2, Dried, "(O)188");
        // One spelling qualified, one bare: the same apple.
        var map = new Dictionary<string, string> { ["4:0"] = "(O)613", ["5:0"] = "613" };
        Assert.DoesNotContain(TamperRule.Targets(new SlotLedger(), new[] { artisan, pantry }, Flavors(map)), t => t.ItemId == Dried);
    }

    [Fact]
    public void An_any_flavour_slot_overlaps_every_flavour_of_its_item()
    {
        // A slot left flavourless takes any dried fruit, Dried Apples included, so neither slot
        // is "the only one" asking for them.
        var artisan = Bundle("Artisan", Theme.Farming, 4, 2, Dried, "(O)24");
        var pantry = Bundle("Pantry", Theme.Farming, 5, 2, Dried, "(O)188");
        var map = new Dictionary<string, string> { ["4:0"] = Apple };
        Assert.DoesNotContain(TamperRule.Targets(new SlotLedger(), new[] { artisan, pantry }, Flavors(map)), t => t.ItemId == Dried);
    }

    [Fact]
    public void A_flavour_on_a_slot_the_patch_would_not_flavour_is_ignored()
    {
        // A stale map entry under a plain item (FlavoredSlotPatch skips it): the slot is a plain Potato.
        var crops = Bundle("Spring Crops", Theme.Farming, 1, 1, Potato);
        var map = new Dictionary<string, string> { ["1:0"] = Apple };
        TamperTarget only = Assert.Single(TamperRule.Targets(new SlotLedger(), new[] { crops }, Flavors(map)));
        Assert.Null(only.Flavor);
    }

    [Fact]
    public void Slot_counts_are_reported_for_the_debug_readout()
    {
        var crops = Bundle("Spring Crops", Theme.Farming, 1, 2, Potato, "(O)24");
        var chef = Bundle("Chef's", Theme.Mixed, 3, 2, Potato, "(O)262");
        Assert.Equal(2, TamperRule.SlotsAsking(new[] { crops, chef }, Potato, null, null));
        Assert.Equal(1, TamperRule.SlotsAsking(new[] { crops, chef }, "(O)24", null, null));
        Assert.Equal(0, TamperRule.SlotsAsking(new[] { crops, chef }, "(O)999", null, null));
    }

    // ---------------------------------------------------------------- replacements after a taint

    [Fact]
    public void A_tainted_item_is_never_a_later_replacement()
    {
        var crops = Bundle("Fall Crops", Theme.Farming, 2, 1, "(O)270");
        var target = new TamperTarget(crops, 0, "(O)270");
        var candidates = new List<TamperCandidate>
        {
            new(Potato, Theme.Farming, 5),        // the closest in effort, and tainted
            new("(O)248", Theme.Farming, 9),
        };
        var tainted = new[] { new TamperRecord { OldItemId = "192" } };   // bare spelling still matches
        for (int seed = 0; seed < 60; seed++)
        {
            TamperCandidate? pick = TamperRule.PickReplacement(target, 5, candidates, new Random(seed), tainted);
            Assert.Equal("(O)248", pick!.ItemId);
        }
    }

    [Fact]
    public void After_a_dried_apple_taint_dried_fruit_of_any_flavour_is_never_a_replacement()
    {
        // A replacement is written unflavoured, so "Any Dried Fruit" would take the tainted apples.
        var artisan = Bundle("Artisan", Theme.Farming, 4, 1, "(O)24");
        var target = new TamperTarget(artisan, 0, "(O)24");
        var candidates = new List<TamperCandidate> { new(Dried, Theme.Farming, 3), new("(O)428", Theme.Farming, 3) };
        var tainted = new[] { new TamperRecord { OldItemId = Dried, OldFlavor = Apple } };
        for (int seed = 0; seed < 60; seed++)
            Assert.Equal("(O)428", TamperRule.PickReplacement(target, 3, candidates, new Random(seed), tainted)!.ItemId);
    }

    [Fact]
    public void Only_tainted_candidates_leave_no_replacement()
    {
        var artisan = Bundle("Artisan", Theme.Farming, 4, 1, "(O)24");
        var target = new TamperTarget(artisan, 0, "(O)24");
        var candidates = new List<TamperCandidate> { new(Potato, Theme.Farming, 3) };
        Assert.Null(TamperRule.PickReplacement(target, 3, candidates, new Random(1), new[] { new TamperRecord { OldItemId = Potato } }));
        Assert.NotNull(TamperRule.PickReplacement(target, 3, candidates, new Random(1)));
    }

    // ---------------------------------------------------------------- the flavour map

    [Fact]
    public void A_rewritten_flavoured_slot_loses_its_flavour()
    {
        var map = new Dictionary<string, string> { ["4:0"] = Apple, ["5:0"] = Cucumber };
        Assert.True(TamperRule.ClearFlavor(map, 4, 0));
        Assert.False(map.ContainsKey("4:0"));
        Assert.Equal(Cucumber, map["5:0"]);
        Assert.False(TamperRule.ClearFlavor(map, 4, 0));
        Assert.False(TamperRule.ClearFlavor(null, 4, 0));
    }

    // ---------------------------------------------------------------- the aura

    private static TaintedItems Taint(params TamperRecord[] records)
    {
        var cache = new TaintedItems();
        cache.Refresh(records.ToList());
        return cache;
    }

    [Fact]
    public void Tainted_dried_apples_mark_dried_apples_only()
    {
        TaintedItems t = Taint(new TamperRecord { OldItemId = Dried, OldFlavor = Apple });
        Assert.True(t.IsTainted(Dried, "613"));
        Assert.True(t.IsTainted(Dried, "(O)613"));
        Assert.False(t.IsTainted(Dried, "Cucumber"));
        Assert.False(t.IsTainted(Dried, null));
        Assert.False(t.IsTainted("(O)613", null));   // the apple itself is not tainted
    }

    [Fact]
    public void A_plain_taint_does_not_mark_a_good_flavoured_with_it_and_back()
    {
        TaintedItems plain = Taint(new TamperRecord { OldItemId = Potato });
        Assert.True(plain.IsTainted(Potato, null));
        Assert.False(plain.IsTainted(Pickles, "192"));   // Pickled Potato is another item

        TaintedItems pickled = Taint(new TamperRecord { OldItemId = Pickles, OldFlavor = "192" });
        Assert.True(pickled.IsTainted(Pickles, "192"));
        Assert.False(pickled.IsTainted(Potato, null));
        Assert.False(pickled.IsTainted(Pickles, "24"));   // Pickled Parsnip is another item
    }

    [Fact]
    public void An_unflavoured_record_marks_every_flavour_of_its_item_as_before()
    {
        // What a pre-change save holds, and what an "any" slot asked for.
        TaintedItems t = Taint(new TamperRecord { OldItemId = Dried });
        Assert.True(t.IsTainted(Dried, "613"));
        Assert.True(t.IsTainted(Dried, null));
        Assert.False(t.IsTainted(Potato, null));
    }

    [Fact]
    public void An_old_save_record_without_a_flavour_reads_back_as_unflavoured()
    {
        const string oldJson = "{\"BundleIndex\":4,\"IngredientIndex\":0,\"BundleName\":\"Artisan\",\"OldItemId\":\"(O)DriedFruit\",\"NewItemId\":\"(O)24\",\"Stack\":3,\"DayOfYear\":86}";
        TamperRecord back = System.Text.Json.JsonSerializer.Deserialize<TamperRecord>(oldJson)!;
        Assert.Null(back.OldFlavor);
        Assert.Equal("(O)DriedFruit", back.OldItemId);
        Assert.True(Taint(back).IsTainted(Dried, "613"));

        var report = System.Text.Json.JsonSerializer.Deserialize<SabotageReport>("{\"Kind\":2,\"OldItemId\":\"(O)24\"}")!;
        Assert.Null(report.OldFlavor);
    }

    [Fact]
    public void Two_records_of_one_base_mark_both_flavours_and_nothing_else()
    {
        TaintedItems t = Taint(
            new TamperRecord { OldItemId = Dried, OldFlavor = Apple },
            new TamperRecord { OldItemId = "DriedFruit", OldFlavor = "(O)634" });
        Assert.True(t.IsTainted(Dried, "613"));
        Assert.True(t.IsTainted(Dried, "634"));
        Assert.False(t.IsTainted(Dried, "636"));
    }

    // ---------------------------------------------------------------- the line

    private static string Game(string word) => word.EndsWith("s") ? word + "es" : word.EndsWith("y") ? word[..^1] + "ies" : word.EndsWith("o") ? word + "es" : word + "s";

    [Theory]
    [InlineData("Dried Apples", Dried, true, "Dried Apples")]           // the game already made it plural
    [InlineData("Dried Cranberries", Dried, true, "Dried Cranberries")]
    [InlineData("Smoked Salmon", "(O)SmokedFish", true, "Smoked Salmon")]
    [InlineData("Potato", Potato, false, "Potatoes")]
    [InlineData("Parsnip", "(O)24", false, "Parsnips")]
    [InlineData("Blueberry Jelly", "(O)344", true, "Blueberry Jelly")]
    [InlineData("Wild Honey", "(O)340", false, "Wild Honey")]
    public void The_old_item_is_named_exactly_and_pluralised(string name, string baseId, bool flavored, string expected)
        => Assert.Equal(expected, ItemPlurals.Tainted(name, baseId, flavored, Game));
}
