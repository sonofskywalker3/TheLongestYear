using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SlotLedgerTests
{
    [Fact]
    public void Add_is_idempotent_and_counts_per_bundle()
    {
        var ledger = new SlotLedger();
        Assert.True(ledger.Add(5, 0, "(O)388"));
        Assert.False(ledger.Add(5, 0, "(O)388"));
        Assert.True(ledger.Add(5, 1, "(O)388"));
        Assert.True(ledger.Add(6, 0, "(O)390"));
        Assert.Equal(2, ledger.FilledCount(5));
        Assert.Equal(1, ledger.FilledCount(6));
        Assert.Equal(3, ledger.Count);
        Assert.Equal(2, ledger.ItemIds.Count);
    }

    [Fact]
    public void Constructed_from_entries_answers_IsFilled()
    {
        var ledger = new SlotLedger(new[]
        {
            new DonatedSlot { BundleIndex = 1, IngredientIndex = 2, ItemId = "(O)24" },
        });
        Assert.True(ledger.IsFilled(1, 2));
        Assert.False(ledger.IsFilled(1, 1));
        Assert.False(ledger.IsFilled(2, 2));
    }
}
