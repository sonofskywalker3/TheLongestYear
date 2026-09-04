using System;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's rulings, 2026-09-04, for the domains beyond fish and forage: crops on 100 tiles
/// with no cost cap (shop seed = a full stack; cart-only and rare seeds limited by supply); crab
/// pots at 10 a season, added to a shellfish's forage mean; monster drops at 60 kills a day for a
/// week of the best reachable monster, every drop; stations by unlock and cost with animals as a
/// stretch; minerals base 4; the mines from a modelled mine day; books, artifacts and cooking stay
/// single.</summary>
public class QuantityBasisTablesTests
{
    private const string Parsnip = "(O)24";
    private const string Starfruit = "(O)268";
    private const string SweetGemBerry = "(O)417";
    private const string AncientFruit = "(O)454";
    private const string Mussel = "(O)719";
    private const string Periwinkle = "(O)722";
    private const string Slime = "(O)766";
    private const string CopperBarDrop = "(O)334";
    private const string Quartz = "(O)80";        // node crystal: a Stone Golem drop, not a "mineral" here
    private const string Esperite = "(O)544";     // geode mineral, base 4
    private const string Diamond = "(O)72";       // gem, single
    private const string Milk = "(O)184";
    private const string Wine = "(O)348";
    private const string AncientDrum = "(O)120";     // artifact: no row
    private const string BookOfStars = "(O)Book_Speed"; // book: no row

    private static DifficultyProfile Profile(DifficultyStep stack)
        => DifficultyResolver.Resolve(new DifficultySettings { StackSize = stack }, new GameplayConfig());

    private static BundleSpec Spec(params (string Id, int Stack, int Quality)[] slots)
        => new("Pantry", 5, "Test", "Test", "O 495 30", 0, slots.Length,
            slots.Select(s => new BundleSlotSpec(s.Id, s.Stack, s.Quality)).ToList());

    private static int Roll(string id, DifficultyStep step, int seed = 1, int quality = 0)
        => QuantityAskPass.Apply(Spec((id, 1, quality)), Profile(step), _ => null, new Random(seed)).Slots[0].Stack;

    [Fact]
    public void Shop_seeds_are_a_full_stack_and_extreme_reaches_eighty()
    {
        Assert.Equal(99, QuantityBasisTables.Crops[Parsnip]);
        Assert.Equal(99, QuantityBasisTables.Crops[Starfruit]);   // no cost cap
        var seen = Enumerable.Range(0, 200).Select(seed => Roll(Parsnip, DifficultyStep.Extreme, seed)).ToList();
        Assert.All(seen, s => Assert.InRange(s, 65, 80));
        Assert.Contains(80, seen);
    }

    [Fact]
    public void Supply_limited_crops_carry_their_own_basis()
    {
        Assert.Equal(16, QuantityBasisTables.Crops[SweetGemBerry]);
        Assert.Equal(5, QuantityBasisTables.Crops[AncientFruit]);
        Assert.InRange(Roll(SweetGemBerry, DifficultyStep.Extreme), 11, 13);
    }

    [Fact]
    public void Crab_pot_yield_adds_to_a_shellfish_forage_mean_and_stands_alone_for_freshwater()
    {
        double musselForage = ForageAskBasis.BasisByDeadline(Mussel, null)!.Value;
        double musselPot = QuantityBasisTables.CrabPot[Mussel];
        int basis = (int)Math.Round(musselForage + musselPot);
        var rolls = Enumerable.Range(0, 200).Select(seed => Roll(Mussel, DifficultyStep.Extreme, seed)).ToList();
        Assert.All(rolls, r => Assert.InRange(r, (int)Math.Ceiling(basis * 0.65), (int)Math.Ceiling(basis * 0.80)));
        Assert.Equal(15, QuantityBasisTables.CrabPot[Periwinkle]);
        Assert.InRange(Roll(Periwinkle, DifficultyStep.Normal), 3, 8);
    }

    [Fact]
    public void Monster_drops_come_from_every_drop_of_the_best_reachable_monster()
    {
        Assert.Equal(99, QuantityBasisTables.MonsterDrops[Slime]);            // capped
        Assert.InRange(QuantityBasisTables.MonsterDrops[CopperBarDrop], 16, 18); // Shadow Guy at 4%, 420 kills
        Assert.False(QuantityBasisTables.MonsterDrops.ContainsKey("(O)848"));  // Cinder Shard: volcano only
        Assert.InRange(Roll(Slime, DifficultyStep.Hard), 50, 65);
    }

    [Fact]
    public void Geode_minerals_are_base_four_and_the_mine_day_model_covers_crystals_gems_and_ore()
    {
        Assert.Equal(4, QuantityBasisTables.Minerals[Esperite]);
        Assert.False(QuantityBasisTables.Minerals.ContainsKey(Diamond));
        Assert.False(QuantityBasisTables.Minerals.ContainsKey(Quartz));
        Assert.InRange(Roll(Esperite, DifficultyStep.Extreme), 3, 4);
        Assert.InRange(Roll(Esperite, DifficultyStep.Easy), 1, 2);
        // Quartz: 80 a week (floor items plus Stone Golems), Jeff: "quartz is much more common".
        Assert.Equal(80, QuantityBasisTables.Mines[Quartz]);
        Assert.InRange(Roll(Quartz, DifficultyStep.Extreme), 52, 64);
        Assert.Equal(3, QuantityBasisTables.Mines[Diamond]);
        Assert.Equal(99, QuantityBasisTables.Mines["(O)378"]);   // Copper Ore
        Assert.Equal(80, QuantityBasisTables.Stations["(O)334"]); // Copper Bar, ore- and coal-limited
    }

    [Fact]
    public void Stations_follow_their_unlock_and_animals_are_a_stretch()
    {
        Assert.Equal(56, QuantityBasisTables.Stations[Milk]);   // a Big Barn of eight
        Assert.Equal(5, QuantityBasisTables.Stations[Wine]);    // Farming 8, five kegs, seven days
        Assert.InRange(Roll(Milk, DifficultyStep.Extreme), 37, 45);
        Assert.InRange(Roll(Wine, DifficultyStep.Normal), 1, 3);
    }

    [Fact]
    public void Books_artifacts_and_cooking_stay_single_asks()
    {
        Assert.False(QuantityAskPass.Covers(AncientDrum));
        Assert.False(QuantityAskPass.Covers(BookOfStars));
        Assert.False(QuantityAskPass.Covers("(O)194"));   // Fried Egg
        Assert.Equal(1, Roll(AncientDrum, DifficultyStep.Extreme));
    }

    [Fact]
    public void Every_table_row_is_a_qualified_id_the_multiplier_will_skip()
    {
        foreach (var table in new[] { QuantityBasisTables.CrabPot, QuantityBasisTables.Crops, QuantityBasisTables.MonsterDrops, QuantityBasisTables.Stations, QuantityBasisTables.Minerals, QuantityBasisTables.Mines })
            foreach (var row in table)
            {
                Assert.StartsWith("(O)", row.Key);
                Assert.True(row.Value >= 1 && row.Value <= 99, row.Key);
                Assert.True(QuantityAskPass.Covers(row.Key), row.Key);
            }
    }
}
