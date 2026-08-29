using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcDonationReconcilerTests
{
    // value layout: name / reward / ingredients(id stack quality …) / color / numberOfSlots
    private static string Bundle(string ingredients, int slots) => $"B/0 0/{ingredients}/671/{slots}";

    private static List<(int, int, string)> Run(
        Dictionary<string, string> data, Dictionary<int, bool[]> completion)
        => CcDonationReconciler
            .DonatedSlots(data, idx => completion.TryGetValue(idx, out var a) ? a : null)
            .Select(Key)
            .ToList();

    private static (int, int, string) Key(DonatedSlot s) => (s.BundleIndex, s.IngredientIndex, s.ItemId);

    [Fact]
    public void Yields_only_completed_concrete_slots()
    {
        var data = new Dictionary<string, string> { ["Pantry/0"] = Bundle("24 1 0 188 1 0 190 1 0", 3) };
        var completion = new Dictionary<int, bool[]> { [0] = new[] { true, false, true } };

        Assert.Equal(new[] { (0, 0, "(O)24"), (0, 2, "(O)190") }, Run(data, completion));
    }

    [Fact]
    public void Skips_category_slot_but_keeps_concrete_slots_aligned()
    {
        // Slot 0 is a category (-5 = any animal product); slot 1 is concrete. Both complete.
        // The category must be skipped WITHOUT shifting slot 1's id — i.e. "(O)24", not the category.
        var data = new Dictionary<string, string> { ["Pantry/0"] = Bundle("-5 1 0 24 1 0", 2) };
        var completion = new Dictionary<int, bool[]> { [0] = new[] { true, true } };

        Assert.Equal(new[] { (0, 1, "(O)24") }, Run(data, completion));
    }

    [Fact]
    public void Skips_vault_and_other_non_item_rooms()
    {
        var data = new Dictionary<string, string>
        {
            ["Vault/34"] = Bundle("42 1 0", 1),       // money room — not themed
            ["AbandonedJojaMart/36"] = Bundle("24 1 0", 1),
        };
        var completion = new Dictionary<int, bool[]> { [34] = new[] { true }, [36] = new[] { true } };

        Assert.Empty(Run(data, completion));
    }

    [Fact]
    public void Normalizes_bare_and_qualified_ids()
    {
        var data = new Dictionary<string, string> { ["Crafts Room/13"] = Bundle("(O)388 1 0 709 1 0", 2) };
        var completion = new Dictionary<int, bool[]> { [13] = new[] { true, true } };

        Assert.Equal(new[] { (13, 0, "(O)388"), (13, 1, "(O)709") }, Run(data, completion));
    }

    [Fact]
    public void Skips_bundle_with_no_completion_array()
    {
        var data = new Dictionary<string, string> { ["Pantry/0"] = Bundle("24 1 0", 1) };
        Assert.Empty(Run(data, new Dictionary<int, bool[]>())); // index 0 absent → null array
    }

    [Fact]
    public void Tolerates_completion_array_shorter_than_ingredients()
    {
        var data = new Dictionary<string, string> { ["Pantry/0"] = Bundle("24 1 0 188 1 0", 2) };
        var completion = new Dictionary<int, bool[]> { [0] = new[] { true } }; // only slot 0 present

        Assert.Equal(new[] { (0, 0, "(O)24") }, Run(data, completion));
    }

    [Fact]
    public void Null_inputs_yield_nothing()
    {
        Assert.Empty(CcDonationReconciler.DonatedSlots(null, _ => null).ToList());
        Assert.Empty(CcDonationReconciler
            .DonatedSlots(new Dictionary<string, string>(), null).ToList());
    }

    [Fact]
    public void A_repeated_id_yields_one_slot_per_position()
    {
        var data = new Dictionary<string, string> { ["Crafts Room/13"] = Bundle("388 99 0 388 99 0 390 99 0 709 10 0", 4) };
        var completion = new Dictionary<int, bool[]> { [13] = new[] { true, true, false, true } };
        Assert.Equal(new[] { (13, 0, "(O)388"), (13, 1, "(O)388"), (13, 3, "(O)709") }, Run(data, completion));
    }
}
