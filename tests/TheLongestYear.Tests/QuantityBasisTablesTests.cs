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
        Assert.False(QuantityBasisTables.MonsterDrops.ContainsKey("(O)74"));   // Prismatic Shard: Skeleton Warrior is dangerous-mines only
        Assert.InRange(QuantityBasisTables.MonsterDrops["(O)428"], 30, 40);    // Cloth: Mummies at a third rate
        Assert.Equal(20, QuantityBasisTables.Crops["(O)433"]);                 // Coffee Bean: supply-limited
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
    public void The_largest_basis_stands_whatever_table_it_comes_from()
    {
        // Codex review, 2026-09-04: fish-first / forage-second precedence had Crab at its 5 pot
        // catches while Lava Crabs drop it at 25%, and Cactus Fruit at its measured forage while the
        // Oasis sells the seed.
        Assert.Equal(QuantityBasisTables.MonsterDrops["(O)717"], QuantityAskPass.BasisByDeadline("(O)717", null));   // Crab
        Assert.Equal(99, QuantityAskPass.BasisByDeadline("(O)90", null));                                            // Cactus Fruit: crop 99 beats forage
        // Green Algae: caught (fish table) and dropped by Slimes; the bigger of the two.
        Assert.Equal(Math.Max(FishAskBasis.BasisByDeadline("(O)153", null)!.Value, QuantityBasisTables.MonsterDrops["(O)153"]), QuantityAskPass.BasisByDeadline("(O)153", null));
    }

    [Fact]
    public void Normal_still_caps_a_vanilla_two_hundred_at_a_stack()
    {
        // Forest's Fiber x200: Normal's factor of 1.0 used to return early and skip the 99 cap
        // that Hard applied, so Hard asked for FEWER (Codex review, 2026-09-04).
        Assert.Equal(99, StackScaling.ScaleStack(200, 1.0));
        Assert.Equal(99, StackScaling.ScaleStack(200, 2.0));
        Assert.Equal(37, StackScaling.ScaleStack(37, 1.0));
    }

    [Fact]
    public void Crops_keep_half_on_gold_fish_three_quarters_and_resources_are_banded()
    {
        var gold = QuantityAskPass.Apply(Spec((Parsnip, 1, 2), (SmallmouthBassId, 1, 2), ("(O)771", 1, 0)), Profile(DifficultyStep.Extreme), _ => Season.Spring, new Random(5));
        Assert.InRange(gold.Slots[0].Stack, 33, 40);   // 65..80 of 99, then half
        Assert.InRange(gold.Slots[1].Stack, 32, 40);   // 43..53, then three quarters
        Assert.InRange(gold.Slots[2].Stack, 65, 80);   // Fiber, Resources 99
        Assert.Equal(14, QuantityBasisTables.Stations["(O)174"]);   // Large Egg: half the hens' 28
        Assert.Equal(7, QuantityBasisTables.Stations["(O)438"]);    // L. Goat Milk
    }

    private const string SmallmouthBassId = "(O)137";

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
        foreach (var table in new[] { QuantityBasisTables.CrabPot, QuantityBasisTables.Crops, QuantityBasisTables.MonsterDrops, QuantityBasisTables.Stations, QuantityBasisTables.Minerals, QuantityBasisTables.Mines, QuantityBasisTables.Resources })
            foreach (var row in table)
            {
                Assert.StartsWith("(O)", row.Key);
                Assert.True(row.Value >= 1 && row.Value <= 99, row.Key);
                Assert.True(QuantityAskPass.Covers(row.Key), row.Key);
            }
    }
}
