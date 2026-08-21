using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class JpBudgetCalculatorTests
{
    private static readonly int[] NoGoalSlots = { 0, 0, 0, 0 };
    private static readonly int[] DefaultGoalSlots = { 4, 5, 6, 7 };

    private static BudgetSlot Slot(string id, Rarity rarity, int season) => new(id, rarity, season);

    private static BudgetBundle Bundle(string room, string name, int need, params BudgetSlot[] slots)
        => new(room, name, need, slots, VaultGold: 0);

    private static BudgetBundle Vault(int gold) => new("Vault", $"{gold}g", 1, new List<BudgetSlot>(), gold);

    [Fact]
    public void Empty_board_pays_only_weekly_quests_and_checkpoints()
    {
        var r = JpBudgetCalculator.Compute(new List<BudgetBundle>(), new JpSettings(), 1.5, NoGoalSlots);

        // Weekly quest 30 × mult × 4 weeks: 120 / 180 / 300 / 480.
        Assert.Equal(new long[] { 120, 180, 300, 480 }, r.WeeklyQuest);
        // Checkpoints at the entering week: none / 150 / 250 / 400.
        Assert.Equal(new long[] { 0, 150, 250, 400 }, r.Checkpoint);
        Assert.Equal(120 + 180 + 300 + 480 + 150 + 250 + 400, r.Total);
        Assert.Equal(r.Total, r.HoardCeiling);
    }

    [Fact]
    public void Each_slot_pays_once_at_its_earliest_season_multiplier()
    {
        var board = new List<BudgetBundle>
        {
            Bundle("Pantry", "Test", 3,
                Slot("(O)24", Rarity.Common, 0),    // Spring: 1
                Slot("(O)88", Rarity.Rare, 1),      // Summer: 10 × 1.5 = 15
                Slot("(O)74", Rarity.VeryRare, 3)), // Winter: 25 × 4 = 100
        };
        var r = JpBudgetCalculator.Compute(board, new JpSettings(), 1.5, NoGoalSlots);

        Assert.Equal(1, r.Donation[0]);
        Assert.Equal(15, r.Donation[1]);
        Assert.Equal(0, r.Donation[2]);
        Assert.Equal(100, r.Donation[3]);
        Assert.Equal(new[] { 1, 1, 0, 1 }, r.SlotsBySeason);
    }

    [Fact]
    public void Bundle_completes_in_the_season_of_its_Nth_earliest_slot_and_room_in_its_last_bundle()
    {
        var board = new List<BudgetBundle>
        {
            // Need 2 of 3: slots obtainable Spring, Fall, Winter → completes in Fall (15 × 2.5 = 38).
            Bundle("Pantry", "A", 2,
                Slot("(O)1", Rarity.Common, 0), Slot("(O)2", Rarity.Common, 2), Slot("(O)3", Rarity.Common, 3)),
            // Need 1 of 1 in Spring → completes Spring (15).
            Bundle("Pantry", "B", 1, Slot("(O)4", Rarity.Common, 0)),
        };
        var r = JpBudgetCalculator.Compute(board, new JpSettings(), 1.5, NoGoalSlots);

        Assert.Equal(15, r.BundleBonus[0]);
        Assert.Equal(38, r.BundleBonus[2]);
        // Room (Pantry) completes with bundle A in Fall: 60 × 2.5 = 150.
        Assert.Equal(new long[] { 0, 0, 150, 0 }, r.RoomBonus);
    }

    [Fact]
    public void Vault_bundles_pay_gold_scaled_unscaled_jp_in_spring_with_no_completion_bonus()
    {
        var board = new List<BudgetBundle> { Vault(3125), Vault(31250) };
        var r = JpBudgetCalculator.Compute(board, new JpSettings(), 1.5, NoGoalSlots);

        Assert.Equal(3 + 31, r.Vault[0]);       // 3.125 → 3, 31.25 → 31
        Assert.Equal(0, r.BundleBonus[0]);
        Assert.Equal(60, r.RoomBonus[0]);       // the Vault room still completes (Spring, 1.0×)
    }

    [Fact]
    public void Selection_bonus_applies_to_the_richest_slots_up_to_the_weekly_cap()
    {
        // Spring cap = 4 weeks × 1 goal slot = 4 slots; 5 Rare Spring slots at 10 JP each.
        var slots = new List<BudgetSlot>();
        for (int i = 0; i < 5; i++) slots.Add(Slot($"(O){i}", Rarity.Rare, 0));
        var board = new List<BudgetBundle> { new("Pantry", "R", 5, slots, 0) };
        var r = JpBudgetCalculator.Compute(board, new JpSettings(), 1.5, new[] { 1, 0, 0, 0 });

        Assert.Equal(50, r.Donation[0]);
        Assert.Equal(4 * 5, r.SelectionBonus[0]);   // 4 slots × (15 − 10)
        Assert.Equal(0, r.SelectionBonus[1]);
    }

    [Fact]
    public void Hoard_ceiling_prices_every_slot_and_bonus_at_winter()
    {
        var board = new List<BudgetBundle>
        {
            Bundle("Pantry", "A", 1, Slot("(O)1", Rarity.Common, 0)), // Spring: 1 JP; at Winter: 4 JP
        };
        var r = JpBudgetCalculator.Compute(board, new JpSettings(), 1.5, DefaultGoalSlots);

        long fixedAwards = r.WeeklyQuest[0] + r.WeeklyQuest[1] + r.WeeklyQuest[2] + r.WeeklyQuest[3]
                           + r.Checkpoint[1] + r.Checkpoint[2] + r.Checkpoint[3];
        // Slot 4 + selection extra (6 − 4 = 2) + bundle 60 + room 240 + fixed awards.
        Assert.Equal(4 + 2 + 60 + 240 + fixedAwards, r.HoardCeiling);
        Assert.True(r.HoardCeiling > r.Total);
    }
}
