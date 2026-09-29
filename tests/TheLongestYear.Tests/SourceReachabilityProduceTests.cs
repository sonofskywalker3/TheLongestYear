using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Machine outputs and animal produce as reachability routes. Trigger: Blue Eggs and
/// Golden Mayo adds Ostrich Mayo, made only from an Ostrich Egg in a Mayonnaise Machine, and a
/// loop asked for it (Ninjamaid, Nexus posts 2026-09-28). Shapes are the live Data/FarmAnimals
/// and Data/Machines rows.</summary>
public class SourceReachabilityProduceTests
{
    private const string Mayo = "(BC)24";
    private const string OstrichEgg = "(O)289";
    private const string OstrichMayo = "(O)6480.blueegg_OstrichMayo";
    private const string DinosaurEgg = "(O)107";
    private const string VoidEgg = "(O)305";
    private const string WhiteEgg = "(O)176";
    private const string Mayonnaise = "(O)306";
    private const string KrobusShop = "ShadowShop";
    private const string SewerMap = "Sewer";

    private static readonly RawAnimalSource[] VanillaAnimals =
    {
        new("White Chicken", true, new[] { "176", "174" }, new[] { "176", "174" }),
        new("Blue Chicken", true, Array.Empty<string>(), new[] { "176", "174" }),
        new("Void Chicken", false, new[] { "305" }, new[] { "305" }),
        new("Dinosaur", false, new[] { "107" }, new[] { "107" }),
        new("Ostrich", false, new[] { "289" }, new[] { "289" }),
    };

    private static readonly RawMachineRule[] MayoRules =
    {
        Rule(WhiteEgg, Mayonnaise),
        Rule(OstrichEgg, Mayonnaise),
        Rule(OstrichEgg, OstrichMayo),
    };

    private static RawMachineRule Rule(string input, string output)
        => new(Mayo, input, Array.Empty<string>(), new[] { output }, 180, -1);

    private static SourceReachability Build(
        IReadOnlyList<RawMachineRule>? rules = null,
        IReadOnlyList<RawAnimalSource>? animals = null,
        params string[] spawns)
        => new(
            new HashSet<string>(StringComparer.Ordinal),
            new[] { new RawShopListing("(O)305", KrobusShop) },
            new[] { new RawShopPlacement(KrobusShop, SewerMap) },
            Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(),
            new HashSet<string>(spawns, StringComparer.Ordinal),
            rules ?? MayoRules, animals ?? VanillaAnimals);

    [Fact]
    public void An_animal_nobody_sells_that_hatches_only_from_its_own_egg_makes_that_egg_unreachable()
        => Assert.True(Build().IsUnreachable(OstrichEgg));

    [Fact]
    public void A_machine_good_made_only_from_an_unreachable_input_is_unreachable()
    {
        var rule = Build();
        Assert.True(rule.IsUnreachable(OstrichMayo));
        Assert.Contains("289", rule.Reasons[OstrichMayo]);
    }

    [Fact]
    public void A_machine_good_with_one_reachable_input_is_allowed()
        => Assert.False(Build().IsUnreachable(Mayonnaise));

    [Fact]
    public void Produce_of_an_animal_Marnie_sells_is_allowed()
        => Assert.False(Build().IsUnreachable(WhiteEgg));

    /// <summary>Dinosaur is not sold and hatches only from its own egg, but the egg turns up in
    /// artifact spots: positive proof beats the closed loop.</summary>
    [Fact]
    public void A_self_hatching_egg_with_another_proven_source_is_allowed()
        => Assert.False(Build(spawns: DinosaurEgg).IsUnreachable(DinosaurEgg));

    [Fact]
    public void A_self_hatching_egg_sold_in_a_reachable_shop_is_allowed()
        => Assert.False(Build().IsUnreachable(VoidEgg));

    /// <summary>An animal hatched from ANOTHER item keeps that item's benefit of the doubt: an
    /// egg nothing traces stays allowed, so its animal's produce does too.</summary>
    [Fact]
    public void An_animal_hatched_from_an_untraced_egg_keeps_its_produce_allowed()
    {
        var animals = new[] { new RawAnimalSource("Mod Duck", false, new[] { "Mod.Egg" }, new[] { "Mod.Feather" }) };
        Assert.False(Build(animals: animals).IsUnreachable("(O)Mod.Feather"));
    }

    [Fact]
    public void A_machine_rule_that_takes_tags_or_nothing_keeps_its_output_allowed()
    {
        var rules = new[]
        {
            new RawMachineRule(Mayo, null, new[] { "egg_item" }, new[] { OstrichMayo }, 180, -1),
            Rule(OstrichEgg, OstrichMayo),
        };
        Assert.False(Build(rules: rules).IsUnreachable(OstrichMayo));
    }

    [Fact]
    public void Without_machine_or_animal_data_nothing_changes()
    {
        var rule = new SourceReachability(
            new HashSet<string>(StringComparer.Ordinal), Array.Empty<RawShopListing>(), Array.Empty<RawShopPlacement>(),
            Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), new HashSet<string>(StringComparer.Ordinal));
        Assert.False(rule.IsUnreachable(OstrichMayo));
        Assert.False(rule.IsUnreachable(OstrichEgg));
    }
}
