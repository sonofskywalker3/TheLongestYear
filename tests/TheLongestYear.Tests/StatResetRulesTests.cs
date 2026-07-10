using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StatResetRulesTests
{
    [Theory]
    // 1.6 power books — permanent passive bonuses earned by reading in-run.
    [InlineData("Book_Trash")]
    [InlineData("Book_Speed")]
    [InlineData("Book_Speed2")]
    [InlineData("Book_PriceCatalogue")]
    [InlineData("Book_AnimalCatalogue")]
    // Mastery claims (mastery_<skill>; mastery_4 gates the trinket slots).
    [InlineData("mastery_0")]
    [InlineData("mastery_4")]
    // Mastery exp + spent-claims counters.
    [InlineData("MasteryExp")]
    [InlineData("masteryLevelsSpent")]
    // Prize-ticket machine ladder position + unclaimed ticket count.
    [InlineData("ticketPrizesClaimed")]
    [InlineData("specialOrderPrizeTickets")]
    // Combat-mastery trinket slot: InventoryPage gates the slot on this stat (NOT on mastery_4),
    // so leaving it would let trinkets be equipped on loop 2 with no mastery re-earned.
    [InlineData("trinketSlots")]
    public void IsRunScoped_true_for_run_scoped_keys(string key)
        => Assert.True(StatResetRules.IsRunScoped(key));

    [Theory]
    // Lifetime/cosmetic counters and anything we didn't explicitly classify stay untouched.
    [InlineData("stepsTaken")]
    [InlineData("trashCansChecked")]
    [InlineData("daysPlayed")]
    [InlineData("seedsSown")]
    [InlineData("Book")]          // bare prefix without underscore is not a book key
    [InlineData("masteryish")]    // not a mastery_<skill> claim
    [InlineData("")]
    [InlineData(null)]
    public void IsRunScoped_false_for_everything_else(string key)
        => Assert.False(StatResetRules.IsRunScoped(key));

    [Fact]
    public void SelectRunScoped_filters_a_mixed_key_set()
    {
        var keys = new[]
        {
            "stepsTaken", "Book_Void", "mastery_2", "MasteryExp",
            "ticketPrizesClaimed", "cropsShipped",
        };
        var selected = StatResetRules.SelectRunScoped(keys);
        Assert.Equal(
            new[] { "Book_Void", "mastery_2", "MasteryExp", "ticketPrizesClaimed" },
            selected.OrderBy(k => k).ToArray());
    }
}
