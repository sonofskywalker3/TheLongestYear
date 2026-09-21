using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Plan 2026-09-21-flavored-bundle-slots. A Dried Fruit slot names one fruit and demands
/// that fruit, the way Mr. Raccoon's bundles do. The flavor is derived from the board seed and
/// never persisted, so these rules have to be pure and repeatable.</summary>
public class FlavoredSlotRulesTests
{
    private const string Apple = "(O)613";        // Fall tree, week 9
    private const string Orange = "(O)635";       // Summer tree, week 5
    private const string Cherry = "(O)638";       // Spring tree: second year or the cart
    private const string Apricot = "(O)634";      // ditto
    private const string Blueberry = "(O)258";    // Summer crop
    private const string Morel = "(O)257";
    private const string Sardine = "(O)131";

    private static PoolItem Fruit(string id) => new(id, 100, 1, new List<Season>(), new List<string>(), -79);
    private static PoolItem Fish(string id) => new(id, 100, 1, new List<Season>(), new List<string>(), -4);

    private static ItemPools Pools() => new()
    {
        Crops = new List<PoolItem> { Fruit(Blueberry) },
        Forage = new List<PoolItem> { Fruit(Morel) with { Category = -81 } },
        Fish = new List<PoolItem> { Fish(Sardine) },
        FruitTreeFruitIds = new HashSet<string> { Apple, Orange, Cherry, Apricot },
    };

    // Weeks straight off AvailabilityWeeks.FruitTreeFruitWeeks, plus a cheap crop and fish.
    private static readonly IReadOnlyDictionary<string, int> Weeks = new Dictionary<string, int>
    {
        [Apple] = 9, [Orange] = 5, [Cherry] = 13, [Apricot] = 13,
        [Blueberry] = 5, [Morel] = 2, [Sardine] = 1,
    };

    private static IReadOnlyList<string> Candidates(string baseId, int deadlineWeek)
        => FlavoredSlotRules.CandidatesFor(baseId, Pools(), id => Weeks.TryGetValue(id, out int w) ? w : null, deadlineWeek);

    [Fact]
    public void A_fruit_slot_offers_fruit_reachable_by_its_deadline()
    {
        IReadOnlyList<string> late = Candidates(FlavoredSlotRules.DriedFruit, 12);
        Assert.Contains(Apple, late);
        Assert.Contains(Orange, late);
        Assert.Contains(Blueberry, late);
    }

    [Fact]
    public void A_fruit_slot_due_early_cannot_ask_for_a_fall_tree()
    {
        IReadOnlyList<string> early = Candidates(FlavoredSlotRules.DriedFruit, 6);
        Assert.Contains(Orange, early);      // week 5, a Summer tree from a week-1 sapling
        Assert.DoesNotContain(Apple, early); // week 9
    }

    /// <summary>Jeff, 2026-09-21: a Spring tree planted in week 1 matures in Summer and would not
    /// bear until a Spring the run never reaches, so AvailabilityWeeks puts Cherry and Apricot at
    /// week 13, "second year or the cart". Cart stock is a coin flip, which is no way to gate a
    /// mandatory slot, so they are barred outright rather than merely gated late.</summary>
    [Theory]
    [InlineData(13)]
    [InlineData(16)]
    public void Cart_only_spring_tree_fruit_is_never_offered(int deadlineWeek)
    {
        IReadOnlyList<string> all = Candidates(FlavoredSlotRules.DriedFruit, deadlineWeek);
        Assert.DoesNotContain(Cherry, all);
        Assert.DoesNotContain(Apricot, all);
    }

    [Fact]
    public void A_fish_slot_offers_fish()
    {
        Assert.Contains(Sardine, Candidates(FlavoredSlotRules.SmokedFish, 16));
        Assert.DoesNotContain(Sardine, Candidates(FlavoredSlotRules.DriedFruit, 16));
    }

    /// <summary>Found by running the game, 2026-09-21. A flavored slot must be written as
    /// vanilla's PreserveType enum name, because that is what the FLAVORED_ITEM query parses. The
    /// name for dried mushrooms is "DriedMushroom", singular, while the object id is
    /// "DriedMushrooms", plural, so no single id works for both the flavor and the icon. Mushrooms
    /// therefore stay an "any" slot.</summary>
    /// <summary>Tried in game 2026-09-21. Naming a mushroom would be worth it (each dries to a
    /// different NAME), but the flavored id must be the PreserveType name "DriedMushroom", which
    /// resolves to no item: BundleCatalogBuilder logged "ItemRegistry returned null for
    /// '(O)DriedMushroom' ... excluding from catalog", dropping the slot out of TLY's own
    /// catalogue on top of vanilla skipping its icon. Fruit and fish are unaffected because their
    /// PreserveType name and object id are the same string.</summary>
    [Fact]
    public void Dried_mushrooms_are_not_a_flavored_slot()
    {
        Assert.False(FlavoredSlotRules.IsFlavored(FlavoredSlotRules.DriedMushrooms));
        Assert.True(FlavoredSlotRules.IsFlavored(FlavoredSlotRules.DriedFruit));
        Assert.True(FlavoredSlotRules.IsFlavored(FlavoredSlotRules.SmokedFish));
    }

