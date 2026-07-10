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
    // Combat-mastery trinket slot: InventoryPage gates the slot on this stat (NOT on mastery_4).
    [InlineData("trinketSlots")]
    // Run-relevant ladders the old allow-list missed — covered by wipe-by-default.
    [InlineData("BillboardQuestsDone")]
    [InlineData("GoldenTagsTurnedIn")]
    [InlineData("blessingOfWaters")]
    [InlineData("individualMoneyEarned")]
    [InlineData("SquidFestScore_12_1")]
    // The whole point of wipe-by-default: unknown future/mod keys can never leak.
    [InlineData("Book2_SomeFutureBook")]
    [InlineData("someFutureVanillaStat")]
    [InlineData("mod.example_customProgress")]
    public void IsRunScoped_true_for_run_scoped_and_unknown_keys(string key)
        => Assert.True(StatResetRules.IsRunScoped(key));

    [Theory]
    // Engine-critical.
    [InlineData("daysPlayed")]
    [InlineData("averageBedtime")]
    // RNG-sequence counters: wiping would restart identical drop sequences every loop.
    [InlineData("timesEnchanted")]
    [InlineData("geodesCracked")]
    [InlineData("MysteryBoxesOpened")]
    // Lifetime tallies (spot checks; full list in StatResetRules).
    [InlineData("stepsTaken")]
    [InlineData("cropsShipped")]
    [InlineData("trashCansChecked")]
    [InlineData("seedsSown")]
    [InlineData("itemsShipped")]
    [InlineData("monstersKilled")]
    [InlineData("completedPrairieKingWithoutDying")]
    // Case-insensitive match on kept keys.
    [InlineData("STEPSTAKEN")]
    // Null/empty are skipped, not wiped.
    [InlineData("")]
    [InlineData(null)]
    public void IsRunScoped_false_for_kept_keys(string key)
        => Assert.False(StatResetRules.IsRunScoped(key));

    [Fact]
    public void SelectRunScoped_filters_a_mixed_key_set()
    {
        var keys = new[]
        {
            "stepsTaken", "Book_Void", "mastery_2", "MasteryExp",
            "ticketPrizesClaimed", "cropsShipped", "someModKey",
        };
        var selected = StatResetRules.SelectRunScoped(keys);
        Assert.Equal(
            new[] { "Book_Void", "mastery_2", "MasteryExp", "someModKey", "ticketPrizesClaimed" },
            selected.OrderBy(k => k).ToArray());
    }
}
