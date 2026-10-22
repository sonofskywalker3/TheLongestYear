using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, sections 2.4 to 2.6: the numbers keyed by the Darkness dial.</summary>
public class DarknessLevelsTests
{
    [Theory]
    [InlineData(DifficultyStep.Easy, 0.04, 8, 12)]
    [InlineData(DifficultyStep.Normal, 0.05, 10, 15)]
    [InlineData(DifficultyStep.Hard, 0.06, 12, 18)]
    [InlineData(DifficultyStep.Extreme, 0.07, 14, 21)]
    public void Blight_share_and_caps_by_level(DifficultyStep level, double share, int summerCap, int otherCap)
    {
        Assert.Equal(share, DarknessLevels.BlightShare(level));
        Assert.Equal(summerCap, DarknessLevels.BlightCap(level, Season.Summer));
        Assert.Equal(otherCap, DarknessLevels.BlightCap(level, Season.Fall));
        Assert.Equal(otherCap, DarknessLevels.BlightCap(level, Season.Winter));
    }

    [Theory]
    [InlineData(0, Season.Summer, DifficultyStep.Normal, 0)]
    [InlineData(1, Season.Summer, DifficultyStep.Normal, 1)]
    [InlineData(10, Season.Summer, DifficultyStep.Normal, 1)]     // 5% of 10 rounds up to 1
    [InlineData(100, Season.Summer, DifficultyStep.Normal, 5)]
    [InlineData(1000, Season.Summer, DifficultyStep.Normal, 10)]  // Summer cap
    [InlineData(1000, Season.Fall, DifficultyStep.Normal, 15)]    // Fall cap
    [InlineData(100, Season.Winter, DifficultyStep.Easy, 4)]
    [InlineData(1000, Season.Winter, DifficultyStep.Extreme, 21)]
    [InlineData(100, Season.Summer, DifficultyStep.Extreme, 7)]
    public void Crop_count_is_the_level_share_clamped(int crops, Season season, DifficultyStep level, int expected)
        => Assert.Equal(expected, BlightRule.Count(crops, season, level));

