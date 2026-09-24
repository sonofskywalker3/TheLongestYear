using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class PastSeasonBoostsTests
{
    private static readonly HashSet<Season> Summer = new() { Season.Summer };

    [Theory]
    [InlineData(BoostId.SpringReturns, Season.Spring, false)]
    [InlineData(BoostId.SpringReturns, Season.Summer, true)]
    [InlineData(BoostId.SpringReturns, Season.Winter, true)]
    [InlineData(BoostId.SummerReturns, Season.Summer, false)]
    [InlineData(BoostId.SummerReturns, Season.Fall, true)]
    [InlineData(BoostId.FallReturns, Season.Fall, false)]
    [InlineData(BoostId.FallReturns, Season.Winter, true)]
    public void A_Boost_Is_Sold_Only_After_Its_Season_Has_Passed(BoostId id, Season today, bool expected)
        => Assert.Equal(expected, PastSeasonBoosts.Available(id, today));

    [Fact]
    public void Buying_Checks_The_Season()
    {
        var meta = new MetaState { JunimoPoints = 1000 };
        const int summerDay = 30, fallDay = 60;
        Assert.Equal(BoostPurchase.Result.NotAvailable,
            BoostPurchase.TryBuy(meta, new RunState(), BoostId.SummerReturns, BoostContext.Simple(summerDay)));
        Assert.Equal(BoostPurchase.Result.Success,
            BoostPurchase.TryBuy(meta, new RunState(), BoostId.SummerReturns, BoostContext.Simple(fallDay)));
    }

    [Fact]
    public void Active_Lists_The_Seasons_Of_Running_Boosts()
    {
        var run = new RunState();
        var meta = new MetaState { JunimoPoints = 1000 };
        const int winterDay = 90;
        BoostPurchase.TryBuy(meta, run, BoostId.SpringReturns, BoostContext.Simple(winterDay));
        Assert.Equal(new HashSet<Season> { Season.Spring }, PastSeasonBoosts.Active(run, winterDay));
        Assert.Empty(PastSeasonBoosts.Active(run, winterDay + 7));
    }

    [Fact]
    public void A_Legendary_With_A_Season_Field_Is_Copied_Without_It()
    {
        // Crimsonfish: Season Summer, a special-order condition that must survive the copy.
        const string condition = "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY";
        var (copy, newCondition) = PastSeasonSpawn.For(Season.Summer, condition, Season.Fall, Summer);
        Assert.True(copy);
        Assert.Equal(condition, newCondition);
    }

    [Fact]
    public void A_Location_Season_Clause_Is_Dropped_And_The_Rest_Kept()
    {
        var (copy, newCondition) = PastSeasonSpawn.For(
            null, "LOCATION_SEASON Here summer, WEATHER Here Rain Storm", Season.Winter, Summer);
        Assert.True(copy);
        Assert.Equal("WEATHER Here Rain Storm", newCondition);
    }

    [Fact]
    public void A_Row_That_Already_Spawns_Today_Is_Not_Copied()
    {
        // Listed for summer AND fall: in Fall it already bites, a copy would double its chance.
        Assert.False(PastSeasonSpawn.For(null, "LOCATION_SEASON Here summer fall", Season.Fall, Summer).Copy);
        Assert.False(PastSeasonSpawn.For(Season.Fall, null, Season.Fall, new HashSet<Season> { Season.Fall }).Copy);
    }

    [Fact]
    public void Rows_For_Other_Seasons_Or_Without_A_Season_Are_Not_Copied()
    {
        Assert.False(PastSeasonSpawn.For(Season.Spring, null, Season.Fall, Summer).Copy);
        Assert.False(PastSeasonSpawn.For(null, "LOCATION_SEASON Here spring", Season.Fall, Summer).Copy);
        Assert.False(PastSeasonSpawn.For(null, "WEATHER Here Rain", Season.Fall, Summer).Copy);
        Assert.False(PastSeasonSpawn.For(null, null, Season.Fall, Summer).Copy);
        Assert.False(PastSeasonSpawn.For(Season.Summer, null, Season.Fall, new HashSet<Season>()).Copy);
    }

    [Fact]
    public void A_Negated_Season_Clause_Is_Not_Copied()
        => Assert.False(PastSeasonSpawn.For(null, "!LOCATION_SEASON Here winter", Season.Fall, Summer).Copy);

    [Fact]
    public void A_Global_Season_Clause_Alone_Leaves_No_Condition()
    {
        var (copy, newCondition) = PastSeasonSpawn.For(null, "SEASON summer", Season.Fall, Summer);
        Assert.True(copy);
        Assert.Null(newCondition);
    }

    [Fact]
    public void Copies_Are_Recognised_By_Their_Id()
    {
        Assert.True(PastSeasonSpawn.IsCopy(PastSeasonSpawn.IdPrefix + "Summer_(O)128"));
        Assert.False(PastSeasonSpawn.IsCopy("(O)128"));
        Assert.False(PastSeasonSpawn.IsCopy(null));
    }
}
