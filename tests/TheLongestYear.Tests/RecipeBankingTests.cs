using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus post ada113, 2026-09-07: a book bought at the loop-boundary shrine must get a
/// chance to be filled before the reset wipes the recipes it was bought to keep.</summary>
public class RecipeBankingTests
{
    private static bool IsStarter(string id) => id == "Fried Egg" || id == "Chest";

    [Fact]
    public void Bankable_skips_banked_and_starter_recipes_and_sorts()
    {
        var known = new[] { "Pancakes", "Fried Egg", "Bread", "Omelet" };
        var banked = new List<string> { "Omelet" };
        Assert.Equal(new[] { "Bread", "Pancakes" }, RecipeBanking.Bankable(known, banked, IsStarter));
    }

    [Fact]
    public void Offer_only_when_a_slot_is_free_and_something_can_fill_it()
    {
        Assert.True(RecipeBanking.ShouldOfferAtReset(slotCount: 5, bankedCount: 0, bankableCount: 3));
        Assert.True(RecipeBanking.ShouldOfferAtReset(slotCount: 5, bankedCount: 4, bankableCount: 1));
        Assert.False(RecipeBanking.ShouldOfferAtReset(slotCount: 12, bankedCount: 20, bankableCount: 3));  // over the cap (pre-0.18.17 tier 3 held 20)
        Assert.False(RecipeBanking.ShouldOfferAtReset(slotCount: 5, bankedCount: 5, bankableCount: 3));   // full
        Assert.False(RecipeBanking.ShouldOfferAtReset(slotCount: 5, bankedCount: 0, bankableCount: 0));   // only starters known
    }

    [Fact]
    public void Bankable_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => RecipeBanking.Bankable(null!, new List<string>(), IsStarter));
        Assert.Throws<ArgumentNullException>(() => RecipeBanking.Bankable(new[] { "A" }, null!, IsStarter));
        Assert.Throws<ArgumentNullException>(() => RecipeBanking.Bankable(new[] { "A" }, new List<string>(), null!));
    }
}
