using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Plan 2026-09-21-flavored-bundle-slots, step 3: the generation pass that names the
/// input on a flavored slot and re-rolls its stack against that input's own supply.</summary>
public class FlavoredSlotPassTests
{
    private const string Blueberry = "(O)258";
    private const string Apple = "(O)613";

    private static PoolItem Fruit(string id) => new(id, 100, 1, new List<Season>(), new List<string>(), -79);

    private static ItemPools Pools() => new()
    {
        Crops = new List<PoolItem> { Fruit(Blueberry) },
        FruitTreeFruitIds = new HashSet<string> { Apple },
    };

    private static readonly IReadOnlyDictionary<string, int> Weeks =
        new Dictionary<string, int> { [Blueberry] = 5, [Apple] = 9 };

    private static BundleSpec Spec(params BundleSlotSpec[] slots)
        => new("Pantry", 3, "Test", "Test", "O 495 30", 0, slots.Length, slots);

    private static BundleSpec Run(BundleSpec spec, out IReadOnlyDictionary<int, string> flavors, int deadlineSeasonIndex = 3)
        => FlavoredSlotPass.Apply(
            spec, seed: 4242, new DifficultyProfile(), Pools(),
            id => Weeks.TryGetValue(id, out int w) ? w : null,
            _ => (Season)deadlineSeasonIndex,
            out flavors);

    [Fact]
    public void A_flavored_slot_gets_an_input_named()
    {
        Run(Spec(new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0)), out IReadOnlyDictionary<int, string> flavors);
        Assert.Single(flavors);
        Assert.Contains(flavors[0], new[] { Blueberry, Apple });
    }

    [Fact]
    public void An_ordinary_slot_is_untouched()
    {
        BundleSpec before = Spec(new BundleSlotSpec("(O)24", 5, 0));
        BundleSpec after = Run(before, out IReadOnlyDictionary<int, string> flavors);
        Assert.Empty(flavors);
        Assert.Same(before, after);
    }

    /// <summary>The whole point of the pass: 18 Dried Apples is 90 apples. The stack has to come
    /// down to what the named fruit yields, divided by the dehydrator's five-to-one.</summary>
    [Fact]
    public void The_stack_is_rerolled_far_below_the_machine_throughput()
    {
        BundleSpec after = Run(Spec(new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0)), out _);
        Assert.True(after.Slots[0].Stack < 18, $"stack stayed at {after.Slots[0].Stack}");
        Assert.True(after.Slots[0].Stack >= 1);
    }

    [Fact]
    public void The_same_seed_produces_the_same_flavor_and_stack()
    {
        BundleSpec a = Run(Spec(new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0)), out IReadOnlyDictionary<int, string> fa);
        BundleSpec b = Run(Spec(new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0)), out IReadOnlyDictionary<int, string> fb);
        Assert.Equal(fa[0], fb[0]);
        Assert.Equal(a.Slots[0].Stack, b.Slots[0].Stack);
    }

    /// <summary>A Spring deadline cannot reach a Fall tree or a Summer crop, so nothing is
    /// reachable and the slot stays flavorless rather than naming something unobtainable.</summary>
    [Fact]
    public void Nothing_reachable_in_time_leaves_the_slot_alone()
    {
        BundleSpec before = Spec(new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0));
        BundleSpec after = Run(before, out IReadOnlyDictionary<int, string> flavors, deadlineSeasonIndex: 0);
        Assert.Empty(flavors);
        Assert.Same(before, after);
    }

    [Fact]
    public void Each_flavored_slot_of_a_bundle_is_keyed_by_its_own_index()
    {
        Run(Spec(
            new BundleSlotSpec("(O)24", 5, 0),
            new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0),
            new BundleSlotSpec(FlavoredSlotRules.SmokedFish, 18, 0)), out IReadOnlyDictionary<int, string> flavors);
        Assert.False(flavors.ContainsKey(0));
        Assert.True(flavors.ContainsKey(1));
    }

    /// <summary>Found by running the game, 2026-09-21: a board asked for 18 Smoked Midnight Squid,
    /// a Night Market fish, because an input with no supply row fell back to the machine's own
    /// throughput. An input we cannot size the ask from is no longer named at all.</summary>
    [Fact]
    public void An_input_with_no_supply_row_is_never_named()
    {
        // (O)9998 is a fruit nothing has a quantity basis for; it is the ONLY candidate here.
        var pools = new ItemPools
        {
            Crops = new List<PoolItem> { Fruit("(O)9998") },
            FruitTreeFruitIds = new HashSet<string>(),
        };
        BundleSpec before = Spec(new BundleSlotSpec(FlavoredSlotRules.DriedFruit, 18, 0));
        BundleSpec after = FlavoredSlotPass.Apply(
            before, seed: 7, new DifficultyProfile(), pools,
            _ => 1,                      // placed in week 1, so reachability is not what excludes it
            _ => Season.Winter,
            out IReadOnlyDictionary<int, string> flavors);
        Assert.Empty(flavors);
        Assert.Same(before, after);
    }

    [Fact]
    public void The_persisted_key_is_bundle_then_slot()
        => Assert.Equal("3:1", FlavoredSlotPass.KeyFor(3, 1));
}
