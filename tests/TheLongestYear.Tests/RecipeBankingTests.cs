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

    /// <summary>0.18.17 lowered tier 3 from 20 slots to 16. A book already holding more than its
    /// cap keeps every recipe: the rows still show and can be removed, the book only refuses new
    /// entries until it is back under the cap.</summary>
    [Fact]
    public void Overflow_stays_visible_and_blocks_new_entries()
    {
        Assert.Equal(20, RecipeBanking.VisibleRows(slotCount: 16, bankedCount: 20));
        Assert.Equal(16, RecipeBanking.VisibleRows(slotCount: 16, bankedCount: 3));
        Assert.True(RecipeBanking.IsOverCap(slotCount: 16, bankedCount: 20));
        Assert.False(RecipeBanking.IsOverCap(slotCount: 16, bankedCount: 16));
        Assert.False(RecipeBanking.CanBank(slotCount: 16, bankedCount: 20));
        Assert.False(RecipeBanking.CanBank(slotCount: 16, bankedCount: 16));
        Assert.True(RecipeBanking.CanBank(slotCount: 16, bankedCount: 15));
    }
}
