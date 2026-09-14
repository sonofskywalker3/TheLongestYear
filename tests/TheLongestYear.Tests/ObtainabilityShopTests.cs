using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityShopTests
{
    private static readonly Dictionary<string, FestivalDates> Festivals = new()
    {
        ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17),
        ["SquidFest"] = new FestivalDates("SquidFest", Season.Winter, 12, 13),
        ["TroutDerby"] = new FestivalDates("TroutDerby", Season.Summer, 20, 21),
        ["DesertFestival"] = new FestivalDates("DesertFestival", Season.Spring, 15, 17),
        ["summer11"] = new FestivalDates("summer11", Season.Summer, 11, 11),
        ["winter8"] = new FestivalDates("winter8", Season.Winter, 8, 8),
    };

    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)775"] = new ObjInfo("(O)775", "Glacierfish", -4, 1000, new string[0], true),
        ["(O)Moss"] = new ObjInfo("(O)Moss", "Moss", -81, 5, new string[0], false),
    };

    private static List<(string ItemId, ObtainSource Source)> Stock(params ShopRow[] rows)
        => ShopSources.Stock(rows, Objects, Festivals).ToList();

    [Fact]
    public void A_seasonal_seed_row_is_dependable_in_its_season()
    {
        var (id, s) = Stock(new ShopRow("SeedShop", "(O)472", "SEASON Spring", false)).Single();
        Assert.Equal("(O)472", id);
        Assert.Equal(SourceKind.Shop, s.Kind);
        Assert.Equal(Reliability.Dependable, s.Reliability);
        Assert.Equal(WeekMask.ForSeason(Season.Spring), s.Weeks);
        Assert.Contains("shop:SeedShop", s.Conditions.Requires);
    }

    [Fact]
    public void The_traveling_cart_expands_its_random_query_as_chance()
    {
        var ids = Stock(new ShopRow("Traveler", "RANDOM_ITEMS (O) 2 789 @isRandomSale @requirePrice", null, false));
        Assert.Equal(new[] { "(O)24" }, ids.Select(x => x.ItemId).ToArray());
        Assert.All(ids, x => Assert.Equal(SourceKind.Cart, x.Source.Kind));
        Assert.All(ids, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
    }

    [Fact]
    public void Festival_shops_open_on_their_festival_days_from_data()
    {
        var luau = Stock(new ShopRow("Festival_Luau_Pierre", "(O)Moss", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(6), luau.Weeks);                       // Summer 11
        Assert.Equal(SourceKind.Festival, luau.Kind);
        Assert.True(luau.Conditions.FewDays);

        var boat = Stock(new ShopRow("Festival_NightMarket_MagicBoat_Day2", "(O)Moss", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(15), boat.Weeks);
        Assert.Equal(SourceKind.NightMarket, boat.Kind);

        var ice = Stock(new ShopRow("Festival_FestivalOfIce_TravelingMerchant", "RANDOM_ITEMS (O) 2 789 @isRandomSale", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(14), ice.Weeks);                        // Winter 8
        Assert.Equal(Reliability.Chance, ice.Reliability);

        var desert = Stock(new ShopRow("Festival_DesertFestival_Vendor", "(O)Moss", null, false)).Single().Source;
        Assert.Equal(WeekMask.Of(3), desert.Weeks);                      // Spring 15-17 from the passive festival id
    }

    [Fact]
    public void An_unplaceable_festival_shop_is_unresolved_not_a_fact()
    {
        var s = Stock(new ShopRow("Festival_SomeModFair_Booth", "(O)Moss", null, false)).Single().Source;
        Assert.True(s.Conditions.Unresolved);
        Assert.Equal(WeekMask.All, s.Weeks);
    }

    [Fact]
    public void Island_shops_and_year_two_rows_are_flagged()
    {
        Assert.True(Stock(new ShopRow("IslandTrade", "(O)Moss", null, false)).Single().Source.Conditions.GingerIsland);
        Assert.True(ShopSources.IsIslandShop("VolcanoShop"));
        Assert.False(ShopSources.IsIslandShop("SeedShop"));
        Assert.True(Stock(new ShopRow("SeedShop", "(O)476", "YEAR 2, SEASON Spring", false)).Single().Source.Conditions.YearTwo);
    }

    [Fact]
    public void Recipe_rows_teach_rather_than_sell_and_year_two_rows_never_teach()
    {
        var rows = new[]
        {
            new ShopRow("Saloon", "(O)196", "SEASON Fall", true),
            new ShopRow("Saloon", "(O)197", "YEAR 2", true),
            new ShopRow("IslandTrade", "(O)198", null, true),
        };
        Assert.Empty(ShopSources.Stock(rows, Objects, Festivals));
        var taught = ShopSources.RecipeWeeks(rows, Festivals);
        Assert.Equal(WeekMask.ForSeason(Season.Fall), taught["(O)196"]);
        Assert.False(taught.ContainsKey("(O)197"));
        Assert.False(taught.ContainsKey("(O)198"));
    }

    [Fact]
    public void Squid_fest_and_trout_derby_rewards_use_their_festival_dates_and_their_odds()
    {
        var rewards = ShopSources.FestivalRewards(Festivals).ToList();
        var book = rewards.Single(r => r.ItemId == "(O)Book_Crabbing").Source;
        Assert.Equal(WeekMask.Of(14), book.Weeks);                       // Winter 12-13
        Assert.True(book.Conditions.FewDays);
        Assert.Equal(Reliability.Dependable, book.Reliability);
        var tent = rewards.Single(r => r.ItemId == "(O)TentKit").Source;
        Assert.Equal(WeekMask.Of(7), tent.Weeks);                        // Summer 20-21
        Assert.Equal(Reliability.Dependable, tent.Reliability);         // the first tag always gives it
        Assert.Equal(Reliability.Chance, rewards.Single(r => r.ItemId == "(O)710").Source.Reliability);   // a spin
        Assert.Contains(rewards, r => r.ItemId == "(O)498" && r.Source.Reliability == Reliability.Chance); // a 50/50
    }

    [Fact]
    public void A_random_shop_alternative_is_chance()
        => Assert.Equal(Reliability.Chance, Stock(new ShopRow("SeedShop", "(O)472", null, false, IsRandom: true)).Single().Source.Reliability);

    [Fact]
    public void A_barter_row_is_only_had_in_weeks_its_trade_item_is()
    {
        var row = new ShopRow("DesertTrade", "(O)Moss", null, false, TradeItemId: "(O)24");
        Assert.Empty(Stock(row));
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = new[] { new ObtainSource(SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable, ObtainConditions.None, "seeds") },
        });
        var (id, source) = ShopSources.Barter(new[] { row }, Objects, Festivals, snapshot).Single();
        Assert.Equal("(O)Moss", id);
        Assert.Equal(WeekMask.ForSeason(Season.Spring), source.Weeks);
        Assert.Contains("trade:(O)24", source.Conditions.Requires);
    }
}
