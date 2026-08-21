using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class JpBudgetCalculatorTests
{
    private static readonly int[] NoGoalSlots = { 0, 0, 0, 0 };
    private static readonly int[] DefaultGoalSlots = { 4, 5, 6, 7 };

    private static BudgetSlot Slot(string id, Rarity rarity, int season, int? pin = null) => new(id, rarity, season, pin);

    private static BudgetBundle Bundle(string room, string name, int need, params BudgetSlot[] slots)
        => new(room, name, need, slots, VaultGold: 0);

    private static BudgetBundle Vault(int gold) => new("Vault", $"{gold}g", 1, new List<BudgetSlot>(), gold);

    private static JpBudgetReport Run(IReadOnlyList<BudgetBundle> board, int[] goalSlots = null)
        => JpBudgetCalculator.Compute(board, new JpSettings(), 1.5, goalSlots ?? NoGoalSlots);

    [Fact]
    public void Empty_board_pays_only_weekly_quests_and_checkpoints()
    {
        var r = Run(new List<BudgetBundle>());

        // Weekly quest 30 × mult × 4 weeks: 120 / 180 / 300 / 480.
        Assert.Equal(new long[] { 120, 180, 300, 480 }, r.WeeklyQuest);
        // Checkpoints at the entering week: none / 150 / 250 / 400.
        Assert.Equal(new long[] { 0, 150, 250, 400 }, r.Checkpoint);
        Assert.Equal(120 + 180 + 300 + 480 + 150 + 250 + 400, r.EarliestTotal);
        Assert.Equal(r.EarliestTotal, r.StrongTotal);
        Assert.Equal(r.EarliestTotal, r.HoardCeiling);
    }

    [Fact]
    public void Earliest_model_pays_each_slot_once_at_its_earliest_season_multiplier()
    {
        var board = new List<BudgetBundle>
        {
            Bundle("Pantry", "Test", 3,
                Slot("(O)24", Rarity.Common, 0),    // Spring: 1
                Slot("(O)88", Rarity.Rare, 1),      // Summer: 10 × 1.5 = 15
                Slot("(O)74", Rarity.VeryRare, 3)), // Winter: 25 × 4 = 100
        };
        var r = Run(board);

        Assert.Equal(new long[] { 1, 15, 0, 100 }, r.Earliest.Donation);
        Assert.Equal(new[] { 1, 1, 0, 1 }, r.Earliest.Slots);
    }

    [Fact]
    public void A_pick_X_of_Y_bundle_pays_for_only_X_slots_the_richest_ones()
    {
        var board = new List<BudgetBundle>
        {
            Bundle("Pantry", "PickTwo", 2,
                Slot("(O)1", Rarity.Common, 0), Slot("(O)2", Rarity.Rare, 0), Slot("(O)3", Rarity.VeryRare, 0)),
        };
        var r = Run(board);

        Assert.Equal(10 + 25, r.Earliest.Donation[0]);
        Assert.Equal(2, r.Earliest.Slots.Sum());
        Assert.Equal(2, r.Strong.Slots.Sum());
        // Ceiling: the two richest at Winter (40 + 100) + bundle 60 + room 240 + fixed awards.
        Assert.Equal(140 + 60 + 240 + r.FixedAwards, r.HoardCeiling);
    }

    [Fact]
    public void Bundle_completes_in_the_season_of_its_Xth_donation_and_room_with_its_last_bundle()
    {
        var board = new List<BudgetBundle>
        {
            // Need 2 of 3 (all Common, so the richest-first tie-break keeps Spring first): slots
            // obtainable Spring, Fall, Winter → the two picked are Spring + Fall → completes Fall.
            Bundle("Pantry", "A", 2,
                Slot("(O)1", Rarity.Common, 0), Slot("(O)2", Rarity.Common, 2), Slot("(O)3", Rarity.Common, 3)),
            Bundle("Pantry", "B", 1, Slot("(O)4", Rarity.Common, 0)),
        };
        var r = Run(board);

        Assert.Equal(15, r.Earliest.BundleBonus[0]);      // B in Spring
        Assert.Equal(38, r.Earliest.BundleBonus[2]);      // A in Fall: 15 × 2.5 = 37.5 → 38
        Assert.Equal(new long[] { 0, 0, 150, 0 }, r.Earliest.RoomBonus);
    }

    [Fact]
    public void Vault_bundles_pay_gold_scaled_unscaled_jp_in_spring_with_no_completion_bonus()
    {
        var r = Run(new List<BudgetBundle> { Vault(3125), Vault(31250) });

        Assert.Equal(3 + 31, r.Vault[0]);            // 3.125 → 3, 31.25 → 31
        Assert.Equal(0, r.Earliest.BundleBonus[0]);
        Assert.Equal(60, r.Earliest.RoomBonus[0]);    // the Vault room still completes (Spring, 1.0×)
        Assert.Equal(60, r.Strong.RoomBonus[0]);
    }

    [Fact]
    public void Selection_bonus_applies_to_the_richest_payouts_up_to_the_weekly_cap()
    {
        // Spring cap = 4 weeks × 1 goal slot = 4 slots; 5 Rare Spring slots at 10 JP each.
        var slots = Enumerable.Range(0, 5).Select(i => Slot($"(O){i}", Rarity.Rare, 0)).ToArray();
        var board = new List<BudgetBundle> { Bundle("Pantry", "R", 5, slots) };
        var r = Run(board, new[] { 1, 0, 0, 0 });

        Assert.Equal(50, r.Earliest.Donation[0]);
        Assert.Equal(4 * 5, r.Earliest.SelectionBonus[0]);   // 4 slots × (15 − 10)
        Assert.Equal(0, r.Earliest.SelectionBonus[1]);
    }

    [Fact]
    public void Strong_model_meets_percentage_minimums_with_the_cheapest_slots_and_hoards_the_rest()
    {
        // Pick 3 of 4, quota [1,1,2,3]; all obtainable in Spring; rarities Common/Common/Rare/VeryRare.
        var board = new List<BudgetBundle>
        {
            new("Pantry", "P", 3,
                new[] { Slot("(O)1", Rarity.Common, 0), Slot("(O)2", Rarity.Common, 0), Slot("(O)3", Rarity.Rare, 0), Slot("(O)4", Rarity.VeryRare, 0) },
                0, CumulativeQuota: new[] { 1, 1, 2, 3 }),
        };
        var r = Run(board);

        // Spring: 1 Common (1). Fall: cumulative 2 → one more Common at 2.5× (3). Winter: the
        // richest leftover (VeryRare 100) fills the 3rd payable slot; the Rare is never donated.
        Assert.Equal(new long[] { 1, 0, 3, 100 }, r.Strong.Donation);
        Assert.Equal(new[] { 1, 0, 1, 1 }, r.Strong.Slots);
        Assert.Equal(60, r.Strong.BundleBonus[3]);      // completes in Winter at 4×
        Assert.Equal(240, r.Strong.RoomBonus[3]);
        Assert.Empty(r.ImpossibleGates);
        Assert.True(r.StrongTotal > r.EarliestTotal);
    }

    [Fact]
    public void Strong_model_donates_seasonal_bundles_in_their_season_and_pinned_items_at_their_pin()
    {
        var board = new List<BudgetBundle>
        {
            new("Pantry", "Spring Crops", 2,
                new[] { Slot("(O)24", Rarity.Common, 0), Slot("(O)188", Rarity.Rare, 0) }, 0, SeasonalSeasonIndex: 0),
            new("Bulletin Board", "Enchanter's", 2,
                new[] { Slot("(O)725", Rarity.Common, 1, pin: 1), Slot("(O)999", Rarity.Common, 0) }, 0),
        };
        var r = Run(board);

        Assert.Equal(11, r.Strong.Donation[0]);   // Spring Crops: 1 + 10 in Spring
        Assert.Equal(2, r.Strong.Donation[1]);    // Oak Resin at its Summer pin: 1 × 1.5 → 2
        Assert.Equal(4, r.Strong.Donation[3]);    // the unpinned Enchanter's slot hoarded to Winter
        Assert.Equal(15, r.Strong.BundleBonus[0]);
        Assert.Equal(60, r.Strong.BundleBonus[3]);
    }

    [Fact]
    public void Strong_model_flags_a_minimum_that_exceeds_obtainable_slots()
    {
        // Quota demands 1 by Summer but nothing is obtainable before Winter (Winter Star shape).
        var board = new List<BudgetBundle>
        {
            new("Bulletin Board", "Winter Star", 2,
                new[] { Slot("(O)283", Rarity.Common, 3), Slot("(O)604", Rarity.Rare, 3) },
                0, CumulativeQuota: new[] { 0, 1, 1, 2 }),
        };
        var r = Run(board);

        Assert.Contains(r.ImpossibleGates, m => m.StartsWith("Winter Star: Summer"));
        Assert.Equal(2, r.Strong.Slots[3]);       // still pays both in Winter
    }

    [Fact]
    public void Hoard_ceiling_prices_every_payable_slot_and_bonus_at_winter()
    {
        var board = new List<BudgetBundle>
        {
            Bundle("Pantry", "A", 1, Slot("(O)1", Rarity.Common, 0)), // Spring: 1 JP; at Winter: 4 JP
        };
        var r = Run(board, DefaultGoalSlots);

        // Slot 4 + selection extra (6 − 4 = 2) + bundle 60 + room 240 + fixed awards.
        Assert.Equal(4 + 2 + 60 + 240 + r.FixedAwards, r.HoardCeiling);
        Assert.True(r.HoardCeiling >= r.StrongTotal);
        Assert.True(r.StrongTotal >= r.EarliestTotal);
    }
}
