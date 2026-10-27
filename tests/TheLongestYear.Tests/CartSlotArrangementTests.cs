using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public class CartSlotArrangementTests
{
    private static readonly string[] Main = { "(O)1", "(O)2", "(O)3", "(O)4", "(O)5" };
    private static readonly string[] Extras = { "(O)347", "(O)433" };
    private const string Guarantee = "(O)485";
    private static readonly IReadOnlySet<string> NoGuarantee = new HashSet<string>();
    private static readonly IReadOnlySet<string> RedCabbage = new HashSet<string> { Guarantee };
    private static readonly string[] Pool = { "(O)476", "(O)485", "(O)489" };

    private static string[] Stock(params string[] extras) => Main.Concat(extras).ToArray();

    /// <summary>A stream whose first NextDouble is the given roll (seed search keeps the tests
    /// independent of .NET's sequence).</summary>
    private static Random RollOf(bool hit)
    {
        for (int seed = 0; ; seed++)
            if (new Random(seed).NextDouble() < CartSlotArrangement.YearTwoChance == hit)
                return new Random(seed);
    }

    [Fact]
    public void Extras_land_in_shown_slots_some_of_the_time()
    {
        int shown = 0;
        for (int seed = 0; seed < 2000; seed++)
        {
            var result = CartSlotArrangement.Arrange(Stock(Extras), Main.Length, NoGuarantee, Pool, 1,
                new Random(seed), RollOf(false));
            if (Extras.Contains(result.Order[0])) shown++;
        }
        // Two extras among seven keys: about 2 in 7 first slots.
        Assert.InRange(shown, 450, 700);
    }

    [Fact]
    public void Every_key_is_kept_once()
    {
        var result = CartSlotArrangement.Arrange(Stock(Extras), Main.Length, NoGuarantee, Pool, 3,
            new Random(7), RollOf(false));
        Assert.Equal(Stock(Extras).OrderBy(k => k), result.Order.OrderBy(k => k));
        Assert.Null(result.Added);
    }

    [Fact]
    public void Main_draw_keeps_its_own_order()
    {
        var result = CartSlotArrangement.Arrange(Stock(Extras), Main.Length, NoGuarantee, Pool, 3,
            new Random(3), RollOf(false));
        Assert.Equal(Main, result.Order.Where(Main.Contains));
    }

    [Fact]
    public void Missed_roll_adds_nothing()
    {
        var result = CartSlotArrangement.Arrange(Stock(), Main.Length, NoGuarantee, Pool, 1,
            new Random(1), RollOf(false));
        Assert.Equal(Main, result.Order);
        Assert.Null(result.Added);
    }

    [Fact]
    public void Hit_roll_puts_a_pool_item_in_a_shown_slot()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var result = CartSlotArrangement.Arrange(Stock(), Main.Length, NoGuarantee, Pool, 2,
                new Random(seed), RollOf(true));
            Assert.Contains(result.Order.Take(2), Pool.Contains);
            Assert.NotNull(result.Added);
            Assert.Equal(Main.Length + 1, result.Order.Count);
        }
    }

    [Fact]
    public void Same_year_two_pick_whatever_the_stock()
    {
        // A purchase removes a key; the pick must not change or CartDayStock drops it.
        var full = CartSlotArrangement.Arrange(Stock(Extras), Main.Length, NoGuarantee, Pool, 2,
            new Random(5), RollOf(true));
        var afterBuy = CartSlotArrangement.Arrange(Stock(Extras).Skip(1).ToArray(), Main.Length - 1, NoGuarantee, Pool, 2,
            new Random(5), RollOf(true));
        Assert.Equal(full.Added, afterBuy.Added);
    }

    [Fact]
    public void Hidden_stock_item_is_moved_up_not_added()
    {
        string[] stock = { "(O)1", "(O)2", "(O)3", "(O)476", "(O)489", "(O)485" };
        var result = CartSlotArrangement.Arrange(stock, stock.Length, NoGuarantee, Pool, 1,
            new Random(2), RollOf(true));
        Assert.Contains(result.Order[0], Pool);
        Assert.Null(result.Added);
        Assert.Equal(stock.Length, result.Order.Count);
    }

    [Fact]
    public void Guarantee_takes_a_shown_slot_without_a_roll()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var result = CartSlotArrangement.Arrange(Stock(Extras.Append(Guarantee).ToArray()), Main.Length,
                RedCabbage, Pool, 1, new Random(seed), RollOf(false));
            Assert.Equal(Guarantee, result.Order[0]);
            Assert.Null(result.Added);
        }
    }

    [Fact]
    public void No_slots_shown_adds_nothing()
    {
        var result = CartSlotArrangement.Arrange(Stock(Guarantee), Main.Length, RedCabbage, Pool, 0,
            new Random(1), RollOf(true));
        Assert.Null(result.Added);
        Assert.Equal(Main.Length + 1, result.Order.Count);
    }

    [Fact]
    public void Empty_pool_adds_nothing()
    {
        var result = CartSlotArrangement.Arrange(Stock(), Main.Length, NoGuarantee, Array.Empty<string>(), 1,
            new Random(1), RollOf(true));
        Assert.Null(result.Added);
    }
}

public class YearTwoShopItemsTests
{
    private static IReadOnlyList<string> Find(params ShopRow[] rows) => YearTwoShopItems.Find(rows, _ => true);

    [Fact]
    public void Year_two_only_line_counts()
        => Assert.Equal(new[] { "(O)476" }, Find(new ShopRow("SeedShop", "(O)476", "SEASON spring, YEAR 2", false)));

    [Fact]
    public void Item_also_sold_in_year_one_does_not_count()
        => Assert.Empty(Find(
            new ShopRow("Carpenter", "(BC)211", "YEAR 2", false),
            new ShopRow("Carpenter", "(BC)211", "!YEAR 2, SEASON winter", false)));

    [Fact]
    public void Cart_lines_are_ignored_both_ways()
    {
        // The cart's Red Cabbage guarantee has no YEAR clause; it must not make the seed a year-1 item.
        Assert.Equal(new[] { "(O)485" }, Find(
            new ShopRow("SeedShop", "(O)485", "SEASON summer, YEAR 2", false),
            new ShopRow("Traveler", "(O)485", "WORLD_STATE_FIELD VisitsUntilY1Guarantee 0 0", false)));
        Assert.Empty(Find(new ShopRow("Traveler", "(O)999", "YEAR 2", false)));
    }

    [Fact]
    public void Recipes_queries_and_unpriced_items_are_left_out()
    {
        Assert.Empty(Find(new ShopRow("Saloon", "(O)801", "YEAR 2", true)));
        Assert.Empty(Find(new ShopRow("Mod", "RANDOM_ITEMS (O) 2 10", "YEAR 2", false)));
        Assert.Empty(Find(new ShopRow("Mod", "(O)Mod_X", "YEAR 2", false, IsRandom: true)));
        Assert.Empty(YearTwoShopItems.Find(new[] { new ShopRow("Mod", "(O)Mod_X", "YEAR 2", false) }, _ => false));
    }

    [Fact]
    public void Modded_and_unqualified_ids_are_read()
        => Assert.Equal(new[] { "(O)369", "(O)Mod.Seed" }, Find(
            new ShopRow("SeedShop", "369", "YEAR 2", false),
            new ShopRow("ModShop", "(O)Mod.Seed", "YEAR 3", false)));
}
