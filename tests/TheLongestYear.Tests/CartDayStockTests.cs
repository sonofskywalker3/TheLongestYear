using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CartDayStockTests
{
    private static readonly string[] Stock = { "(O)1", "(O)2", "(O)3", "(O)4", "(O)5" };

    [Fact]
    public void First_build_of_the_day_takes_the_first_allowed_and_remembers_them()
    {
        var run = new RunState();
        var kept = CartDayStock.Select(run, day: 10, Stock, allowed: 2);
        Assert.Equal(new[] { "(O)1", "(O)2" }, kept);
        Assert.Equal(10, run.CartStockDay);
        Assert.Equal(new List<string> { "(O)1", "(O)2" }, run.CartStockIds);
    }

    [Fact]
    public void Later_build_same_day_keeps_only_the_remembered_ids_no_slide_up()
    {
        var run = new RunState { CartStockDay = 10, CartStockIds = new List<string> { "(O)1", "(O)2" } };
        var afterBuying1 = new[] { "(O)2", "(O)3", "(O)4", "(O)5" };
        var kept = CartDayStock.Select(run, day: 10, afterBuying1, allowed: 2);
        Assert.Equal(new[] { "(O)2" }, kept);
        Assert.Equal(new List<string> { "(O)1", "(O)2" }, run.CartStockIds);   // memory untouched
    }

    [Fact]
    public void New_day_reselects()
    {
        var run = new RunState { CartStockDay = 10, CartStockIds = new List<string> { "(O)1", "(O)2" } };
        var kept = CartDayStock.Select(run, day: 11, new[] { "(O)9", "(O)8", "(O)7" }, allowed: 1);
        Assert.Equal(new[] { "(O)9" }, kept);
        Assert.Equal(11, run.CartStockDay);
        Assert.Equal(new List<string> { "(O)9" }, run.CartStockIds);
    }

    [Fact]
    public void Same_day_with_empty_memory_chooses_fresh()
    {
        var run = new RunState { CartStockDay = 10, CartStockIds = new List<string>() };
        var kept = CartDayStock.Select(run, day: 10, Stock, allowed: 3);
        Assert.Equal(new[] { "(O)1", "(O)2", "(O)3" }, kept);
    }

    [Fact]
    public void Allowed_zero_or_negative_keeps_nothing_and_stores_nothing()
    {
        var run = new RunState();
        Assert.Empty(CartDayStock.Select(run, day: 10, Stock, allowed: 0));
        Assert.Equal(-1, run.CartStockDay);
    }

    [Fact]
    public void Allowed_larger_than_stock_keeps_everything()
    {
        var run = new RunState();
        Assert.Equal(Stock, CartDayStock.Select(run, day: 10, Stock, allowed: 10));
    }
}
