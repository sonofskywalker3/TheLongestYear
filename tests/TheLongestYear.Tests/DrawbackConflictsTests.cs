using System;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class DrawbackConflictsTests
{
    private const string Ghostfish = "(O)156", RedMushroom = "(O)420", Sunfish = "(O)145", CaveCarrot = "(O)78";
    private const string Slime = "(O)766", Daffodil = "(O)18", Parsnip = "(O)24", SolarEssence = "(O)768";

    private static PoolItem Item(string id, params string[] locations)
        => new(id, 10, 1, Array.Empty<Season>(), locations);

    private static ShopWeeks KrobusSellsSolarEssence()
    {
        LocationWeeks walked = LocationWeeks.Build(
            new[] { new RawLocationLink("Farm", "BusStop"), new RawLocationLink("BusStop", "Town"), new RawLocationLink("Town", "Sewer") },
            _ => false);
        return ShopWeeks.Build(
            new[] { new RawShopListing(SolarEssence, "ShadowShop") },
            new[] { new RawShopPlacement("ShadowShop", "Sewer") },
            walked);
    }

    private static ItemPools Pools() => new()
    {
        Fish = new[] { Item(Ghostfish, "UndergroundMine"), Item(Sunfish, "Forest", "Town") },
        Forage = new[] { Item(RedMushroom), Item(Daffodil), Item(Parsnip) },
        Crops = new[] { Item(Parsnip) },
        MonsterDrops = new[] { Item(Slime), Item(SolarEssence) },
        ShopWeeks = KrobusSellsSolarEssence(),
    };

    private static ItemSourceIndex Index() => ItemSourceIndex.From(Pools());

    [Fact]
    public void Ghostfish_conflicts_with_mines_closed()
        => Assert.True(DrawbackConflicts.Blocks(DrawbackConflicts.MinesClosed, Index().For(Ghostfish)));

    [Fact]
    public void Ghostfish_does_not_conflict_with_fish_bite_down()
        => Assert.False(DrawbackConflicts.Blocks("fish_bite_down", Index().For(Ghostfish)));

    [Fact]
    public void Red_Mushroom_conflicts_with_forage_off()
        => Assert.True(DrawbackConflicts.Blocks(DrawbackConflicts.ForageOff, Index().For(RedMushroom)));

    [Fact]
    public void A_river_fish_does_not_conflict_with_mines_closed()
        => Assert.False(DrawbackConflicts.Blocks(DrawbackConflicts.MinesClosed, Index().For(Sunfish)));

    [Fact]
    public void Cave_Carrot_and_monster_drops_are_mine_only_unless_a_shop_sells_them()
    {
        ItemSourceIndex index = Index();
        Assert.True(index.For(CaveCarrot).MineOnly);
        Assert.True(index.For(Slime).MineOnly);
        Assert.False(index.For(SolarEssence).MineOnly);   // Krobus sells it
    }

    [Fact]
    public void A_forage_item_that_is_also_a_crop_is_not_forage_only()
        => Assert.False(Index().For(Parsnip).ForageOnly);

    [Fact]
    public void An_unknown_item_is_open_and_never_blocks()
    {
        Assert.Equal(ItemSourceProfile.Open, Index().For("(O)999999"));
        Assert.False(DrawbackConflicts.BlocksAny(DrawbackConflicts.MinesClosed, new[] { "(O)999999" }, Index().For));
    }

    [Fact]
    public void Blocks_any_sees_a_conflict_anywhere_in_the_list()
    {
        Assert.True(DrawbackConflicts.BlocksAny(DrawbackConflicts.MinesClosed, new[] { Sunfish, Ghostfish }, Index().For));
        Assert.False(DrawbackConflicts.BlocksAny("machines_slow", new[] { Sunfish, Ghostfish, RedMushroom }, Index().For));
    }

    [Fact]
    public void The_redraw_picks_a_different_drawback_that_blocks_nothing()
    {
        ItemSourceIndex index = Index();
        bool redrewAtLeastOnce = false;
        for (int seed = 0; seed < 300; seed++)
            for (int week = 1; week <= 16; week++)
            {
                string plain = RandomPairing.LiabilityFor(seed, week, Theme.Fishing, true);
                string checkedDraw = RandomPairing.LiabilityFor(seed, week, Theme.Fishing, true, null, new[] { Ghostfish }, index.For);
                Assert.NotEqual(DrawbackConflicts.MinesClosed, checkedDraw);
                if (plain == DrawbackConflicts.MinesClosed)
                {
                    redrewAtLeastOnce = true;
                    Assert.NotEqual(plain, checkedDraw);
                }
                else
                    Assert.Equal(plain, checkedDraw);   // an unaffected week keeps its drawback
            }
        Assert.True(redrewAtLeastOnce);
    }

    [Fact]
    public void The_redraw_is_deterministic_for_a_seed()
    {
        ItemSourceIndex index = Index();
        for (int seed = 0; seed < 100; seed++)
            Assert.Equal(
                RandomPairing.LiabilityFor(seed, 3, Theme.Kitchen, true, null, new[] { Ghostfish, RedMushroom }, index.For),
                RandomPairing.LiabilityFor(seed, 3, Theme.Kitchen, true, null, new[] { Ghostfish, RedMushroom }, index.For));
    }

    [Fact]
    public void Both_hard_drawbacks_stay_off_a_week_whose_goals_need_the_mines_and_forage()
    {
        ItemSourceIndex index = Index();
        for (int seed = 0; seed < 300; seed++)
        {
            string drawn = RandomPairing.LiabilityFor(seed, 5, Theme.Mixed, true, Theme.Kitchen, new[] { Ghostfish, RedMushroom }, index.For);
            Assert.NotEqual(DrawbackConflicts.MinesClosed, drawn);
            Assert.NotEqual(DrawbackConflicts.ForageOff, drawn);
            Assert.Contains(drawn, RandomPairing.AllLiabilities);
        }
    }

    [Fact]
    public void Random_pairings_off_keeps_the_themes_own_drawback()
        => Assert.Equal("mines_closed",
            RandomPairing.LiabilityFor(7, 2, Theme.Foraging, false, null, new[] { Ghostfish }, Index().For));
}