    /// <summary>A qualified id fails the enum parse, which made the menu throw and the slot
    /// accept nothing. The written id is the bare name.</summary>
    [Theory]
    [InlineData("(O)DriedFruit", "DriedFruit")]
    [InlineData("(O)SmokedFish", "SmokedFish")]
    [InlineData("(O)24", "(O)24")]
    public void A_flavored_slot_is_written_as_its_preserve_type_name(string baseId, string written)
        => Assert.Equal(written, FlavoredSlotRules.WrittenIdFor(baseId));

    [Fact]
    public void An_unplaced_item_is_not_offered()
        => Assert.DoesNotContain("(O)9999", FlavoredSlotRules.CandidatesFor(
            FlavoredSlotRules.DriedFruit,
            Pools() with { Crops = new List<PoolItem> { Fruit("(O)9999") } },
            _ => null, 16));

    [Fact]
    public void Candidates_come_back_in_a_stable_order()
    {
        IReadOnlyList<string> a = Candidates(FlavoredSlotRules.DriedFruit, 16);
        IReadOnlyList<string> b = Candidates(FlavoredSlotRules.DriedFruit, 16);
        Assert.Equal(a, b);
        Assert.Equal(a.OrderBy(x => x, System.StringComparer.Ordinal), a);
    }

    // --- the pick ----------------------------------------------------------------------

    [Fact]
    public void The_same_seed_and_slot_always_pick_the_same_flavor()
    {
        IReadOnlyList<string> c = Candidates(FlavoredSlotRules.DriedFruit, 16);
        string? first = FlavoredSlotRules.Pick(12345, bundleIndex: 3, ingredientIndex: 2, c);
        Assert.NotNull(first);
        for (int i = 0; i < 20; i++)
            Assert.Equal(first, FlavoredSlotRules.Pick(12345, 3, 2, c));
    }

    [Fact]
    public void Different_slots_of_one_bundle_can_differ()
    {
        var many = new List<string>();
        for (int i = 0; i < 40; i++) many.Add($"(O){1000 + i}");
        var picks = new HashSet<string?>();
        for (int slot = 0; slot < 8; slot++)
            picks.Add(FlavoredSlotRules.Pick(999, bundleIndex: 1, ingredientIndex: slot, many));
        Assert.True(picks.Count > 1, "every slot of the bundle drew the same flavor");
    }

    [Fact]
    public void No_candidates_means_no_flavor()
        => Assert.Null(FlavoredSlotRules.Pick(1, 1, 1, new List<string>()));

    // --- the stack ---------------------------------------------------------------------

    /// <summary>The dehydrator takes five fruit per dried one, so the ask has to be the fruit's own
    /// weekly supply divided by five, never the machine's 35-a-week throughput. Without this a
    /// Normal board rolls 18 Dried Apples, which is 90 apples off trees that give one a day.</summary>
    [Fact]
    public void A_dried_fruit_basis_is_the_inputs_supply_over_five()
        => Assert.Equal(6, FlavoredSlotRules.BasisFor(FlavoredSlotRules.DriedFruit, inputBasis: 30));

    [Fact]
    public void A_smoked_fish_basis_is_the_fishs_own_supply()
        => Assert.Equal(12, FlavoredSlotRules.BasisFor(FlavoredSlotRules.SmokedFish, inputBasis: 12));

    /// <summary>However plentiful the input, five dehydrators only make so many a week.</summary>
    [Fact]
    public void The_machine_throughput_still_caps_it()
        => Assert.Equal(FlavoredSlotRules.StationThroughput,
            FlavoredSlotRules.BasisFor(FlavoredSlotRules.DriedFruit, inputBasis: 1000));

    /// <summary>Jeff, 2026-09-21, after a live board asked for 13 Dried Mushrooms (65 mushrooms):
    /// an "any" dried slot is sized by what the machines can RUN, and a dehydrator run eats five
    /// inputs, so counting runs as units overstates it fivefold. The smoker is one for one and
    /// keeps its row.</summary>
    [Fact]
    public void An_any_dried_slot_is_not_sized_by_machine_runs_alone()
    {
        Assert.Equal(7, QuantityBasisTables.Stations["(O)DriedMushrooms"]);
        Assert.Equal(7, QuantityBasisTables.Stations["(O)DriedFruit"]);
        Assert.Equal(7, QuantityBasisTables.Stations["(O)Raisins"]);
        Assert.Equal(35, QuantityBasisTables.Stations["(O)SmokedFish"]);
    }

    [Fact]
    public void A_basis_never_falls_below_one()
        => Assert.Equal(1, FlavoredSlotRules.BasisFor(FlavoredSlotRules.DriedFruit, inputBasis: 2));
}
