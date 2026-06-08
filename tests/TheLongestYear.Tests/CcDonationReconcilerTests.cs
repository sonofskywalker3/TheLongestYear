using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcDonationReconcilerTests
{
    // value layout: name / reward / ingredients(id stack quality …) / color / numberOfSlots
    private static string Bundle(string ingredients, int slots) => $"B/0 0/{ingredients}/671/{slots}";

    private static List<string> Run(
        Dictionary<string, string> data, Dictionary<int, bool[]> completion)
        => CcDonationReconciler
            .DonatedConcreteIds(data, idx => completion.TryGetValue(idx, out var a) ? a : null)
            .ToList();

    [Fact]
    public void Yields_only_completed_concrete_slots()
    {
        var data = new Dictionary<string, string> { ["Pantry/0"] = Bundle("24 1 0 188 1 0 190 1 0", 3) };
        var completion = new Dictionary<int, bool[]> { [0] = new[] { true, false, true } };

        List<string> donated = Run(data, completion);

        Assert.Equal(new[] { "(O)24", "(O)190" }, donated);
    }

    [Fact]
    public void Skips_category_slot_but_keeps_concrete_slots_aligned()
    {
        // Slot 0 is a category (-5 = any animal product); slot 1 is concrete. Both complete.
        // The category must be skipped WITHOUT shifting slot 1's id — i.e. "(O)24", not the category.
        var data = new Dictionary<string, string> { ["Pantry/0"] = Bundle("-5 1 0 24 1 0", 2) };
        var completion = new Dictionary<int, bool[]> { [0] = new[] { true, true } };

        List<string> donated = Run(data, completion);

        Assert.Equal(new[] { "(O)24" }, donated);
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

        Assert.Equal(new[] { "(O)388", "(O)709" }, Run(data, completion));
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

        Assert.Equal(new[] { "(O)24" }, Run(data, completion));
    }

    [Fact]
    public void Null_inputs_yield_nothing()
    {
        Assert.Empty(CcDonationReconciler.DonatedConcreteIds(null, _ => null).ToList());
        Assert.Empty(CcDonationReconciler
            .DonatedConcreteIds(new Dictionary<string, string>(), null).ToList());
    }
}