    [Theory]
    [InlineData(0, Season.Summer, DifficultyStep.Normal, 0)]
    [InlineData(1, Season.Summer, DifficultyStep.Normal, 1)]
    [InlineData(100, Season.Summer, DifficultyStep.Normal, 5)]
    [InlineData(10000, Season.Summer, DifficultyStep.Normal, 10)]
    [InlineData(10000, Season.Fall, DifficultyStep.Hard, 18)]
    public void Storage_count_uses_the_same_share_and_caps(int units, Season season, DifficultyStep level, int expected)
        => Assert.Equal(expected, BlightRule.SpoilCount(units, season, level));

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.0)]
    [InlineData(DifficultyStep.Normal, 0.0)]
    [InlineData(DifficultyStep.Hard, 0.10)]
    [InlineData(DifficultyStep.Extreme, 0.30)]
    public void Unmoderated_chance_by_level(DifficultyStep level, double chance)
        => Assert.Equal(chance, DarknessLevels.UnmoderatedChance(level));

    [Fact]
    public void Only_extreme_reaches_tools_and_machines()
    {
        Assert.False(DarknessLevels.StorageReachesEverything(DifficultyStep.Hard));
        Assert.True(DarknessLevels.StorageReachesEverything(DifficultyStep.Extreme));
        Assert.Equal(3, DarknessLevels.BigCraftableUnits);
    }

    [Theory]
    [InlineData(1, false, 1)]
    [InlineData(40, false, 40)]
    [InlineData(1, true, 3)]
    [InlineData(2, true, 6)]
    public void A_big_craftable_weighs_three_units(int stack, bool bigCraftable, int units)
        => Assert.Equal(units, BlightRule.UnitsOf(stack, bigCraftable));

    [Fact]
    public void A_legendary_fish_is_always_one()
        => Assert.Equal(1, TamperRule.MaxCount("(O)163", 40.0));   // Legend

    [Fact]
    public void No_basis_means_no_max_so_the_stack_rule_asks_for_one()
    {
        Assert.Equal(0, TamperRule.MaxCount("(O)24", null));
        Assert.Equal(1, TamperRule.Stack(0, 2, DifficultyStep.Extreme, new System.Random(1)));
    }

    [Fact]
    public void Max_count_is_the_boards_own_ceiling_of_the_basis()
        => Assert.Equal(8, TamperRule.MaxCount("(O)24", 10.0));     // ceil(10 * 0.8)

    [Theory]
    [InlineData(1, DifficultyStep.Extreme)]
    [InlineData(4, DifficultyStep.Easy)]
    public void A_legendary_stays_one_through_the_stack_roll(int week, DifficultyStep level)
    {
        for (int seed = 0; seed < 20; seed++)
            Assert.Equal(1, TamperRule.Stack(TamperRule.MaxCount("(O)775", 30.0), week, level, new System.Random(seed)));
    }

    /// <summary>Section 2.5's pool, one cell per row. isPlainObject, isBigCraftable, placedOnMap,
    /// onFarm, warded, everything (Extreme).</summary>
    [Theory]
    // A tool in a chest: safe below Extreme, taken on Extreme.
    [InlineData(false, false, false, true, false, false, false)]
    [InlineData(false, false, false, true, false, true, true)]
    // A big craftable kept in a chest: the same.
    [InlineData(false, true, false, true, false, false, false)]
    [InlineData(false, true, false, true, false, true, true)]
    // A plain stack is in the pool at every level.
    [InlineData(true, false, false, true, false, false, true)]
    [InlineData(true, false, false, true, false, true, true)]
    // A warded chest is safe even on Extreme.
    [InlineData(true, false, false, true, true, true, false)]
    [InlineData(false, false, false, true, true, true, false)]
    // A machine placed on the farm: only on Extreme, only there, never on a circle.
    [InlineData(false, true, true, true, false, true, true)]
    [InlineData(false, true, true, false, false, true, false)]
    [InlineData(false, true, true, true, false, false, false)]
    [InlineData(false, true, true, true, true, true, false)]
    // A placed plain object (a sprinkler, a fence post) is never in the pool.
    [InlineData(true, false, true, true, false, true, false)]
    public void The_storage_pool_by_level(bool plain, bool big, bool placed, bool onFarm, bool warded, bool everything, bool inPool)
        => Assert.Equal(inPool, BlightRule.InStoragePool(plain, big, placed, onFarm, warded, everything));

    [Theory]
    // A plain chest, a big chest, a hopper, a Junimo Chest: in the draw.
    [InlineData(false, true)]
    // A Mini-Shipping Bin ships overnight before the strike lands.
    [InlineData(true, false)]
    public void The_chest_draw_skips_only_shipping_bins(bool shipsOvernight, bool inDraw)
        => Assert.Equal(inDraw, BlightRule.ChestInDraw(shipsOvernight));

    // ---------------------------------------------------------------- shared inventories (Junimo Chests)

    [Fact]
    public void Junimo_chests_share_one_seat_in_the_draw()
    {
        // Two plain chests and three Junimo Chests (inventory 7): the Junimo stock is drawn once.
        var seats = new[]
        {
            new ChestSeat(1, OnFarm: true, Warded: false),
            new ChestSeat(7, OnFarm: false, Warded: false),
            new ChestSeat(2, OnFarm: true, Warded: false),
            new ChestSeat(7, OnFarm: true, Warded: false),
            new ChestSeat(7, OnFarm: true, Warded: false),
        };
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(seats);
        Assert.Equal(new[] { true, false, true, true, false }, hosts.Select(h => h.Host).ToArray());
    }

    [Fact]
    public void The_shared_stock_is_shown_at_a_junimo_chest_on_the_farm_when_there_is_one()
    {
        var offFarmOnly = BlightRule.ChestHosts(new[] { new ChestSeat(7, false, false), new ChestSeat(7, false, false) });
        Assert.Equal(new[] { true, false }, offFarmOnly.Select(h => h.Host).ToArray());
        var farmSecond = BlightRule.ChestHosts(new[] { new ChestSeat(7, false, false), new ChestSeat(7, true, false) });
        Assert.Equal(new[] { false, true }, farmSecond.Select(h => h.Host).ToArray());
    }

    [Fact]
    public void One_warded_junimo_chest_protects_the_shared_stock()
    {
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(new[]
        {
            new ChestSeat(7, true, false), new ChestSeat(7, false, true), new ChestSeat(3, true, false),
        });
        Assert.True(hosts[0].Host);
        Assert.True(hosts[0].Warded);
        Assert.False(hosts[2].Warded);
    }

    [Fact]
    public void A_shared_stock_counted_once_is_weighted_once_in_the_pick()
    {
        // One plain chest of 10 units and one Junimo stock of 10 units seen through two Junimo
        // Chests: with one seat each the night lands in either about half the time. Counted twice,
        // the Junimo stock would win about two thirds of the nights.
        var seats = new[] { new ChestSeat(1, true, false), new ChestSeat(7, true, false), new ChestSeat(7, true, false) };
        IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(seats);
        var pool = new List<TakeCandidate>();
        for (int i = 0; i < seats.Length; i++)
            if (hosts[i].Host) pool.Add(new TakeCandidate(seats[i].InventoryId, 10, false));
        int junimo = 0;
        const int nights = 2000;
        for (int seed = 0; seed < nights; seed++)
        {
            IReadOnlyList<int> taken = BlightRule.PlanTake(pool, 1, new System.Random(seed));
            if (pool[taken[0]].OwnerId == 7) junimo++;
        }
        Assert.InRange(junimo, nights * 40 / 100, nights * 60 / 100);
    }
}